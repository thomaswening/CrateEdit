using CrateEdit.Core.Files;

namespace CrateEdit.Tests.Writing;

public sealed class LockingProcessesTests : IDisposable
{
    private readonly string _path = Path.Combine(Directory.CreateTempSubdirectory("crateedit-lock-").FullName, "track.mp3");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_path)!, recursive: true);

    [Fact]
    public void Names_the_program_holding_a_file_on_windows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows locks files this way.");
        File.WriteAllBytes(_path, [1, 2, 3]);

        using (new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.NotEmpty(LockingProcesses.Find(_path));
        }

        Assert.Empty(LockingProcesses.Find(_path));
    }

    [Fact]
    public void Unknown_file_gives_no_names_instead_of_failing()
    {
        Assert.Empty(LockingProcesses.Find(Path.Combine(Path.GetDirectoryName(_path)!, "missing.mp3")));
    }
}
