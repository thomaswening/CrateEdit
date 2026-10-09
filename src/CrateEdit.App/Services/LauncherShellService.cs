using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Serilog;

namespace CrateEdit.App.Services;

/// <summary><see cref="IShellService"/> backed by Avalonia's platform launcher.</summary>
public sealed class LauncherShellService(Func<TopLevel> topLevel) : IShellService
{
    public async Task OpenFolderAsync(string path)
    {
        Directory.CreateDirectory(path);
        if (!await topLevel().Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path)))
        {
            Log.Warning("Could not open folder {Path}", path);
        }
    }
}
