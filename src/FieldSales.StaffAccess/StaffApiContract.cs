using System.Security.Claims;

namespace FieldSales.StaffAccess;

public static class StaffApiContract
{
    public const string Scope = "fieldsales.api";
    public const string Audience = "fieldsales-api";
    public const string WebClientId = "fieldsales-staff-web";
    public const string LookupUnavailableKey = "staff-role-lookup-unavailable";

    public static bool HasScope(this ClaimsPrincipal principal, string scope) => principal.FindAll("scope")
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Contains(scope, StringComparer.Ordinal);

    // Staff tokens use the first sub claim. Identity administration retains its actor resolver.
    public static string? GetStaffSubject(this ClaimsPrincipal? principal) => principal?.FindFirst("sub")?.Value;
}

public sealed record StaffSessionResponse(string Subject, string[] Roles);
public sealed record StaffRoleReplacement(bool Changed, IReadOnlyList<string> RemovedRoles);
