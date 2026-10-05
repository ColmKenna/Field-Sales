using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Coverage;

internal static class CoverageSubjects
{
    public static string Validate(string? subject, string field)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > CoverageFields.MaximumSubjectLength
            || subject != subject.Trim() || subject.Any(char.IsControl))
            throw new CoverageValidationException(field, "Choose a valid staff account.");
        // Do not trim, change case, or otherwise rewrite an identity subject.
        return subject;
    }
}

public sealed class CoverageValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
