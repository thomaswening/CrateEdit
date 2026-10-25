using CrateEdit.App.Commands;
using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Commands;

public class AppCommandsTests
{
    public static TheoryData<string> CommandsWithShortcuts() =>
        new(AppCommands.All.Where(c => c.DefaultShortcut is not null).Select(c => c.Id));

    private static Shortcut DefaultShortcutOf(string commandId) =>
        AppCommands.All.Single(c => c.Id == commandId).DefaultShortcut!;

    [Fact]
    [Trait("Req", "REQ-109")]
    public void Default_shortcuts_are_unique()
    {
        // Act
        var registry = new CommandRegistry(AppCommands.All);

        // Assert
        Assert.Equal(AppCommands.All.Count, registry.Commands.Count);
    }

    [Theory]
    [MemberData(nameof(CommandsWithShortcuts))]
    public void Default_shortcuts_map_to_real_keys(string commandId)
    {
        // Arrange
        var shortcut = DefaultShortcutOf(commandId);

        // Act
        var gesture = ShortcutBinder.ToKeyGesture(shortcut);

        // Assert
        Assert.NotNull(gesture);
    }
}
