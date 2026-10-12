using System.Globalization;
using CrateEdit.App.Resources;
using CrateEdit.App.Tags;
using CrateEdit.Core.Files;
using CrateEdit.Core.Results;
using CrateEdit.Core.Tags;

namespace CrateEdit.App.Errors;

/// <summary>
/// Translates errors into messages for the user (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0009-result-pattern-and-typed-errors.md">ADR-0009</see>). The message for an error code is the
/// resource "Error_" + code, with '.' and '-' replaced by '_' (e.g. "tags.malformed" becomes
/// "Error_tags_malformed"). Messages may contain placeholders for user-facing details such as a
/// field name; technical details stay in the logs. An error may select a more specific variant of
/// its message, e.g. "Error_files_locked_by" when the program holding a file is known.
/// </summary>
public static class ErrorMessages
{
    public static string ResourceKey(string code) => "Error_" + code.Replace('.', '_').Replace('-', '_');

    public static string For(Error error)
    {
        var key = ResourceKey(error.Code) + VariantSuffix(error);
        var template = Strings.ResourceManager.GetString(key, Strings.Culture);
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

    /// <summary>Suffix of a more specific message variant, or empty for the error's standard message.</summary>
    internal static string VariantSuffix(Error error) => error switch
    {
        FileAccessError { Problem: FileProblem.Locked, LockedBy.Count: > 0 } => "_by",
        _ => "",
    };

    /// <summary>The user-facing values for a message's placeholders ({0}, {1}, ...).</summary>
    private static object[] ArgumentsFor(Error error) => error switch
    {
        TagNotWritable { Field: { } field } => [FieldNames.For(field)],
        FileAccessError { Problem: FileProblem.Locked, LockedBy.Count: > 0 } locked => [string.Join(", ", locked.LockedBy)],
        _ => [],
    };
}
