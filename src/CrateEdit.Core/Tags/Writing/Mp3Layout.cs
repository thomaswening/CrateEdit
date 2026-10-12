using System.Security.Cryptography;
using CrateEdit.Core.Tags.Id3;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// Where the tags and the audio of an MP3 file are, with hashes for verification:
/// <c>[ID3v2 tag][audio][ID3v1 tag?]</c>. See docs/formats/id3.md.
/// </summary>
internal sealed record Mp3Layout(long Length, byte[] Id3v2Bytes, byte[]? Id3v1Bytes, byte[] AudioHash, byte[] FileHash)
{
    public long AudioStart => Id3v2Bytes.Length;

    public long AudioLength => Length - AudioStart - (Id3v1Bytes?.Length ?? 0);

    public static Mp3Layout Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var tagSize = Mp3TagReader.ReadId3v2(stream)?.Header.TotalSizeBytes ?? 0;

        var tag = new byte[tagSize];
        stream.Position = 0;
        stream.ReadExactly(tag);

        byte[]? id3v1 = null;
        if (stream.Length - tagSize >= Id3v1Tag.SizeBytes)
        {
            var last = new byte[Id3v1Tag.SizeBytes];
            stream.Position = stream.Length - Id3v1Tag.SizeBytes;
            stream.ReadExactly(last);
            id3v1 = Id3v1Tag.IsPresent(last) ? last : null;
        }

        var audioLength = stream.Length - tagSize - (id3v1?.Length ?? 0);
        stream.Position = tagSize;
        var audioHash = HashRange(stream, audioLength);
        stream.Position = 0;
        var fileHash = SHA256.HashData(stream);

        return new Mp3Layout(stream.Length, tag, id3v1, audioHash, fileHash);
    }

    public static byte[] HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }

    private static byte[] HashRange(Stream stream, long count)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        while (count > 0)
        {
            var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            hash.AppendData(buffer, 0, read);
            count -= read;
        }

        return hash.GetHashAndReset();
    }
}
