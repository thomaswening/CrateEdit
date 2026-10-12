using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

/// <summary>
/// Runs the preservation checks against real MP3 files when CRATEEDIT_SAMPLES points to a folder
/// of them. Skipped otherwise (real music cannot be committed). The files are copied to a
/// temporary folder first; the originals are only read. Safe-write tests copy to
/// CRATEEDIT_WRITE_DIR if set (e.g. a folder on a USB stick or a cloud-sync drive), so the
/// write technique can be checked on that storage.
/// </summary>
public class RealFileTests : Mp3TagTestBase
{
    private static string? SampleDirectory => Environment.GetEnvironmentVariable("CRATEEDIT_SAMPLES");

    private static string? WriteDirectory => Environment.GetEnvironmentVariable("CRATEEDIT_WRITE_DIR");

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
        Assert.True(original.CanWrite, original.WriteProblem?.ToString());

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

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-092")]
    public void Safe_write_and_crash_recovery_work_on_a_real_file(string fileName)
    {
        Assert.SkipWhen(SampleDirectory is null, "CRATEEDIT_SAMPLES is not set.");
        var dir = Directory.CreateDirectory(Path.Combine(WriteDirectory ?? Path.GetTempPath(), "crateedit-test-" + Guid.NewGuid().ToString("N"))).FullName;
        var store = new FileRecoveryStore(Path.Combine(Path.GetTempPath(), "crateedit-recovery-" + Guid.NewGuid().ToString("N")));
        try
        {
            var path = Path.Combine(dir, fileName);
            File.Copy(Path.Combine(SampleDirectory!, fileName), path);
            var original = File.ReadAllBytes(path);
            var changes = new Dictionary<TagField, string?> { [TagField.Label] = "Edited Label ✓", [TagField.Key] = "8A" };

            Assert.True(new SafeFileWriter(store).Write(path, changes).IsSuccess);
            Assert.Equal("Edited Label ✓", Mp3TagReader.Read(path).Values[TagField.Label]);
            Assert.Empty(store.LoadAll());

            // Crash during a replace that left the file missing (possible on FAT32 or cloud drives).
            File.WriteAllBytes(path, original);
            var crashing = new SafeFileWriter(store) { AfterStep = step => { if (step == WriteStep.TempVerified) throw new OperationCanceledException(); } };
            Assert.Throws<OperationCanceledException>(() => crashing.Write(path, changes));
            File.Delete(path);
            Assert.Equal(RecoveryOutcome.WriteCompleted, Assert.Single(RecoveryReconciler.ReconcileAll(store)).Outcome);
            Assert.Equal("8A", Mp3TagReader.Read(path).Values[TagField.Key]);
            Assert.False(File.Exists(SafeFileWriter.TempPathFor(path)));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(dir, "*", new EnumerationOptions { AttributesToSkip = 0 }))
            {
                File.Delete(file);
            }

            Directory.Delete(dir);
        }
    }
}
