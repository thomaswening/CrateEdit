using CrateEdit.Core.Diagnostics;

namespace CrateEdit.Tests.Diagnostics;

[Trait("Req", "REQ-116")]
public sealed class LogRetentionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-logs-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string CreateFile(string name, int ageInDays)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, Now.AddDays(-ageInDays).UtcDateTime);
        return path;
    }

    [Fact]
    public void Deletes_logs_older_than_30_days_and_keeps_newer_ones()
    {
        // Arrange
        var expired = CreateFile("crateedit-20260901.log", ageInDays: 31);
        var recent = CreateFile("crateedit-20261001.log", ageInDays: 29);

        // Act
        var deleted = LogRetention.Prune(_dir, Now);

        // Assert
        Assert.Equal(1, deleted);
        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void Leaves_files_that_are_not_application_logs()
    {
        // Arrange
        var other = CreateFile("notes.txt", ageInDays: 400);

        // Act
        LogRetention.Prune(_dir, Now);

        // Assert
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void Missing_directory_is_not_an_error()
    {
        // Arrange
        var missing = Path.Combine(_dir, "missing");

        // Act
        var deleted = LogRetention.Prune(missing, Now);

        // Assert
        Assert.Equal(0, deleted);
    }
}
