using CrateEdit.Core.Results;
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
        if (riff.IsFailure)
        {
            return new FileTags(TagValues.Empty, (TagNotWritable)riff.Error);
        }

        var parts = ReadParts(riff.Value);
        return parts.IsFailure
            ? new FileTags(TagValues.Empty, (TagNotWritable)parts.Error)
            : new FileTags(Combine(parts.Value), null);
    }

    /// <summary>Finds and parses the tag chunks.</summary>
    /// <returns>The parts, or <see cref="TagNotWritable"/> if a tag chunk is duplicated or cannot be parsed completely.</returns>
    internal static Result<WavTagParts> ReadParts(RiffFile riff)
    {
        var infoChunks = riff.Chunks.Where(InfoChunk.IsInfoList).ToList();
        var id3Chunks = riff.Chunks.Where(IsId3Chunk).ToList();

        // With two copies of a tag chunk it is unclear which one other software reads, and
        // editing one would leave the other stale. Both duplicated kinds are named for the log.
        var duplicated = new List<string>();
        if (infoChunks.Count > 1)
        {
            duplicated.Add("LIST/INFO");
        }

        if (id3Chunks.Count > 1)
        {
            duplicated.Add("id3");
        }

        if (duplicated.Count > 0)
        {
            return new TagNotWritable(TagProblem.Malformed, string.Join(", ", duplicated));
        }

        List<InfoEntry>? info = null;
        if (infoChunks is [var infoChunk])
        {
            var parsed = ReadInfoChunk(infoChunk);
            if (parsed.IsFailure)
            {
                return parsed.Error;
            }

            info = parsed.Value;
        }

        Id3v2Tag? id3 = null;
        if (id3Chunks is [var id3Chunk])
        {
            var parsed = ReadId3Chunk(id3Chunk);
            if (parsed.IsFailure)
            {
                return parsed.Error;
            }

            id3 = parsed.Value;
        }

        return new WavTagParts(infoChunks.FirstOrDefault(), info, id3Chunks.FirstOrDefault(), id3);
    }

    private static Result<List<InfoEntry>> ReadInfoChunk(RiffChunk chunk) =>
        InfoChunk.Parse(chunk.Body!) is { } entries
            ? entries
            : new TagNotWritable(TagProblem.Malformed, chunk.Id, chunk.Offset);

    private static Result<Id3v2Tag> ReadId3Chunk(RiffChunk chunk)
    {
        if (!IsCompleteId3Tag(chunk.Body!, out var header))
        {
            return new TagNotWritable(TagProblem.Malformed, chunk.Id, chunk.Offset);
        }

        var tag = Id3v2Tag.Parse(header, chunk.Body!);
        return tag.Problem is { } problem ? problem : tag;
    }

    /// <summary>
    /// The chunk body must start with a valid ID3v2 header, contain the whole tag that header
    /// announces, and have nothing but zero padding after it. Otherwise the chunk holds something
    /// the tag code would not reproduce when rewriting it.
    /// </summary>
    private static bool IsCompleteId3Tag(byte[] body, out Id3v2Header header)
    {
        if (Id3v2Header.TryParse(body) is not { } parsed || parsed.TotalSizeBytes > body.Length)
        {
            header = default;
            return false;
        }

        header = parsed;
        return !body.AsSpan(parsed.TotalSizeBytes).ContainsAnyExcept((byte)0);
    }

    /// <summary>
    /// The value of each field as Rekordbox shows it: from INFO if INFO has a non-empty entry for
    /// it, otherwise from the "id3 " chunk. Label, BPM and key never have an INFO entry, so they
    /// always come from "id3 ".
    /// </summary>
    internal static TagValues Combine(WavTagParts parts)
    {
        var id3Values = parts.Id3 is { } tag ? Id3FieldMap.Read(tag.Frames, tag.Version) : TagValues.Empty;
        var values = new Dictionary<TagField, string>();
        foreach (var field in Enum.GetValues<TagField>())
        {
            var value = InfoValue(parts.Info, field);
            if (string.IsNullOrEmpty(value))
            {
                value = id3Values[field];
            }

            if (value is not null)
            {
                values[field] = value;
            }
        }

        return new TagValues(values);
    }

    /// <summary>The decoded INFO entry for <paramref name="field"/>, or null if INFO has none (or cannot hold the field).</summary>
    private static string? InfoValue(List<InfoEntry>? info, TagField field)
    {
        if (info is null || InfoChunk.IdFor(field) is not { } id)
        {
            return null;
        }

        var entry = info.FirstOrDefault(e => e.Id == id);
        return entry is null ? null : InfoChunk.Decode(entry.Value);
    }

    internal static bool IsId3Chunk(RiffChunk chunk) => chunk.Id is "id3 " or "ID3 ";
}
