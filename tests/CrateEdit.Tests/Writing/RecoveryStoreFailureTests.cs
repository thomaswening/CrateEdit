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
        public Func<RecoveryRecord, bool> FailSave { get; set; } = _ => false;

        public bool FailDelete { get; set; }

        public void Save(RecoveryRecord record)
        {
            if (FailSave(record))
            {
                throw new IOException("There is not enough space on the disk.", unchecked((int)0x80070070));
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
        _inner = new FileRecoveryStore(Path.Combine(_dir, "recovery"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    [Trait("Req", "REQ-089")]
    public void No_write_without_recovery_material()
    {
        var store = new FailingStore(_inner) { FailSave = _ => true };

        var error = Assert.IsType<RecoveryUnavailable>(new SafeFileWriter(store).Write(_path, Changes).Error);

        Assert.IsType<IOException>(error.Cause);
        Assert.Equal(_original, File.ReadAllBytes(_path));
        Assert.False(File.Exists(SafeFileWriter.TempPathFor(_path)));
    }

    [Fact]
    [Trait("Req", "REQ-089")]
    public void Failing_to_record_the_verified_temp_keeps_the_original()
    {
        var store = new FailingStore(_inner) { FailSave = record => record.NewFileHash is not null };

        Assert.IsType<RecoveryUnavailable>(new SafeFileWriter(store).Write(_path, Changes).Error);

        Assert.Equal(_original, File.ReadAllBytes(_path));
        Assert.False(File.Exists(SafeFileWriter.TempPathFor(_path)));
        Assert.Empty(_inner.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-090")]
    public void Failing_to_clean_up_after_a_successful_write_is_still_a_success()
    {
        var store = new FailingStore(_inner) { FailDelete = true };

        var result = new SafeFileWriter(store).Write(_path, Changes);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal("New Label", Mp3TagReader.Read(_path).Values[TagField.Label]);

        // The leftover record is resolved at the next start without touching the file again.
        var edited = File.ReadAllBytes(_path);
        Assert.Equal(RecoveryOutcome.WriteCompleted, Assert.Single(RecoveryReconciler.ReconcileAll(_inner)).Outcome);
        Assert.Equal(edited, File.ReadAllBytes(_path));
        Assert.Empty(_inner.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Unreadable_record_is_kept_and_does_not_stop_the_others()
    {
        var recoveryDir = Path.Combine(_dir, "recovery");
        Directory.CreateDirectory(recoveryDir);
        var corrupt = Path.Combine(recoveryDir, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(corrupt, "{ this is not json");
        _inner.Save(new RecoveryRecord(Guid.NewGuid(), _path, "tmp", 1, [], null, [], [], null));

        var records = _inner.LoadAll();

        Assert.Single(records);
        Assert.True(File.Exists(corrupt));
    }
}
