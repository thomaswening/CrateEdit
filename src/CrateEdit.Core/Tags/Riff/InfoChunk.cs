using System.Buffers.Binary;
using System.Text;

namespace CrateEdit.Core.Tags.Riff;

/// <summary>One entry of a LIST/INFO chunk, kept as raw bytes so it can be written back unchanged.</summary>
internal sealed record InfoEntry(string Id, byte[] Value);

/// <summary>
/// The "LIST" chunk of type "INFO": the original WAV metadata, and the only tags Rekordbox reads
/// from WAV files (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0011-wav-tagging.md">ADR-0011</see>). Values are null-terminated text; written as UTF-8, which
/// Rekordbox expects, and read as UTF-8 with a Windows-1252 fallback for older files.
/// </summary>
internal static class InfoChunk
{
    public const string ListId = "LIST";
    public static readonly byte[] InfoType = "INFO"u8.ToArray();

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static InfoChunk() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>The INFO id for a field, or null if INFO cannot hold it (label, BPM, key).</summary>
    public static string? IdFor(TagField field) => field switch
    {
        TagField.Title => "INAM",
        TagField.Artist => "IART",
        TagField.Album => "IPRD",
        TagField.Genre => "IGNR",
        TagField.Comment => "ICMT",
        TagField.Year => "ICRD",
        _ => null,
    };

    public static bool IsInfoList(RiffChunk chunk) =>
        chunk.Id == ListId && chunk.Body is { Length: >= 4 } body && body.AsSpan(0, 4).SequenceEqual(InfoType);

    /// <summary>The entries of an INFO list body, or null if it is malformed.</summary>
    public static List<InfoEntry>? Parse(byte[] listBody)
    {
        var entries = new List<InfoEntry>();
        var position = 4;
        while (position + RiffChunk.HeaderSizeBytes <= listBody.Length)
        {
            var id = Encoding.ASCII.GetString(listBody, position, 4);
            var size = (long)BinaryPrimitives.ReadUInt32LittleEndian(listBody.AsSpan(position + 4));
            if (position + RiffChunk.HeaderSizeBytes + size > listBody.Length)
            {
                return null;
            }

            entries.Add(new InfoEntry(id, listBody[(position + 8)..(position + 8 + (int)size)]));
            position += RiffChunk.HeaderSizeBytes + (int)size + (int)(size & 1);
        }

        // Anything left must be padding.
        return listBody.AsSpan(Math.Min(position, listBody.Length)).ContainsAnyExcept((byte)0) ? null : entries;
    }

    public static byte[] Render(IEnumerable<InfoEntry> entries)
    {
        var result = new List<byte>(InfoType);
        foreach (var entry in entries)
        {
            var size = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)entry.Value.Length);
            result.AddRange(Encoding.ASCII.GetBytes(entry.Id));
            result.AddRange(size);
            result.AddRange(entry.Value);
            if ((entry.Value.Length & 1) == 1)
            {
                result.Add(0);
            }
        }

        return [.. result];
    }

    public static string Decode(byte[] value)
    {
        var text = value.AsSpan().TrimEnd((byte)0);
        try
        {
            return StrictUtf8.GetString(text);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(text);
        }
    }

    /// <summary>UTF-8 with a terminating zero byte.</summary>
    public static byte[] Encode(string value) => [.. Encoding.UTF8.GetBytes(value), 0];
}
