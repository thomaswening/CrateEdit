using System.Globalization;
using CrateEdit.Core;
using CrateEdit.Core.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace CrateEdit.Tests.Diagnostics;

public sealed class DiagnosticLogTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("crateedit-data-").FullName;

    public void Dispose() => Directory.Delete(_dataDir, recursive: true);

    [Fact]
    [Trait("Req", "REQ-116")]
    [Trait("Req", "REQ-118")]
    public void Writes_redacted_entries_to_a_local_log_file()
    {
        var paths = new AppPaths(_dataDir);
        var redactor = new PathRedactor(@"C:\Users\jane", caseSensitive: false);

        using (var logger = DiagnosticLog.Create(paths, redactor, TimeProvider.System))
        {
            logger.Information("Could not read {Path}", @"C:\Users\jane\Music\track.mp3");
        }

        var file = Assert.Single(Directory.GetFiles(paths.LogDirectory, "crateedit-*.log"));
        var content = File.ReadAllText(file);
        Assert.Contains(@"~\Music\track.mp3", content);
        Assert.DoesNotContain("jane", content);
    }

    [Fact]
    [Trait("Req", "REQ-129")]
    public void Log_entries_do_not_depend_on_the_ui_language()
    {
        var paths = new AppPaths(_dataDir);
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            using var logger = DiagnosticLog.Create(paths, PathRedactor.ForCurrentUser, TimeProvider.System);
            logger.Information("Retrying after {Seconds} seconds", 1.5);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        var content = File.ReadAllText(Assert.Single(Directory.GetFiles(paths.LogDirectory)));
        Assert.Contains("Retrying after 1.5 seconds", content);
    }

    [Fact]
    [Trait("Req", "REQ-116")]
    public void Prunes_expired_logs_on_startup()
    {
        var paths = new AppPaths(_dataDir);
        Directory.CreateDirectory(paths.LogDirectory);
        var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        var expired = Path.Combine(paths.LogDirectory, "crateedit-20260801.log");
        File.WriteAllText(expired, "old");
        File.SetLastWriteTimeUtc(expired, now.AddDays(-31).UtcDateTime);

        using (DiagnosticLog.Create(paths, PathRedactor.ForCurrentUser, new FakeTimeProvider(now)))
        {
        }

        Assert.False(File.Exists(expired));
    }
}
