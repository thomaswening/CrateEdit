using System.Globalization;
using CrateEdit.Core.Text;

namespace CrateEdit.Tests.Text;

public class TextMatchingTests
{
    // "é" as one code point, and as "e" followed by a combining acute accent (as in macOS file names).
    private const string ComposedE = "Beyoncé";
    private const string DecomposedE = "Beyoncé";

    [Theory]
    [Trait("Req", "REQ-126")]
    [InlineData("Beyoncé", "beyonce")]
    [InlineData("Beyoncé", "BEYONCÉ")]
    [InlineData("Röyksopp", "royksopp")]
    [InlineData("Sigur Rós - Hoppípolla", "hoppipolla")]
    [InlineData("Daft Punk", "")]
    public void Search_ignores_case_and_accents(string text, string query)
    {
        Assert.True(TextMatching.Contains(text, query));
    }

    [Fact]
    [Trait("Req", "REQ-126")]
    public void Search_does_not_match_different_letters()
    {
        Assert.False(TextMatching.Contains("Beyoncé", "beyonca"));
    }

    [Fact]
    [Trait("Req", "REQ-128")]
    public void Search_matches_decomposed_text()
    {
        Assert.True(TextMatching.Contains(DecomposedE, "beyonce"));
        Assert.True(TextMatching.Contains(DecomposedE, ComposedE));
    }

    [Theory]
    [Trait("Req", "REQ-127")]
    [InlineData("de-DE")]
    [InlineData("en-GB")]
    public void Sorting_places_umlauts_with_their_base_letter(string cultureName)
    {
        string[] names = ["Zedd", "Ärzte", "Arzt", "Bonobo"];

        var sorted = names.Order(TextMatching.SortComparer(CultureInfo.GetCultureInfo(cultureName))).ToArray();

        Assert.Equal(["Arzt", "Ärzte", "Bonobo", "Zedd"], sorted);
    }

    [Fact]
    [Trait("Req", "REQ-127")]
    public void Sorting_ignores_case()
    {
        string[] names = ["bonobo", "Arzt", "Burial"];

        var sorted = names.Order(TextMatching.SortComparer(CultureInfo.GetCultureInfo("en-GB"))).ToArray();

        Assert.Equal(["Arzt", "bonobo", "Burial"], sorted);
    }

    [Fact]
    [Trait("Req", "REQ-128")]
    public void Composed_and_decomposed_forms_are_equivalent()
    {
        Assert.NotEqual(ComposedE, DecomposedE);
        Assert.True(TextMatching.AreEquivalent(ComposedE, DecomposedE));
    }

    [Fact]
    [Trait("Req", "REQ-128")]
    public void Equivalence_still_respects_case_and_accents()
    {
        Assert.False(TextMatching.AreEquivalent("Beyoncé", "beyoncé"));
        Assert.False(TextMatching.AreEquivalent("Beyoncé", "Beyonce"));
    }
}
