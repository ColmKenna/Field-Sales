using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace FieldSales.StaffAccess;

public static class BusinessRoles
{
    public const string FieldSalesperson = "Field Salesperson";
    public const string SalesManager = "Sales Manager";
    public const string HeadOfficeUser = "Head Office User";

    public static readonly string[] All = [FieldSalesperson, SalesManager, HeadOfficeUser];

    public static bool Contains(string role) => All.Contains(role, StringComparer.Ordinal);
}

public sealed record CurrentStaffRolesResponse(string Subject, string[] Roles);

public sealed record StaffRoleLookupResult(bool TokenAccepted, IReadOnlyList<string> Roles)
{
    public static StaffRoleLookupResult Rejected { get; } = new(false, []);
    public static StaffRoleLookupResult Found(IReadOnlyList<string> roles) => new(true, roles);
}

public interface IStaffRoleLookup
{
    Task<StaffRoleLookupResult> GetRolesAsync(
        string accessToken, string subject, CancellationToken cancellationToken);
}

public sealed class HttpStaffRoleLookup(HttpClient client, IConfiguration configuration) : IStaffRoleLookup
{
    public async Task<StaffRoleLookupResult> GetRolesAsync(
        string accessToken, string subject, CancellationToken cancellationToken)
    {
        string authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        using HttpRequestMessage request = new(HttpMethod.Get,
            $"{authority.TrimEnd('/')}/staff/current-roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return StaffRoleLookupResult.Rejected;
        response.EnsureSuccessStatusCode();

        CurrentStaffRolesResponse? body = await response.Content
            .ReadFromJsonAsync<CurrentStaffRolesResponse>(cancellationToken);
        if (body is null || body.Subject != subject || body.Roles is null
            || body.Roles.Any(role => !BusinessRoles.Contains(role)))
            throw new InvalidDataException("The current staff roles response is invalid.");

        return StaffRoleLookupResult.Found(body.Roles.Distinct(StringComparer.Ordinal).ToArray());
    }
}

public static class StaffRoleClaims
{
    public static StaffRoleReplacement ReplaceBusinessRoles(ClaimsPrincipal principal, IReadOnlyList<string> roles)
    {
        if (roles.Any(role => !BusinessRoles.Contains(role)))
            throw new InvalidDataException("The current staff roles response contains an unknown role.");

        string[] previous = principal.FindAll("role")
            .Select(claim => claim.Value)
            .Where(BusinessRoles.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] current = roles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (previous.SequenceEqual(current, StringComparer.Ordinal)) return new(false, []);

        foreach (ClaimsIdentity identity in principal.Identities)
            foreach (Claim claim in identity.FindAll("role").Where(claim => BusinessRoles.Contains(claim.Value)).ToArray())
                identity.RemoveClaim(claim);
        ClaimsIdentity destination = principal.Identities.First();
        foreach (string role in current)
            destination.AddClaim(new Claim("role", role));
        return new(true, previous.Except(current, StringComparer.Ordinal).ToArray());
    }
}
