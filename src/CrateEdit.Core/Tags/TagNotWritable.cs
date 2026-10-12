using CrateEdit.Core.Results;

namespace CrateEdit.Core.Tags;

/// <summary>Why a file's tags cannot be written safely (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-130">REQ-130</see>). The file is never changed in that case.</summary>
public enum TagProblem
{
    /// <summary>The file's format is not supported (judged by its extension).</summary>
    UnsupportedFormat,

    /// <summary>A tag version CrateEdit cannot edit, e.g. ID3v2.2.</summary>
    UnsupportedVersion,

    /// <summary>A tag feature CrateEdit does not support: unsynchronisation, an extended header or a footer.</summary>
    UnsupportedFeature,

    /// <summary>The tag could not be parsed completely: an invalid frame, or unexpected data after the frames.</summary>
    Malformed,

    /// <summary>The frame of an edited field is compressed, encrypted or otherwise encoded.</summary>
    EncodedFrame,

    /// <summary>The tag contains more than one frame for an edited field.</summary>
    DuplicateFrame,
}

/// <summary>
/// The file's tags cannot be written without risking data loss.
/// </summary>
/// <param name="Problem">The kind of problem; determines the error code and user message.</param>
/// <param name="FrameId">The frame involved, if any (e.g. "TPE1"). For logs.</param>
/// <param name="Offset">Byte offset in the tag where parsing failed, if any. For logs.</param>
/// <param name="Version">The ID3v2 major version of the tag, if known. For logs.</param>
public sealed record TagNotWritable(TagProblem Problem, string? FrameId = null, long? Offset = null, int? Version = null) : Error
{
    /// <summary>The field the user tried to edit, for problems with a single field's frame. Shown to the user.</summary>
    public TagField? Field { get; init; }

    public override string Code => Problem switch
    {
        TagProblem.UnsupportedFormat => ErrorCodes.TagsUnsupportedFormat,
        TagProblem.UnsupportedVersion => ErrorCodes.TagsUnsupportedVersion,
        TagProblem.UnsupportedFeature => ErrorCodes.TagsUnsupportedFeature,
        TagProblem.Malformed => ErrorCodes.TagsMalformed,
        TagProblem.EncodedFrame => ErrorCodes.TagsEncodedFrame,
        TagProblem.DuplicateFrame => ErrorCodes.TagsDuplicateFrame,
        _ => throw new ArgumentOutOfRangeException(nameof(Problem)),
    };
}
