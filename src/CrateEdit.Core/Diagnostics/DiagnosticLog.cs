using System.Globalization;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Display;

namespace CrateEdit.Core.Diagnostics;

/// <summary>
/// Local diagnostic logging for troubleshooting. Logs are written only to files in the
/// application's data directory and are never transmitted anywhere (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-117">REQ-117</see>); files older than
/// 30 days are deleted (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-116">REQ-116</see>) and the user's home directory is redacted (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-118">REQ-118</see>).
/// </summary>
public static class DiagnosticLog
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>Prunes expired log files and creates a logger writing to a daily log file.</summary>
    public static Logger Create(AppPaths paths, PathRedactor redactor, TimeProvider timeProvider)
    {
        Directory.CreateDirectory(paths.LogDirectory);
        LogRetention.Prune(paths.LogDirectory, timeProvider.GetUtcNow());

        var formatter = new RedactingFormatter(
            new MessageTemplateTextFormatter(OutputTemplate, CultureInfo.InvariantCulture), redactor);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                formatter,
                Path.Combine(paths.LogDirectory, "crateedit-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: null,
                shared: true)
            .CreateLogger();
    }
}
