using CrateEdit.Core;
using CrateEdit.Core.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace CrateEdit.Tests.Diagnostics;

public sealed class DiagnosticLogTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("crateedit-data-").FullName;

    public void Dispose() => Directory.Delete(_dataDir, recursive: true);

    private AppPaths Paths => new(_dataDir);

    /// <summary>The content of the only log file; fails the test if there is not exactly one.</summary>
    private string LogContent() => File.ReadAllText(Assert.Single(Directory.GetFiles(Paths.LogDirectory, "crateedit-*.log")));

    [Fact]
    [Trait("Req", "REQ-116")]
    [Trait("Req", "REQ-118")]
    public void Writes_redacted_entries_to_a_local_log_file()
    {
        // Arrange
        var redactor = new PathRedactor(@"C:\Users\jane", caseSensitive: false);
        var logger = DiagnosticLog.Create(Paths, redactor, TimeProvider.System);

        // Act
        logger.Information("Could not read {Path}", @"C:\Users\jane\Music\track.mp3");
        logger.Dispose();

        // Assert
        var content = LogContent();
        Assert.Contains(@"~\Music\track.mp3", content);
        Assert.DoesNotContain("jane", content);
    }

    [Fact]
    [Trait("Req", "REQ-129")]
    public void Log_entries_do_not_depend_on_the_ui_language()
    {
        // Arrange
        var logger = DiagnosticLog.Create(Paths, PathRedactor.ForCurrentUser, TimeProvider.System);

        // Act
        using (CultureScope.Formatting("de-DE"))
        {
            logger.Information("Retrying after {Seconds} seconds", 1.5);
        }

        logger.Dispose();

        // Assert
        Assert.Contains("Retrying after 1.5 seconds", LogContent());
    }

    [Fact]
    [Trait("Req", "REQ-116")]
    public void Prunes_expired_logs_on_startup()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(Paths.LogDirectory);
        var expired = Path.Combine(Paths.LogDirectory, "crateedit-20260801.log");
        File.WriteAllText(expired, "old");
        File.SetLastWriteTimeUtc(expired, now.AddDays(-31).UtcDateTime);

        // Act
        DiagnosticLog.Create(Paths, PathRedactor.ForCurrentUser, new FakeTimeProvider(now)).Dispose();

        // Assert
        Assert.False(File.Exists(expired));
    }
}
