using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;
using CrateEdit.Tests.Tags;

namespace CrateEdit.Tests.Writing;

/// <summary>What happens when the recovery store itself fails (e.g. the system drive is full).</summary>
public sealed class RecoveryStoreFailureTests : IDisposable
{
    /// <summary>Delegates to a real store but can be told to fail.</summary>
    private sealed class FailingStore(IRecoveryStore inner) : IRecoveryStore
    {
        private const int DiskFullHResult = unchecked((int)0x80070070);

        public Func<RecoveryRecord, bool> FailSave { get; init; } = _ => false;

        public bool FailDelete { get; init; }

        public void Save(RecoveryRecord record)
        {
            if (FailSave(record))
            {
                throw new IOException("There is not enough space on the disk.", DiskFullHResult);
            }

            inner.Save(record);
        }

        public void Delete(Guid id)
        {
            if (FailDelete)
            {
                throw new IOException("Access to the recovery folder failed.");
            }

            inner.Delete(id);
        }

        public IReadOnlyList<RecoveryRecord> LoadAll() => inner.LoadAll();
    }

    private static readonly Dictionary<TagField, string?> Changes = new() { [TagField.Label] = "New Label" };

    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-storefail-").FullName;
    private readonly string _path;
    private readonly byte[] _original = Mp3Fixture.Build(4, Frames.BeatportLike());
    private readonly FileRecoveryStore _inner;

    public RecoveryStoreFailureTests()
    {
        _path = Path.Combine(_dir, "track.mp3");
        File.WriteAllBytes(_path, _original);
        _inner = new FileRecoveryStore(RecoveryDirectory);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string RecoveryDirectory => Path.Combine(_dir, "recovery");

    private byte[] CurrentFile => File.ReadAllBytes(_path);

    private bool TempFileExists => File.Exists(SafeFileWriter.TempPathFor(_path));

    private static bool IsRecordOfVerifiedTemp(RecoveryRecord record) => record.NewFileHash is not null;

    [Fact]
    [Trait("Req", "REQ-089")]
    public void No_write_without_recovery_material()
    {
        // Arrange
        var store = new FailingStore(_inner) { FailSave = _ => true };

        // Act
        var result = new SafeFileWriter(store).Write(_path, Changes);

        // Assert
        var error = Assert.IsType<RecoveryUnavailable>(result.Error);
        Assert.IsType<IOException>(error.Cause);
        Assert.Equal(_original, CurrentFile);
        Assert.False(TempFileExists);
    }

    [Fact]
    [Trait("Req", "REQ-089")]
    public void Failing_to_record_the_verified_temp_keeps_the_original()
    {
        // Arrange
        var store = new FailingStore(_inner) { FailSave = IsRecordOfVerifiedTemp };

        // Act
        var result = new SafeFileWriter(store).Write(_path, Changes);

        // Assert
        Assert.IsType<RecoveryUnavailable>(result.Error);
        Assert.Equal(_original, CurrentFile);
        Assert.False(TempFileExists);
        Assert.Empty(_inner.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-090")]
    public void Failing_to_clean_up_after_a_successful_write_is_still_a_success()
    {
        // Arrange
        var store = new FailingStore(_inner) { FailDelete = true };

        // Act
        var result = new SafeFileWriter(store).Write(_path, Changes);

        // Assert
        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal("New Label", Mp3TagReader.Read(_path).Values[TagField.Label]);
    }

    [Fact]
    [Trait("Req", "REQ-090")]
    public void Record_left_by_a_failed_clean_up_is_resolved_without_touching_the_file()
    {
        // Arrange
        var store = new FailingStore(_inner) { FailDelete = true };
        new SafeFileWriter(store).Write(_path, Changes);
        var edited = CurrentFile;

        // Act
        var outcome = Assert.Single(RecoveryReconciler.ReconcileAll(_inner)).Outcome;

        // Assert
        Assert.Equal(RecoveryOutcome.WriteCompleted, outcome);
        Assert.Equal(edited, CurrentFile);
        Assert.Empty(_inner.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Unreadable_record_is_kept_and_does_not_stop_the_others()
    {
        // Arrange
        Directory.CreateDirectory(RecoveryDirectory);
        var corrupt = Path.Combine(RecoveryDirectory, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(corrupt, "{ this is not json");
        _inner.Save(new RecoveryRecord(Guid.NewGuid(), _path, "tmp", 1, [], [], [], [], null));

        // Act
        var records = _inner.LoadAll();

        // Assert
        Assert.Single(records);
        Assert.True(File.Exists(corrupt));
    }
}
