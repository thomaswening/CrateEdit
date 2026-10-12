namespace CrateEdit.App.Services;

/// <summary>Hands files and folders to the operating system.</summary>
public interface IShellService
{
    /// <summary>Opens the folder in the OS file browser (Explorer, Finder).</summary>
    Task OpenFolderAsync(string path);
}
