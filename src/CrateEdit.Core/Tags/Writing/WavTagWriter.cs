using System.Buffers.Binary;
using System.Text;
using CrateEdit.Core.Files;
using CrateEdit.Core.Results;
using CrateEdit.Core.Tags.Id3;
using CrateEdit.Core.Tags.Riff;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// Writes edited WAV tags (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0011-wav-tagging.md">ADR-0011</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-088">REQ-088</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-130">REQ-130</see>). Every edit goes into both
/// tag chunks: the LIST/INFO chunk (what Rekordbox reads, for the fields it can hold) and the
/// "id3 " chunk (all fields, read by other software). In each, only the edited entries and frames
/// are rebuilt. All other chunks, their order and the audio are copied byte for byte. A missing
/// tag chunk is created at the end of the file, filled with all current values, so the audio
/// never moves.
/// </summary>
/// <remarks>Only the apply pipeline may use this class, like <see cref="Mp3TagWriter"/>.</remarks>
public static class WavTagWriter
{
    /// <summary>
    /// Writes <paramref name="sourcePath"/> with <paramref name="changes"/> applied to
    /// <paramref name="destinationPath"/>. A null or empty value clears the field.
    /// </summary>
    /// <returns>Success, or <see cref="TagNotWritable"/> without creating the destination. I/O failures throw.</returns>
    public static Result Rewrite(string sourcePath, string destinationPath, IReadOnlyDictionary<TagField, string?> changes)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var riff = RiffFile.Read(source);
        if (riff is null)
        {
            return new TagNotWritable(TagProblem.Malformed, Offset: 0);
        }

        if (riff.Problem is { } riffProblem)
        {
            return riffProblem;
        }

        var (parts, partsProblem) = WavTagReader.ReadParts(riff);
        if (partsProblem is not null)
        {
            return partsProblem;
        }

        var current = WavTagReader.Combine(parts!);
        string? Target(TagField field) =>
            changes.TryGetValue(field, out var changed) ? (string.IsNullOrEmpty(changed) ? null : changed) : current[field];

        // ID3: edit the existing tag, or create one with all current values.
        var id3 = parts!.Id3 ?? Id3v2Tag.CreateEmpty();
        IReadOnlyList<Id3v2Frame> frames = id3.Frames;
        foreach (var field in parts.Id3 is null ? Enum.GetValues<TagField>() : changes.Keys)
        {
            if (Id3FieldMap.CheckEditable(frames, field, id3.Version) is { } fieldProblem)
            {
                return fieldProblem;
            }

            frames = Id3FieldMap.Apply(frames, field, Target(field), id3.Version);
        }

        var newId3 = frames.Count > 0 || parts.Id3 is not null ? id3.Render(frames, Mp3TagWriter.PaddingWhenGrowingBytes) : null;

        // INFO: edit the existing entries, or create the chunk with all current values.
        var entries = parts.Info ?? [];
        foreach (var field in parts.Info is null ? Enum.GetValues<TagField>() : changes.Keys)
        {
            if (InfoChunk.IdFor(field) is not { } id)
            {
                continue;
            }

            var index = entries.FindIndex(e => e.Id == id);
            if (Target(field) is { } value)
            {
                var entry = new InfoEntry(id, InfoChunk.Encode(value));
                if (index >= 0)
                {
                    entries[index] = entry;
                }
                else
                {
                    entries.Add(entry);
                }
            }
            else if (index >= 0)
            {
                entries.RemoveAt(index);
            }
        }

        var newInfo = entries.Count > 0 || parts.Info is not null ? InfoChunk.Render(entries) : null;

        // An existing INFO chunk is only rebuilt if one of its fields was edited (REQ-130).
        var infoEdited = parts.Info is not null && changes.Keys.Any(field => InfoChunk.IdFor(field) is not null);

        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        destination.Write("RIFF"u8);
        destination.Write(new byte[4]); // total size, filled in at the end
        destination.Write("WAVE"u8);

        var lastChunkLacksPad = false;
        foreach (var chunk in riff.Chunks)
        {
            if (chunk == parts.InfoChunk && infoEdited)
            {
                WriteChunk(destination, InfoChunk.ListId, newInfo!);
                lastChunkLacksPad = false;
            }
            else if (chunk == parts.Id3Chunk)
            {
                WriteChunk(destination, chunk.Id, newId3!);
                lastChunkLacksPad = false;
            }
            else
            {
                // Copied as stored, including the audio and any pad byte.
                var stored = Math.Min(chunk.TotalSizeBytes, riff.Length - chunk.Offset);
                source.Position = chunk.Offset;
                StreamCopy.CopyBytes(source, destination, stored);
                lastChunkLacksPad = stored < chunk.TotalSizeBytes;
            }
        }

        var appendInfo = parts.InfoChunk is null && newInfo is not null;
        var appendId3 = parts.Id3Chunk is null && newId3 is not null;
        if (lastChunkLacksPad && (appendInfo || appendId3))
        {
            // The last chunk was stored without its pad byte; new chunks must start on an even offset.
            destination.WriteByte(0);
        }

        if (appendInfo)
        {
            WriteChunk(destination, InfoChunk.ListId, newInfo!);
        }

        if (appendId3)
        {
            WriteChunk(destination, "id3 ", newId3!);
        }

        var riffSize = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(riffSize, checked((uint)(destination.Length - 8)));
        destination.Position = 4;
        destination.Write(riffSize);
        destination.Flush(flushToDisk: true);
        return Result.Success;
    }

    private static void WriteChunk(Stream destination, string id, byte[] body)
    {
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)body.Length);
        destination.Write(Encoding.ASCII.GetBytes(id));
        destination.Write(size);
        destination.Write(body);
        if ((body.Length & 1) == 1)
        {
            destination.WriteByte(0);
        }
    }
}
