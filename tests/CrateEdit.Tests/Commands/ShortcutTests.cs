using CrateEdit.Core.Commands;

namespace CrateEdit.Tests.Commands;

public class ShortcutTests
{
    [Fact]
    public void Parses_modifiers_and_key()
    {
        // Act
        var shortcut = Shortcut.Parse("Primary+Shift+L");

        // Assert
        Assert.Equal("L", shortcut.Key);
        Assert.Equal(ShortcutModifiers.Primary | ShortcutModifiers.Shift, shortcut.Modifiers);
    }

    [Fact]
    public void Parses_a_key_without_modifiers()
    {
        // Act
        var shortcut = Shortcut.Parse("F2");

        // Assert
        Assert.Equal(new Shortcut("F2"), shortcut);
    }

    [Fact]
    public void Round_trips_through_text_in_canonical_order()
    {
        // Arrange
        var shortcut = Shortcut.Parse("alt+shift+primary+P");

        // Act
        var text = shortcut.ToString();

        // Assert
        Assert.Equal("Primary+Shift+Alt+P", text);
    }

    [Fact]
    public void Keys_are_compared_case_insensitively()
    {
        // Act
        var lowerCase = Shortcut.Parse("Primary+l");
        var upperCase = Shortcut.Parse("Primary+L");

        // Assert
        Assert.Equal(lowerCase, upperCase);
    }

    [Theory]
    [InlineData("Hyper+L")]
    [InlineData("None+L")]
    [InlineData("Primary+")]
    public void Rejects_invalid_text(string text)
    {
        // Act and assert
        Assert.ThrowsAny<Exception>(() => Shortcut.Parse(text));
    }
}
