using System.Reflection;

namespace CrateEdit.Core.Results;

/// <summary>
/// Every error code in the application. Each code must have a user-facing message in English and
/// German (enforced by tests), so new codes are added here, never as ad-hoc strings.
/// </summary>
public static class ErrorCodes
{
    public const string TagsUnsupportedVersion = "tags.unsupported-version";
    public const string TagsUnsupportedFeature = "tags.unsupported-feature";
    public const string TagsMalformed = "tags.malformed";
    public const string TagsEncodedFrame = "tags.encoded-frame";
    public const string TagsDuplicateFrame = "tags.duplicate-frame";

    /// <summary>All codes declared above.</summary>
    public static IReadOnlyList<string> All { get; } = typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();
}
