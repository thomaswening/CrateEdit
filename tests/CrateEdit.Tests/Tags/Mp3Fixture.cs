using System.Buffers.Binary;
using System.Text;

namespace CrateEdit.Tests.Tags;

/// <summary>A raw ID3v2 frame as stored in a file.</summary>
internal sealed record RawFrame(string Id, ushort Flags, byte[] Body)
{
    public bool SameBytesAs(RawFrame other) => Id == other.Id && Flags == other.Flags && Body.AsSpan().SequenceEqual(other.Body);

    public override string ToString() => $"{Id} ({Body.Length} bytes)";
}

/// <summary>
/// Builds synthetic MP3 files and parses them back, independently of the production code, so
/// tests never verify the writer with its own parser. Audio is random bytes: the tag code must
/// never interpret or change it.
/// </summary>
internal static class Mp3Fixture
{
    public static byte[] Build(int version, IEnumerable<RawFrame> frames, int padding = 256, int audioLength = 20_000,
        byte[]? id3v1 = null, byte flags = 0)
    {
        var body = new List<byte>();
        foreach (var frame in frames)
        {
            body.AddRange(Encoding.ASCII.GetBytes(frame.Id));
            var size = new byte[4];
            if (version == 4)
            {
                WriteSynchsafe(size, frame.Body.Length);
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(size, frame.Body.Length);
            }

            body.AddRange(size);
            body.Add((byte)(frame.Flags >> 8));
            body.Add((byte)frame.Flags);
            body.AddRange(frame.Body);
        }

        body.AddRange(new byte[padding]);
        var header = new byte[10];
        "ID3"u8.CopyTo(header);
        header[3] = (byte)version;
        header[5] = flags;
        WriteSynchsafe(header.AsSpan(6), body.Count);

        return [.. header, .. body, .. Audio(audioLength), .. id3v1 ?? []];
    }

    public static byte[] BuildWithoutId3v2(int audioLength = 20_000, byte[]? id3v1 = null) => [.. Audio(audioLength), .. id3v1 ?? []];

    /// <summary>Deterministic pseudo-random audio starting with an MPEG frame sync.</summary>
    public static byte[] Audio(int length)
    {
        var audio = new byte[length];
        new Random(42).NextBytes(audio);
        audio[0] = 0xFF;
        audio[1] = 0xFB;
        return audio;
    }

    public static ParsedMp3 Parse(byte[] file)
    {
        if (!file.AsSpan().StartsWith("ID3"u8))
        {
            return new ParsedMp3(null, 0, [], SplitAudio(file, 0), Id3v1(file));
        }

        int version = file[3], size = ReadSynchsafe(file.AsSpan(6)), position = 10;
        var frames = new List<RawFrame>();
        while (position < 10 + size && file[position] != 0)
        {
            var frameSize = version == 4 ? ReadSynchsafe(file.AsSpan(position + 4)) : BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(position + 4));
            frames.Add(new RawFrame(
                Encoding.ASCII.GetString(file, position, 4),
                BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(position + 8)),
                file[(position + 10)..(position + 10 + frameSize)]));
            position += 10 + frameSize;
        }

        return new ParsedMp3(version, size, frames, SplitAudio(file, 10 + size), Id3v1(file));
    }

    private static byte[] SplitAudio(byte[] file, int start) => file[start..(file.Length - (Id3v1(file) is null ? 0 : 128))];

    private static byte[]? Id3v1(byte[] file) =>
        file.Length >= 128 && file.AsSpan(file.Length - 128).StartsWith("TAG"u8) ? file[^128..] : null;

    private static int ReadSynchsafe(ReadOnlySpan<byte> b) => (b[0] << 21) | (b[1] << 14) | (b[2] << 7) | b[3];

    private static void WriteSynchsafe(Span<byte> d, int v)
    {
        d[0] = (byte)((v >> 21) & 0x7F);
        d[1] = (byte)((v >> 14) & 0x7F);
        d[2] = (byte)((v >> 7) & 0x7F);
        d[3] = (byte)(v & 0x7F);
    }
}

internal sealed record ParsedMp3(int? Version, int TagSize, List<RawFrame> Frames, byte[] Audio, byte[]? Id3v1);

/// <summary>Frame bodies resembling real files: store downloads and DJ software data.</summary>
internal static class Frames
{
    public static RawFrame Text(string id, string value, byte encoding = 3, bool terminated = true) =>
        new(id, 0, [encoding, .. Encode(value, encoding), .. terminated ? Terminator(encoding) : []]);

