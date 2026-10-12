using CrateEdit.App.Resources;
using CrateEdit.Core.Tags;

namespace CrateEdit.App.Tags;

/// <summary>User-facing names of the logical fields, as shown in column headers and messages.</summary>
public static class FieldNames
{
    public static string ResourceKey(TagField field) => "Field_" + field;

    public static string For(TagField field) =>
        Strings.ResourceManager.GetString(ResourceKey(field), Strings.Culture) ?? field.ToString();
}
