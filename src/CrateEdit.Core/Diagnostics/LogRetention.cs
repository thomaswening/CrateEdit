namespace CrateEdit.Core.Diagnostics;

/// <summary>Deletes diagnostic log files once they are older than the retention period (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-116">REQ-116</see>).</summary>
public static class LogRetention
{
    public static readonly TimeSpan Period = TimeSpan.FromDays(30);

    internal const string FilePattern = "crateedit-*.log";

    /// <summary>Deletes log files last written before <paramref name="now"/> minus <see cref="Period"/>.</summary>
    /// <returns>The number of files deleted.</returns>
    public static int Prune(string logDirectory, DateTimeOffset now)
    {
        if (!Directory.Exists(logDirectory))
        {
            return 0;
        }

        var cutoff = (now - Period).UtcDateTime;
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(logDirectory, FilePattern))
        {
            if (File.GetLastWriteTimeUtc(file) >= cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (IOException)
            {
                // Still open by another process; retried on the next start.
            }
            catch (UnauthorizedAccessException)
            {
                // Retried on the next start.
            }
        }

        return deleted;
    }
}
