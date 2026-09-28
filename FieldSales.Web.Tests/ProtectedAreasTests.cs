using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace FieldSales.Web.Tests;

public sealed class ProtectedAreasTests
{
    [Fact]
    public void BrowserCookieUsesServerSideTicketStore()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        _ = factory.CreateClient();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.IsType<FieldSales.Web.Security.SqlTicketStore>(options.SessionStore);
    }

    [Theory]
    [InlineData("/Rep", "Field Salesperson")]
    [InlineData("/Manager", "Sales Manager")]
    [InlineData("/HeadOffice", "Head Office User")]
    public async Task ProtectedAreaRequiresItsBusinessRole(string path, string role)
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);

        client.DefaultRequestHeaders.Add("X-Test-Roles", "SysAdmin");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Roles");

        string otherRole = role == "Field Salesperson" ? "Sales Manager" : "Field Salesperson";
        client.DefaultRequestHeaders.Add("X-Test-Roles", otherRole);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Roles");

        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        HttpResponseMessage allowed = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("workspace is ready", await allowed.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Authentication:Authority"] = "https://localhost:7201",
                    ["Authentication:ClientSecret"] = "test-secret",
                    ["ConnectionStrings:StaffWebDb"] = "Server=localhost;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(
                    "Test", _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                });
            });
        });

    private sealed class HeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Roles", out var header))
                return Task.FromResult(AuthenticateResult.NoResult());
            Claim[] claims = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(role => new Claim(ClaimTypes.Role, role.Trim())).ToArray();
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role)),
                "Test")));
        }
    }
}
