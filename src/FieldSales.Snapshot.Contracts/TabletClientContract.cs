namespace FieldSales.Snapshot.Contracts;

/// <summary>The tablet app's registration in the identity host (P7), shared by both sides of the Sync contract.</summary>
/// <remarks>
/// A public native client: authorization code with PKCE in the system browser (RFC 8252), no secret. Its refresh
/// token is used once and expires 24 hours after the connected sign-in (developer, 2026-10-24); refreshing never
/// extends the tablet's 24-hour local window (I3.1).
/// </remarks>
public static class TabletClientContract
{
    public const string ClientId = "fieldsales-tablet";
    public const string ClientName = "Field Sales Tablet";
    public const string RedirectUri = "com.fieldsales.tablet://signin-callback";
    public const string PostLogoutRedirectUri = "com.fieldsales.tablet://signout-callback";
    public const string Scopes = "openid profile roles fieldsales.api offline_access";
    public const string TokenPath = "connect/token";
    /// <summary>Only an active Field Salesperson may start a tablet day (P7); the API checks the same role.</summary>
    public const string RequiredRole = "Field Salesperson";
    public static TimeSpan SignInLifetime { get; } = TimeSpan.FromHours(24);
}
