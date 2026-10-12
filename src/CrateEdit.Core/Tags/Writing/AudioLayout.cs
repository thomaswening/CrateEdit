using System.Security.Cryptography;
using CrateEdit.Core.Files;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// An audio file split into <c>[prefix][audio][suffix]</c>, with hashes for verification. The
/// audio is never changed by a tag write; prefix and suffix hold the metadata around it. For MP3,
/// the prefix is the ID3v2 tag and the suffix the ID3v1 tag (see docs/formats/id3.md).
/// </summary>
/// <param name="Length">Length of the whole file, in bytes.</param>
/// <param name="Prefix">All bytes before the audio.</param>
/// <param name="Suffix">All bytes after the audio (empty if none).</param>
/// <param name="AudioHash">SHA-256 of the audio bytes.</param>
/// <param name="FileHash">SHA-256 of the whole file.</param>
internal sealed record AudioLayout(long Length, byte[] Prefix, byte[] Suffix, byte[] AudioHash, byte[] FileHash)
{
    public long AudioStart => Prefix.Length;

    public long AudioLength => Length - Prefix.Length - Suffix.Length;

    /// <summary>Reads the layout of a file whose audio starts after <paramref name="prefixLength"/> bytes and ends <paramref name="suffixLength"/> bytes before the end.</summary>
    public static AudioLayout Read(string path, long prefixLength, long suffixLength)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (prefixLength + suffixLength > stream.Length)
        {
            throw new EndOfStreamException("The file is shorter than its metadata.");
        }

        var prefix = new byte[prefixLength];
        stream.ReadExactly(prefix);
        var audioHash = StreamCopy.HashBytes(stream, stream.Length - prefixLength - suffixLength);
        var suffix = new byte[suffixLength];
        stream.ReadExactly(suffix);

        stream.Position = 0;
        return new AudioLayout(stream.Length, prefix, suffix, audioHash, SHA256.HashData(stream));
    }

    public static byte[] HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }
}
