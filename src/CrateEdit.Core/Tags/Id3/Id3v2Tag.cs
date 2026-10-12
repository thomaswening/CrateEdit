using System.Buffers.Binary;

namespace CrateEdit.Core.Tags.Id3;

/// <summary>One ID3v2 frame, kept as raw bytes so it can be written back unchanged.</summary>
internal sealed record Id3v2Frame(string Id, ushort Flags, byte[] Body)
{
    // Format flags that change how the body is stored (compression, encryption, unsynchronisation,
    // grouping, data length indicator). Frames with these cannot be read or replaced as plain text.
    private const ushort V3FormatFlags = 0x00E0;
    private const ushort V4FormatFlags = 0x004F;

    public bool HasFormatFlags(int version) => (Flags & (version == 4 ? V4FormatFlags : V3FormatFlags)) != 0;
}

/// <summary>
/// A parsed ID3v2.3 or ID3v2.4 tag at the start of a file. Frames are kept in their original
/// order and byte form; only frames that are explicitly replaced are ever re-encoded.
/// </summary>
internal sealed class Id3v2Tag
{
    public const int HeaderSize = 10;

    private const byte UnsynchronisationFlag = 0x80;
    private const byte ExtendedHeaderFlag = 0x40;
    private const byte FooterFlag = 0x10;

    private Id3v2Tag(int version, byte revision, byte flags, int size, List<Id3v2Frame> frames, string? notWritableReason)
    {
        Version = version;
        Revision = revision;
        Flags = flags;
        Size = size;
        Frames = frames;
        NotWritableReason = notWritableReason;
    }

    /// <summary>Major version: 3 or 4.</summary>
    public int Version { get; }

    public byte Revision { get; }

    public byte Flags { get; }

    /// <summary>Tag size from the header: everything after the 10-byte header, including padding.</summary>
    public int Size { get; }

    /// <summary>Total bytes the tag occupies at the start of the file, including header and footer.</summary>
    public int TotalSize => HeaderSize + Size + ((Flags & FooterFlag) != 0 ? HeaderSize : 0);

    public IReadOnlyList<Id3v2Frame> Frames { get; }

    /// <summary>Null if the tag can be rewritten safely; otherwise why not.</summary>
    public string? NotWritableReason { get; }

    /// <summary>Reads the tag size from a file's first 10 bytes, or null if there is no ID3v2 tag.</summary>
    public static int? ReadTotalSize(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderSize || !header.StartsWith("ID3"u8) || !IsSyncSafe(header[6..10]))
        {
            return null;
        }

        var footer = (header[5] & FooterFlag) != 0 ? HeaderSize : 0;
        return HeaderSize + ReadSyncSafe(header[6..10]) + footer;
    }

    /// <summary>Parses the tag from the start of <paramref name="data"/>, or returns null if there is none.</summary>
    public static Id3v2Tag? Parse(ReadOnlySpan<byte> data)
    {
        if (ReadTotalSize(data) is not { } totalSize || data.Length < totalSize)
        {
            return null;
        }

        var version = data[3];
        var revision = data[4];
        var flags = data[5];
        var size = ReadSyncSafe(data[6..10]);
        var frames = new List<Id3v2Frame>();

        if (version is not (3 or 4))
        {
            return new Id3v2Tag(version, revision, flags, size, frames, $"ID3v2.{version} is not supported.");
        }

        if ((flags & (UnsynchronisationFlag | ExtendedHeaderFlag | FooterFlag)) != 0)
        {
            return new Id3v2Tag(version, revision, flags, size, frames,
                "The tag uses unsynchronisation, an extended header or a footer, which are not supported.");
        }

        var body = data.Slice(HeaderSize, size);
        var position = 0;
        while (position + HeaderSize <= body.Length && body[position] != 0)
        {
            var id = body.Slice(position, 4);
            if (!IsValidFrameId(id))
            {
                return Invalid($"Invalid frame identifier at offset {HeaderSize + position}.");
            }

            var sizeBytes = body.Slice(position + 4, 4);
            var frameSize = version == 4 ? ReadSyncSafe(sizeBytes) : BinaryPrimitives.ReadInt32BigEndian(sizeBytes);
            if (frameSize < 0 || (version == 4 && !IsSyncSafe(sizeBytes)) ||
                position + HeaderSize + frameSize > body.Length)
            {
                return Invalid($"Frame {System.Text.Encoding.ASCII.GetString(id)} has an invalid size.");
            }

            frames.Add(new Id3v2Frame(
                System.Text.Encoding.ASCII.GetString(id),
                BinaryPrimitives.ReadUInt16BigEndian(body.Slice(position + 8, 2)),
                body.Slice(position + HeaderSize, frameSize).ToArray()));
            position += HeaderSize + frameSize;
        }

        // Everything after the last frame must be padding. Anything else means the tag was not
        // fully understood, and rewriting it could lose data.
        if (body[position..].ContainsAnyExcept((byte)0))
        {
            return Invalid($"Unrecognised data after the last frame at offset {HeaderSize + position}.");
        }

        return new Id3v2Tag(version, revision, flags, size, frames, null);

        Id3v2Tag Invalid(string reason) => new(version, revision, flags, size, frames, reason);
    }

    /// <summary>
    /// Renders a tag with the same header and the given frames. Uses the original size when the
    /// frames fit (so audio does not move); otherwise grows with <paramref name="paddingWhenGrowing"/>.
    /// </summary>
    public byte[] Render(IReadOnlyList<Id3v2Frame> frames, int paddingWhenGrowing)
    {
        var framesSize = frames.Sum(f => HeaderSize + f.Body.Length);
        var size = framesSize <= Size ? Size : framesSize + paddingWhenGrowing;
        var result = new byte[HeaderSize + size];

        "ID3"u8.CopyTo(result);
        result[3] = (byte)Version;
        result[4] = Revision;
        result[5] = Flags;
        WriteSyncSafe(result.AsSpan(6, 4), size);

        var position = HeaderSize;
        foreach (var frame in frames)
        {
            System.Text.Encoding.ASCII.GetBytes(frame.Id, result.AsSpan(position, 4));
            if (Version == 4)
            {
                WriteSyncSafe(result.AsSpan(position + 4, 4), frame.Body.Length);
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(position + 4, 4), frame.Body.Length);
            }

            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(position + 8, 2), frame.Flags);
            frame.Body.CopyTo(result, position + HeaderSize);
            position += HeaderSize + frame.Body.Length;
        }

        return result;
    }

    /// <summary>A new, empty ID3v2.3 tag for files without one; v2.3 is read by all major DJ software.</summary>
    public static Id3v2Tag CreateEmpty() => new(3, 0, 0, 0, [], null);

    private static bool IsValidFrameId(ReadOnlySpan<byte> id)
    {
        foreach (var b in id)
        {
            if (b is not ((>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9')))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSyncSafe(ReadOnlySpan<byte> bytes) =>
        (bytes[0] | bytes[1] | bytes[2] | bytes[3]) < 0x80;

    private static int ReadSyncSafe(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 21) | (bytes[1] << 14) | (bytes[2] << 7) | bytes[3];

    private static void WriteSyncSafe(Span<byte> destination, int value)
    {
        if (value >= 1 << 28)
        {
            throw new InvalidOperationException("ID3v2 sizes must be below 256 MB.");
        }

        destination[0] = (byte)((value >> 21) & 0x7F);
        destination[1] = (byte)((value >> 14) & 0x7F);
        destination[2] = (byte)((value >> 7) & 0x7F);
        destination[3] = (byte)(value & 0x7F);
    }
}
