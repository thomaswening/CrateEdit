using System.Globalization;
using CrateEdit.App.Resources;
using CrateEdit.Core.Results;

namespace CrateEdit.App.Errors;

/// <summary>
/// Translates errors into messages for the user (ADR-0009). The message for an error code is the
/// resource "Error_" + code, with '.' and '-' replaced by '_' (e.g. "tags.malformed" becomes
/// "Error_tags_malformed"). Technical details stay in the logs, never in the message.
/// </summary>
public static class ErrorMessages
{
    public static string ResourceKey(string code) => "Error_" + code.Replace('.', '_').Replace('-', '_');

    public static string For(Error error)
    {
        var message = Strings.ResourceManager.GetString(ResourceKey(error.Code), Strings.Culture);
        return message ?? string.Format(CultureInfo.CurrentCulture, Strings.Error_Unknown, error.Code);
    }
}
