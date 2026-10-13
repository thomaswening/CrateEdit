using System.Buffers.Binary;
using System.Text;
using CrateEdit.Core.Results;

namespace CrateEdit.Core.Tags.Riff;

// Byte layout of WAV files: docs/formats/wav.md

/// <summary>
/// One chunk of a RIFF file: a 4-character id, a 32-bit little-endian size, the body, and a pad
/// byte if the size is odd. The body of the "data" chunk (the audio) is never loaded.
/// </summary>
/// <param name="Id">Chunk id, e.g. "fmt ", "data", "LIST", "id3 ".</param>
/// <param name="Offset">Position of the chunk header in the file.</param>
/// <param name="Size">Size of the body in bytes, without the header and the pad byte.</param>
/// <param name="Body">The body, or null for the "data" chunk.</param>
internal sealed record RiffChunk(string Id, long Offset, long Size, byte[]? Body)
{
    public const int HeaderSizeBytes = 8;

    public const string DataId = "data";

    public long BodyOffset => Offset + HeaderSizeBytes;

    /// <summary>Header, body and pad byte together.</summary>
    public long TotalSizeBytes => HeaderSizeBytes + Size + (Size & 1);
}

/// <summary>
/// The chunk structure of a WAV file (<c>"RIFF" size "WAVE" chunk*</c>). Only chunk headers and the
/// bodies of metadata chunks are read; the audio stays on disk. A file that cannot be parsed
/// completely is reported as <see cref="TagNotWritable"/>, so a rewrite never drops data
/// (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-130">REQ-130</see>).
/// </summary>
internal sealed class RiffFile
{
    public const int FileHeaderSizeBytes = 12;

    /// <summary>
    /// Metadata chunks are loaded into memory; the audio is not. Real metadata is at most a few MB
    /// (cover art), so a larger size field means a damaged file, not a chunk worth loading.
    /// </summary>
    private const long MaxMetadataChunkSizeBytes = 64 * 1024 * 1024;

    private RiffFile(long length, List<RiffChunk> chunks)
    {
        Length = length;
        Chunks = chunks;
    }

    public long Length { get; }

    /// <summary>All chunks in file order.</summary>
    public IReadOnlyList<RiffChunk> Chunks { get; }

    /// <summary>The single "data" chunk holding the audio.</summary>
    public RiffChunk Data => Chunks.Single(c => c.Id == RiffChunk.DataId);

    /// <summary>Reads the chunk structure.</summary>
    /// <returns>The structure, or <see cref="TagNotWritable"/> if the file is not a WAV file or cannot be parsed completely.</returns>
    public static Result<RiffFile> Read(Stream stream)
    {
        if (CheckFileHeader(stream) is { IsFailure: true } badHeader)
        {
            return badHeader.Error;
        }

        var chunks = new List<RiffChunk>();
        var position = (long)FileHeaderSizeBytes;
        while (position + RiffChunk.HeaderSizeBytes <= stream.Length)
        {
            var chunk = ReadChunk(stream, position);
            if (chunk.IsFailure)
            {
                return chunk.Error;
            }

            chunks.Add(chunk.Value);

            // The pad byte after an odd-sized chunk may be missing in the very last chunk.
            position = Math.Min(position + chunk.Value.TotalSizeBytes, stream.Length);
        }

        if (position != stream.Length)
        {
            // A few bytes after the last chunk, too short to be a chunk: unknown data.
            return Malformed(position);
        }

        if (chunks.Count(c => c.Id == RiffChunk.DataId) != 1)
        {
            // No audio, or two candidates for it.
            return new TagNotWritable(TagProblem.Malformed, RiffChunk.DataId);
        }

        return new RiffFile(stream.Length, chunks);
    }

    /// <summary>
    /// The file must start with <c>"RIFF" size "WAVE"</c>. The size field is not checked against the
    /// file length: many writers get it wrong, and the chunks are parsed up to the real end instead.
    /// </summary>
    private static Result CheckFileHeader(Stream stream)
    {
        Span<byte> header = stackalloc byte[FileHeaderSizeBytes];
        stream.Position = 0;
        if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length ||
            !header[8..12].SequenceEqual("WAVE"u8))
        {
            return Malformed(0);
        }

        if (header[..4].SequenceEqual("RF64"u8))
        {
            // 64-bit WAV for files over 4 GB; sizes live in a "ds64" chunk instead.
            return new TagNotWritable(TagProblem.UnsupportedVersion);
        }

        return header[..4].SequenceEqual("RIFF"u8) ? Result.Success : Malformed(0);
    }

    /// <summary>Reads the chunk at <paramref name="position"/>, loading its body unless it is the audio.</summary>
    private static Result<RiffChunk> ReadChunk(Stream stream, long position)
    {
        Span<byte> header = stackalloc byte[RiffChunk.HeaderSizeBytes];
        stream.Position = position;
        stream.ReadExactly(header);

        if (!IsValidChunkId(header[..4]))
        {
            return Malformed(position);
        }

        var id = Encoding.ASCII.GetString(header[..4]);
        long size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
        if (!FitsInFile(position, size, stream.Length))
        {
            return Malformed(position);
        }

        if (id == RiffChunk.DataId)
        {
            return new RiffChunk(id, position, size, Body: null);
        }

        if (IsTooLargeToLoad(size))
        {
            return Malformed(position);
        }

        var body = new byte[size];
        stream.ReadExactly(body);
        return new RiffChunk(id, position, size, body);
    }

    /// <summary>Chunk ids are four printable ASCII characters; anything else means the parser lost its place.</summary>
    private static bool IsValidChunkId(ReadOnlySpan<byte> id) => !id.ContainsAnyExceptInRange((byte)0x20, (byte)0x7E);

    /// <summary>The body must end within the file; otherwise the file is truncated or the size field is wrong.</summary>
    private static bool FitsInFile(long position, long bodySize, long fileLength) =>
        position + RiffChunk.HeaderSizeBytes + bodySize <= fileLength;

    private static bool IsTooLargeToLoad(long bodySize) => bodySize > MaxMetadataChunkSizeBytes;

    private static TagNotWritable Malformed(long offset) => new(TagProblem.Malformed, Offset: offset);
}
