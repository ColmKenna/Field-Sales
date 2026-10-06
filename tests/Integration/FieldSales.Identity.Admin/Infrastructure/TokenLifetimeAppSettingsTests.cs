using System.Text.Json;
using FieldSales.Identity.Services.Validation;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

/// <summary>
///     Guards against the shipped appsettings.json and Config.TokenLifetimes.Default
///     drifting apart silently. Both are meant to state the same values explicitly.
/// </summary>
[Trait("Category", "Integration")]
public class TokenLifetimeAppSettingsTests
{
    [Fact]
    public void ShippedAppsettingsJson_DeclaresTheSameValuesAsTheCodeDefault()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        JsonElement tokenLifetimes = document.RootElement
            .GetProperty("IdentityServer")
            .GetProperty("TokenLifetimes");

        Assert.Equal(
            TokenLifetimes.Default.AccessTokenLifetimeSeconds,
            tokenLifetimes.GetProperty("AccessTokenLifetimeSeconds").GetInt32());
        Assert.Equal(
            TokenLifetimes.Default.IdentityTokenLifetimeSeconds,
            tokenLifetimes.GetProperty("IdentityTokenLifetimeSeconds").GetInt32());
        Assert.Equal(
            TokenLifetimes.Default.AuthorizationCodeLifetimeSeconds,
            tokenLifetimes.GetProperty("AuthorizationCodeLifetimeSeconds").GetInt32());
    }
}
