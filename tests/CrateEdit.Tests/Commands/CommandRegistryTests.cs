using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Commands;

[Trait("Req", "REQ-109")]
public class CommandRegistryTests
{
    private const string TogglePending = "pending.toggle";
    private const string OpenStaging = "staging.open";
    private const string Undo = "edit.undo";

    private static readonly Shortcut CtrlP = Shortcut.Parse("Primary+P");
    private static readonly Shortcut CtrlZ = Shortcut.Parse("Primary+Z");

    /// <summary>Toggle pending has Ctrl+P, open staging has no shortcut, undo has a fixed Ctrl+Z.</summary>
    private static CommandRegistry CreateRegistry() => new(
    [
        new CommandDefinition(TogglePending, "Toggle pending", CtrlP),
        new CommandDefinition(OpenStaging, "Open staging"),
        new CommandDefinition(Undo, "Undo", CtrlZ, IsConfigurable: false),
    ]);

    [Fact]
    public void Uses_default_shortcuts_initially()
    {
        // Act
        var registry = CreateRegistry();

        // Assert
        Assert.Equal(CtrlP, registry.GetShortcut(TogglePending));
        Assert.Null(registry.GetShortcut(OpenStaging));
        Assert.Equal(TogglePending, registry.FindCommand(CtrlP)?.Id);
    }

    [Fact]
    public void Assigns_a_free_shortcut()
    {
        // Arrange
        var registry = CreateRegistry();
        var shortcut = Shortcut.Parse("Primary+Shift+S");

        // Act
        var result = registry.TryAssign(OpenStaging, shortcut);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(shortcut, registry.GetShortcut(OpenStaging));
    }

    [Fact]
    public void Rejects_a_shortcut_used_by_another_command_and_keeps_the_old_assignment()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.TryAssign(OpenStaging, CtrlP);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(TogglePending, result.ConflictingCommand?.Id);
        Assert.Null(registry.GetShortcut(OpenStaging));
        Assert.Equal(TogglePending, registry.FindCommand(CtrlP)?.Id);
    }

    [Fact]
    public void Fixed_shortcuts_of_non_configurable_commands_are_reserved()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.TryAssign(OpenStaging, CtrlZ);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(Undo, result.ConflictingCommand?.Id);
    }

    [Fact]
    public void Non_configurable_commands_cannot_be_reassigned()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.TryAssign(Undo, Shortcut.Parse("Primary+U"));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(CtrlZ, registry.GetShortcut(Undo));
    }

    [Fact]
    public void Reassigning_the_same_shortcut_to_its_owner_succeeds()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.TryAssign(TogglePending, CtrlP);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Removing_a_shortcut_frees_it_for_other_commands()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.TryAssign(TogglePending, null);

        // Act
        var result = registry.TryAssign(OpenStaging, CtrlP);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Conflicting_default_shortcuts_are_rejected_at_startup()
    {
        // Arrange
        CommandDefinition[] commands =
        [
            new("a", "A", CtrlP),
            new("b", "B", Shortcut.Parse("primary+p")),
        ];

        // Act and assert
        Assert.Throws<ArgumentException>(() => new CommandRegistry(commands));
    }

    [Fact]
    public void Duplicate_command_ids_are_rejected()
    {
        // Arrange
        CommandDefinition[] commands =
        [
            new("a", "A"),
            new("a", "Again"),
        ];

        // Act and assert
        Assert.Throws<ArgumentException>(() => new CommandRegistry(commands));
    }
}
