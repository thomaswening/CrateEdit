using CrateEdit.App.Commands;
using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Commands;

public class AppCommandsTests
{
    [Fact]
    [Trait("Req", "REQ-109")]
    public void Default_shortcuts_are_unique()
    {
        var registry = new CommandRegistry(AppCommands.All);

        Assert.Equal(AppCommands.All.Count, registry.Commands.Count);
    }

    public static TheoryData<string> CommandsWithShortcuts() =>
        new(AppCommands.All.Where(c => c.DefaultShortcut is not null).Select(c => c.Id));

    [Theory]
    [MemberData(nameof(CommandsWithShortcuts))]
    public void Default_shortcuts_map_to_real_keys(string commandId)
    {
        var shortcut = AppCommands.All.Single(c => c.Id == commandId).DefaultShortcut!;

        ShortcutBinder.ToKeyGesture(shortcut);
    }
}
