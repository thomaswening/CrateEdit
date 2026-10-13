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
/// never moves. Byte layout: https://github.com/thomaswening/CrateEdit/blob/main/docs/formats/wav.md
/// </summary>
/// <remarks>Only the apply pipeline may use this class, like <see cref="Mp3TagWriter"/>.</remarks>
public static class WavTagWriter
{
    private const string NewId3ChunkId = "id3 ";

    /// <summary>
    /// Writes <paramref name="sourcePath"/> with <paramref name="changes"/> applied to
    /// <paramref name="destinationPath"/>. A null or empty value clears the field.
    /// </summary>
    /// <returns>Success, or <see cref="TagNotWritable"/> without creating the destination. I/O failures throw.</returns>
    public static Result Rewrite(string sourcePath, string destinationPath, IReadOnlyDictionary<TagField, string?> changes)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var structure = ReadStructure(source);
        if (structure.IsFailure)
        {
            return structure.Error;
        }

        var (riff, parts) = structure.Value;
        var target = TargetValues(WavTagReader.Combine(parts), changes);

        var id3 = BuildId3Chunk(parts.Id3, FieldsToWrite(parts.Id3 is not null, changes), target);
        if (id3.IsFailure)
        {
            return id3.Error;
        }

        var info = BuildInfoChunk(parts.Info, FieldsToWrite(parts.Info is not null, changes), target);

