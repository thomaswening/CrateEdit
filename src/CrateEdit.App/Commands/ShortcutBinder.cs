using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using CrateEdit.Core.Commands;

namespace CrateEdit.App.Commands;

/// <summary>Translates registry shortcuts into Avalonia key bindings.</summary>
public static class ShortcutBinder
{
    /// <summary>Replaces the element's key bindings with one per command that has a shortcut and a handler.</summary>
    public static void Bind(InputElement element, CommandRegistry registry, IReadOnlyDictionary<string, ICommand> handlers)
    {
        element.KeyBindings.Clear();
        foreach (var (commandId, handler) in handlers)
        {
            if (registry.GetShortcut(commandId) is { } shortcut)
            {
                element.KeyBindings.Add(new KeyBinding { Gesture = ToKeyGesture(shortcut), Command = handler });
            }
        }
    }

    /// <summary>
    /// Shows each command's current shortcut on menu items whose <see cref="Control.Tag"/> is
    /// the command id, so menus always display the configured shortcut.
    /// </summary>
    public static void ShowGestures(Control root, CommandRegistry registry)
    {
        foreach (var item in root.GetLogicalDescendants().OfType<MenuItem>())
        {
            if (item.Tag is string commandId)
            {
                item.InputGesture = registry.GetShortcut(commandId) is { } shortcut ? ToKeyGesture(shortcut) : null;
            }
        }
    }

    public static KeyGesture ToKeyGesture(Shortcut shortcut)
    {
        if (!Enum.TryParse<Key>(shortcut.Key, ignoreCase: true, out var key))
        {
            throw new FormatException($"Unknown key '{shortcut.Key}' in shortcut {shortcut}.");
        }

        var modifiers = KeyModifiers.None;
        if (shortcut.Modifiers.HasFlag(ShortcutModifiers.Primary))
        {
            modifiers |= OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        }

        if (shortcut.Modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (shortcut.Modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            modifiers |= KeyModifiers.Alt;
        }

        return new KeyGesture(key, modifiers);
    }
}
