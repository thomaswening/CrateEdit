using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CrateEdit.Tests.Localization;

/// <summary>
/// Guards <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-123">REQ-123</see>: every user-facing string comes from resources, and every supported
/// language translates every string.
/// </summary>
[Trait("Req", "REQ-123")]
public partial class TranslationTests
{
    private static readonly string[] SupportedTranslations = ["de"];

    // Attributes that display text to the user.
    private static readonly string[] TextAttributes =
        ["Header", "Text", "Content", "Title", "ToolTip.Tip", "Watermark", "PlaceholderText", "Label"];

    public static TheoryData<string> Translations() => new(SupportedTranslations);

    private static string NeutralResourceFile =>
        Path.Combine(RepositoryFiles.SourceDirectory, "CrateEdit.App", "Resources", "Strings.resx");

    private static string ResourceFile(string language) => Path.ChangeExtension(NeutralResourceFile, $".{language}.resx");

    private static IEnumerable<string> AllResourceFiles => [NeutralResourceFile, .. SupportedTranslations.Select(ResourceFile)];

    private static Dictionary<string, string> ReadEntries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")?.Value ?? "");

    /// <summary>The keys whose value has an access key marker ('_') in one file but not in the other.</summary>
    private static IEnumerable<string> KeysWithDifferentAccessKeys(Dictionary<string, string> neutral, Dictionary<string, string> translated) =>
        neutral.Keys.Where(key => neutral[key].Contains('_') != translated[key].Contains('_'));

    /// <summary>"file: key" for every resource text, in any language, that contains the text.</summary>
    private static IEnumerable<string> EntriesContaining(string text) =>
        from file in AllResourceFiles
        from entry in ReadEntries(file)
        where entry.Value.Contains(text, StringComparison.OrdinalIgnoreCase)
        select $"{Path.GetFileName(file)}: {entry.Key}";

    /// <summary>"file: element.attribute" for every text attribute in the XAML that is not a markup extension.</summary>
    private static IEnumerable<string> HardCodedXamlTexts() =>
        from file in Directory.EnumerateFiles(RepositoryFiles.SourceDirectory, "*.axaml", SearchOption.AllDirectories)
        from element in XDocument.Load(file).Descendants()
        from attribute in element.Attributes()
        where TextAttributes.Contains(attribute.Name.LocalName)
              && attribute.Value.Length > 0
              && !MarkupExtension().IsMatch(attribute.Value)
        select $"{Path.GetFileName(file)}: {element.Name.LocalName}.{attribute.Name.LocalName}=\"{attribute.Value}\"";

    [Theory]
    [MemberData(nameof(Translations))]
    public void Every_string_is_translated(string language)
    {
        // Arrange
        var neutral = ReadEntries(NeutralResourceFile);

        // Act
        var translated = ReadEntries(ResourceFile(language));

        // Assert
        Assert.Equal(neutral.Keys.Order(), translated.Keys.Order());
        Assert.DoesNotContain(translated, entry => string.IsNullOrWhiteSpace(entry.Value));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void Access_keys_are_kept_in_translations(string language)
    {
        // Arrange
        var neutral = ReadEntries(NeutralResourceFile);
        var translated = ReadEntries(ResourceFile(language));

        // Act
        var mismatches = KeysWithDifferentAccessKeys(neutral, translated);

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public void Texts_do_not_contain_the_application_name()
    {
        // The name may change and users know which application is talking. The window title
        // comes from AppInfo.Name instead.

        // Act
        var offending = EntriesContaining("CrateEdit");

        // Assert
        Assert.Empty(offending);
    }

    [Fact]
    public void Xaml_contains_no_hard_coded_text()
    {
        // Act
        var violations = HardCodedXamlTexts();

        // Assert
        Assert.Empty(violations);
    }

    [GeneratedRegex(@"^\{.*\}$")]
    private static partial Regex MarkupExtension();
}
