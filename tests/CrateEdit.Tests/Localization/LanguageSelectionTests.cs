using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using CrateEdit.App;
using CrateEdit.App.Commands;
using CrateEdit.App.Services;
using CrateEdit.Core;
using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Localization;

/// <summary>The UI follows the display language, with English as fallback (REQ-123, REQ-124).</summary>
public class LanguageSelectionTests
{
    private sealed class NoShell : IShellService
    {
        public Task OpenFolderAsync(string path) => Task.CompletedTask;
    }

    private static (string MenuHeader, string Status) RenderIn(string cultureName)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var window = MainWindowFactory.Create(
                new AppPaths(Path.GetTempPath()), new CommandRegistry(AppCommands.All), new NoShell());
            var menu = window.GetLogicalDescendants().OfType<MenuItem>().First();
            return ((string)menu.Header!, window.FindControl<TextBlock>("StatusBar")!.Text!);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-123")]
    [Trait("Req", "REQ-124")]
    public void German_display_language_shows_german_text()
    {
        Assert.Equal(("_Hilfe", "Keine Sammlung geladen."), RenderIn("de-DE"));
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-124")]
    public void English_display_language_shows_english_text()
    {
        Assert.Equal(("_Help", "No collection loaded."), RenderIn("en-US"));
    }

    [AvaloniaFact]
    [Trait("Req", "REQ-124")]
    public void Unsupported_display_language_falls_back_to_english()
    {
        Assert.Equal(("_Help", "No collection loaded."), RenderIn("fr-FR"));
    }
}
