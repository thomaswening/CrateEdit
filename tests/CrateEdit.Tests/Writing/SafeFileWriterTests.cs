using CrateEdit.Core.Files;
using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;
using CrateEdit.Tests.Tags;

namespace CrateEdit.Tests.Writing;

/// <summary>
/// Tests the safe write (ADR-0010) including simulated crashes after every step. A simulated
/// crash is an exception the writer does not catch, so none of its cleanup runs, as in a real
/// crash; reconciliation must then always end with either the exact original or the exact new file.
/// </summary>
public sealed class SafeFileWriterTests : IDisposable
{
    private sealed class SimulatedCrash : Exception;

    private static readonly Dictionary<TagField, string?> Changes = new()
    {
        [TagField.Artist] = "Röyksopp",
        [TagField.Key] = "8A",
        [TagField.Comment] = "A comment that does not fit into the padding of the original tag, so the tag must grow",
    };

    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-safe-").FullName;
    private readonly string _path;
    private readonly byte[] _original;
    private readonly byte[] _expected;
    private readonly FileRecoveryStore _store;

    public SafeFileWriterTests()
    {
        _original = Mp3Fixture.Build(4, [.. Frames.BeatportLike(), .. Frames.DjSoftwareData()], padding: 16,
            id3v1: Frames.Id3v1("Old title", "Old artist"));
        _path = Path.Combine(_dir, "track.mp3");
        File.WriteAllBytes(_path, _original);
        File.SetCreationTimeUtc(_path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _store = new FileRecoveryStore(Path.Combine(_dir, "recovery"));

        var copy = Path.Combine(_dir, "expected-source.mp3");
        File.WriteAllBytes(copy, _original);
        Assert.True(Mp3TagWriter.Rewrite(copy, Path.Combine(_dir, "expected.mp3"), Changes).IsSuccess);
        _expected = File.ReadAllBytes(Path.Combine(_dir, "expected.mp3"));
        File.Delete(copy);
        File.Delete(Path.Combine(_dir, "expected.mp3"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string TempPath => SafeFileWriter.TempPathFor(_path);

    private void AssertClean()
    {
        Assert.False(File.Exists(TempPath), "Temporary file left behind.");
        Assert.Empty(_store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-090")]
    public void Successful_write_replaces_the_file_and_removes_all_recovery_material()
    {
        var result = new SafeFileWriter(_store).Write(_path, Changes);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(_expected, File.ReadAllBytes(_path));
        AssertClean();
    }

    [Fact]
    public void Successful_write_keeps_the_creation_date_on_windows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Creation dates of replaced files are only preserved on Windows.");

        new SafeFileWriter(_store).Write(_path, Changes);

        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), File.GetCreationTimeUtc(_path));
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Refused_tag_leaves_the_file_unchanged_and_nothing_behind()
    {
        var unsupported = Mp3Fixture.Build(3, [Frames.Text("TIT2", "Title", encoding: 0)], flags: 0x80);
        File.WriteAllBytes(_path, unsupported);

        var result = new SafeFileWriter(_store).Write(_path, Changes);

        Assert.IsType<TagNotWritable>(result.Error);
        Assert.Equal(unsupported, File.ReadAllBytes(_path));
        AssertClean();
    }

    [Fact]
    [Trait("Req", "REQ-079")]
    public void File_held_open_by_another_program_is_a_transient_failure_and_stays_unchanged()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows prevents replacing a file that is open.");

        FileAccessError error;
        using (new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            error = Assert.IsType<FileAccessError>(new SafeFileWriter(_store).Write(_path, Changes).Error);
        }

        Assert.Equal(FileProblem.Locked, error.Problem);
        Assert.True(error.IsTransient);
        Assert.NotNull(error.Cause);
        Assert.Equal(_original, File.ReadAllBytes(_path));
        AssertClean();
    }

    [Fact]
    public void Missing_file_is_reported_as_not_found()
    {
        var error = Assert.IsType<FileAccessError>(new SafeFileWriter(_store).Write(Path.Combine(_dir, "missing.mp3"), Changes).Error);

        Assert.Equal(FileProblem.NotFound, error.Problem);
        Assert.Empty(_store.LoadAll());
    }

    private static void FlipByte(string path, int offsetFromEnd)
    {
        var bytes = File.ReadAllBytes(path);
        bytes[^offsetFromEnd] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Corrupted_temp_file_is_caught_before_it_replaces_the_original()
    {
        // E.g. a faulty USB stick: the temp file reads back with one audio byte changed.
        var writer = new SafeFileWriter(_store) { AfterStep = step => { if (step == WriteStep.TempWritten) FlipByte(TempPath, 500); } };

        var error = Assert.IsType<FileAccessError>(writer.Write(_path, Changes).Error);

        Assert.Equal(FileProblem.VerificationFailed, error.Problem);
        Assert.Equal(_original, File.ReadAllBytes(_path));
        AssertClean();
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-089")]
    public void Wrong_result_after_the_replace_is_detected_and_the_original_restored()
    {
        // Damage the replaced file's tag (audio intact): the final check fails, and the original is rebuilt.
        var writer = new SafeFileWriter(_store)
        {
            AfterStep = step =>
            {
                if (step == WriteStep.Replaced)
                {
                    var bytes = File.ReadAllBytes(_path);
                    bytes[20] ^= 0xFF;
                    File.WriteAllBytes(_path, bytes);
                }
            },
        };

        var error = Assert.IsType<FileAccessError>(writer.Write(_path, Changes).Error);

        Assert.Equal(FileProblem.VerificationFailed, error.Problem);
        Assert.Equal(_original, File.ReadAllBytes(_path));
        AssertClean();
    }

    public static TheoryData<WriteStep, bool> CrashPoints() => new()
    {
        { WriteStep.RecoveryRecorded, false },
        { WriteStep.TempWritten, false },
        { WriteStep.TempVerified, false },
        { WriteStep.Replaced, true },
        { WriteStep.ResultVerified, true },
    };

    [Theory]
    [MemberData(nameof(CrashPoints))]
    [Trait("Req", "REQ-089")]
    [Trait("Req", "REQ-092")]
    public void Crash_after_any_step_ends_with_exactly_the_original_or_the_new_file(WriteStep crashAfter, bool expectNew)
    {
        var writer = new SafeFileWriter(_store) { AfterStep = step => { if (step == crashAfter) throw new SimulatedCrash(); } };
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));

        var outcome = Assert.Single(RecoveryReconciler.ReconcileAll(_store)).Outcome;

        Assert.Equal(expectNew ? _expected : _original, File.ReadAllBytes(_path));
        Assert.Equal(expectNew ? RecoveryOutcome.WriteCompleted : RecoveryOutcome.OriginalKept, outcome);
        AssertClean();
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Interrupted_non_atomic_replace_with_the_original_missing_is_finished()
    {
        // On FAT32 or Google Drive a crash during the replace can leave the original name missing.
        var writer = new SafeFileWriter(_store) { AfterStep = step => { if (step == WriteStep.TempVerified) throw new SimulatedCrash(); } };
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));
        File.Delete(_path);

        Assert.Equal(RecoveryOutcome.WriteCompleted, Assert.Single(RecoveryReconciler.ReconcileAll(_store)).Outcome);
        Assert.Equal(_expected, File.ReadAllBytes(_path));
        AssertClean();
    }

    [Fact]
    [Trait("Req", "REQ-089")]
    [Trait("Req", "REQ-092")]
    public void Original_missing_and_temp_unverified_restores_the_original_from_recovery_material()
    {
        var writer = new SafeFileWriter(_store) { AfterStep = step => { if (step == WriteStep.TempWritten) throw new SimulatedCrash(); } };
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));
        File.Delete(_path);

        Assert.Equal(RecoveryOutcome.OriginalRestored, Assert.Single(RecoveryReconciler.ReconcileAll(_store)).Outcome);
        Assert.Equal(_original, File.ReadAllBytes(_path));
        AssertClean();
    }

