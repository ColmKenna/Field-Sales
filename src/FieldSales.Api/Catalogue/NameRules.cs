using System.Diagnostics.CodeAnalysis;

namespace FieldSales.Api.Catalogue;

public static class NameRules
{
    public static string ErrorMessage(string label, int maximumLength) =>
        $"Enter a {label} name of up to {maximumLength} characters.";

    public static bool IsValid([NotNullWhen(true)] string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;
}
