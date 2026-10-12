using CrateEdit.Core;
using CrateEdit.Core.Files;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Writing;

public sealed class FileRecoveryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Directory.CreateTempSubdirectory("crateedit-recovery-").FullName, "Recovery");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_dir)!, recursive: true);

    private static RecoveryRecord Record(byte[]? newHash = null) => new(
        Guid.NewGuid(), "/music/track.mp3", "/music/.track.mp3.crateedit-tmp", 12345,
        [0x49, 0x44, 0x33, 4, 0, 0, 0, 0, 0, 0], [], [1, 2, 3], [4, 5, 6], newHash);

    [Fact]
    [Trait("Req", "REQ-089")]
    public void Saved_records_are_loaded_back_with_all_bytes()
    {
        var store = new FileRecoveryStore(_dir);
        var record = Record();

        store.Save(record);
        var loaded = Assert.Single(store.LoadAll());

        Assert.Equal(record.Id, loaded.Id);
        Assert.Equal(record.OriginalPrefix, loaded.OriginalPrefix);
        Assert.Equal(record.OriginalFileHash, loaded.OriginalFileHash);
        Assert.Null(loaded.NewFileHash);
    }

    [Fact]
    public void Saving_again_replaces_the_record()
    {
        var store = new FileRecoveryStore(_dir);
        var record = Record();
        store.Save(record);

        store.Save(record with { NewFileHash = [9, 9] });

        Assert.Equal([9, 9], Assert.Single(store.LoadAll()).NewFileHash);
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Record_half_saved_during_a_crash_is_ignored_and_the_previous_version_kept()
    {
        var store = new FileRecoveryStore(_dir);
        var record = Record();
        store.Save(record);
        File.WriteAllText(Path.Combine(_dir, record.Id.ToString("N") + ".json.partial"), "{ \"Id\": \"tru");

        Assert.Equal(record.Id, Assert.Single(store.LoadAll()).Id);
        Assert.Empty(Directory.GetFiles(_dir, "*.partial"));
    }

    [Fact]
    public void Deleted_records_are_gone()
    {
        var store = new FileRecoveryStore(_dir);
        var record = Record();
        store.Save(record);

        store.Delete(record.Id);

        Assert.Empty(store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-013")]
    public void Recovery_material_lives_in_local_app_data_not_next_to_the_collection()
    {
        var paths = new AppPaths(Path.Combine("data", "CrateEdit"));

        Assert.Equal(Path.Combine("data", "CrateEdit", "Recovery"), paths.RecoveryDirectory);
    }
}

public class FileAccessErrorTests
{
    [Theory]
    [Trait("Req", "REQ-079")]
    [InlineData(unchecked((int)0x80070020), FileProblem.Locked, true)]   // sharing violation
    [InlineData(unchecked((int)0x80070021), FileProblem.Locked, true)]   // lock violation
    [InlineData(unchecked((int)0x80070070), FileProblem.DiskFull, false)]
    [InlineData(unchecked((int)0x80070027), FileProblem.DiskFull, false)]
    [InlineData(unchecked((int)0x80070005), FileProblem.IoError, false)]
    public void Io_exceptions_are_classified_by_their_error_code(int hresult, FileProblem expected, bool transient)
    {
        var error = FileAccessError.From(new IOException("boom", hresult), "track.mp3");

        Assert.Equal(expected, error.Problem);
        Assert.Equal(transient, error.IsTransient);
    }

    [Fact]
    public void Exception_types_are_classified_and_kept_as_cause()
    {
        var notFound = new FileNotFoundException();

        Assert.Equal(FileProblem.NotFound, FileAccessError.From(notFound, "a").Problem);
        Assert.Same(notFound, FileAccessError.From(notFound, "a").Cause);
        Assert.Equal(FileProblem.NotFound, FileAccessError.From(new DirectoryNotFoundException(), "a").Problem);
        Assert.Equal(FileProblem.AccessDenied, FileAccessError.From(new UnauthorizedAccessException(), "a").Problem);
    }
}
