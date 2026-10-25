using CrateEdit.Core.Files;

namespace CrateEdit.Tests.Writing;

public sealed class LockingProcessesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-lock-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string CreateFile()
    {
        var path = Path.Combine(_dir, "track.mp3");
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    [Fact]
    public void Names_the_program_holding_a_file_on_windows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows locks files this way.");

        // Arrange
        var path = CreateFile();
        using var openFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var programs = LockingProcesses.Find(path);

        // Assert
        Assert.NotEmpty(programs);
    }

    [Fact]
    public void File_that_nobody_holds_gives_no_names()
    {
        // Arrange
        var path = CreateFile();

        // Act
        var programs = LockingProcesses.Find(path);

        // Assert
        Assert.Empty(programs);
    }

    [Fact]
    public void Unknown_file_gives_no_names_instead_of_failing()
    {
        // Arrange
        var missing = Path.Combine(_dir, "missing.mp3");

        // Act
        var programs = LockingProcesses.Find(missing);

        // Assert
        Assert.Empty(programs);
    }
}
