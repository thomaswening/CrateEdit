using CrateEdit.Core.Tags.Id3;

namespace CrateEdit.Core.Tags;

/// <summary>
/// Reads MP3 tags. Only the ID3v2 tag at the start and the last 128 bytes are read, so reading
/// is fast even for large files. Never modifies the file.
/// </summary>
/// <remarks>
/// A tag that cannot be written is still a successful read (reported in <see cref="FileTags.WriteProblem"/>).
/// I/O failures (missing file, no access) are exceptions, converted into errors by the caller (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0009-result-pattern-and-typed-errors.md">ADR-0009</see>).
/// </remarks>
public static class Mp3TagReader
{
    public static FileTags Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var tag = ReadId3v2(stream);
        if (tag is null)
        {
            return new FileTags(TagValues.Empty, null);
        }

        return new FileTags(tag.Problem is null ? Id3FieldMap.Read(tag.Frames, tag.Version) : TagValues.Empty, tag.Problem);
    }

    /// <summary>The ID3v2 tag at the start of the file, or null if the file has none.</summary>
    internal static Id3v2Tag? ReadId3v2(Stream stream)
    {
        if (!TryReadHeader(stream, out var header))
        {
            return null;
        }

        var tag = new byte[header.TotalSizeBytes];
        stream.Position = 0;
        stream.ReadExactly(tag);
        return Id3v2Tag.Parse(header, tag);
    }

    /// <summary>
    /// True if the file starts with a valid ID3v2 header and the whole tag it announces fits in the
    /// file. A file that is too short, starts differently, or announces more bytes than it has,
    /// is treated as having no tag.
    /// </summary>
    private static bool TryReadHeader(Stream stream, out Id3v2Header header)
    {
        // On the stack: no heap allocation for these 10 bytes, which are read for every file in the collection.
        Span<byte> headerBytes = stackalloc byte[Id3v2Header.SizeBytes];
        stream.Position = 0;

        // A single Read may return fewer bytes than requested (e.g. on cloud-synced drives).
        // ReadAtLeast keeps reading until the buffer is full or the file ends; a file shorter
        // than 10 bytes simply has no tag, so no exception is wanted.
        if (stream.ReadAtLeast(headerBytes, headerBytes.Length, throwOnEndOfStream: false) < headerBytes.Length ||
            Id3v2Header.TryParse(headerBytes) is not { } parsed || parsed.TotalSizeBytes > stream.Length)
        {
            header = default;
            return false;
        }

        header = parsed;
        return true;
    }
}
