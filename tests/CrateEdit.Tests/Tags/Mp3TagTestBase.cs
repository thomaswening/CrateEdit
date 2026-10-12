using CrateEdit.Core.Results;
using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

/// <summary>Writes fixtures to a temporary folder and applies edits through the real writer.</summary>
public abstract class Mp3TagTestBase : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-tags-").FullName;
    private int _counter;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    internal string WriteFile(byte[] content)
    {
        var path = Path.Combine(_dir, $"source-{_counter++}.mp3");
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>Applies the changes and returns the edited file; fails the test if the writer refuses.</summary>
    internal byte[] Edit(byte[] source, IReadOnlyDictionary<TagField, string?> changes)
    {
        var (result, edited) = TryEdit(source, changes);
        Assert.True(result.IsSuccess, result.ToString());
        return edited!;
    }

    internal byte[] Edit(byte[] source, TagField field, string? value) => Edit(source, new Dictionary<TagField, string?> { [field] = value });

    /// <summary>
    /// Applies the changes and returns the writer's result. Always checks that the source is
    /// unchanged, and that a refused write creates no destination file.
    /// </summary>
    internal (Result Result, byte[]? Edited) TryEdit(byte[] source, IReadOnlyDictionary<TagField, string?> changes)
    {
        var sourcePath = WriteFile(source);
        var destination = Path.Combine(_dir, $"edited-{_counter++}.mp3");

        var result = Mp3TagWriter.Rewrite(sourcePath, destination, changes);

        Assert.Equal(source, File.ReadAllBytes(sourcePath));
        Assert.Equal(result.IsSuccess, File.Exists(destination));
        return (result, result.IsSuccess ? File.ReadAllBytes(destination) : null);
    }

    /// <summary>Asserts that the writer refuses the edit with the given problem.</summary>
    internal TagNotWritable AssertRefused(byte[] source, TagField field, TagProblem expected)
    {
        var (result, _) = TryEdit(source, new Dictionary<TagField, string?> { [field] = "New" });
        var error = Assert.IsType<TagNotWritable>(result.Error);
        Assert.Equal(expected, error.Problem);
        return error;
    }

    internal Mp3Tags Read(byte[] file) => Mp3TagReader.Read(WriteFile(file));

    /// <summary>
    /// Asserts that only the frames named in <paramref name="editedFrameIds"/> differ, everything
    /// else (other frames and their order, audio, ID3v1 presence, version) is byte-identical.
    /// </summary>
    internal static void AssertOnlyEdited(ParsedMp3 before, ParsedMp3 after, params string[] editedFrameIds)
    {
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Audio, after.Audio);
        Assert.Equal(before.Id3v1 is null, after.Id3v1 is null);

        var untouchedBefore = before.Frames.Where(f => !editedFrameIds.Contains(f.Id)).ToList();
        var untouchedAfter = after.Frames.Where(f => !editedFrameIds.Contains(f.Id)).ToList();
        Assert.Equal(untouchedBefore.Count, untouchedAfter.Count);
        for (var i = 0; i < untouchedBefore.Count; i++)
        {
            Assert.True(untouchedBefore[i].SameBytesAs(untouchedAfter[i]),
                $"Frame {untouchedBefore[i]} changed or moved (now {untouchedAfter[i]}).");
        }
    }
}
