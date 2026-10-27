using System.Net;
using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using FieldSales.Identity.Admin.Tests.Infrastructure;
using FieldSales.Identity.Data;
using FieldSales.Identity.Services.Validation;
using FieldSales.Snapshot.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Identity.Admin.Tests.Clients;

// WI-032 increment 3 (P7): the tablet app's identity registration. Each test seeds its own in-memory host.
public sealed class TabletClientRegistrationTests
{
    [Fact]
    public async Task Should_RegisterAPublicPkceClientWithA24HourSignIn_When_DevelopmentSeedsClients()
    {
        await using AdminWebFactory host = new();
        await SeedAsync(host.Services);
        await SeedAsync(host.Services); // The seeder is additive: running it again adds nothing.

        await host.RunInScopeAsync(async services =>
        {
            Assert.Equal(1, await services.GetRequiredService<ConfigurationDbContext>().Clients
                .CountAsync(client => client.ClientId == TabletClientContract.ClientId));
            Client tablet = (await services.GetRequiredService<IClientStore>().FindClientByIdAsync(TabletClientContract.ClientId, CancellationToken.None))!;
            Assert.True(tablet.Enabled);
            Assert.False(tablet.RequireClientSecret);
            Assert.Empty(tablet.ClientSecrets);
            Assert.True(tablet.RequirePkce);
            Assert.False(tablet.AllowPlainTextPkce);
            Assert.Equal(GrantTypes.Code, tablet.AllowedGrantTypes);
            Assert.Equal([TabletClientContract.RedirectUri], tablet.RedirectUris);
            Assert.Equal([TabletClientContract.PostLogoutRedirectUri], tablet.PostLogoutRedirectUris);
            Assert.Equal(new[] { "fieldsales.api", "openid", "profile", "roles" }, tablet.AllowedScopes.Order());
            Assert.True(tablet.AllowOfflineAccess);
            Assert.Equal(TokenUsage.OneTimeOnly, tablet.RefreshTokenUsage);
            Assert.Equal(TokenExpiration.Absolute, tablet.RefreshTokenExpiration);
            Assert.Equal(24 * 60 * 60, tablet.AbsoluteRefreshTokenLifetime);
            Assert.False(tablet.RequireConsent);
            Assert.Equal(3600, tablet.AccessTokenLifetime);
        });
    }

    [Fact]
    public async Task Should_SendTheRepToSignIn_When_TheTabletRequestsAuthorizationCorrectly()
    {
        await using AdminWebFactory host = new();
        await using WebApplicationFactory<Program> signIn = WithLoginPage(host);
        await SeedAsync(signIn.Services);
        using HttpResponseMessage response = await AuthorizeAsync(signIn, TabletClientContract.RedirectUri, withPkce: true);
        // Duende 8 redirects to the login page with 303 See Other, carrying the validated request.
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.StartsWith("/Account/Login?ReturnUrl=%2Fconnect%2Fauthorize%2Fcallback", PathOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://attacker.example/callback", true)]
    [InlineData("com.fieldsales.tablet://other", true)]
    [InlineData(TabletClientContract.RedirectUri, false)]
    public async Task Should_RefuseTheRequest_When_TheRedirectOrPkceDoesNotMatchTheRegistration(string redirectUri, bool withPkce)
    {
        await using AdminWebFactory host = new();
        await using WebApplicationFactory<Program> signIn = WithLoginPage(host);
        await SeedAsync(signIn.Services);
        using HttpResponseMessage response = await AuthorizeAsync(signIn, redirectUri, withPkce);
        // Duende's error page: no sign-in page, and no code or error sent to the requested address.
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.StartsWith("/home/error?errorId=", PathOf(response), StringComparison.OrdinalIgnoreCase);
    }

    private static string PathOf(HttpResponseMessage response) =>
        response.Headers.Location is { IsAbsoluteUri: true } absolute ? absolute.PathAndQuery : response.Headers.Location?.OriginalString ?? "";

    // The admin harness makes its test scheme the default, which leaves Duende no login page to send an
    // anonymous user to. Production derives both values from the Identity cookie; this states them.
    // The derived host has its own in-memory database, so tests seed through it.
    private static WebApplicationFactory<Program> WithLoginPage(AdminWebFactory host) =>
        host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<IdentityServerOptions>(options =>
            {
                options.UserInteraction.LoginUrl = "/Account/Login";
                options.UserInteraction.LoginReturnUrlParameter = "ReturnUrl";
            })));

    private static async Task<HttpResponseMessage> AuthorizeAsync(WebApplicationFactory<Program> signIn, string redirectUri, bool withPkce)
    {
        using HttpClient client = signIn.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Dictionary<string, string> query = new()
        {
            ["client_id"] = TabletClientContract.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = TabletClientContract.Scopes,
            ["state"] = "tablet-state",
            ["nonce"] = "tablet-nonce"
        };
        if (withPkce) { query["code_challenge"] = challenge; query["code_challenge_method"] = "S256"; }
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/connect/authorize?" + string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}")));
        request.Headers.Add("X-Test-Auth", "anonymous");
        return await client.SendAsync(request);
    }

    private static async Task SeedAsync(IServiceProvider root)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        await SeedData.SeedAsync(
        services.GetRequiredService<ApplicationDbContext>(),
        services.GetRequiredService<ConfigurationDbContext>(),
        services.GetRequiredService<PersistedGrantDbContext>(),
        services.GetRequiredService<UserManager<ApplicationUser>>(),
        services.GetRequiredService<RoleManager<IdentityRole>>(),
        [new(Config.StaffWebClientId, "Field Sales Staff Website", AbsoluteHttpUri.Create("https://localhost:7203"), "secret")],
        "admin@sales.local",
        "Password123!",
        "Password123!",
        TokenLifetimes.Default);
    }
}