        // An existing INFO chunk is only rebuilt if one of its fields was edited (REQ-130).
        var infoEdited = parts.Info is not null && changes.Keys.Any(field => InfoChunk.IdFor(field) is not null);

        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        WriteFile(source, destination, riff, parts, id3.Value, info, infoEdited);
        return Result.Success;
    }

    private static Result<(RiffFile Riff, WavTagParts Parts)> ReadStructure(Stream source)
    {
        var riff = RiffFile.Read(source);
        if (riff.IsFailure)
        {
            return riff.Error;
        }

        var parts = WavTagReader.ReadParts(riff.Value);
        if (parts.IsFailure)
        {
            return parts.Error;
        }

        return (riff.Value, parts.Value);
    }

    /// <summary>
    /// The value every field should have after the edit: the new value for edited fields (null
    /// when cleared), the current value for all others.
    /// </summary>
    private static Dictionary<TagField, string?> TargetValues(TagValues current, IReadOnlyDictionary<TagField, string?> changes)
    {
        var target = new Dictionary<TagField, string?>();
        foreach (var field in Enum.GetValues<TagField>())
        {
            if (changes.TryGetValue(field, out var changed))
            {
                // An empty value clears the field.
                target[field] = string.IsNullOrEmpty(changed) ? null : changed;
            }
            else
            {
                target[field] = current[field];
            }
        }

        return target;
    }

    /// <summary>
    /// In an existing chunk only the edited fields are written. A new chunk gets every field,
    /// so other software sees complete tags.
    /// </summary>
    private static IEnumerable<TagField> FieldsToWrite(bool chunkExists, IReadOnlyDictionary<TagField, string?> changes) =>
        chunkExists ? changes.Keys : Enum.GetValues<TagField>();

    /// <summary>The new "id3 " chunk body, or null if the file has none and there is nothing to put in one.</summary>
    private static Result<byte[]?> BuildId3Chunk(Id3v2Tag? existing, IEnumerable<TagField> fields, Dictionary<TagField, string?> target)
    {
        var tag = existing ?? Id3v2Tag.CreateEmpty();
        IReadOnlyList<Id3v2Frame> frames = tag.Frames;
        foreach (var field in fields)
        {
            if (Id3FieldMap.CheckEditable(frames, field, tag.Version) is { } problem)
            {
                return problem;
            }

            frames = Id3FieldMap.Apply(frames, field, target[field], tag.Version);
        }

        if (existing is null && frames.Count == 0)
        {
            return (byte[]?)null;
        }

        return tag.Render(frames, Mp3TagWriter.PaddingWhenGrowingBytes);
    }

    /// <summary>The new LIST/INFO chunk body, or null if the file has none and there is nothing to put in one.</summary>
    private static byte[]? BuildInfoChunk(List<InfoEntry>? existing, IEnumerable<TagField> fields, Dictionary<TagField, string?> target)
    {
        var entries = existing is null ? [] : new List<InfoEntry>(existing);
        foreach (var field in fields)
        {
            // Label, BPM and key have no INFO entry; they only go into the "id3 " chunk.
            if (InfoChunk.IdFor(field) is { } id)
            {
                SetEntry(entries, id, target[field]);
            }
        }

        return existing is null && entries.Count == 0 ? null : InfoChunk.Render(entries);
    }

    /// <summary>Replaces the entry in place, appends it if missing, or removes it when the value is null.</summary>
    private static void SetEntry(List<InfoEntry> entries, string id, string? value)
    {
        var index = entries.FindIndex(e => e.Id == id);
        if (value is null)
        {
            if (index >= 0)
            {
                entries.RemoveAt(index);
            }
        }
        else if (index >= 0)
        {
            entries[index] = new InfoEntry(id, InfoChunk.Encode(value));
        }
        else
        {
            entries.Add(new InfoEntry(id, InfoChunk.Encode(value)));
        }
    }

    /// <summary>
    /// Writes all chunks in their original order, rebuilt tag chunks in place, new tag chunks at the
    /// end, then the RIFF size in the header.
    /// </summary>
    private static void WriteFile(Stream source, FileStream destination, RiffFile riff, WavTagParts parts,
        byte[]? newId3, byte[]? newInfo, bool infoEdited)
    {
        destination.Write("RIFF"u8);
        destination.Write(new byte[4]); // the total size, known only at the end
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
                lastChunkLacksPad = !CopyChunk(source, destination, chunk, riff.Length);
            }
        }

        var appendInfo = parts.InfoChunk is null && newInfo is not null;
        var appendId3 = parts.Id3Chunk is null && newId3 is not null;
        if (lastChunkLacksPad && (appendInfo || appendId3))
        {
            // The original last chunk was stored without its pad byte; a new chunk must start at an even offset.
            destination.WriteByte(0);
        }

        if (appendInfo)
        {
            WriteChunk(destination, InfoChunk.ListId, newInfo!);
        }

        if (appendId3)
        {
            WriteChunk(destination, NewId3ChunkId, newId3!);
        }

        WriteRiffSize(destination);
        destination.Flush(flushToDisk: true);
    }

    /// <summary>Copies a chunk exactly as stored, including the audio and any pad byte.</summary>
    /// <returns>False if the chunk was stored without the pad byte its odd size calls for (only possible at the end of a file).</returns>
    private static bool CopyChunk(Stream source, Stream destination, RiffChunk chunk, long fileLength)
    {
        var storedBytes = Math.Min(chunk.TotalSizeBytes, fileLength - chunk.Offset);
        source.Position = chunk.Offset;
        StreamCopy.CopyBytes(source, destination, storedBytes);
        return storedBytes == chunk.TotalSizeBytes;
    }

    private static void WriteChunk(Stream destination, string id, byte[] body)
    {
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)body.Length);
        destination.Write(Encoding.ASCII.GetBytes(id));
        destination.Write(size);
        destination.Write(body);

        // RIFF requires every chunk to start at an even offset, so an odd-sized body is followed by
        // one zero pad byte. The pad byte is not counted in the chunk size.
        if ((body.Length & 1) == 1)
        {
            destination.WriteByte(0);
        }
    }

    /// <summary>The RIFF size field counts everything after itself: the whole file minus the first 8 bytes.</summary>
    private static void WriteRiffSize(Stream destination)
    {
        var riffSize = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(riffSize, checked((uint)(destination.Length - 8)));
        destination.Position = 4;
        destination.Write(riffSize);
    }
}
