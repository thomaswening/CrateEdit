using CrateEdit.Core.Tags.Id3;

namespace CrateEdit.Core.Tags;

/// <summary>The tags read from an MP3 file.</summary>
/// <param name="Values">The logical field values.</param>
/// <param name="Id3Version">The ID3v2 major version (3 or 4), or null if the file has no ID3v2 tag.</param>
/// <param name="NotWritableReason">Null if CrateEdit can safely write this file's tags; otherwise why not.</param>
public sealed record Mp3Tags(TagValues Values, int? Id3Version, string? NotWritableReason)
{
    public bool CanWrite => NotWritableReason is null;
}

/// <summary>
/// Reads MP3 tags. Only the ID3v2 tag at the start and the last 128 bytes are read, so reading
/// is fast even for large files. Never modifies the file.
/// </summary>
public static class Mp3TagReader
{
    public static Mp3Tags Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var tag = ReadId3v2(stream);
        if (tag is null)
        {
            return new Mp3Tags(TagValues.Empty, null, null);
        }

        return new Mp3Tags(
            tag.NotWritableReason is null ? Id3FieldMap.Read(tag.Frames, tag.Version) : TagValues.Empty,
            tag.Version,
            tag.NotWritableReason);
    }

    internal static Id3v2Tag? ReadId3v2(Stream stream)
    {
        Span<byte> header = stackalloc byte[Id3v2Tag.HeaderSize];
        stream.Position = 0;
        if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length ||
            Id3v2Tag.ReadTotalSize(header) is not { } totalSize || totalSize > stream.Length)
        {
            return null;
        }

        var data = new byte[totalSize];
        stream.Position = 0;
        stream.ReadExactly(data);
        return Id3v2Tag.Parse(data);
    }
}
