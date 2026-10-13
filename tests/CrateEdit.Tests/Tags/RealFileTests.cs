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
        SampleDirectory is { } dir && Directory.Exists(dir) && Directory.GetFiles(dir, "*.mp3") is { Length: > 0 } files
            ? new(files.Select(Path.GetFileName)!)
            : new("(set CRATEEDIT_SAMPLES to a folder with MP3 files to run)");

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_all_fields_of_a_real_file_changes_only_those_frames(string fileName)
    {
        Assert.SkipWhen(SampleDirectory is null || !File.Exists(Path.Combine(SampleDirectory, fileName)), "No MP3 samples.");
        var source = File.ReadAllBytes(Path.Combine(SampleDirectory!, fileName));
        var original = Read(source);
        Assert.True(original.CanWrite, original.WriteProblem?.ToString());

        var changes = Enum.GetValues<TagField>().ToDictionary(f => f, f => (string?)$"Edited {f} ✓");
        var edited = Edit(source, changes);

        var before = Mp3Fixture.Parse(source);
        var after = Mp3Fixture.Parse(edited);
        var editedIds = new[] { "TPE1", "TIT2", "TALB", "TCON", "TPUB", "TBPM", "TKEY", "COMM", before.Version == 4 ? "TDRC" : "TYER" };
        AssertOnlyEdited(before, after, editedIds);

        // Only the plain comment (no description) is the comment field. Comments with a
        // description, e.g. a store's "ID3v1 Comment", keep their bytes; a plain comment is added
        // if the file had none (ADR-0012).
        var commentsBefore = before.Frames.Where(f => f.Id == "COMM").ToList();
        var commentsAfter = after.Frames.Where(f => f.Id == "COMM").ToList();
        Assert.All(commentsBefore.Where(c => !IsPlainComment(c)), c => Assert.Contains(commentsAfter, a => a.SameBytesAs(c)));
        Assert.Single(commentsAfter, IsPlainComment);

        var reread = Read(edited).Values;
        Assert.All(changes, change => Assert.Equal(change.Value, reread[change.Key]));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-092")]
    public void Safe_write_and_crash_recovery_work_on_a_real_file(string fileName)
    {
        Assert.SkipWhen(SampleDirectory is null || !File.Exists(Path.Combine(SampleDirectory, fileName)), "No MP3 samples.");
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

    public static TheoryData<string> WavSamples() =>
        SampleDirectory is { } dir && Directory.Exists(dir) && Directory.GetFiles(dir, "*.wav") is { Length: > 0 } files
            ? new(files.Select(Path.GetFileName)!)
            : new("(set CRATEEDIT_SAMPLES to a folder with WAV files to run)");

    [Theory]
    [MemberData(nameof(WavSamples))]
    [Trait("Req", "REQ-003")]
    [Trait("Req", "REQ-088")]
    public void Safe_write_of_all_fields_of_a_real_wav_keeps_its_audio_and_other_chunks(string fileName)
    {
        Assert.SkipWhen(SampleDirectory is null || !File.Exists(Path.Combine(SampleDirectory, fileName)), "No WAV samples.");
        var dir = Directory.CreateTempSubdirectory("crateedit-wav-real-").FullName;
        try
        {
            var path = Path.Combine(dir, fileName);
            File.Copy(Path.Combine(SampleDirectory!, fileName), path);
            var before = WavFixture.Parse(File.ReadAllBytes(path));
            var changes = Enum.GetValues<TagField>().ToDictionary(f => f, f => (string?)$"Edited {f} ✓");

            var result = new SafeFileWriter(new FileRecoveryStore(Path.Combine(dir, "recovery"))).Write(path, changes);

            Assert.True(result.IsSuccess, result.ToString());
            var after = WavFixture.Parse(File.ReadAllBytes(path));
            Assert.Equal(before.Single(c => c.Id == "data").Offset, after.Single(c => c.Id == "data").Offset);
            foreach (var chunk in before.Where(c => c.Id is not ("LIST" or "id3 ")))
            {
                Assert.True(chunk.SameBytesAs(after.Single(c => c.Id == chunk.Id)), $"{chunk} changed.");
            }

            // Inside the tag chunks, everything that is not one of the edited fields must survive,
            // e.g. Traktor's "NITR" INFO entry and Serato's GEOB frames.
            string[] infoFieldIds = ["INAM", "IART", "IPRD", "IGNR", "ICMT", "ICRD"];
            if (before.SingleOrDefault(c => c.Id == "LIST") is { } infoBefore)
            {
                var entriesAfter = WavFixture.InfoEntries(after.Single(c => c.Id == "LIST").Body);
                foreach (var (id, value) in WavFixture.InfoEntries(infoBefore.Body).Where(e => !infoFieldIds.Contains(e.Key)))
                {
                    Assert.True(entriesAfter.TryGetValue(id, out var kept) && kept.AsSpan().SequenceEqual(value), $"INFO entry {id} changed.");
                }
            }

            string[] id3FieldIds = ["TPE1", "TIT2", "TALB", "TCON", "TPUB", "TBPM", "TKEY", "COMM", "TYER", "TDRC"];
            if (before.SingleOrDefault(c => c.Id is "id3 " or "ID3 ") is { } id3Before)
            {
                var framesAfter = WavFixture.Id3Frames(after.Single(c => c.Id == id3Before.Id).Body);
                foreach (var frame in WavFixture.Id3Frames(id3Before.Body).Where(f => !id3FieldIds.Contains(f.Id)))
                {
                    Assert.Contains(framesAfter, kept => kept.SameBytesAs(frame));
                }
            }

            var values = TagReader.Read(path).Values;
            Assert.All(changes, change => Assert.Equal(change.Value, values[change.Key]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// A comment frame whose description is empty: after the encoding byte and the 3-byte language
    /// comes the terminator right away (for UTF-16, optionally preceded by a byte-order mark).
    /// </summary>
    private static bool IsPlainComment(RawFrame comment)
    {
        var afterLanguage = comment.Body.AsSpan(4);
        return comment.Body[0] is 1 or 2
            ? afterLanguage.StartsWith((ReadOnlySpan<byte>)[0, 0]) || afterLanguage[2..].StartsWith((ReadOnlySpan<byte>)[0, 0])
            : afterLanguage.StartsWith((ReadOnlySpan<byte>)[0]);
    }
}
