using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CrateEdit.App.ViewModels;
using CrateEdit.App.Views;
using CrateEdit.Core;

namespace CrateEdit.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public void Main_window_shows_application_name_and_status()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel() };
        window.Show();

        Assert.Equal(AppInfo.Name, window.Title);
        Assert.Equal("No collection loaded.", window.FindControl<TextBlock>("StatusBar")!.Text);
    }
}
