namespace CrateEdit.Core.Commands;

/// <summary>Modifier keys of a <see cref="Shortcut"/>.</summary>
[Flags]
public enum ShortcutModifiers
{
    None = 0,

    /// <summary>The platform's primary modifier: Ctrl on Windows, Cmd on macOS.</summary>
    Primary = 1,

    Shift = 2,

    Alt = 4,
}

/// <summary>
/// A platform-independent keyboard shortcut such as <c>Primary+Shift+L</c>. The key is named
/// like the UI framework's key names (e.g. "L", "F2", "Delete", "Space").
/// </summary>
public sealed record Shortcut
{
    public Shortcut(string key, ShortcutModifiers modifiers = ShortcutModifiers.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
        Modifiers = modifiers;
    }

    public string Key { get; }

    public ShortcutModifiers Modifiers { get; }

    /// <summary>Parses text such as "Primary+Shift+L" (modifier names are case-insensitive).</summary>
    public static Shortcut Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = ShortcutModifiers.None;
        foreach (var part in parts[..^1])
        {
            modifiers |= Enum.TryParse<ShortcutModifiers>(part, ignoreCase: true, out var modifier)
                         && modifier != ShortcutModifiers.None
                ? modifier
                : throw new FormatException($"Unknown modifier '{part}' in shortcut '{text}'.");
        }

        return new Shortcut(parts[^1], modifiers);
    }

    public bool Equals(Shortcut? other) =>
        other is not null
        && Modifiers == other.Modifiers
        && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        HashCode.Combine(Modifiers, StringComparer.OrdinalIgnoreCase.GetHashCode(Key));

    public override string ToString()
    {
        var parts = new List<string>(4);
        foreach (var modifier in new[] { ShortcutModifiers.Primary, ShortcutModifiers.Shift, ShortcutModifiers.Alt })
        {
            if (Modifiers.HasFlag(modifier))
            {
                parts.Add(modifier.ToString());
            }
        }

        parts.Add(Key);
        return string.Join('+', parts);
    }
}
