extern alias CatalogueApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FieldSales.StaffAccess;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using CategoryTree = CatalogueApi::FieldSales.Api.Catalogue.CategoryTree;
using Category = CatalogueApi::FieldSales.Api.Catalogue.Category;

namespace FieldSales.Web.Tests;

public sealed class ProductApplication(CatalogueSqlServerFixture sql) : IAsyncLifetime
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));
    public TestStaffRoleLookup Roles { get; } = new();
    private WebApplicationFactory<CatalogueDbContext>? _api;
    public WebApplicationFactory<CatalogueDbContext> Api => _api ?? throw new InvalidOperationException("The test application is not initialized.");
    public string ConnectionString { get; private set; } = null!;

    public Task InitializeAsync() => FixtureCleanup.OnFailureAsync(async () =>
    {
        ConnectionString = sql.CreateConnectionString("Product");
        _api = NewApi();
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
    }, DisposeAsync);

    public async Task DisposeAsync()
    {
        var api = Interlocked.Exchange(ref _api, null);
        if (api is not null) await api.DisposeAsync();
    }

    public WebApplicationFactory<CatalogueDbContext> NewApi() =>
        new WebApplicationFactory<CatalogueDbContext>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = Issuer,
                ["ConnectionStrings:CatalogueDb"] = ConnectionString
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CatalogueDbContext>();
                services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
                    .UseSqlServer(ConnectionString).Options));
                services.RemoveAll<IStaffRoleLookup>();
                services.AddSingleton<IStaffRoleLookup>(Roles);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new ProductClock());
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    OpenIdConnectConfiguration configuration = new() { Issuer = Issuer };
                    configuration.SigningKeys.Add(SigningKey);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        });

    public StaffWebsiteFactory CreateWebsite(WebApplicationFactory<CatalogueDbContext>? api = null) =>
        new((api ?? Api).Server.CreateHandler(), Token());

    public async Task RestartAsync()
    {
        await DisposeAsync();
        await FixtureCleanup.OnFailureAsync(() =>
        {
            _api = NewApi();
            _ = Api.Server;
            return Task.CompletedTask;
        }, DisposeAsync);
    }

    public HttpClient CreateApiClient()
    {
        HttpClient client = Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        return client;
    }

    public async Task<Guid> ResetAsync()
    {
        Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductBasePrices.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        // Remove deepest categories first to respect the predecessor's restricted parent relationship.
        foreach (Category category in (await db.Categories.ToListAsync()).Reverse<Category>()) db.Categories.Remove(category);
        await db.SaveChangesAsync();
        CategoryTree tree = new([]);
        Guid? parent = null;
        foreach (string name in new[] { "Health", "Skincare", "Suncare", "Lotions" })
        {
            Category category = tree.Add(name, parent);
            db.Categories.Add(category);
            parent = category.Id;
        }
        await db.SaveChangesAsync();
        return parent!.Value;
    }

    public async Task<(int Products, int Prices)> CountsAsync()
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        return (await db.Products.CountAsync(), await db.ProductBasePrices.CountAsync());
    }

    private static string Token()
    {
        JwtSecurityToken token = new(issuer: Issuer, audience: "fieldsales-api",
            claims: [new Claim("sub", "niamh"), new Claim("scope", "fieldsales.api"), new Claim("role", StaffRoles.HeadOfficeUser)],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ProductClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 30, 23, 30, 0, TimeSpan.Zero);
    }
}
