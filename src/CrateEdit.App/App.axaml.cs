using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrateEdit.App.Commands;
using CrateEdit.Core;
using CrateEdit.Core.Commands;

namespace CrateEdit.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = MainWindowFactory.Create(AppPaths.Default, new CommandRegistry(AppCommands.All));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
