using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CrateEdit.Tests.Localization;

/// <summary>
/// Guards REQ-123: every user-facing string comes from resources, and every supported
/// language translates every string.
/// </summary>
[Trait("Req", "REQ-123")]
public partial class TranslationTests
{
    private static readonly string[] SupportedTranslations = ["de"];

    // Attributes that display text to the user.
    private static readonly string[] TextAttributes =
        ["Header", "Text", "Content", "Title", "ToolTip.Tip", "Watermark", "PlaceholderText", "Label"];

    private static string NeutralResourceFile =>
        Path.Combine(RepositoryFiles.SourceDirectory, "CrateEdit.App", "Resources", "Strings.resx");

    private static Dictionary<string, string> ReadEntries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")?.Value ?? "");

    public static TheoryData<string> Translations() => new(SupportedTranslations);

    [Theory]
    [MemberData(nameof(Translations))]
    public void Every_string_is_translated(string language)
    {
        var neutral = ReadEntries(NeutralResourceFile);
        var translated = ReadEntries(Path.ChangeExtension(NeutralResourceFile, $".{language}.resx"));

        Assert.Empty(neutral.Keys.Except(translated.Keys));
        Assert.Empty(translated.Keys.Except(neutral.Keys));
        Assert.DoesNotContain(translated, entry => string.IsNullOrWhiteSpace(entry.Value));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void Access_keys_are_kept_in_translations(string language)
    {
        var neutral = ReadEntries(NeutralResourceFile);
        var translated = ReadEntries(Path.ChangeExtension(NeutralResourceFile, $".{language}.resx"));

        foreach (var (key, value) in neutral)
        {
            Assert.True(value.Contains('_') == translated[key].Contains('_'),
                $"'{key}': access key marker '_' must be present in both English and '{language}'.");
        }
    }

    [Fact]
    public void Xaml_contains_no_hard_coded_text()
    {
        var violations =
            from file in Directory.EnumerateFiles(RepositoryFiles.SourceDirectory, "*.axaml", SearchOption.AllDirectories)
            from element in XDocument.Load(file).Descendants()
            from attribute in element.Attributes()
            where TextAttributes.Contains(attribute.Name.LocalName)
                  && attribute.Value.Length > 0
                  && !MarkupExtension().IsMatch(attribute.Value)
            select $"{Path.GetFileName(file)}: {element.Name.LocalName}.{attribute.Name.LocalName}=\"{attribute.Value}\"";

        Assert.Empty(violations);
    }

    [GeneratedRegex(@"^\{.*\}$")]
    private static partial Regex MarkupExtension();
}
