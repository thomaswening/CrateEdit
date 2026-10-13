namespace CrateEdit.Core.Tags.Id3;

/// <summary>
/// Maps logical fields to ID3v2 frames, and reads and replaces exactly those frames.
/// Store-specific duplicates (e.g. TXXX:label, TXXX:initial_key) are deliberately not mapped:
/// DJ software reads the standard frames, and CrateEdit never touches data it does not own.
/// </summary>
internal static class Id3FieldMap
{
    private const string CommentFrameId = "COMM";
    private const string DefaultCommentLanguage = "eng";

    public static string FrameId(TagField field, int version) => field switch
    {
        TagField.Artist => "TPE1",
        TagField.Title => "TIT2",
        TagField.Album => "TALB",
        TagField.Genre => "TCON",
        TagField.Label => "TPUB",
        TagField.Bpm => "TBPM",
        TagField.Key => "TKEY",
        TagField.Comment => CommentFrameId,
        TagField.Year => version == 4 ? "TDRC" : "TYER",
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    /// <summary>
    /// The index of the frame that holds <paramref name="field"/>, or -1 if there is none.
    /// </summary>
    /// <remarks>
    /// The comment is the comment frame without a description: the only one Rekordbox and Serato
    /// show (manual tests, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0012-plain-comment-frame.md">ADR-0012</see>). Comments with a description, such as a store's "ID3v1
    /// Comment" or iTunes' "iTunNORM", are other software's data and are never read or changed.
    /// </remarks>
    public static int FindFrame(IReadOnlyList<Id3v2Frame> frames, TagField field, int version)
    {
        var id = FrameId(field, version);
        if (field != TagField.Comment)
        {
            return IndexOf(frames, f => f.Id == id);
        }

        return IndexOf(frames, f => IsPlainComment(f, version));
    }

    /// <summary>A comment frame with an empty description: the one DJ software shows as "Comment".</summary>
    private static bool IsPlainComment(Id3v2Frame frame, int version) =>
        frame.Id == CommentFrameId && TryReadComment(frame, version) is { Description: "" };

    /// <summary>Why a field's frame cannot be edited safely, or null if it can.</summary>
    public static TagNotWritable? CheckEditable(IReadOnlyList<Id3v2Frame> frames, TagField field, int version)
    {
        var index = FindFrame(frames, field, version);
        if (index < 0)
        {
            return null;
        }

        var frame = frames[index];
        if (frame.HasFormatFlags(version))
        {
            return new TagNotWritable(TagProblem.EncodedFrame, frame.Id, Version: version) { Field = field };
        }

        if (field != TagField.Comment && frames.Count(f => f.Id == frame.Id) > 1)
        {
            return new TagNotWritable(TagProblem.DuplicateFrame, frame.Id, Version: version) { Field = field };
        }

        return ReadFrameValue(frame, field, version) is null
            ? new TagNotWritable(TagProblem.Malformed, frame.Id, Version: version) { Field = field }
            : null;
    }

    public static TagValues Read(IReadOnlyList<Id3v2Frame> frames, int version) =>
        new(Enum.GetValues<TagField>()
            .Select(field => (field, index: FindFrame(frames, field, version)))
            .Where(x => x.index >= 0 && !frames[x.index].HasFormatFlags(version))
            .Select(x => new KeyValuePair<TagField, string>(x.field, ReadFrameValue(frames[x.index], x.field, version) ?? "")));

    /// <summary>
    /// Returns the frames with <paramref name="field"/> set to <paramref name="value"/>, or the
    /// frame removed when the value is null or empty. All other frames are kept as they are.
    /// </summary>
    public static List<Id3v2Frame> Apply(IReadOnlyList<Id3v2Frame> frames, TagField field, string? value, int version)
    {
        var result = frames.ToList();
        var index = FindFrame(result, field, version);

        if (string.IsNullOrEmpty(value))
        {
            if (index >= 0)
            {
                result.RemoveAt(index);
            }

            return result;
        }

        var existing = index >= 0 ? result[index] : null;
        var replacement = field == TagField.Comment
            ? BuildComment(existing, value, version)
            : BuildText(existing, FrameId(field, version), value, version);

        if (index >= 0)
        {
            result[index] = replacement;
        }
        else
        {
            result.Add(replacement);
        }

        return result;
    }

    private static string? ReadFrameValue(Id3v2Frame frame, TagField field, int version)
    {
        if (field == TagField.Comment)
        {
            return TryReadComment(frame, version)?.Text;
        }

        if (frame.Body.Length == 0 || !Id3Text.IsValid(frame.Body[0], version))
        {
            return null;
        }

        return Id3Text.ReadValues(frame.Body.AsSpan(1), (Id3TextEncoding)frame.Body[0]);
    }

    private static (string Language, string Description, string Text)? TryReadComment(Id3v2Frame frame, int version)
    {
        var body = frame.Body;
        if (body.Length < 4 || !Id3Text.IsValid(body[0], version) || frame.HasFormatFlags(version))
        {
            return null;
        }

        var encoding = (Id3TextEncoding)body[0];
        var language = System.Text.Encoding.Latin1.GetString(body, 1, 3);
        var (description, consumed) = Id3Text.ReadTerminated(body.AsSpan(4), encoding);
        var text = Id3Text.ReadValues(body.AsSpan(4 + consumed), encoding);
        return (language, description, text);
    }

    private static Id3v2Frame BuildText(Id3v2Frame? existing, string id, string value, int version)
    {
        var encoding = ChooseEncoding(existing?.Body is [var e, ..] ? (Id3TextEncoding)e : null, value, version);
        byte[] body = [(byte)encoding, .. Id3Text.Encode(value, encoding), .. Terminator(existing, encoding)];
        return new Id3v2Frame(id, ClearFormatFlags(existing?.Flags ?? 0), body);
    }

    private static Id3v2Frame BuildComment(Id3v2Frame? existing, string value, int version)
    {
        var current = existing is null ? null : TryReadComment(existing, version);
        var encoding = ChooseEncoding(existing?.Body is [var e, ..] ? (Id3TextEncoding)e : null, value + current?.Description, version);
        var language = current?.Language ?? DefaultCommentLanguage;
        byte[] body =
        [
            (byte)encoding,
            .. System.Text.Encoding.Latin1.GetBytes(language),
            .. Id3Text.Encode(current?.Description ?? "", encoding),
            .. new byte[Id3Text.TerminatorLength(encoding)],
            .. Id3Text.Encode(value, encoding),
            .. Terminator(existing, encoding),
        ];
        return new Id3v2Frame(CommentFrameId, ClearFormatFlags(existing?.Flags ?? 0), body);
    }

    /// <summary>Keeps the frame's encoding if it can represent the text; otherwise UTF-8 (v2.4) or UTF-16 (v2.3).</summary>
    private static Id3TextEncoding ChooseEncoding(Id3TextEncoding? current, string text, int version)
    {
        if (current is { } encoding && Id3Text.IsValid((byte)encoding, version) &&
            (encoding != Id3TextEncoding.Latin1 || Id3Text.CanEncodeAsLatin1(text)))
        {
            return encoding;
        }

        return Id3Text.CanEncodeAsLatin1(text) ? Id3TextEncoding.Latin1
            : version == 4 ? Id3TextEncoding.Utf8 : Id3TextEncoding.Utf16WithBom;
    }

    /// <summary>Keeps the original frame's terminator style, so an edit changes as little as possible.</summary>
    private static byte[] Terminator(Id3v2Frame? existing, Id3TextEncoding encoding)
    {
        var hadTerminator = existing is { Body: [.., 0] };
        return hadTerminator ? new byte[Id3Text.TerminatorLength(encoding)] : [];
    }

    // Status flags (e.g. "tag alter preservation") are kept; format flags no longer apply to the new body.
    private static ushort ClearFormatFlags(ushort flags) => (ushort)(flags & 0xF000);

    private static int IndexOf(IReadOnlyList<Id3v2Frame> frames, Func<Id3v2Frame, bool> predicate)
    {
        for (var i = 0; i < frames.Count; i++)
        {
            if (predicate(frames[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
