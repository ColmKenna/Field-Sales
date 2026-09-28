using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FieldSales.Web.Security;

public sealed class StaffCookieEvents(
    IHttpClientFactory clients,
    IConfiguration configuration,
    TimeProvider timeProvider,
    IStaffRoleLookup roleLookup,
    StaffAreaService areas,
    AccessChangedTokenService accessChangedTokens,
    ILogger<StaffCookieEvents> logger) : CookieAuthenticationEvents
{
    public const string LookupUnavailableKey = "staff-role-lookup-unavailable";
    public const string RemovedRoleClaimType = "fieldsales:removed-staff-role";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // Sign-out remains available when the identity host cannot answer role lookups.
        if (context.Request.Path == "/SignOut") return;

        string? expiryText = context.Properties.GetTokenValue("expires_at");
        if (!DateTimeOffset.TryParse(expiryText, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset expiry)
            || expiry <= timeProvider.GetUtcNow().AddMinutes(1))
        {
            if (!await RefreshTokenAsync(context)) return;
        }

        string? token = context.Properties.GetTokenValue("access_token");
        string? subject = context.Principal?.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(subject))
        {
            await RejectAsync(context);
            return;
        }

        StaffRoleLookupResult result;
        try
        {
            result = await roleLookup.GetRolesAsync(token, subject, context.HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Current staff roles could not be checked.");
            context.HttpContext.Items[LookupUnavailableKey] = true;
            context.RejectPrincipal();
            return;
        }
        if (!result.TokenAccepted)
        {
            await RejectAsync(context);
            return;
        }

        string[] previousRoles = context.Principal!.FindAll("role")
            .Select(claim => claim.Value)
            .Where(BusinessRoles.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (StaffRoleClaims.ReplaceBusinessRoles(context.Principal, result.Roles))
        {
            ClaimsIdentity identity = context.Principal!.Identities.First();
            foreach (Claim claim in identity.FindAll(RemovedRoleClaimType)
                         .Where(claim => result.Roles.Contains(claim.Value, StringComparer.Ordinal)).ToArray())
                identity.RemoveClaim(claim);
            foreach (string role in previousRoles.Except(result.Roles, StringComparer.Ordinal))
                if (!context.Principal.HasClaim(RemovedRoleClaimType, role))
                    identity.AddClaim(new Claim(RemovedRoleClaimType, role));
            context.ReplacePrincipal(context.Principal!);
            context.ShouldRenew = true;
            await areas.ClearUnpermittedAreaAsync(context.Principal!, context.HttpContext.RequestAborted);
        }
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        StaffArea? area = StaffAreas.ForLocalUrl(context.Request.Path.ToString());
        string? subject = context.HttpContext.User.FindFirst("sub")?.Value;
        if (area is null || string.IsNullOrWhiteSpace(subject)
            || !context.HttpContext.User.HasClaim(RemovedRoleClaimType, area.Role))
            return base.RedirectToAccessDenied(context);

        bool wasWrite = !HttpMethods.IsGet(context.Request.Method)
                        && !HttpMethods.IsHead(context.Request.Method);
        string state = accessChangedTokens.Create(subject, area.Key, wasWrite);
        context.Response.Redirect($"/AccessChanged?state={Uri.EscapeDataString(state)}");
        return Task.CompletedTask;
    }

    private async Task<bool> RefreshTokenAsync(CookieValidatePrincipalContext context)
    {

        string? refreshToken = context.Properties.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            await RejectAsync(context);
            return false;
        }

        string authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        string secret = configuration["Authentication:ClientSecret"]
            ?? throw new InvalidOperationException("Authentication:ClientSecret is required.");
        using HttpClient client = clients.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            $"{authority.TrimEnd('/')}/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = "fieldsales-staff-web",
                ["client_secret"] = secret,
                ["refresh_token"] = refreshToken
            }), context.HttpContext.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            await RejectAsync(context);
            return false;
        }

        using JsonDocument json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted),
            cancellationToken: context.HttpContext.RequestAborted);
        if (!json.RootElement.TryGetProperty("access_token", out JsonElement accessToken)
            || accessToken.ValueKind != JsonValueKind.String
            || !json.RootElement.TryGetProperty("expires_in", out JsonElement expiresIn)
            || !expiresIn.TryGetInt32(out int seconds)
            || seconds <= 0)
        {
            await RejectAsync(context);
            return false;
        }

        context.Properties.UpdateTokenValue("access_token", accessToken.GetString()!);
        context.Properties.UpdateTokenValue("expires_at", timeProvider.GetUtcNow().AddSeconds(seconds).ToString("o"));
        if (json.RootElement.TryGetProperty("refresh_token", out JsonElement newRefreshToken)
            && newRefreshToken.ValueKind == JsonValueKind.String)
            context.Properties.UpdateTokenValue("refresh_token", newRefreshToken.GetString()!);
        context.ShouldRenew = true; // Cookie middleware renews this key in the server-side store.
        return true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
