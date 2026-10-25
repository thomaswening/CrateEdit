using System.Buffers.Binary;
using System.Text;

namespace CrateEdit.Tests.Tags;

/// <summary>A raw RIFF chunk as stored in a file.</summary>
internal sealed record RawChunk(string Id, long Offset, byte[] Body)
{
    public bool SameBytesAs(RawChunk other) => Id == other.Id && Body.AsSpan().SequenceEqual(other.Body);

    public override string ToString() => $"{Id} ({Body.Length} bytes at {Offset})";
}

/// <summary>
/// Builds and parses synthetic WAV files independently of the production code (see
/// https://github.com/thomaswening/CrateEdit/blob/main/docs/formats/wav.md). Audio is random bytes that the tag code must copy unchanged.
/// </summary>
internal static class WavFixture
{
    public static byte[] Chunk(string id, byte[] body)
    {
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)body.Length);
        return [.. Encoding.ASCII.GetBytes(id), .. size, .. body, .. (body.Length & 1) == 1 ? [(byte)0] : Array.Empty<byte>()];
    }

    /// <summary>A WAV file from the given chunks, with a correct RIFF size.</summary>
    public static byte[] Build(params byte[][] chunks)
    {
        byte[] body = [.. "WAVE"u8, .. chunks.SelectMany(c => c)];
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)body.Length);
        return [.. "RIFF"u8, .. size, .. body];
    }

    /// <summary>PCM, 2 channels, 44.1 kHz, 16 bit.</summary>
    public static byte[] Fmt() => Chunk("fmt ", [1, 0, 2, 0, 0x44, 0xAC, 0, 0, 0x10, 0xB1, 2, 0, 4, 0, 16, 0]);

    public static byte[] Data(int length = 10_000) => Chunk("data", Mp3Fixture.Audio(length));

    /// <summary>A LIST/INFO chunk with UTF-8 values.</summary>
    public static byte[] Info(params (string Id, string Value)[] entries) =>
        InfoRaw(entries.Select(e => (e.Id, (byte[])[.. Encoding.UTF8.GetBytes(e.Value), 0])).ToArray());

    /// <summary>A LIST/INFO chunk followed by zero bytes inside the list, as some software writes it.</summary>
    public static byte[] InfoWithTrailingZeros(int count, params (string Id, string Value)[] entries) =>
        Chunk("LIST", [.. Info(entries)[8..], .. new byte[count]]);

    public static byte[] InfoRaw(params (string Id, byte[] Value)[] entries) =>
        Chunk("LIST", [.. "INFO"u8, .. entries.SelectMany(e => Chunk(e.Id, e.Value))]);

    public static byte[] Id3(IEnumerable<RawFrame> frames, int version = 3, int padding = 64) =>
        Chunk("id3 ", Mp3Fixture.Tag(version, frames, padding));

    /// <summary>A copy of a file whose last chunk has an odd size, without its pad byte, as some writers produce.</summary>
    public static byte[] WithoutFinalPadByte(byte[] file)
    {
        var trimmed = file[..^1];
        BinaryPrimitives.WriteUInt32LittleEndian(trimmed.AsSpan(4), (uint)(trimmed.Length - 8));
        return trimmed;
    }

    public static ParsedWav Parse(byte[] file) => new(ParseChunks(file));

    private static List<RawChunk> ParseChunks(byte[] file)
    {
        var chunks = new List<RawChunk>();
        var position = 12;
        while (position + 8 <= file.Length)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(position + 4));
            chunks.Add(new RawChunk(Encoding.ASCII.GetString(file, position, 4), position, file[(position + 8)..(position + 8 + size)]));
            position += 8 + size + (size & 1);
        }

        Assert.Equal(file.Length - 8, (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4)));
        return chunks;
    }

    /// <summary>The entries of a LIST/INFO chunk body.</summary>
    public static Dictionary<string, byte[]> InfoEntries(byte[] listBody)
    {
        var entries = new Dictionary<string, byte[]>();
        var position = 4;
        while (position + 8 <= listBody.Length)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(listBody.AsSpan(position + 4));
            entries[Encoding.ASCII.GetString(listBody, position, 4)] = listBody[(position + 8)..(position + 8 + size)];
            position += 8 + size + (size & 1);
        }

        return entries;
    }

    /// <summary>The frames of the ID3v2 tag inside an "id3 " chunk body.</summary>
    public static List<RawFrame> Id3Frames(byte[] id3Body) => Mp3Fixture.Parse(id3Body).Frames;
}

