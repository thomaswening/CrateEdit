using System.Buffers.Binary;
using System.Text;

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

    public long BodyOffset => Offset + HeaderSizeBytes;

    /// <summary>Header, body and pad byte together.</summary>
    public long TotalSizeBytes => HeaderSizeBytes + Size + (Size & 1);
}

/// <summary>
/// The chunk structure of a WAV file (<c>"RIFF" size "WAVE" chunk*</c>). Only chunk headers and the
/// bodies of metadata chunks are read; the audio stays on disk. A file that cannot be parsed
/// completely is reported as not writable (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-130">REQ-130</see>), so a rewrite never drops data.
/// </summary>
internal sealed class RiffFile
{
    public const int FileHeaderSizeBytes = 12;

    /// <summary>Metadata chunks larger than this are not loaded; such files are refused.</summary>
    private const long MaxMetadataChunkSizeBytes = 64 * 1024 * 1024;

    private RiffFile(long length, List<RiffChunk> chunks, TagNotWritable? problem)
    {
        Length = length;
        Chunks = chunks;
        Problem = problem;
    }

    public long Length { get; }

    /// <summary>All chunks in file order.</summary>
    public IReadOnlyList<RiffChunk> Chunks { get; }

    /// <summary>Null if the file can be rewritten safely; otherwise why not.</summary>
    public TagNotWritable? Problem { get; }

    /// <summary>The single "data" chunk holding the audio. Only valid if <see cref="Problem"/> is null.</summary>
    public RiffChunk Data => Chunks.Single(c => c.Id == "data");

    /// <summary>Reads the chunk structure, or returns null if the stream is not a RIFF/WAVE file.</summary>
    public static RiffFile? Read(Stream stream)
    {
        Span<byte> header = stackalloc byte[FileHeaderSizeBytes];
        stream.Position = 0;
        if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length ||
            !header[8..12].SequenceEqual("WAVE"u8))
        {
            return null;
        }

        var chunks = new List<RiffChunk>();
        if (header[..4].SequenceEqual("RF64"u8))
        {
            // 64-bit WAV for files over 4 GB; sizes live in a "ds64" chunk instead.
            return new RiffFile(stream.Length, chunks, new TagNotWritable(TagProblem.UnsupportedVersion));
        }

        if (!header[..4].SequenceEqual("RIFF"u8))
        {
            return null;
        }

        var position = (long)FileHeaderSizeBytes;
        Span<byte> chunkHeader = stackalloc byte[RiffChunk.HeaderSizeBytes];
        while (position + RiffChunk.HeaderSizeBytes <= stream.Length)
        {
            stream.Position = position;
            stream.ReadExactly(chunkHeader);
            if (chunkHeader[..4].ContainsAnyExceptInRange((byte)0x20, (byte)0x7E))
            {
                return Malformed(position);
            }

            var id = Encoding.ASCII.GetString(chunkHeader[..4]);
            long size = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);
            if (position + RiffChunk.HeaderSizeBytes + size > stream.Length)
            {
                // Truncated, or a size field that does not match the file.
                return Malformed(position);
            }

            byte[]? body = null;
            if (id != "data")
            {
                if (size > MaxMetadataChunkSizeBytes)
                {
                    return Malformed(position);
                }

                body = new byte[size];
                stream.ReadExactly(body);
            }

            var chunk = new RiffChunk(id, position, size, body);
            chunks.Add(chunk);

            // The pad byte after an odd-sized chunk may be missing in the very last chunk.
            position = Math.Min(position + chunk.TotalSizeBytes, stream.Length);
        }

        if (position != stream.Length)
        {
            // Trailing bytes too short to be a chunk.
            return Malformed(position);
        }

        if (chunks.Count(c => c.Id == "data") != 1)
        {
            return Malformed(null);
        }

        return new RiffFile(stream.Length, chunks, null);

        RiffFile Malformed(long? offset) => new(stream.Length, chunks, new TagNotWritable(TagProblem.Malformed, Offset: offset));
    }
}
