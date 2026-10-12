namespace CrateEdit.Core;

/// <summary>
/// Locations of the application's own local data. Everything here is independent of the
/// collection root, so it survives the collection becoming unavailable (REQ-013).
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
    }

    /// <summary>
    /// Per-user local data: %LOCALAPPDATA%\CrateEdit on Windows,
    /// ~/Library/Application Support/CrateEdit on macOS.
    /// </summary>
    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppInfo.Name));

    public string DataDirectory { get; }

    public string LogDirectory => Path.Combine(DataDirectory, "Logs");

    /// <summary>Recovery records for writes in progress (ADR-0010).</summary>
    public string RecoveryDirectory => Path.Combine(DataDirectory, "Recovery");
}
