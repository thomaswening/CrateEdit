namespace CrateEdit.Core.Tags;

/// <summary>The tags read from an audio file, in any supported format.</summary>
/// <param name="Values">The logical field values.</param>
/// <param name="WriteProblem">Null if the file's tags can be written safely; otherwise why not.</param>
public sealed record FileTags(TagValues Values, TagNotWritable? WriteProblem)
{
    public bool CanWrite => WriteProblem is null;
}
