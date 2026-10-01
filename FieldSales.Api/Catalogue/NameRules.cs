using System.Diagnostics.CodeAnalysis;

namespace FieldSales.Api.Catalogue;

public static class NameRules
{
    public static bool IsValid([NotNullWhen(true)] string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;
}
