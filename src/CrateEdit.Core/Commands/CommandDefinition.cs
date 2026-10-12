namespace CrateEdit.Core.Commands;

/// <summary>
/// A user action that can be triggered from menus, buttons and keyboard shortcuts.
/// Every action is defined once, so all of its entry points share one shortcut.
/// </summary>
/// <param name="Id">Stable identifier, e.g. "collection.markPending". Used to persist shortcut overrides.</param>
/// <param name="DisplayName">Name shown to the user, e.g. in menus and the shortcut settings.</param>
/// <param name="DefaultShortcut">Shortcut used unless the user configures another one.</param>
/// <param name="IsConfigurable">
/// False for actions with fixed platform-standard shortcuts (e.g. undo). Their shortcuts are
/// reserved and cannot be assigned to other actions.
/// </param>
public sealed record CommandDefinition(
    string Id,
    string DisplayName,
    Shortcut? DefaultShortcut = null,
    bool IsConfigurable = true);