/// <summary>The chunks of a WAV file, with accessors for the two tag chunks.</summary>
internal sealed record ParsedWav(IReadOnlyList<RawChunk> Chunks)
{
    public IEnumerable<string> ChunkIds => Chunks.Select(c => c.Id);

    /// <summary>The only chunk with this ID; fails the test if there is none or more than one.</summary>
    public RawChunk Chunk(string id) => Assert.Single(Chunks, c => c.Id == id);

    public RawChunk LastChunk => Chunks[^1];

    public bool HasInfo => Chunks.Any(c => c.Id == "LIST");

    /// <summary>Some software names the ID3 chunk in upper case.</summary>
    public bool HasId3 => Chunks.Any(IsId3Chunk);

    /// <summary>The raw entries of the LIST/INFO chunk.</summary>
    public Dictionary<string, byte[]> InfoEntries => WavFixture.InfoEntries(Chunk("LIST").Body);

    /// <summary>An INFO entry decoded as UTF-8 without its terminator, or null if it is missing.</summary>
    public string? InfoText(string id) =>
        InfoEntries.TryGetValue(id, out var value) ? Encoding.UTF8.GetString(value).TrimEnd('\0') : null;

    public List<RawFrame> Id3Frames => WavFixture.Id3Frames(Assert.Single(Chunks, IsId3Chunk).Body);

    /// <summary>The only ID3 frame with this ID; fails the test if there is none or more than one.</summary>
    public RawFrame Id3Frame(string id) => Assert.Single(Id3Frames, f => f.Id == id);

    public IEnumerable<string> Id3FrameIds => Id3Frames.Select(f => f.Id);

    /// <summary>All chunks except the two tag chunks (LIST/INFO and id3).</summary>
    public IEnumerable<RawChunk> ChunksOtherThanTags => Chunks.Where(c => c.Id != "LIST" && !IsId3Chunk(c));

    private static bool IsId3Chunk(RawChunk chunk) => chunk.Id is "id3 " or "ID3 ";
}

/// <summary>Assertions that compare a WAV file before and after an edit.</summary>
internal static class WavAssert
{
    /// <summary>The chunks are in the same order, and all but the tag chunks are byte-identical.</summary>
    public static void OnlyTagChunksChanged(ParsedWav before, ParsedWav after)
    {
        Assert.Equal(before.ChunkIds, after.ChunkIds);
        foreach (var (chunkBefore, chunkAfter) in before.ChunksOtherThanTags.Zip(after.ChunksOtherThanTags))
        {
            Assert.True(chunkBefore.SameBytesAs(chunkAfter), $"{chunkBefore} changed.");
        }
    }

    /// <summary>The non-tag chunks are byte-identical, wherever they are now.</summary>
    public static void OtherChunksKept(ParsedWav before, ParsedWav after)
    {
        foreach (var chunk in before.ChunksOtherThanTags)
        {
            Assert.True(chunk.SameBytesAs(after.Chunk(chunk.Id)), $"{chunk} changed.");
        }
    }

    /// <summary>Every INFO entry not in <paramref name="editedIds"/> is kept byte for byte. Passes if there was no INFO.</summary>
    public static void OtherInfoEntriesKept(ParsedWav before, ParsedWav after, params string[] editedIds)
    {
        if (!before.HasInfo)
        {
            return;
        }

        var entriesAfter = after.InfoEntries;
        foreach (var (id, value) in before.InfoEntries.Where(entry => !editedIds.Contains(entry.Key)))
        {
            Assert.True(entriesAfter.TryGetValue(id, out var kept) && kept.AsSpan().SequenceEqual(value), $"INFO entry {id} changed.");
        }
    }

    /// <summary>Every ID3 frame not in <paramref name="editedIds"/> is kept byte for byte. Passes if there was no ID3 chunk.</summary>
    public static void OtherId3FramesKept(ParsedWav before, ParsedWav after, params string[] editedIds)
    {
        if (!before.HasId3)
        {
            return;
        }

        var framesAfter = after.Id3Frames;
        foreach (var frame in before.Id3Frames.Where(f => !editedIds.Contains(f.Id)))
        {
            Assert.Contains(framesAfter, kept => kept.SameBytesAs(frame));
        }
    }
}
