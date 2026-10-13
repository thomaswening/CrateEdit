using CrateEdit.Core;
using CrateEdit.Core.Files;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Writing;

public sealed class FileRecoveryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Directory.CreateTempSubdirectory("crateedit-recovery-").FullName, "Recovery");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_dir)!, recursive: true);

    private static RecoveryRecord Record() => new(
        Id: Guid.NewGuid(),
        TargetPath: "/music/track.mp3",
        TempPath: "/music/.track.mp3.crateedit-tmp",
        OriginalLength: 12345,
        OriginalPrefix: [0x49, 0x44, 0x33, 4, 0, 0, 0, 0, 0, 0],
        OriginalSuffix: [],
        OriginalAudioHash: [1, 2, 3],
        OriginalFileHash: [4, 5, 6],
        NewFileHash: null);

    [Fact]
    [Trait("Req", "REQ-089")]
    public void Saved_records_are_loaded_back_with_all_bytes()
    {
        // Arrange
        var store = new FileRecoveryStore(_dir);
        var record = Record();
        store.Save(record);

        // Act
        var loaded = Assert.Single(store.LoadAll());

        // Assert
        Assert.Equal(record.Id, loaded.Id);
        Assert.Equal(record.OriginalPrefix, loaded.OriginalPrefix);
        Assert.Equal(record.OriginalFileHash, loaded.OriginalFileHash);
        Assert.Null(loaded.NewFileHash);
    }

    [Fact]
    public void Saving_again_replaces_the_record()
    {
        // Arrange
        var store = new FileRecoveryStore(_dir);
        var record = Record();
        store.Save(record);

        // Act
        store.Save(record with { NewFileHash = [9, 9] });

        // Assert
        Assert.Equal([9, 9], Assert.Single(store.LoadAll()).NewFileHash);
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Record_half_saved_during_a_crash_is_ignored_and_the_previous_version_kept()
    {
        // Arrange
        var store = new FileRecoveryStore(_dir);
        var record = Record();
        store.Save(record);
        var halfSaved = Path.Combine(_dir, record.Id.ToString("N") + ".json.partial");
        File.WriteAllText(halfSaved, "{ \"Id\": \"tru");

        // Act
        var records = store.LoadAll();

        // Assert
        Assert.Equal(record.Id, Assert.Single(records).Id);
        Assert.False(File.Exists(halfSaved));
    }

    [Fact]
    public void Deleted_records_are_gone()
    {
        // Arrange
        var store = new FileRecoveryStore(_dir);
        var record = Record();
        store.Save(record);

        // Act
        store.Delete(record.Id);

        // Assert
        Assert.Empty(store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-013")]
    public void Recovery_material_lives_in_local_app_data_not_next_to_the_collection()
    {
        // Arrange
        var appData = Path.Combine("data", "CrateEdit");

        // Act
        var paths = new AppPaths(appData);

        // Assert
        Assert.Equal(Path.Combine(appData, "Recovery"), paths.RecoveryDirectory);
    }
}

public class FileAccessErrorTests
{
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);
    private const int DiskFull = unchecked((int)0x80070070);
    private const int HandleDiskFull = unchecked((int)0x80070027);
    private const int AccessDenied = unchecked((int)0x80070005);

    [Theory]
    [Trait("Req", "REQ-079")]
    [InlineData(SharingViolation, FileProblem.Locked, true)]
    [InlineData(LockViolation, FileProblem.Locked, true)]
    [InlineData(DiskFull, FileProblem.DiskFull, false)]
    [InlineData(HandleDiskFull, FileProblem.DiskFull, false)]
    [InlineData(AccessDenied, FileProblem.IoError, false)]
    public void Io_exceptions_are_classified_by_their_error_code(int hresult, FileProblem expected, bool transient)
    {
        // Arrange
        var exception = new IOException("boom", hresult);

        // Act
        var error = FileAccessError.From(exception, "track.mp3");

        // Assert
        Assert.Equal(expected, error.Problem);
        Assert.Equal(transient, error.IsTransient);
    }

    public static TheoryData<Exception, FileProblem> ExceptionTypes() => new()
    {
        { new FileNotFoundException(), FileProblem.NotFound },
        { new DirectoryNotFoundException(), FileProblem.NotFound },
        { new UnauthorizedAccessException(), FileProblem.AccessDenied },
    };

    [Theory]
    [MemberData(nameof(ExceptionTypes))]
    public void Exception_types_are_classified(Exception exception, FileProblem expected)
    {
        // Act
        var error = FileAccessError.From(exception, "track.mp3");

        // Assert
        Assert.Equal(expected, error.Problem);
    }

    [Fact]
    public void Exception_is_kept_as_the_cause()
    {
        // Arrange
        var exception = new FileNotFoundException();

        // Act
        var error = FileAccessError.From(exception, "track.mp3");

        // Assert
        Assert.Same(exception, error.Cause);
    }
}
