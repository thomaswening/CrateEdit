using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using CrateEdit.App;
using CrateEdit.App.Commands;
using CrateEdit.App.Services;
using CrateEdit.Core;
using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Localization;

/// <summary>The UI follows the display language, with English as fallback (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-123">REQ-123</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-124">REQ-124</see>).</summary>
public class LanguageSelectionTests
{
    private sealed class NoShell : IShellService
    {
        public Task OpenFolderAsync(string path) => Task.CompletedTask;
    }

    private static Window CreateWindow() =>
        MainWindowFactory.Create(new AppPaths(Path.GetTempPath()), new CommandRegistry(AppCommands.All), new NoShell());

    private static string FirstMenuHeader(Window window) =>
        (string)window.GetLogicalDescendants().OfType<MenuItem>().First().Header!;

    private static string StatusText(Window window) => window.FindControl<TextBlock>("StatusBar")!.Text!;

    [AvaloniaFact]
    [Trait("Req", "REQ-123")]
    [Trait("Req", "REQ-124")]
    public void German_display_language_shows_german_text()
    {
        // Arrange
        using var german = CultureScope.Ui("de-DE");

        // Act
        var window = CreateWindow();

        // Assert
        Assert.Equal("_Hilfe", FirstMenuHeader(window));
        Assert.Equal("Keine Sammlung geladen.", StatusText(window));
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-124")]
    public void English_display_language_shows_english_text()
    {
        // Arrange
        using var english = CultureScope.Ui("en-US");

        // Act
        var window = CreateWindow();

        // Assert
        Assert.Equal("_Help", FirstMenuHeader(window));
        Assert.Equal("No collection loaded.", StatusText(window));
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-124")]
    public void Unsupported_display_language_falls_back_to_english()
    {
        // Arrange
        using var french = CultureScope.Ui("fr-FR");

        // Act
        var window = CreateWindow();

        // Assert
        Assert.Equal("_Help", FirstMenuHeader(window));
        Assert.Equal("No collection loaded.", StatusText(window));
    }
}