    [Fact]
    [Trait("Req", "REQ-089")]
    [Trait("Req", "REQ-092")]
    public void Damaged_tag_with_intact_audio_is_restored_to_the_original()
    {
        var writer = new SafeFileWriter(_store) { AfterStep = step => { if (step == WriteStep.Replaced) throw new SimulatedCrash(); } };
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));
        var damaged = File.ReadAllBytes(_path);
        damaged[20] ^= 0xFF;
        File.WriteAllBytes(_path, damaged);

        Assert.Equal(RecoveryOutcome.OriginalRestored, Assert.Single(RecoveryReconciler.ReconcileAll(_store)).Outcome);
        Assert.Equal(_original, File.ReadAllBytes(_path));
        AssertClean();
    }

    [Fact]
    [Trait("Req", "REQ-093")]
    public void File_changed_by_another_program_is_left_alone_and_kept_for_the_user()
    {
        var writer = new SafeFileWriter(_store) { AfterStep = step => { if (step == WriteStep.TempWritten) throw new SimulatedCrash(); } };
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));
        var foreign = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Something else")], audioLength: 5000);
        File.WriteAllBytes(_path, foreign);

        Assert.Equal(RecoveryOutcome.Unresolved, Assert.Single(RecoveryReconciler.ReconcileAll(_store)).Outcome);
        Assert.Equal(foreign, File.ReadAllBytes(_path));
        Assert.Single(_store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Incomplete_temp_with_missing_original_is_not_guessed_at()
    {
        var writer = new SafeFileWriter(_store) { AfterStep = step => { if (step == WriteStep.TempWritten) throw new SimulatedCrash(); } };
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));
        File.Delete(_path);
        var truncated = File.ReadAllBytes(TempPath)[..^1000];
        File.WriteAllBytes(TempPath, truncated);

        Assert.Equal(RecoveryOutcome.Unresolved, Assert.Single(RecoveryReconciler.ReconcileAll(_store)).Outcome);
        Assert.Equal(truncated, File.ReadAllBytes(TempPath));
        Assert.Single(_store.LoadAll());
    }
}
