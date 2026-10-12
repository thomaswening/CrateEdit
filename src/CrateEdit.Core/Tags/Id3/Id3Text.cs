using System.Text;

namespace CrateEdit.Core.Tags.Id3;

/// <summary>Text encodings of ID3v2 frames (the first byte of a text frame).</summary>
internal enum Id3TextEncoding : byte
{
    Latin1 = 0,
    Utf16WithBom = 1,
    Utf16BigEndian = 2,
    Utf8 = 3,
}

/// <summary>Decoding and encoding of ID3v2 text.</summary>
internal static class Id3Text
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    public static bool IsValid(byte encoding, int version) =>
        encoding <= 1 || (version == 4 && encoding <= 3);

    public static int TerminatorLength(Id3TextEncoding encoding) =>
        encoding is Id3TextEncoding.Utf16WithBom or Id3TextEncoding.Utf16BigEndian ? 2 : 1;

    /// <summary>
    /// Splits <paramref name="data"/> at the first terminator. Returns the decoded string and the
    /// number of bytes consumed including the terminator (or all bytes if there is none).
    /// </summary>
    public static (string Text, int Consumed) ReadTerminated(ReadOnlySpan<byte> data, Id3TextEncoding encoding)
    {
        var end = FindTerminator(data, encoding);
        if (end < 0)
        {
            return (Decode(data, encoding), data.Length);
        }

        return (Decode(data[..end], encoding), end + TerminatorLength(encoding));
    }

    /// <summary>
    /// Decodes the remaining text of a frame. ID3v2.4 allows several values separated by
    /// terminators; they are joined with ", ", matching how DJs type multiple values.
    /// </summary>
    public static string ReadValues(ReadOnlySpan<byte> data, Id3TextEncoding encoding)
    {
        var values = new List<string>();
        while (data.Length > 0)
        {
            var (text, consumed) = ReadTerminated(data, encoding);
            values.Add(text);
            data = data[consumed..];
        }

        return string.Join(", ", values.Where(v => v.Length > 0));
    }

    public static string Decode(ReadOnlySpan<byte> data, Id3TextEncoding encoding) => encoding switch
    {
        Id3TextEncoding.Latin1 => Latin1.GetString(data),
        Id3TextEncoding.Utf8 => Encoding.UTF8.GetString(data.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? data[3..] : data),
        Id3TextEncoding.Utf16BigEndian => Encoding.BigEndianUnicode.GetString(data),
        Id3TextEncoding.Utf16WithBom when data.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]) => Encoding.BigEndianUnicode.GetString(data[2..]),
        Id3TextEncoding.Utf16WithBom when data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) => Encoding.Unicode.GetString(data[2..]),
        Id3TextEncoding.Utf16WithBom => Encoding.Unicode.GetString(data),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
    };

    /// <summary>Encodes text without a terminator. UTF-16 is written little-endian with a BOM.</summary>
    public static byte[] Encode(string text, Id3TextEncoding encoding) => encoding switch
    {
        Id3TextEncoding.Latin1 => Latin1.GetBytes(text),
        Id3TextEncoding.Utf8 => Encoding.UTF8.GetBytes(text),
        Id3TextEncoding.Utf16BigEndian => Encoding.BigEndianUnicode.GetBytes(text),
        Id3TextEncoding.Utf16WithBom => [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(text)],
        _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
    };

    public static bool CanEncodeAsLatin1(string text) => text.All(c => c <= 'ÿ');

    private static int FindTerminator(ReadOnlySpan<byte> data, Id3TextEncoding encoding)
    {
        if (TerminatorLength(encoding) == 1)
        {
            return data.IndexOf((byte)0);
        }

        for (var i = 0; i + 1 < data.Length; i += 2)
        {
            if (data[i] == 0 && data[i + 1] == 0)
            {
                return i;
            }
        }

        return -1;
    }
}
