extern alias CatalogueApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FieldSales.StaffAccess;
using FieldSales.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;

namespace FieldSales.Web.Tests;

public sealed class CategoryIntegrationTests
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(
        Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));

    [Fact]
    public async Task Should_PersistSixLevelTreeCreatedThroughWebsite_When_ApplicationRestarts()
    {
        await using MsSqlContainer sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        string connectionString = sql.GetConnectionString();
        string token = CreateToken();
        string sixthUrl;

        await using (WebApplicationFactory<CatalogueDbContext> api = CreateApi(connectionString))
        {
            _ = api.Server;
            await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();

            await using StaffWebsiteFactory website = new(api.Server.CreateHandler(), token);
            using HttpClient browser = website.CreateBrowser();
            await SignInAsync(browser);
            string categoryUrl = "/HeadOffice/Categories";
            string[] names = ["Health", "Skincare", "Suncare", "Lotions", "Face", "Daily"];
            for (int level = 0; level < names.Length; level++)
            {
                string form = await GetPageAsync(browser, categoryUrl);
                using HttpResponseMessage created = await PostAsync(browser, categoryUrl, form, "Name", names[level]);
                Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
                categoryUrl = created.Headers.Location!.OriginalString;
            }
            sixthUrl = categoryUrl;
            string sixthPage = await GetPageAsync(browser, sixthUrl);
            AssertPath(sixthPage, names);

            // Rename the fourth level through the website before restarting both hosts.
            string fourthUrl = await FindAncestorUrlAsync(browser, sixthUrl, "Lotions");
            string fourthPage = await GetPageAsync(browser, fourthUrl);
            using HttpResponseMessage renamed = await PostAsync(browser,
                $"{fourthUrl}?handler=Rename", fourthPage, "RenameName", "Lotions & Creams");
            Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode);
        }

        await using WebApplicationFactory<CatalogueDbContext> restartedApi = CreateApi(connectionString);
        await using StaffWebsiteFactory restartedWebsite = new(restartedApi.Server.CreateHandler(), token);
        using HttpClient reopened = restartedWebsite.CreateBrowser();
        await SignInAsync(reopened);
        string afterRestart = await GetPageAsync(reopened, sixthUrl);
        AssertPath(afterRestart, ["Health", "Skincare", "Suncare", "Lotions & Creams", "Face", "Daily"]);
        Assert.DoesNotContain(">Lotions<", afterRestart);
        await using AsyncServiceScope restartedScope = restartedApi.Services.CreateAsyncScope();
        Assert.Equal(6, await restartedScope.ServiceProvider.GetRequiredService<CatalogueDbContext>()
            .Categories.CountAsync());
    }

    private static WebApplicationFactory<CatalogueDbContext> CreateApi(string connectionString) =>
        new WebApplicationFactory<CatalogueDbContext>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Authentication:Authority"] = Issuer,
                    ["ConnectionStrings:CatalogueDb"] = connectionString
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CatalogueDbContext>();
                services.AddScoped(_ => new CatalogueDbContext(
                    new DbContextOptionsBuilder<CatalogueDbContext>()
                        .UseSqlServer(connectionString).Options));
                services.RemoveAll<IStaffRoleLookup>();
                services.AddSingleton<IStaffRoleLookup>(new HeadOfficeRoleLookup());
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    OpenIdConnectConfiguration configuration = new() { Issuer = Issuer };
                    configuration.SigningKeys.Add(SigningKey);
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        });

    private static string CreateToken()
    {
        JwtSecurityToken token = new(issuer: Issuer, audience: "fieldsales-api",
            claims:
            [
                new Claim("sub", "niamh"), new Claim("scope", "fieldsales.api"),
                new Claim("role", StaffRoles.HeadOfficeUser)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task SignInAsync(HttpClient browser)
    {
        using HttpResponseMessage response = await browser.GetAsync(
            $"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(StaffRoles.HeadOfficeUser)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> GetPageAsync(HttpClient browser, string url)
    {
        using HttpResponseMessage response = await browser.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string url,
        string html, string field, string value)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "The category form must include an antiforgery token.");
        return browser.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [field] = value,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));
    }

    private static void AssertPath(string html, string[] names)
    {
        int position = html.IndexOf("aria-label=\"Category path\"", StringComparison.Ordinal);
        Assert.True(position >= 0);
        foreach (string name in names)
        {
            position = html.IndexOf($">{WebUtility.HtmlEncode(name)}<", position, StringComparison.Ordinal);
            Assert.True(position >= 0, $"Missing breadcrumb segment: {name}");
            position += name.Length;
        }
    }

    private static async Task<string> FindAncestorUrlAsync(HttpClient browser, string sixthUrl,
        string name)
    {
        string html = await GetPageAsync(browser, sixthUrl);
        Match match = Regex.Match(html, $"href=\"([^\"]+)\">{Regex.Escape(name)}</a>");
        Assert.True(match.Success, $"Missing breadcrumb link for {name}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class HeadOfficeRoleLookup : IStaffRoleLookup
    {
        public Task<StaffRoleLookupResult> GetRolesAsync(string accessToken, string subject,
            CancellationToken cancellationToken) =>
            Task.FromResult(StaffRoleLookupResult.Found([StaffRoles.HeadOfficeUser]));
    }
}
