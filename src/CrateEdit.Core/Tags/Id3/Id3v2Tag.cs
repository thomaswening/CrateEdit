using System.Buffers.Binary;
using System.Text;
using CrateEdit.Core.Results;

namespace CrateEdit.Core.Tags.Id3;

// Byte layout of ID3v2 tags, with an annotated example: docs/formats/id3.md

/// <summary>One ID3v2 frame, kept as raw bytes so it can be written back unchanged.</summary>
internal sealed record Id3v2Frame(string Id, ushort Flags, byte[] Body)
{
    // Format flags change how the body is stored (compression, encryption, unsynchronisation,
    // grouping, data length indicator). Frames with any of these cannot be read or replaced as
    // plain text. The flag bits differ between ID3v2.3 and ID3v2.4.
    private const ushort V3FormatFlags = 0x00E0;
    private const ushort V4FormatFlags = 0x004F;

    public bool HasFormatFlags(int version) => (Flags & (version == 4 ? V4FormatFlags : V3FormatFlags)) != 0;
}

/// <summary>
/// The 10-byte header at the start of an ID3v2 tag:
/// <c>"ID3"</c>, major version, revision, flags, and the tag size as a synchsafe integer.
/// </summary>
internal readonly record struct Id3v2Header(int Version, byte Revision, byte Flags, int TagSizeBytes)
{
    public const int SizeBytes = 10;

    // Header flags: each is one bit of the flags byte, tested with `Flags & flag`.

    /// <summary>
    /// The tag is "unsynchronised": a 0x00 was inserted after every 0xFF so no byte pair in the
    /// tag looks like the start of audio. Bytes on disk then differ from the real frame bytes.
    /// </summary>
    public const byte UnsynchronisationFlag = 0x80;

    /// <summary>An optional extra block follows the header (may contain a checksum over the frames).</summary>
    public const byte ExtendedHeaderFlag = 0x40;

    /// <summary>ID3v2.4 only: a copy of the header follows the tag, so it can be found from the end.</summary>
    public const byte FooterFlag = 0x10;

    /// <summary>Bytes the whole tag occupies at the start of the file: header, tag body and footer.</summary>
    public int TotalSizeBytes => SizeBytes + TagSizeBytes + (HasFlag(FooterFlag) ? SizeBytes : 0);

    public bool HasFlag(byte flag) => (Flags & flag) != 0;

    /// <summary>Parses the header from the first 10 bytes of a file, or returns null if they are not an ID3v2 header.</summary>
    public static Id3v2Header? TryParse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < SizeBytes || !bytes.StartsWith("ID3"u8) || !Synchsafe.IsValid(bytes[6..10]))
        {
            return null;
        }

        return new Id3v2Header(bytes[3], bytes[4], bytes[5], Synchsafe.Read(bytes[6..10]));
    }
}

/// <summary>
/// A parsed ID3v2.3 or ID3v2.4 tag. Frames are kept in their original order and byte form;
/// only frames that are explicitly replaced are ever re-encoded.
/// </summary>
internal sealed class Id3v2Tag
{
    /// <summary>Every frame starts with a 10-byte frame header: 4-byte id, 4-byte body size, 2 flag bytes.</summary>
    public const int FrameHeaderSizeBytes = 10;

    private Id3v2Tag(Id3v2Header header, List<Id3v2Frame> frames, TagNotWritable? problem)
    {
        Header = header;
        Frames = frames;
        Problem = problem;
    }

    public Id3v2Header Header { get; }

    /// <summary>Major version: 3 or 4.</summary>
    public int Version => Header.Version;

    public IReadOnlyList<Id3v2Frame> Frames { get; }

    /// <summary>Null if the tag can be rewritten safely; otherwise why not.</summary>
    public TagNotWritable? Problem { get; }

