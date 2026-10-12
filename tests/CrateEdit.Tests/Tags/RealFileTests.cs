using CrateEdit.Core.Tags;

namespace CrateEdit.Tests.Tags;

/// <summary>
/// Runs the preservation checks against real MP3 files when CRATEEDIT_SAMPLES points to a folder
/// of them. Skipped otherwise (real music cannot be committed). The files are copied to a
/// temporary folder first; the originals are only read.
/// </summary>
public class RealFileTests : Mp3TagTestBase
{
    private static string? SampleDirectory => Environment.GetEnvironmentVariable("CRATEEDIT_SAMPLES");

    public static TheoryData<string> Samples() =>
        SampleDirectory is { } dir && Directory.Exists(dir)
            ? new(Directory.GetFiles(dir, "*.mp3").Select(Path.GetFileName)!)
            : new("(set CRATEEDIT_SAMPLES to run)");

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_all_fields_of_a_real_file_changes_only_those_frames(string fileName)
    {
        Assert.SkipWhen(SampleDirectory is null, "CRATEEDIT_SAMPLES is not set.");
        var source = File.ReadAllBytes(Path.Combine(SampleDirectory!, fileName));
        var original = Read(source);
        Assert.True(original.CanWrite, original.NotWritableReason);

        var changes = Enum.GetValues<TagField>().ToDictionary(f => f, f => (string?)$"Edited {f} ✓");
        var edited = Edit(source, changes);

        var before = Mp3Fixture.Parse(source);
        var after = Mp3Fixture.Parse(edited);
        var editedIds = new[] { "TPE1", "TIT2", "TALB", "TCON", "TPUB", "TBPM", "TKEY", "COMM", before.Version == 4 ? "TDRC" : "TYER" };
        AssertOnlyEdited(before, after, editedIds);

        // Comments other than the edited one keep their bytes.
        var commentsBefore = before.Frames.Where(f => f.Id == "COMM").ToList();
        var commentsAfter = after.Frames.Where(f => f.Id == "COMM").ToList();
        Assert.Equal(Math.Max(commentsBefore.Count, 1), commentsAfter.Count);
        Assert.True(commentsAfter.Count(a => commentsBefore.Any(b => b.SameBytesAs(a))) >= commentsBefore.Count - 1);

        var reread = Read(edited).Values;
        Assert.All(changes, change => Assert.Equal(change.Value, reread[change.Key]));
    }
}
