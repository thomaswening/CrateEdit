using CrateEdit.App.Commands;
using CrateEdit.App.Services;
using CrateEdit.App.ViewModels;
using CrateEdit.App.Views;
using CrateEdit.Core;
using CrateEdit.Core.Commands;

namespace CrateEdit.App;

/// <summary>Composes the main window with its view model, services and shortcut bindings.</summary>
public static class MainWindowFactory
{
    public static MainWindow Create(AppPaths paths, CommandRegistry registry, IShellService? shell = null)
    {
        MainWindow? window = null;
        var viewModel = new MainWindowViewModel(paths, shell ?? new LauncherShellService(() => window!));
        window = new MainWindow { DataContext = viewModel };
        ShortcutBinder.Bind(window, registry, viewModel.CommandHandlers);
        ShortcutBinder.ShowGestures(window, registry);
        return window;
    }
}
