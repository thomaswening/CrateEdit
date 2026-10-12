using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Commands;

[Trait("Req", "REQ-109")]
public class CommandRegistryTests
{
    private static readonly Shortcut CtrlP = Shortcut.Parse("Primary+P");
    private static readonly Shortcut CtrlZ = Shortcut.Parse("Primary+Z");

    private static CommandRegistry CreateRegistry() => new(
    [
        new CommandDefinition("pending.toggle", "Toggle pending", CtrlP),
        new CommandDefinition("staging.open", "Open staging"),
        new CommandDefinition("edit.undo", "Undo", CtrlZ, IsConfigurable: false),
    ]);

    [Fact]
    public void Uses_default_shortcuts_initially()
    {
        var registry = CreateRegistry();

        Assert.Equal(CtrlP, registry.GetShortcut("pending.toggle"));
        Assert.Null(registry.GetShortcut("staging.open"));
        Assert.Equal("pending.toggle", registry.FindCommand(CtrlP)?.Id);
    }

    [Fact]
    public void Assigns_a_free_shortcut()
    {
        var registry = CreateRegistry();
        var shortcut = Shortcut.Parse("Primary+Shift+S");

        var result = registry.TryAssign("staging.open", shortcut);

        Assert.True(result.Succeeded);
        Assert.Equal(shortcut, registry.GetShortcut("staging.open"));
    }

    [Fact]
    public void Rejects_a_shortcut_used_by_another_command_and_keeps_the_old_assignment()
    {
        var registry = CreateRegistry();

        var result = registry.TryAssign("staging.open", CtrlP);

        Assert.False(result.Succeeded);
        Assert.Equal("pending.toggle", result.ConflictingCommand?.Id);
        Assert.Null(registry.GetShortcut("staging.open"));
        Assert.Equal("pending.toggle", registry.FindCommand(CtrlP)?.Id);
    }

    [Fact]
    public void Fixed_shortcuts_of_non_configurable_commands_are_reserved()
    {
        var registry = CreateRegistry();

        var result = registry.TryAssign("staging.open", CtrlZ);

        Assert.False(result.Succeeded);
        Assert.Equal("edit.undo", result.ConflictingCommand?.Id);
    }

    [Fact]
    public void Non_configurable_commands_cannot_be_reassigned()
    {
        var registry = CreateRegistry();

        var result = registry.TryAssign("edit.undo", Shortcut.Parse("Primary+U"));

        Assert.False(result.Succeeded);
        Assert.Equal(CtrlZ, registry.GetShortcut("edit.undo"));
    }

    [Fact]
    public void Reassigning_the_same_shortcut_to_its_owner_succeeds()
    {
        Assert.True(CreateRegistry().TryAssign("pending.toggle", CtrlP).Succeeded);
    }

    [Fact]
    public void Removing_a_shortcut_frees_it_for_other_commands()
    {
        var registry = CreateRegistry();

        registry.TryAssign("pending.toggle", null);

        Assert.True(registry.TryAssign("staging.open", CtrlP).Succeeded);
    }

    [Fact]
    public void Conflicting_default_shortcuts_are_rejected_at_startup()
    {
        Assert.Throws<ArgumentException>(() => new CommandRegistry(
        [
            new CommandDefinition("a", "A", CtrlP),
            new CommandDefinition("b", "B", Shortcut.Parse("primary+p")),
        ]));
    }

    [Fact]
    public void Duplicate_command_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new CommandRegistry(
        [
            new CommandDefinition("a", "A"),
            new CommandDefinition("a", "Again"),
        ]));
    }
}