    /// <summary>
    /// Parses the frames of a tag. <paramref name="tag"/> holds the whole tag as described by
    /// <paramref name="header"/> (<see cref="Id3v2Header.TotalSizeBytes"/> bytes, starting with the header).
    /// </summary>
    public static Id3v2Tag Parse(Id3v2Header header, ReadOnlySpan<byte> tag)
    {
        var frames = new List<Id3v2Frame>();

        if (header.Version is not (3 or 4))
        {
            return new Id3v2Tag(header, frames, new TagNotWritable(TagProblem.UnsupportedVersion, Version: header.Version));
        }

        if (UsesUnsupportedFeature(header))
        {
            return new Id3v2Tag(header, frames, new TagNotWritable(TagProblem.UnsupportedFeature, Version: header.Version));
        }

        // Walk the frames: read a frame header, take the body by its declared size, continue
        // directly after it. A zero byte where the next frame id should start begins the padding.
        var body = tag.Slice(Id3v2Header.SizeBytes, header.TagSizeBytes);
        var position = 0;
        while (HasAnotherFrame(body, position))
        {
            var frame = ReadFrame(body, position, header.Version);
            if (frame.IsFailure)
            {
                return Malformed(((TagNotWritable)frame.Error).FrameId, position);
            }

            frames.Add(frame.Value);
            position += FrameHeaderSizeBytes + frame.Value.Body.Length;
        }

        // Everything after the last frame must be padding. Anything else means the tag was not
        // fully understood, and rewriting it could lose data.
        if (body[position..].ContainsAnyExcept((byte)0))
        {
            return Malformed(frameId: null, position);
        }

        return new Id3v2Tag(header, frames, null);

        Id3v2Tag Malformed(string? frameId, int bodyPosition) => new(header, frames,
            new TagNotWritable(TagProblem.Malformed, frameId, Id3v2Header.SizeBytes + bodyPosition, header.Version));
    }

