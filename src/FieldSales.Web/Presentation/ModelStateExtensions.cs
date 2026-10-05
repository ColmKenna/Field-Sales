using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldSales.Web.Presentation;

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
}
