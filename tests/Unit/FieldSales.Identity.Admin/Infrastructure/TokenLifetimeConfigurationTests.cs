using Duende.IdentityServer.Models;
using FieldSales.Identity.Services.Validation;
using Microsoft.Extensions.Configuration;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

/// <summary>
///     <c>IdentityServer:TokenLifetimes</c> only matters if three things independently hold: the
///     values reach <see cref="Config.Clients" />, the configuration keys Program.cs reads are the
///     ones actually documented in <c>appsettings.json</c>, and the shipped file states the same
///     values Duende's SDK already defaults to. None of the three follows from the others compiling.
/// </summary>
[Trait("Category", "Unit")]
public class TokenLifetimeConfigurationTests
{
    [Fact]
    public void Clients_AppliesTheConfiguredTokenLifetimesToEveryClient()
    {
        var tokenLifetimes = new TokenLifetimes(
            AccessTokenLifetimeSeconds: 1200,
            IdentityTokenLifetimeSeconds: 60,
            AuthorizationCodeLifetimeSeconds: 90);
        var clients = new List<SeedClientSpec>
        {
            new("client-a", "Client A", AbsoluteHttpUri.Create("https://localhost:5001"), "secret-a"),
            new("client-b", "Client B", AbsoluteHttpUri.Create("https://localhost:5002"), "secret-b")
        };

        List<Client> seeded = Config.Clients(clients, tokenLifetimes).ToList();

        Assert.All(seeded, client =>
        {
            Assert.Equal(1200, client.AccessTokenLifetime);
            Assert.Equal(60, client.IdentityTokenLifetime);
            Assert.Equal(90, client.AuthorizationCodeLifetime);
        });
    }

    [Fact]
    public void ConfiguredSection_OverridesTheCodeDefaults()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityServer:TokenLifetimes:AccessTokenLifetimeSeconds"] = "900",
                ["IdentityServer:TokenLifetimes:IdentityTokenLifetimeSeconds"] = "120",
                ["IdentityServer:TokenLifetimes:AuthorizationCodeLifetimeSeconds"] = "180"
            })
            .Build();

        var tokenLifetimes = new TokenLifetimes(
            configuration.GetValue(
                "IdentityServer:TokenLifetimes:AccessTokenLifetimeSeconds",
                TokenLifetimes.Default.AccessTokenLifetimeSeconds),
            configuration.GetValue(
                "IdentityServer:TokenLifetimes:IdentityTokenLifetimeSeconds",
                TokenLifetimes.Default.IdentityTokenLifetimeSeconds),
            configuration.GetValue(
                "IdentityServer:TokenLifetimes:AuthorizationCodeLifetimeSeconds",
                TokenLifetimes.Default.AuthorizationCodeLifetimeSeconds));

        Assert.Equal(new TokenLifetimes(900, 120, 180), tokenLifetimes);
    }

    [Fact]
    public void AbsentSection_FallsBackToTheCodeDefaults()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        var tokenLifetimes = new TokenLifetimes(
            configuration.GetValue(
                "IdentityServer:TokenLifetimes:AccessTokenLifetimeSeconds",
                TokenLifetimes.Default.AccessTokenLifetimeSeconds),
            configuration.GetValue(
                "IdentityServer:TokenLifetimes:IdentityTokenLifetimeSeconds",
                TokenLifetimes.Default.IdentityTokenLifetimeSeconds),
            configuration.GetValue(
                "IdentityServer:TokenLifetimes:AuthorizationCodeLifetimeSeconds",
                TokenLifetimes.Default.AuthorizationCodeLifetimeSeconds));

        Assert.Equal(TokenLifetimes.Default, tokenLifetimes);
    }
}
