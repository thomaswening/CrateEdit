namespace CrateEdit.Core.Diagnostics;

/// <summary>
/// Removes personal information from diagnostic text by replacing the user's home directory
/// with "~" (REQ-118), e.g. C:\Users\name\Music\track.mp3 becomes ~\Music\track.mp3.
/// </summary>
public sealed class PathRedactor
{
    private readonly string _homeDirectory;
    private readonly StringComparison _comparison;

    public PathRedactor(string homeDirectory, bool caseSensitive)
    {
        _homeDirectory = homeDirectory.TrimEnd('/', '\\');
        _comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }

    public static PathRedactor ForCurrentUser { get; } = new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        caseSensitive: !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS());

    public string Redact(string text)
    {
        if (_homeDirectory.Length == 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text.Replace(_homeDirectory, "~", _comparison);
    }
}
