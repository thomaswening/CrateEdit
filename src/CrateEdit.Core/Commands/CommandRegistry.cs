namespace CrateEdit.Core.Commands;

/// <summary>
/// The single source of truth for which shortcut triggers which command. Guarantees that a
/// shortcut maps to at most one command (REQ-109), including the fixed shortcuts of
/// non-configurable commands.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandDefinition> _commands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Shortcut?> _shortcuts = new(StringComparer.Ordinal);

    public CommandRegistry(IEnumerable<CommandDefinition> commands)
    {
        foreach (var command in commands)
        {
            if (!_commands.TryAdd(command.Id, command))
            {
                throw new ArgumentException($"Command '{command.Id}' is defined more than once.", nameof(commands));
            }

            if (command.DefaultShortcut is { } shortcut && FindCommand(shortcut) is { } existing)
            {
                throw new ArgumentException(
                    $"Default shortcut {shortcut} of '{command.Id}' is already used by '{existing.Id}'.",
                    nameof(commands));
            }

            _shortcuts[command.Id] = command.DefaultShortcut;
        }
    }

    public IReadOnlyCollection<CommandDefinition> Commands => _commands.Values;

    public CommandDefinition Get(string commandId) =>
        _commands.TryGetValue(commandId, out var command)
            ? command
            : throw new KeyNotFoundException($"Unknown command '{commandId}'.");

    /// <summary>The shortcut currently assigned to the command, or null if it has none.</summary>
    public Shortcut? GetShortcut(string commandId)
    {
        Get(commandId);
        return _shortcuts[commandId];
    }

    /// <summary>The command currently assigned to the shortcut, or null if it is free.</summary>
    public CommandDefinition? FindCommand(Shortcut shortcut) =>
        _shortcuts.FirstOrDefault(entry => entry.Value == shortcut) is { Key: { } id } ? _commands[id] : null;

    /// <summary>
    /// Assigns a shortcut to a configurable command, or removes its shortcut when
    /// <paramref name="shortcut"/> is null. A shortcut already used by another command is
    /// rejected and the existing assignment stays unchanged (REQ-109).
    /// </summary>
    public ShortcutAssignmentResult TryAssign(string commandId, Shortcut? shortcut)
    {
        var command = Get(commandId);
        if (!command.IsConfigurable)
        {
            return ShortcutAssignmentResult.NotConfigurable;
        }

        if (shortcut is not null && FindCommand(shortcut) is { } owner && owner.Id != commandId)
        {
            return ShortcutAssignmentResult.Conflict(owner);
        }

        _shortcuts[commandId] = shortcut;
        return ShortcutAssignmentResult.Assigned;
    }
}

/// <summary>Outcome of <see cref="CommandRegistry.TryAssign"/>.</summary>
public sealed record ShortcutAssignmentResult(bool Succeeded, CommandDefinition? ConflictingCommand = null)
{
    public static ShortcutAssignmentResult Assigned { get; } = new(true);

    public static ShortcutAssignmentResult NotConfigurable { get; } = new(false);

    public static ShortcutAssignmentResult Conflict(CommandDefinition owner) => new(false, owner);
}
