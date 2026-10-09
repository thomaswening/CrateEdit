using CrateEdit.Core.Diagnostics;

namespace CrateEdit.Tests.Diagnostics;

[Trait("Req", "REQ-116")]
public sealed class LogRetentionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-logs-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string CreateFile(string name, TimeSpan age)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, (Now - age).UtcDateTime);
        return path;
    }

    [Fact]
    public void Deletes_logs_older_than_30_days_and_keeps_newer_ones()
    {
        var expired = CreateFile("crateedit-20260901.log", TimeSpan.FromDays(31));
        var recent = CreateFile("crateedit-20261001.log", TimeSpan.FromDays(29));

        var deleted = LogRetention.Prune(_dir, Now);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void Leaves_files_that_are_not_application_logs()
    {
        var other = CreateFile("notes.txt", TimeSpan.FromDays(400));

        LogRetention.Prune(_dir, Now);

        Assert.True(File.Exists(other));
    }

    [Fact]
    public void Missing_directory_is_not_an_error()
    {
        Assert.Equal(0, LogRetention.Prune(Path.Combine(_dir, "missing"), Now));
    }
}
