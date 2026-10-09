using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Commands;

public class ShortcutTests
{
    [Fact]
    public void Parses_modifiers_and_key()
    {
        var shortcut = Shortcut.Parse("Primary+Shift+L");

        Assert.Equal("L", shortcut.Key);
        Assert.Equal(ShortcutModifiers.Primary | ShortcutModifiers.Shift, shortcut.Modifiers);
    }

    [Fact]
    public void Parses_a_key_without_modifiers()
    {
        Assert.Equal(new Shortcut("F2"), Shortcut.Parse("F2"));
    }

    [Fact]
    public void Round_trips_through_text_in_canonical_order()
    {
        Assert.Equal("Primary+Shift+Alt+P", Shortcut.Parse("alt+shift+primary+P").ToString());
    }

    [Fact]
    public void Keys_are_compared_case_insensitively()
    {
        Assert.Equal(Shortcut.Parse("Primary+l"), Shortcut.Parse("Primary+L"));
    }

    [Theory]
    [InlineData("Hyper+L")]
    [InlineData("None+L")]
    [InlineData("Primary+")]
    public void Rejects_invalid_text(string text)
    {
        Assert.ThrowsAny<Exception>(() => Shortcut.Parse(text));
    }
}
