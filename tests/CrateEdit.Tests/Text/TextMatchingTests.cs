using System.Globalization;
using CrateEdit.Core.Text;

namespace CrateEdit.Tests.Text;

public class TextMatchingTests
{
    // "é" as one code point, and as "e" followed by a combining acute accent (as in macOS file names).
    private const string ComposedE = "Beyoncé";
    private const string DecomposedE = "Beyoncé";

    private static string[] SortIn(string cultureName, string[] names) =>
        names.Order(TextMatching.SortComparer(CultureInfo.GetCultureInfo(cultureName))).ToArray();

    [Theory]
    [Trait("Req", "REQ-126")]
    [InlineData("Beyoncé", "beyonce")]
    [InlineData("Beyoncé", "BEYONCÉ")]
    [InlineData("Röyksopp", "royksopp")]
    [InlineData("Sigur Rós - Hoppípolla", "hoppipolla")]
    [InlineData("Daft Punk", "")]
    public void Search_ignores_case_and_accents(string text, string query)
    {
        // Act
        var matches = TextMatching.Contains(text, query);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    [Trait("Req", "REQ-126")]
    public void Search_does_not_match_different_letters()
    {
        // Act
        var matches = TextMatching.Contains("Beyoncé", "beyonca");

        // Assert
        Assert.False(matches);
    }

    [Theory]
    [Trait("Req", "REQ-128")]
    [InlineData("beyonce")]
    [InlineData(ComposedE)]
    public void Search_matches_decomposed_text(string query)
    {
        // Act
        var matches = TextMatching.Contains(DecomposedE, query);

        // Assert
        Assert.True(matches);
    }

    [Theory]
    [Trait("Req", "REQ-127")]
    [InlineData("de-DE")]
    [InlineData("en-GB")]
    public void Sorting_places_umlauts_with_their_base_letter(string cultureName)
    {
        // Arrange
        string[] names = ["Zedd", "Ärzte", "Arzt", "Bonobo"];

        // Act
        var sorted = SortIn(cultureName, names);

        // Assert
        Assert.Equal(["Arzt", "Ärzte", "Bonobo", "Zedd"], sorted);
    }

    [Fact]
    [Trait("Req", "REQ-127")]
    public void Sorting_ignores_case()
    {
        // Arrange
        string[] names = ["bonobo", "Arzt", "Burial"];

        // Act
        var sorted = SortIn("en-GB", names);

        // Assert
        Assert.Equal(["Arzt", "bonobo", "Burial"], sorted);
    }

    [Fact]
    [Trait("Req", "REQ-128")]
    public void Composed_and_decomposed_forms_are_equivalent()
    {
        // Arrange
        Assert.NotEqual(ComposedE, DecomposedE);

        // Act
        var equivalent = TextMatching.AreEquivalent(ComposedE, DecomposedE);

        // Assert
        Assert.True(equivalent);
    }

    [Theory]
    [Trait("Req", "REQ-128")]
    [InlineData("beyoncé")]
    [InlineData("Beyonce")]
    public void Equivalence_still_respects_case_and_accents(string other)
    {
        // Act
        var equivalent = TextMatching.AreEquivalent("Beyoncé", other);

        // Assert
        Assert.False(equivalent);
    }
}
