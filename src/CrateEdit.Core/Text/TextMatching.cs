using System.Globalization;
using System.Text;

namespace CrateEdit.Core.Text;

/// <summary>
/// The rules for comparing user-visible text. All search, sorting and equality checks on
/// metadata and paths go through here, so they behave the same everywhere in the application.
/// </summary>
public static class TextMatching
{
    private const CompareOptions SearchOptions =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace |
        CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth;

    /// <summary>
    /// True if <paramref name="text"/> contains <paramref name="query"/>, ignoring letter case and
    /// diacritics (REQ-126). Independent of the UI language, so search results do not change
    /// when the language changes. Canonically equivalent Unicode forms match (REQ-128).
    /// </summary>
    public static bool Contains(string text, string query) =>
        query.Length == 0 || CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, query, SearchOptions) >= 0;

    /// <summary>
    /// Comparer for sorting text columns by the conventions of <paramref name="culture"/>
    /// (REQ-127), ignoring letter case.
    /// </summary>
    public static StringComparer SortComparer(CultureInfo culture) =>
        StringComparer.Create(culture, ignoreCase: true);

    /// <summary>
    /// True if both strings are identical after Unicode normalization (REQ-128). Case and
    /// accents still matter: use this for identity checks such as paths and stored values.
    /// </summary>
    public static bool AreEquivalent(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    /// <summary>The composed (NFC) form, for use as a lookup key. Never written back to files.</summary>
    public static string Normalize(string text) =>
        text.IsNormalized(NormalizationForm.FormC) ? text : text.Normalize(NormalizationForm.FormC);
}
