using System.Security.Cryptography;

namespace CrateEdit.Core.Files;

/// <summary>Copies and hashes byte ranges of streams in chunks, so large audio files never have to fit in memory.</summary>
internal static class StreamCopy
{
    /// <summary>
    /// Chunk size for copying and hashing. 80 KB is the default buffer size of
    /// <see cref="Stream.CopyTo(Stream)"/>: large enough for efficient disk access, and just below
    /// the 85,000-byte threshold above which .NET allocates on the slower large object heap.
    /// </summary>
    public const int BufferSizeBytes = 81920;

    /// <summary>Copies exactly <paramref name="count"/> bytes from the current position of <paramref name="source"/>.</summary>
    /// <exception cref="EndOfStreamException">The source ends before <paramref name="count"/> bytes were copied.</exception>
    public static void CopyBytes(Stream source, Stream destination, long count)
    {
        var buffer = new byte[BufferSizeBytes];
        while (count > 0)
        {
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
            {
                throw new EndOfStreamException("The source ended before all bytes were copied.");
            }

            destination.Write(buffer, 0, read);
            count -= read;
        }
    }

    /// <summary>SHA-256 of the next <paramref name="count"/> bytes of <paramref name="source"/>.</summary>
    /// <exception cref="EndOfStreamException">The source ends before <paramref name="count"/> bytes were read.</exception>
    public static byte[] HashBytes(Stream source, long count)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSizeBytes];
        while (count > 0)
        {
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
            {
                throw new EndOfStreamException("The source ended before all bytes were hashed.");
            }

            hash.AppendData(buffer, 0, read);
            count -= read;
        }

        return hash.GetHashAndReset();
    }
}
