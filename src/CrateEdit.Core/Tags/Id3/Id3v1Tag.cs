using System.Text;

namespace CrateEdit.Core.Tags.Id3;

/// <summary>
/// The legacy 128-byte ID3v1 tag at the end of a file. CrateEdit never adds one, but keeps an
/// existing one consistent with edited fields, since some software falls back to it.
/// </summary>
internal static class Id3v1Tag
{
    public const int SizeBytes = 128;

    // ID3v1 genres 0-79 from the original specification.
    internal static readonly string[] Genres =
    [
        "Blues", "Classic Rock", "Country", "Dance", "Disco", "Funk", "Grunge", "Hip-Hop", "Jazz", "Metal",
        "New Age", "Oldies", "Other", "Pop", "R&B", "Rap", "Reggae", "Rock", "Techno", "Industrial",
        "Alternative", "Ska", "Death Metal", "Pranks", "Soundtrack", "Euro-Techno", "Ambient", "Trip-Hop", "Vocal", "Jazz+Funk",
        "Fusion", "Trance", "Classical", "Instrumental", "Acid", "House", "Game", "Sound Clip", "Gospel", "Noise",
        "AlternRock", "Bass", "Soul", "Punk", "Space", "Meditative", "Instrumental Pop", "Instrumental Rock", "Ethnic", "Gothic",
        "Darkwave", "Techno-Industrial", "Electronic", "Pop-Folk", "Eurodance", "Dream", "Southern Rock", "Comedy", "Cult", "Gangsta",
        "Top 40", "Christian Rap", "Pop/Funk", "Jungle", "Native American", "Cabaret", "New Wave", "Psychadelic", "Rave", "Showtunes",
        "Trailer", "Lo-Fi", "Tribal", "Acid Punk", "Acid Jazz", "Polka", "Retro", "Musical", "Rock & Roll", "Hard Rock",
    ];

    private const byte UnknownGenre = 255;

    public static bool IsPresent(ReadOnlySpan<byte> lastBytes) =>
        lastBytes.Length >= SizeBytes && lastBytes[^SizeBytes..].StartsWith("TAG"u8);

    /// <summary>Returns a copy of <paramref name="tag"/> with the given fields updated.</summary>
    public static byte[] Apply(ReadOnlySpan<byte> tag, IReadOnlyDictionary<TagField, string?> changes)
    {
        var result = tag.ToArray();

        // ID3v1.1 stores a track number in the last comment byte when the byte before it is zero.
        var hasTrackNumber = result[125] == 0 && result[126] != 0;

        foreach (var (field, value) in changes)
        {
            switch (field)
            {
                case TagField.Title:
                    WriteText(result.AsSpan(3, 30), value);
                    break;
                case TagField.Artist:
                    WriteText(result.AsSpan(33, 30), value);
                    break;
                case TagField.Album:
                    WriteText(result.AsSpan(63, 30), value);
                    break;
                case TagField.Year:
                    WriteText(result.AsSpan(93, 4), value);
                    break;
                case TagField.Comment:
                    WriteText(result.AsSpan(97, hasTrackNumber ? 28 : 30), value);
                    break;
                case TagField.Genre:
                    var index = Array.FindIndex(Genres, g => string.Equals(g, value, StringComparison.OrdinalIgnoreCase));
                    result[127] = index >= 0 ? (byte)index : UnknownGenre;
                    break;
            }
        }

        return result;
    }

    /// <summary>Latin-1, truncated to fit, unused bytes zeroed. Characters outside Latin-1 become '?'.</summary>
    private static void WriteText(Span<byte> destination, string? value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var latin1 = new string(value.Select(c => c <= 'ÿ' ? c : '?').ToArray());
        var bytes = Encoding.Latin1.GetBytes(latin1);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}
