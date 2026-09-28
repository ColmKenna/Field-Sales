using System.Security.Claims;
using FieldSales.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FieldSales.Web.Tests;

// Runs the website with its real cookie, ticket store, role policies and pages. Only the
// successful OIDC callback is replaced by a test-only endpoint that issues the same principal.
public sealed class StaffWebsiteFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public StaffWebsiteFactory() => _connection.Open();

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
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
            services.RemoveAll<StaffWebDbContext>();
            services.AddScoped(_ => new StaffWebDbContext(
                new DbContextOptionsBuilder<StaffWebDbContext>().UseSqlite(_connection).Options));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<IStartupFilter, TestSignInStartupFilter>();
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration
                    {
                        Issuer = "https://localhost:7201",
                        AuthorizationEndpoint = "https://localhost:7201/connect/authorize",
                        EndSessionEndpoint = "https://localhost:7201/connect/endsession"
                    });
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);
        using IServiceScope scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    private sealed class TestSignInStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuePipeline) =>
            {
                if (context.Request.Path != "/__test/sign-in")
                {
                    await continuePipeline();
                    return;
                }

                string subject = context.Request.Query["subject"].ToString();
                string[] roles = context.Request.Query["roles"].ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (string.IsNullOrWhiteSpace(subject) || roles.Length == 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                Claim[] claims =
                [
                    new("sub", subject),
                    new("name", subject),
                    .. roles.Select(role => new Claim("role", role))
                ];
                ClaimsPrincipal user = new(new ClaimsIdentity(claims,
                    CookieAuthenticationDefaults.AuthenticationScheme, "name", "role"));
                AuthenticationProperties properties = new()
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
                };
                properties.StoreTokens(
                [
                    new AuthenticationToken
                    {
                        Name = "expires_at",
                        Value = DateTimeOffset.UtcNow.AddHours(1).ToString("o")
                    }
                ]);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, properties);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            });
            next(app);
        };
    }
}
