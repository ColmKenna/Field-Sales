using Microsoft.AspNetCore.Identity;

namespace FieldSales.Identity.Presentation;

public static class IdentityErrors
{
    public static string Describe(this IdentityResult result, string separator = " ") =>
        string.Join(separator, result.Errors.Select(error => error.Description));
}
