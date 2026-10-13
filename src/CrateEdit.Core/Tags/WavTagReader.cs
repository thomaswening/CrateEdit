using CrateEdit.Core.Tags.Id3;
using CrateEdit.Core.Tags.Riff;

namespace CrateEdit.Core.Tags;

/// <summary>The tag chunks of a WAV file: the LIST/INFO chunk and the embedded ID3v2 tag, each if present.</summary>
internal sealed record WavTagParts(RiffChunk? InfoChunk, List<InfoEntry>? Info, RiffChunk? Id3Chunk, Id3v2Tag? Id3);

/// <summary>
/// Reads WAV tags (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0011-wav-tagging.md">ADR-0011</see>). Title, artist, album, genre, comment and year come from the
/// LIST/INFO chunk first, because that is what Rekordbox shows; label, BPM and key, which INFO
/// cannot hold, come from the "id3 " chunk, which is also the fallback for empty INFO fields.
/// Only the metadata chunks are read; the audio stays on disk.
/// </summary>
public static class WavTagReader
{
    public static FileTags Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var riff = RiffFile.Read(stream);
        if (riff is null)
        {
            return new FileTags(TagValues.Empty, new TagNotWritable(TagProblem.Malformed, Offset: 0));
        }

        if (riff.Problem is not null)
        {
            return new FileTags(TagValues.Empty, riff.Problem);
        }

        var (parts, problem) = ReadParts(riff);
        return problem is not null
            ? new FileTags(TagValues.Empty, problem)
            : new FileTags(Combine(parts!), null);
    }

    internal static (WavTagParts? Parts, TagNotWritable? Problem) ReadParts(RiffFile riff)
    {
        var infoChunks = riff.Chunks.Where(InfoChunk.IsInfoList).ToList();
        var id3Chunks = riff.Chunks.Where(IsId3Chunk).ToList();
        if (infoChunks.Count > 1 || id3Chunks.Count > 1)
        {
            // Unclear which one other software reads; editing one would leave the other stale.
            return (null, new TagNotWritable(TagProblem.Malformed, infoChunks.Count > 1 ? "LIST" : "id3 "));
        }

        List<InfoEntry>? info = null;
        if (infoChunks is [var infoChunk])
        {
            info = InfoChunk.Parse(infoChunk.Body!);
            if (info is null)
            {
                return (null, new TagNotWritable(TagProblem.Malformed, "LIST", infoChunk.Offset));
            }
        }

        Id3v2Tag? id3 = null;
        if (id3Chunks is [var id3Chunk])
        {
            var body = id3Chunk.Body!;
            if (Id3v2Header.TryParse(body) is not { } header || header.TotalSizeBytes > body.Length ||
                body.AsSpan(header.TotalSizeBytes).ContainsAnyExcept((byte)0))
            {
                return (null, new TagNotWritable(TagProblem.Malformed, id3Chunk.Id, id3Chunk.Offset));
            }

            id3 = Id3v2Tag.Parse(header, body);
            if (id3.Problem is { } id3Problem)
            {
                return (null, id3Problem);
            }
        }

        return (new WavTagParts(infoChunks.FirstOrDefault(), info, id3Chunks.FirstOrDefault(), id3), null);
    }

    /// <summary>INFO first for the fields it can hold, then ID3.</summary>
    internal static TagValues Combine(WavTagParts parts)
    {
        var id3Values = parts.Id3 is { } tag ? Id3FieldMap.Read(tag.Frames, tag.Version) : TagValues.Empty;
        return new TagValues(Enum.GetValues<TagField>().Select(field =>
        {
            var infoValue = InfoChunk.IdFor(field) is { } id && parts.Info?.FirstOrDefault(e => e.Id == id) is { } entry
                ? InfoChunk.Decode(entry.Value)
                : null;
            return new KeyValuePair<TagField, string>(field, string.IsNullOrEmpty(infoValue) ? id3Values[field] ?? "" : infoValue);
        }));
    }

    internal static bool IsId3Chunk(RiffChunk chunk) => chunk.Id is "id3 " or "ID3 ";
}
