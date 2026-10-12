using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrateEdit.App.Commands;
using CrateEdit.App.Resources;
using CrateEdit.App.Services;
using CrateEdit.Core;

namespace CrateEdit.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly IShellService _shell;

    public MainWindowViewModel(AppPaths paths, IShellService shell)
    {
        _paths = paths;
        _shell = shell;
        CommandHandlers = new Dictionary<string, ICommand>
        {
            [AppCommands.OpenLogFolder] = OpenLogFolderCommand,
        };
    }

    public string Title => AppInfo.Name;

    /// <summary>Handlers for registry commands, keyed by command id; used for shortcut bindings.</summary>
    public IReadOnlyDictionary<string, ICommand> CommandHandlers { get; }

    [ObservableProperty]
    private string _statusText = Strings.Status_NoCollection;

    [RelayCommand]
    private Task OpenLogFolder() => _shell.OpenFolderAsync(_paths.LogDirectory);
}
