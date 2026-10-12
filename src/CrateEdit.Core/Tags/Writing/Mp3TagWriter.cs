using CrateEdit.Core.Tags.Id3;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>Thrown when tags cannot be written without risking data loss. The source file is unchanged.</summary>
public sealed class TagWriteException(string message) : Exception(message);

/// <summary>
/// Writes edited MP3 tags (REQ-088, REQ-130). Only the frames of the edited fields are rebuilt;
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
    internal const int PaddingWhenGrowing = 4096;

    /// <summary>
    /// Writes <paramref name="sourcePath"/> with <paramref name="changes"/> applied to
    /// <paramref name="destinationPath"/>. A null or empty value clears the field.
    /// </summary>
    /// <exception cref="TagWriteException">The tag or an edited frame is not supported for writing.</exception>
    public static void Rewrite(string sourcePath, string destinationPath, IReadOnlyDictionary<TagField, string?> changes)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var existing = Mp3TagReader.ReadId3v2(source);
        var tag = existing ?? Id3v2Tag.CreateEmpty();
        if (tag.NotWritableReason is { } reason)
        {
            throw new TagWriteException(reason);
        }

        IReadOnlyList<Id3v2Frame> frames = tag.Frames;
        foreach (var (field, value) in changes)
        {
            if (Id3FieldMap.CheckEditable(frames, field, tag.Version) is { } fieldReason)
            {
                throw new TagWriteException(fieldReason);
            }

            frames = Id3FieldMap.Apply(frames, field, value, tag.Version);
        }

        var newTag = tag.Render(frames, PaddingWhenGrowing);
        var audioStart = existing?.TotalSize ?? 0;

        var lastBytes = new byte[Math.Min(Id3v1Tag.Size, source.Length - audioStart)];
        source.Position = source.Length - lastBytes.Length;
        source.ReadExactly(lastBytes);
        var hasId3v1 = Id3v1Tag.IsPresent(lastBytes);
        var audioEnd = source.Length - (hasId3v1 ? Id3v1Tag.Size : 0);

        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        destination.Write(newTag);
        source.Position = audioStart;
        CopyBytes(source, destination, audioEnd - audioStart);
        if (hasId3v1)
        {
            destination.Write(Id3v1Tag.Apply(lastBytes, changes));
        }

        destination.Flush(flushToDisk: true);
    }

    private static void CopyBytes(Stream source, Stream destination, long count)
    {
        var buffer = new byte[81920];
        while (count > 0)
        {
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
            {
                throw new EndOfStreamException("The source file ended unexpectedly.");
            }

            destination.Write(buffer, 0, read);
            count -= read;
        }
    }
}
