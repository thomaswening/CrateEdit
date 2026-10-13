using CrateEdit.Core.Files;
using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;
using CrateEdit.Tests.Tags;

namespace CrateEdit.Tests.Writing;

/// <summary>
/// Tests the safe write (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0010-safe-write-technique.md">ADR-0010</see>) including simulated crashes after every step. A simulated
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

    private static readonly DateTime OriginalCreationTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>A byte inside the ID3v2 tag, i.e. outside the audio.</summary>
    private const int TagByte = 20;

    /// <summary>A byte inside the audio, counted from the end of the file.</summary>
    private static readonly Index AudioByte = ^500;

    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-safe-").FullName;
    private readonly string _path;
    private readonly byte[] _original;
    private readonly byte[] _expected;
    private readonly FileRecoveryStore _store;

    public SafeFileWriterTests()
    {
        _original = Mp3Fixture.Build(4, [.. Frames.BeatportLike(), .. Frames.DjSoftwareData()], padding: 16,
            id3v1: Frames.Id3v1("Old title", "Old artist"));
        _expected = RewrittenByTagWriter(_original);
        _path = Path.Combine(_dir, "track.mp3");
        File.WriteAllBytes(_path, _original);
        File.SetCreationTimeUtc(_path, OriginalCreationTime);
        _store = new FileRecoveryStore(Path.Combine(_dir, "recovery"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string TempPath => SafeFileWriter.TempPathFor(_path);

    private byte[] CurrentFile => File.ReadAllBytes(_path);

    /// <summary>What the tag writer alone makes of the file: the result a safe write must produce.</summary>
    private byte[] RewrittenByTagWriter(byte[] file)
    {
        var source = Path.Combine(_dir, "expected-source.mp3");
        var destination = Path.Combine(_dir, "expected.mp3");
        File.WriteAllBytes(source, file);
        Assert.True(Mp3TagWriter.Rewrite(source, destination, Changes).IsSuccess);
        var rewritten = File.ReadAllBytes(destination);
        File.Delete(source);
        File.Delete(destination);
        return rewritten;
    }

    private SafeFileWriter WriterThatRunsAfter(WriteStep step, Action action) =>
        new(_store) { AfterStep = current => { if (current == step) action(); } };

    /// <summary>Starts a write that crashes right after <paramref name="step"/>, leaving whatever the crash leaves.</summary>
    private void CrashAfter(WriteStep step)
    {
        var writer = WriterThatRunsAfter(step, () => throw new SimulatedCrash());
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));
    }

    /// <summary>Runs recovery as at the next start and returns the outcome for the one interrupted write.</summary>
    private RecoveryOutcome Reconcile() => Assert.Single(RecoveryReconciler.ReconcileAll(_store)).Outcome;

    private static void FlipByte(string path, Index position)
    {
        var bytes = File.ReadAllBytes(path);
        bytes[position] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    private void AssertNothingLeftBehind()
    {
        Assert.False(File.Exists(TempPath), "Temporary file left behind.");
        Assert.Empty(_store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-090")]
    public void Successful_write_replaces_the_file_and_removes_all_recovery_material()
    {
        // Act
        var result = new SafeFileWriter(_store).Write(_path, Changes);

        // Assert
        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(_expected, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    public void Successful_write_keeps_the_creation_date_on_windows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Creation dates of replaced files are only preserved on Windows.");

        // Act
        new SafeFileWriter(_store).Write(_path, Changes);

        // Assert
        Assert.Equal(OriginalCreationTime, File.GetCreationTimeUtc(_path));
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Refused_tag_leaves_the_file_unchanged_and_nothing_behind()
    {
        // Arrange
        const byte UnsynchronisationFlag = 0x80;
        var unsupported = Mp3Fixture.Build(3, [Frames.Text("TIT2", "Title", TextEncodings.Latin1)], flags: UnsynchronisationFlag);
        File.WriteAllBytes(_path, unsupported);

        // Act
        var result = new SafeFileWriter(_store).Write(_path, Changes);

        // Assert
        Assert.IsType<TagNotWritable>(result.Error);
        Assert.Equal(unsupported, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    [Trait("Req", "REQ-079")]
    public void File_held_open_by_another_program_is_a_transient_failure_and_stays_unchanged()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows prevents replacing a file that is open.");

        // Arrange
        using var otherProgram = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var result = new SafeFileWriter(_store).Write(_path, Changes);

        // Assert
        var error = Assert.IsType<FileAccessError>(result.Error);
        Assert.Equal(FileProblem.Locked, error.Problem);
        Assert.True(error.IsTransient);
        Assert.NotEmpty(error.LockedBy);
        Assert.NotNull(error.Cause);
        Assert.Equal(_original, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    public void Missing_file_is_reported_as_not_found()
    {
        // Arrange
        var missing = Path.Combine(_dir, "missing.mp3");

        // Act
        var result = new SafeFileWriter(_store).Write(missing, Changes);

        // Assert
        var error = Assert.IsType<FileAccessError>(result.Error);
        Assert.Equal(FileProblem.NotFound, error.Problem);
        Assert.Empty(_store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Corrupted_temp_file_is_caught_before_it_replaces_the_original()
    {
        // Arrange: a faulty USB stick, the temp file reads back with one audio byte changed.
        var writer = WriterThatRunsAfter(WriteStep.TempWritten, () => FlipByte(TempPath, AudioByte));

        // Act
        var result = writer.Write(_path, Changes);

        // Assert
        var error = Assert.IsType<FileAccessError>(result.Error);
        Assert.Equal(FileProblem.VerificationFailed, error.Problem);
        Assert.Equal(_original, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-089")]
    public void Wrong_result_after_the_replace_is_detected_and_the_original_restored()
    {
        // Arrange: the replaced file's tag is damaged, its audio is intact.
        var writer = WriterThatRunsAfter(WriteStep.Replaced, () => FlipByte(_path, TagByte));

        // Act
        var result = writer.Write(_path, Changes);

        // Assert
        var error = Assert.IsType<FileAccessError>(result.Error);
        Assert.Equal(FileProblem.VerificationFailed, error.Problem);
        Assert.Equal(_original, CurrentFile);
        AssertNothingLeftBehind();
    }

    public static TheoryData<WriteStep> StepsBeforeTheReplace() =>
        new(WriteStep.RecoveryRecorded, WriteStep.TempWritten, WriteStep.TempVerified);

    public static TheoryData<WriteStep> StepsFromTheReplaceOn() =>
        new(WriteStep.Replaced, WriteStep.ResultVerified);

    [Theory]
    [MemberData(nameof(StepsBeforeTheReplace))]
    [Trait("Req", "REQ-089")]
    [Trait("Req", "REQ-092")]
    public void Crash_before_the_replace_ends_with_exactly_the_original_file(WriteStep crashAfter)
    {
        // Arrange
        CrashAfter(crashAfter);

        // Act
        var outcome = Reconcile();

        // Assert
        Assert.Equal(RecoveryOutcome.OriginalKept, outcome);
        Assert.Equal(_original, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Theory]
    [MemberData(nameof(StepsFromTheReplaceOn))]
    [Trait("Req", "REQ-089")]
    [Trait("Req", "REQ-092")]
    public void Crash_from_the_replace_on_ends_with_exactly_the_new_file(WriteStep crashAfter)
    {
        // Arrange
        CrashAfter(crashAfter);

        // Act
        var outcome = Reconcile();

        // Assert
        Assert.Equal(RecoveryOutcome.WriteCompleted, outcome);
        Assert.Equal(_expected, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Interrupted_non_atomic_replace_with_the_original_missing_is_finished()
    {
        // Arrange: on FAT32 or Google Drive a crash during the replace can leave the original name missing.
        CrashAfter(WriteStep.TempVerified);
        File.Delete(_path);

        // Act
        var outcome = Reconcile();

        // Assert
        Assert.Equal(RecoveryOutcome.WriteCompleted, outcome);
        Assert.Equal(_expected, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    [Trait("Req", "REQ-089")]
    [Trait("Req", "REQ-092")]
    public void Original_missing_and_temp_unverified_restores_the_original_from_recovery_material()
    {
        // Arrange
        CrashAfter(WriteStep.TempWritten);
        File.Delete(_path);

        // Act
        var outcome = Reconcile();

        // Assert
        Assert.Equal(RecoveryOutcome.OriginalRestored, outcome);
        Assert.Equal(_original, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    [Trait("Req", "REQ-089")]
    [Trait("Req", "REQ-092")]
    public void Damaged_tag_with_intact_audio_is_restored_to_the_original()
    {
        // Arrange
        CrashAfter(WriteStep.Replaced);
        FlipByte(_path, TagByte);

        // Act
        var outcome = Reconcile();

        // Assert
        Assert.Equal(RecoveryOutcome.OriginalRestored, outcome);
        Assert.Equal(_original, CurrentFile);
        AssertNothingLeftBehind();
    }

    [Fact]
    [Trait("Req", "REQ-093")]
    public void File_changed_by_another_program_is_left_alone_and_kept_for_the_user()
    {
        // Arrange
        CrashAfter(WriteStep.TempWritten);
        var changedByOtherProgram = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Something else")], audioLength: 5000);
        File.WriteAllBytes(_path, changedByOtherProgram);

        // Act
        var outcome = Reconcile();

        // Assert
        Assert.Equal(RecoveryOutcome.Unresolved, outcome);
        Assert.Equal(changedByOtherProgram, CurrentFile);
        Assert.Single(_store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Incomplete_temp_with_missing_original_is_not_guessed_at()
    {
        // Arrange
        CrashAfter(WriteStep.TempWritten);
        File.Delete(_path);
        var truncatedTemp = File.ReadAllBytes(TempPath)[..^1000];
        File.WriteAllBytes(TempPath, truncatedTemp);

        // Act
        var outcome = Reconcile();

        // Assert
        Assert.Equal(RecoveryOutcome.Unresolved, outcome);
        Assert.Equal(truncatedTemp, File.ReadAllBytes(TempPath));
        Assert.Single(_store.LoadAll());
    }
}
