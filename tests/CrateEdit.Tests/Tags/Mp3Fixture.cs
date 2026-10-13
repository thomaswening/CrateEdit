using System.Buffers.Binary;
using System.Text;

namespace CrateEdit.Tests.Tags;

/// <summary>A raw ID3v2 frame as stored in a file.</summary>
internal sealed record RawFrame(string Id, ushort Flags, byte[] Body)
{
    public bool SameBytesAs(RawFrame other) => Id == other.Id && Flags == other.Flags && Body.AsSpan().SequenceEqual(other.Body);

    /// <summary>The text encoding of a text or comment frame: its first body byte.</summary>
    public byte TextEncoding => Body[0];

    /// <summary>
    /// A comment frame whose description is empty: after the encoding byte and the 3-byte language
    /// comes the terminator right away (for UTF-16, optionally preceded by a byte-order mark).
    /// </summary>
    public bool IsCommentWithoutDescription
    {
        get
        {
            if (Id != "COMM")
            {
                return false;
            }

            var afterLanguage = Body.AsSpan(4);
            return TextEncoding is TextEncodings.Utf16 or TextEncodings.Utf16BigEndian
                ? afterLanguage.StartsWith((ReadOnlySpan<byte>)[0, 0]) || afterLanguage[2..].StartsWith((ReadOnlySpan<byte>)[0, 0])
                : afterLanguage.StartsWith((ReadOnlySpan<byte>)[0]);
        }
    }

    /// <summary>The value of a text frame, decoded, without terminator or byte-order mark.</summary>
    public string Text
    {
        get
        {
            var encoding = TextEncoding switch
            {
                TextEncodings.Latin1 => Encoding.Latin1,
                TextEncodings.Utf16 => Encoding.Unicode,
                TextEncodings.Utf16BigEndian => Encoding.BigEndianUnicode,
                _ => Encoding.UTF8,
            };
            return encoding.GetString(Body, 1, Body.Length - 1).TrimStart('﻿').TrimEnd('\0');
        }
    }

    public override string ToString() => $"{Id} ({Body.Length} bytes)";
}

/// <summary>The text encodings of ID3v2 text frames (first byte of the frame body).</summary>
internal static class TextEncodings
{
    public const byte Latin1 = 0;
    public const byte Utf16 = 1;
    public const byte Utf16BigEndian = 2;
    public const byte Utf8 = 3;
}

/// <summary>
/// Builds synthetic MP3 files and parses them back, independently of the production code, so
/// tests never verify the writer with its own parser. Audio is random bytes: the tag code must
/// never interpret or change it.
/// </summary>
internal static class Mp3Fixture
{
    public static byte[] Build(int version, IEnumerable<RawFrame> frames, int padding = 256, int audioLength = 20_000,
        byte[]? id3v1 = null, byte flags = 0) =>
        [.. Tag(version, frames, padding, flags), .. Audio(audioLength), .. id3v1 ?? []];

    /// <summary>An ID3v2 tag on its own (header, frames, padding), e.g. for the "id3 " chunk of a WAV file.</summary>
    public static byte[] Tag(int version, IEnumerable<RawFrame> frames, int padding = 256, byte flags = 0)
    {
        var body = new List<byte>();
        foreach (var frame in frames)
        {
            // A UTF-8 frame in ID3v2.3 is invalid, so a test using one would not test what it claims.
            var hasTextEncoding = frame.Id.StartsWith('T') || frame.Id == "COMM";
            if (version == 3 && hasTextEncoding && frame.TextEncoding == TextEncodings.Utf8)
            {
                throw new ArgumentException($"{frame.Id}: ID3v2.3 has no UTF-8; use TextEncodings.Latin1 or Utf16.");
            }

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

        return [.. header, .. body];
    }

    /// <summary>A copy of the file with a non-zero byte at the end of the tag's padding.</summary>
    public static byte[] WithDataInPadding(byte[] file)
    {
        var copy = file.ToArray();
        var lastPaddingByte = 10 + Parse(file).TagSize - 1;
        copy[lastPaddingByte] = 0x42;
        return copy;
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

internal sealed record ParsedMp3(int? Version, int TagSize, List<RawFrame> Frames, byte[] Audio, byte[]? Id3v1)
{
    public IEnumerable<string> FrameIds => Frames.Select(f => f.Id);

    /// <summary>The only frame with this ID; fails the test if there is none or more than one.</summary>
    public RawFrame Frame(string id) => Assert.Single(Frames, f => f.Id == id);

    public IEnumerable<RawFrame> FramesWithId(string id) => Frames.Where(f => f.Id == id);

    public bool HasFrame(string id) => Frames.Any(f => f.Id == id);

    /// <summary>Comment frames with a description: other software's data, such as a store's "ID3v1 Comment".</summary>
    public IEnumerable<RawFrame> CommentsWithDescription => FramesWithId("COMM").Where(f => !f.IsCommentWithoutDescription);

    public IEnumerable<RawFrame> CommentsWithoutDescription => Frames.Where(f => f.IsCommentWithoutDescription);

    /// <summary>The ID3v1 tag at the end of the file, decoded; fails the test if there is none.</summary>
    public ParsedId3v1 ParsedId3v1 => ParsedId3v1.From(Assert.IsType<byte[]>(Id3v1));
}

/// <summary>The fields of a 128-byte ID3v1.1 tag, with padding removed.</summary>
internal sealed record ParsedId3v1(string Title, string Artist, byte Track, byte Genre)
{
    public static ParsedId3v1 From(byte[] tag) =>
        new(Field(tag, 3), Field(tag, 33), tag[126], tag[127]);

    private static string Field(byte[] tag, int offset) => Encoding.Latin1.GetString(tag, offset, 30).TrimEnd('\0');
}

/// <summary>Frame bodies resembling real files: store downloads and DJ software data.</summary>
internal static class Frames
{
    public static RawFrame Text(string id, string value, byte encoding = 3, bool terminated = true) =>
        new(id, 0, [encoding, .. Encode(value, encoding), .. terminated ? Terminator(encoding) : []]);

    public static RawFrame Txxx(string description, string value) =>
        new("TXXX", 0, [3, .. Encoding.UTF8.GetBytes(description), 0, .. Encoding.UTF8.GetBytes(value), 0]);

    public static RawFrame Comment(string description, string value, byte encoding = 3, string language = "eng", bool terminated = true) =>
        new("COMM", 0,
        [
            encoding, .. Encoding.Latin1.GetBytes(language),
            .. Encode(description, encoding), .. Terminator(encoding),
            .. Encode(value, encoding), .. terminated ? Terminator(encoding) : [],
        ]);

    /// <summary>Text frame with several values, as ID3v2.4 allows (UTF-8, separated by a null byte).</summary>
    public static RawFrame MultiValueText(string id, params string[] values) =>
        new(id, 0, [TextEncodings.Utf8, .. Encoding.UTF8.GetBytes(string.Join('\0', values))]);

    /// <summary>The same frame with compression flagged (ID3v2.4 format flag bit 3).</summary>
    public static RawFrame Compressed(RawFrame frame) => frame with { Flags = 0x0008 };

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
