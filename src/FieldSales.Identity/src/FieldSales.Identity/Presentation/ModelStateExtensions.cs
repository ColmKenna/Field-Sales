using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldSales.Identity.Presentation;

public static class ModelStateExtensions
{
    public static void AddErrors(this ModelStateDictionary state,
        IReadOnlyDictionary<string, string[]> errors, string? prefix = null)
    {
        foreach ((string field, string[] messages) in errors)
        {
            string key = string.IsNullOrEmpty(field) || string.IsNullOrEmpty(prefix)
                || field.StartsWith(prefix + ".", StringComparison.Ordinal) ? field : $"{prefix}.{field}";
            foreach (string message in messages) state.AddModelError(key, message);
        }
    }

    public static int ValidationTab(this ModelStateDictionary state, params string[] secondTabPrefixes) =>
        FormPresentation.ValidationTab(
            state.Where(entry => entry.Value is { Errors.Count: > 0 }).Select(entry => entry.Key),
            secondTabPrefixes);
}
