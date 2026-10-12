using CrateEdit.Core.Commands;

namespace CrateEdit.App.Commands;

/// <summary>All commands of the application and their default shortcuts.</summary>
public static class AppCommands
{
    public const string OpenLogFolder = "diagnostics.openLogFolder";

    public static IReadOnlyList<CommandDefinition> All { get; } =
    [
        new(OpenLogFolder, "Open log folder", Shortcut.Parse("Primary+Shift+L")),
    ];
}
