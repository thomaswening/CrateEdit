using CrateEdit.Core.Files;
using CrateEdit.Core.Results;
using CrateEdit.Core.Tags.Id3;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// Writes edited MP3 tags (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-088">REQ-088</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-130">REQ-130</see>). Only the frames of the edited fields are rebuilt;
/// every other frame, the audio and anything after it are copied byte for byte. An existing
/// ID3v1 tag is updated to match; none is ever added.
/// </summary>
/// <remarks>
/// Only the apply pipeline may use this class (enforced by an architecture test). It never
/// modifies the source file: it writes the result to a separate destination file, and replacing
/// the original safely is the caller's responsibility.
/// </remarks>
public static class Mp3TagWriter
{
    /// <summary>Extra space reserved when a tag must grow, so later edits fit without moving the audio.</summary>
    internal const int PaddingWhenGrowingBytes = 4096;

    /// <summary>
    /// Writes <paramref name="sourcePath"/> with <paramref name="changes"/> applied to
    /// <paramref name="destinationPath"/>. A null or empty value clears the field.
    /// </summary>
    /// <returns>
    /// Success, or <see cref="TagNotWritable"/> if the tag or an edited frame cannot be written
    /// safely; no destination file is created in that case. I/O failures throw (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0009-result-pattern-and-typed-errors.md">ADR-0009</see>).
    /// </returns>
    public static Result Rewrite(string sourcePath, string destinationPath, IReadOnlyDictionary<TagField, string?> changes)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var existing = Mp3TagReader.ReadId3v2(source);
        var tag = existing ?? Id3v2Tag.CreateEmpty();
        if (tag.Problem is { } tagProblem)
        {
            return tagProblem;
        }

        IReadOnlyList<Id3v2Frame> frames = tag.Frames;
        foreach (var (field, value) in changes)
        {
            if (Id3FieldMap.CheckEditable(frames, field, tag.Version) is { } fieldProblem)
            {
                return fieldProblem;
            }

            frames = Id3FieldMap.Apply(frames, field, value, tag.Version);
        }

        var newTag = tag.Render(frames, PaddingWhenGrowingBytes);
        var audioStart = existing?.Header.TotalSizeBytes ?? 0;

        var lastBytes = new byte[Math.Min(Id3v1Tag.SizeBytes, source.Length - audioStart)];
        source.Position = source.Length - lastBytes.Length;
        source.ReadExactly(lastBytes);
        var hasId3v1 = Id3v1Tag.IsPresent(lastBytes);
        var audioEnd = source.Length - (hasId3v1 ? Id3v1Tag.SizeBytes : 0);

        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        destination.Write(newTag);
        source.Position = audioStart;
        StreamCopy.CopyBytes(source, destination, audioEnd - audioStart);
        if (hasId3v1)
        {
            destination.Write(Id3v1Tag.Apply(lastBytes, changes));
        }

        destination.Flush(flushToDisk: true);
        return Result.Success;
    }
}
