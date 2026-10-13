namespace CrateEdit.Tests;

/// <summary>A new, empty folder that is deleted with everything in it, hidden files included, on dispose.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    public TemporaryFolder(string? parent = null)
    {
        Path = System.IO.Path.Combine(parent ?? System.IO.Path.GetTempPath(), "crateedit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        // Hidden files, such as a safe write's temporary file, are included.
        foreach (var file in Directory.GetFiles(Path, "*", new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = true }))
        {
            File.Delete(file);
        }

        Directory.Delete(Path, recursive: true);
    }
}
