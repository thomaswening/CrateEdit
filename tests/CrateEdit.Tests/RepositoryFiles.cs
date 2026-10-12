namespace CrateEdit.Tests;

/// <summary>Locates source files in the repository, for tests that check the code itself.</summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string SourceDirectory => Path.Combine(Root, "src");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CrateEdit.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (CrateEdit.slnx) not found.");
    }
}
