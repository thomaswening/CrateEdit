using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using CrateEdit.App;
using CrateEdit.App.Commands;
using CrateEdit.App.Resources;
using CrateEdit.App.Services;
using CrateEdit.Core;
using CrateEdit.Core.Commands;

namespace CrateEdit.Tests;

public class MainWindowTests
{
    private sealed class FakeShell : IShellService
    {
        public List<string> OpenedFolders { get; } = [];

        public Task OpenFolderAsync(string path)
        {
            OpenedFolders.Add(path);
            return Task.CompletedTask;
        }
    }

    private static readonly AppPaths Paths = new(Path.Combine(Path.GetTempPath(), "crateedit-ui-tests"));

    private static RawInputModifiers PrimaryModifier =>
        OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static Window ShowWindow(FakeShell shell, CommandRegistry? registry = null)
    {
        var window = MainWindowFactory.Create(Paths, registry ?? new CommandRegistry(AppCommands.All), shell);
        window.Show();
        return window;
    }

    private static MenuItem MenuItemFor(Window window, string commandId) =>
        window.GetLogicalDescendants().OfType<MenuItem>().Single(item => Equals(item.Tag, commandId));

    private static void PressDefaultOpenLogFolderShortcut(Window window) =>
        window.KeyPressQwerty(PhysicalKey.L, PrimaryModifier | RawInputModifiers.Shift);

    [AvaloniaFact]
    public void Main_window_shows_application_name_and_status()
    {
        // Act
        var window = ShowWindow(new FakeShell());

        // Assert
        Assert.Equal(AppInfo.Name, window.Title);
        Assert.Equal(Strings.Status_NoCollection, window.FindControl<TextBlock>("StatusBar")!.Text);
    }

    [AvaloniaFact]
    public void Default_shortcut_opens_the_log_folder()
    {
        // Arrange
        var shell = new FakeShell();
        var window = ShowWindow(shell);

        // Act
        PressDefaultOpenLogFolderShortcut(window);

        // Assert
        Assert.Equal([Paths.LogDirectory], shell.OpenedFolders);
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-108")]
    public void Reassigned_shortcut_runs_the_command()
    {
        // Arrange
        var shell = new FakeShell();
        var registry = new CommandRegistry(AppCommands.All);
        registry.TryAssign(AppCommands.OpenLogFolder, Shortcut.Parse("Primary+Alt+D"));
        var window = ShowWindow(shell, registry);

        // Act
        window.KeyPressQwerty(PhysicalKey.D, PrimaryModifier | RawInputModifiers.Alt);

        // Assert
        Assert.Single(shell.OpenedFolders);
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-108")]
    public void Reassigned_shortcut_replaces_the_default()
    {
        // Arrange
        var shell = new FakeShell();
        var registry = new CommandRegistry(AppCommands.All);
        registry.TryAssign(AppCommands.OpenLogFolder, Shortcut.Parse("Primary+Alt+D"));
        var window = ShowWindow(shell, registry);

        // Act
        PressDefaultOpenLogFolderShortcut(window);

        // Assert
        Assert.Empty(shell.OpenedFolders);
    }

    [AvaloniaFact]
    public void Menu_shows_the_configured_shortcut()
    {
        // Arrange
        var registry = new CommandRegistry(AppCommands.All);
        var expected = ShortcutBinder.ToKeyGesture(registry.GetShortcut(AppCommands.OpenLogFolder)!);

        // Act
        var window = MainWindowFactory.Create(Paths, registry, new FakeShell());

        // Assert
        Assert.Equal(expected, MenuItemFor(window, AppCommands.OpenLogFolder).InputGesture);
    }
}
