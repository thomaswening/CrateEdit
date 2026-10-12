using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using CrateEdit.App;
using CrateEdit.App.Commands;
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

    [AvaloniaFact]
    public void Main_window_shows_application_name_and_status()
    {
        var window = MainWindowFactory.Create(Paths, new CommandRegistry(AppCommands.All), new FakeShell());
        window.Show();

        Assert.Equal(AppInfo.Name, window.Title);
        Assert.Equal("No collection loaded.", window.FindControl<TextBlock>("StatusBar")!.Text);
    }

    [AvaloniaFact]
    public void Default_shortcut_opens_the_log_folder()
    {
        var shell = new FakeShell();
        var window = MainWindowFactory.Create(Paths, new CommandRegistry(AppCommands.All), shell);
        window.Show();

        window.KeyPressQwerty(PhysicalKey.L, PrimaryModifier | RawInputModifiers.Shift);

        Assert.Equal([Paths.LogDirectory], shell.OpenedFolders);
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-108")]
    public void Reassigned_shortcut_replaces_the_default()
    {
        var shell = new FakeShell();
        var registry = new CommandRegistry(AppCommands.All);
        registry.TryAssign(AppCommands.OpenLogFolder, Shortcut.Parse("Primary+Alt+D"));
        var window = MainWindowFactory.Create(Paths, registry, shell);
        window.Show();

        window.KeyPressQwerty(PhysicalKey.L, PrimaryModifier | RawInputModifiers.Shift);
        Assert.Empty(shell.OpenedFolders);

        window.KeyPressQwerty(PhysicalKey.D, PrimaryModifier | RawInputModifiers.Alt);
        Assert.Single(shell.OpenedFolders);
    }

    [AvaloniaFact]
    public void Menu_shows_the_configured_shortcut()
    {
        var registry = new CommandRegistry(AppCommands.All);
        var window = MainWindowFactory.Create(Paths, registry, new FakeShell());

        var item = window.GetLogicalDescendants().OfType<MenuItem>()
            .Single(i => Equals(i.Tag, AppCommands.OpenLogFolder));

        Assert.Equal(ShortcutBinder.ToKeyGesture(registry.GetShortcut(AppCommands.OpenLogFolder)!), item.InputGesture);
    }
}
