using System.Collections;

namespace CrateEdit.Core.Tags;

/// <summary>
/// The values of the logical fields of one file, as text exactly as stored (e.g. BPM "128.00"
/// is not rounded). A field that is absent or empty has no entry.
/// </summary>
public sealed class TagValues : IEnumerable<KeyValuePair<TagField, string>>
{
    private readonly Dictionary<TagField, string> _values;

    public TagValues(IEnumerable<KeyValuePair<TagField, string>> values)
    {
        _values = values.Where(v => v.Value.Length > 0).ToDictionary(v => v.Key, v => v.Value);
    }

    public static TagValues Empty { get; } = new([]);

    /// <summary>The field's value, or null if the field is absent or empty.</summary>
    public string? this[TagField field] => _values.GetValueOrDefault(field);

    public int Count => _values.Count;

    public IEnumerator<KeyValuePair<TagField, string>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