    /// <summary>
    /// Renders a tag with the same header fields and the given frames. Keeps the original tag size
    /// when the frames fit (so the audio does not move); otherwise grows with
    /// <paramref name="paddingWhenGrowingBytes"/> of new padding.
    /// </summary>
    public byte[] Render(IReadOnlyList<Id3v2Frame> frames, int paddingWhenGrowingBytes)
    {
        var framesSizeBytes = frames.Sum(f => FrameHeaderSizeBytes + f.Body.Length);
        var tagSizeBytes = framesSizeBytes <= Header.TagSizeBytes ? Header.TagSizeBytes : framesSizeBytes + paddingWhenGrowingBytes;
        var result = new byte[Id3v2Header.SizeBytes + tagSizeBytes];

        // Header. Bytes after the last frame stay zero: that is the padding.
        "ID3"u8.CopyTo(result);
        result[3] = (byte)Header.Version;
        result[4] = Header.Revision;
        result[5] = Header.Flags;
        Synchsafe.Write(result.AsSpan(6, 4), tagSizeBytes);

        var position = Id3v2Header.SizeBytes;
        foreach (var frame in frames)
        {
            Encoding.ASCII.GetBytes(frame.Id, result.AsSpan(position, 4));
            if (Header.Version == 4)
            {
                Synchsafe.Write(result.AsSpan(position + 4, 4), frame.Body.Length);
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(position + 4, 4), frame.Body.Length);
            }

            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(position + 8, 2), frame.Flags);
            frame.Body.CopyTo(result, position + FrameHeaderSizeBytes);
            position += FrameHeaderSizeBytes + frame.Body.Length;
        }

        return result;
    }

    /// <summary>
    /// Unsynchronisation, an extended header and a footer each change how the bytes after the
    /// header must be interpreted or kept consistent; rewriting such a tag correctly is not supported.
    /// </summary>
    private static bool UsesUnsupportedFeature(Id3v2Header header) =>
        header.HasFlag(Id3v2Header.UnsynchronisationFlag) || header.HasFlag(Id3v2Header.ExtendedHeaderFlag) ||
        header.HasFlag(Id3v2Header.FooterFlag);

    /// <summary>
    /// Another frame follows if a complete frame header fits before the end of the tag and the next
    /// byte is not zero (a zero byte where a frame id would start begins the padding).
    /// </summary>
    private static bool HasAnotherFrame(ReadOnlySpan<byte> body, int position) =>
        position + FrameHeaderSizeBytes <= body.Length && body[position] != 0;

    /// <summary>Reads the frame at <paramref name="position"/>: header (id, size, flags), then the body by its size.</summary>
    /// <returns>The frame, or <see cref="TagNotWritable"/> if its id or size is invalid.</returns>
    private static Result<Id3v2Frame> ReadFrame(ReadOnlySpan<byte> body, int position, int version)
    {
        var idBytes = body.Slice(position, 4);
        if (!IsValidFrameId(idBytes))
        {
            return new TagNotWritable(TagProblem.Malformed);
        }

        var id = Encoding.ASCII.GetString(idBytes);
        var sizeBytes = body.Slice(position + 4, 4);
        if (!IsValidFrameSize(sizeBytes, version, out var bodySize) || !FitsInTag(body, position, bodySize))
        {
            return new TagNotWritable(TagProblem.Malformed, id);
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(body.Slice(position + 8, 2));
        return new Id3v2Frame(id, flags, body.Slice(position + FrameHeaderSizeBytes, bodySize).ToArray());
    }

    /// <summary>
    /// Decodes the frame body size: synchsafe in ID3v2.4, a plain 32-bit integer in ID3v2.3. A
    /// negative size, or a v2.4 size byte with its top bit set, means the size field is corrupt.
    /// </summary>
    private static bool IsValidFrameSize(ReadOnlySpan<byte> sizeBytes, int version, out int bodySize)
    {
        if (version == 4)
        {
            bodySize = Synchsafe.Read(sizeBytes);
            return Synchsafe.IsValid(sizeBytes);
        }

        bodySize = BinaryPrimitives.ReadInt32BigEndian(sizeBytes);
        return bodySize >= 0;
    }

    /// <summary>The frame body must end within the tag; otherwise the tag is truncated or the size is wrong.</summary>
    private static bool FitsInTag(ReadOnlySpan<byte> body, int position, int frameBodySize) =>
        position + FrameHeaderSizeBytes + frameBodySize <= body.Length;

    /// <summary>A new, empty ID3v2.3 tag for files without one; v2.3 is read by all major DJ software.</summary>
    public static Id3v2Tag CreateEmpty() => new(new Id3v2Header(3, 0, 0, 0), [], null);

    /// <summary>Frame ids are four characters from A-Z and 0-9, e.g. "TIT2".</summary>
    private static bool IsValidFrameId(ReadOnlySpan<byte> id) =>
        !id.ContainsAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"u8);
}

/// <summary>
/// Synchsafe integers store 7 bits per byte and keep the top bit of every byte at 0, so a size
/// field can never contain 0xFF and be mistaken for the start of MPEG audio. 4 bytes hold 28 bits.
/// </summary>
internal static class Synchsafe
{
    public const int MaxValue = (1 << 28) - 1;

    /// <summary>True if no byte has its top bit (0x80) set.</summary>
    public static bool IsValid(ReadOnlySpan<byte> bytes) => !bytes.ContainsAnyInRange((byte)0x80, (byte)0xFF);

    public static int Read(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 21) | (bytes[1] << 14) | (bytes[2] << 7) | bytes[3];

    public static void Write(Span<byte> destination, int value)
    {
        if (value is < 0 or > MaxValue)
        {
            throw new InvalidOperationException("ID3v2 sizes must be below 256 MB.");
        }

        destination[0] = (byte)((value >> 21) & 0x7F);
        destination[1] = (byte)((value >> 14) & 0x7F);
        destination[2] = (byte)((value >> 7) & 0x7F);
        destination[3] = (byte)(value & 0x7F);
    }
}