    public static RawFrame Txxx(string description, string value) =>
        new("TXXX", 0, [3, .. Encoding.UTF8.GetBytes(description), 0, .. Encoding.UTF8.GetBytes(value), 0]);

    public static RawFrame Comment(string description, string value, byte encoding = 3, string language = "eng") =>
        new("COMM", 0, [encoding, .. Encoding.Latin1.GetBytes(language), .. Encode(description, encoding), .. Terminator(encoding), .. Encode(value, encoding), .. Terminator(encoding)]);

    /// <summary>Beatport writes a UFID with an owner but no identifier.</summary>
    public static RawFrame UfidOwnerOnly(string owner) => new("UFID", 0, [.. Encoding.Latin1.GetBytes(owner), 0]);

    public static RawFrame Picture(int size = 5000) =>
        new("APIC", 0, [0, .. "image/jpeg"u8, 0, 3, .. "Cover (front)"u8, 0, .. RandomBytes(size, 7)]);

    public static RawFrame Url(string id, string url) => new(id, 0, Encoding.Latin1.GetBytes(url));

    /// <summary>Serato stores markers, beatgrid and overview as GEOB frames with binary payloads.</summary>
    public static RawFrame SeratoGeob(string description, int size, int seed) =>
        new("GEOB", 0, [0, .. "application/octet-stream"u8, 0, 0, .. Encoding.Latin1.GetBytes(description), 0, .. RandomBytes(size, seed)]);

    /// <summary>Traktor stores cues and beatgrid in a PRIV frame owned by "TRAKTOR4".</summary>
    public static RawFrame TraktorPriv(int size = 2000) => new("PRIV", 0, [.. "TRAKTOR4"u8, 0, .. RandomBytes(size, 11)]);

    public static IEnumerable<RawFrame> DjSoftwareData() =>
    [
        SeratoGeob("Serato Analysis", 2, 1),
        SeratoGeob("Serato Markers_", 470, 2),
        SeratoGeob("Serato Markers2", 900, 3),
        SeratoGeob("Serato BeatGrid", 39, 4),
        SeratoGeob("Serato Overview", 3842, 5),
        TraktorPriv(),
    ];

    /// <summary>Frames like a Beatport download: ID3v2.4, UTF-8, TXXX duplicates, UFID, store comment.</summary>
    public static IEnumerable<RawFrame> BeatportLike() =>
    [
        Text("TIT2", "Deep Inside (Deep Inside Remix)"),
        Text("TPE1", "Hardrive"),
        Text("TALB", "Deep Inside (Remixes)"),
        Text("TDRC", "2026"),
        Text("TCON", "Dance / Pop"),
        Text("TBPM", "157"),
        Text("TKEY", "Bmin"),
        Txxx("bpm", "157"),
        UfidOwnerOnly("track-23872911"),
        Txxx("initial_key", "B Minor"),
        Text("TPUB", "Strictly Rhythm Records"),
        Txxx("label", "Strictly Rhythm Records"),
        Txxx("organization", "Strictly Rhythm Records"),
        Comment("ID3v1 Comment", "Purchased at Beatport", encoding: 0),
        Url("WOAF", "https://www.beatport.com/track/deep-inside/23872911"),
        Picture(),
    ];

    /// <summary>A 128-byte ID3v1.1 tag with a track number.</summary>
    public static byte[] Id3v1(string title, string artist, byte genre = 35, byte track = 1)
    {
        var tag = new byte[128];
        "TAG"u8.CopyTo(tag);
        Encoding.Latin1.GetBytes(title).AsSpan(0, Math.Min(30, title.Length)).CopyTo(tag.AsSpan(3));
        Encoding.Latin1.GetBytes(artist).AsSpan(0, Math.Min(30, artist.Length)).CopyTo(tag.AsSpan(33));
        "2026"u8.CopyTo(tag.AsSpan(93));
        "Purchased at Traxsource"u8.CopyTo(tag.AsSpan(97));
        tag[126] = track;
        tag[127] = genre;
        return tag;
    }

    public static byte[] RandomBytes(int size, int seed)
    {
        var bytes = new byte[size];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static byte[] Encode(string value, byte encoding) => encoding switch
    {
        0 => Encoding.Latin1.GetBytes(value),
        1 => [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(value)],
        2 => Encoding.BigEndianUnicode.GetBytes(value),
        _ => Encoding.UTF8.GetBytes(value),
    };

    private static byte[] Terminator(byte encoding) => encoding is 1 or 2 ? [0, 0] : [0];
}
