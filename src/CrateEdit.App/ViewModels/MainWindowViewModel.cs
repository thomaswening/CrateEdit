using CommunityToolkit.Mvvm.ComponentModel;
using CrateEdit.Core;

namespace CrateEdit.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public string Title => AppInfo.Name;

    [ObservableProperty]
    private string _statusText = "No collection loaded.";
}
