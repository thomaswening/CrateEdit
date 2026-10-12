using System.Collections;

namespace CrateEdit.Core.Tags;

/// <summary>
/// The values of the logical fields of one file, as text exactly as stored (e.g. BPM "128.00"
/// is not rounded). A field that is absent or empty has no entry.
/// </summary>
public sealed class TagValues : IReadOnlyDictionary<TagField, string>
{
    private readonly Dictionary<TagField, string> _values;

    public TagValues(IEnumerable<KeyValuePair<TagField, string>> values)
    {
        _values = values.Where(v => v.Value.Length > 0).ToDictionary(v => v.Key, v => v.Value);
    }

    public static TagValues Empty { get; } = new([]);

    public string? this[TagField field] => _values.GetValueOrDefault(field);

    string IReadOnlyDictionary<TagField, string>.this[TagField key] => _values[key];

    public IEnumerable<TagField> Keys => _values.Keys;

    public IEnumerable<string> Values => _values.Values;

    public int Count => _values.Count;

    public bool ContainsKey(TagField key) => _values.ContainsKey(key);

    public bool TryGetValue(TagField key, out string value) => _values.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<TagField, string>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
