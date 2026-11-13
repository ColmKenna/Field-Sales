using System.Collections;

namespace FieldSales.Identity.Services.Validation;

/// <summary>
///     A collection of validation error messages keyed by member or field name,
///     providing fluent methods to accumulate errors across multiple validation checks.
/// </summary>
public sealed class ValidationErrorDictionary : IReadOnlyDictionary<string, string[]>
{
    private readonly Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);

    public static ValidationErrorDictionary Empty => new();

    public bool HasErrors => errors.Count > 0;
    public bool IsEmpty => errors.Count == 0;
    public int Count => errors.Count;

    public string[] this[string key] => errors[key].ToArray();

    public IEnumerable<string> Keys => errors.Keys;

    public IEnumerable<string[]> Values => errors.Values.Select(v => v.ToArray());

    public bool ContainsKey(string key) => errors.ContainsKey(key);

    public bool TryGetValue(string key, out string[] value)
    {
        if (errors.TryGetValue(key, out List<string>? list))
        {
            value = list.ToArray();
            return true;
        }

        value = Array.Empty<string>();
        return false;
    }

    public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator() =>
        errors.Select(kvp => new KeyValuePair<string, string[]>(kvp.Key, kvp.Value.ToArray())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ValidationErrorDictionary AddError(string field, string message)
    {
        string key = field ?? string.Empty;
        if (!errors.TryGetValue(key, out List<string>? list))
        {
            list = [];
            errors[key] = list;
        }

        list.Add(message);
        return this;
    }

    public ValidationErrorDictionary AddErrors(string field, IEnumerable<string> messages)
    {
        string key = field ?? string.Empty;
        if (!errors.TryGetValue(key, out List<string>? list))
        {
            list = [];
            errors[key] = list;
        }

        list.AddRange(messages);
        return this;
    }

    public IReadOnlyDictionary<string, string[]> ToDictionary() =>
        errors.ToDictionary(k => k.Key, v => v.Value.ToArray(), StringComparer.Ordinal);
}