using System.Globalization;
using CrateEdit.App.Resources;
using CrateEdit.App.Tags;
using CrateEdit.Core.Results;
using CrateEdit.Core.Tags;

namespace CrateEdit.App.Errors;

/// <summary>
/// Translates errors into messages for the user (ADR-0009). The message for an error code is the
/// resource "Error_" + code, with '.' and '-' replaced by '_' (e.g. "tags.malformed" becomes
/// "Error_tags_malformed"). Messages may contain placeholders for user-facing details such as a
/// field name; technical details stay in the logs.
/// </summary>
public static class ErrorMessages
{
    public static string ResourceKey(string code) => "Error_" + code.Replace('.', '_').Replace('-', '_');

    public static string For(Error error)
    {
        var template = Strings.ResourceManager.GetString(ResourceKey(error.Code), Strings.Culture);
        if (template is not null)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, ArgumentsFor(error));
            }
            catch (FormatException)
            {
                // The error lacks a detail its message needs; show the generic message instead of failing.
            }
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.Error_Unknown, error.Code);
    }

    /// <summary>The user-facing values for a message's placeholders ({0}, {1}, ...).</summary>
    private static object[] ArgumentsFor(Error error) => error switch
    {
        TagNotWritable { Field: { } field } => [FieldNames.For(field)],
        _ => [],
    };
}
