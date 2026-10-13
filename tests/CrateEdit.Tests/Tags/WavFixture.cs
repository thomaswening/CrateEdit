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
/// docs/formats/wav.md). Audio is random bytes that the tag code must copy unchanged.
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

    public static byte[] InfoRaw(params (string Id, byte[] Value)[] entries) =>
        Chunk("LIST", [.. "INFO"u8, .. entries.SelectMany(e => Chunk(e.Id, e.Value))]);

    public static byte[] Id3(IEnumerable<RawFrame> frames, int version = 3, int padding = 64) =>
        Chunk("id3 ", Mp3Fixture.Tag(version, frames, padding));

    public static List<RawChunk> Parse(byte[] file)
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
