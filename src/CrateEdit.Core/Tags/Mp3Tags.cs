using CrateEdit.Core.Tags.Id3;

namespace CrateEdit.Core.Tags;

/// <summary>The tags read from an MP3 file.</summary>
/// <param name="Values">The logical field values.</param>
/// <param name="Id3Version">The ID3v2 major version (3 or 4), or null if the file has no ID3v2 tag.</param>
/// <param name="WriteProblem">Null if CrateEdit can safely write this file's tags; otherwise why not.</param>
public sealed record Mp3Tags(TagValues Values, int? Id3Version, TagNotWritable? WriteProblem)
{
    public bool CanWrite => WriteProblem is null;
}

/// <summary>
/// Reads MP3 tags. Only the ID3v2 tag at the start and the last 128 bytes are read, so reading
/// is fast even for large files. Never modifies the file.
/// </summary>
/// <remarks>
/// A tag that cannot be written is still a successful read (reported in <see cref="Mp3Tags.WriteProblem"/>).
/// I/O failures (missing file, no access) are exceptions, converted into errors by the caller (ADR-0009).
/// </remarks>
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
            tag.Problem is null ? Id3FieldMap.Read(tag.Frames, tag.Version) : TagValues.Empty,
            tag.Version,
            tag.Problem);
    }

    internal static Id3v2Tag? ReadId3v2(Stream stream)
    {
        // On the stack: no heap allocation for these 10 bytes, which are read for every file in the collection.
        Span<byte> headerBytes = stackalloc byte[Id3v2Header.SizeBytes];
        stream.Position = 0;

        // A single Read may return fewer bytes than requested (e.g. on cloud-synced drives).
        // ReadAtLeast keeps reading until the buffer is full or the file ends; a file shorter
        // than 10 bytes simply has no tag, so no exception is wanted.
        if (stream.ReadAtLeast(headerBytes, headerBytes.Length, throwOnEndOfStream: false) < headerBytes.Length ||
            Id3v2Header.TryParse(headerBytes) is not { } header || header.TotalSizeBytes > stream.Length)
        {
            return null;
        }

        var tag = new byte[header.TotalSizeBytes];
        stream.Position = 0;
        stream.ReadExactly(tag);
        return Id3v2Tag.Parse(header, tag);
    }
}
