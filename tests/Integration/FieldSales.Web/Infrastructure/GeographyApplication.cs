extern alias CatalogueApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
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
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using LocationTownUsageSource = CatalogueApi::FieldSales.Api.Directory.LocationTownUsageSource;

namespace FieldSales.Web.Tests;

public class GeographyApplication(DirectorySqlServerFixture sql) : IAsyncLifetime
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));
    public TestStaffRoleLookup Roles { get; } = new();
    public TestLocationUsageSource Locations { get; } = new();
    protected virtual bool UseTestLocationUsage => true;
    private WebApplicationFactory<DirectoryDbContext>? _api;
    public WebApplicationFactory<DirectoryDbContext> Api => _api ?? throw new InvalidOperationException("The test application is not initialized.");
    public string ConnectionString { get; private set; } = null!;

    public Task InitializeAsync() => FixtureCleanup.OnFailureAsync(async () =>
    {
        ConnectionString = sql.CreateConnectionString("Geography");
        Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        _api = NewApi();
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Database.MigrateAsync();
        await InitializeAdditionalAsync();
    }, DisposeAsync);
    protected virtual Task InitializeAdditionalAsync() => Task.CompletedTask;
    protected virtual void ConfigureAdditionalServices(IServiceCollection services, string connectionString) { }
    public async Task DisposeAsync()
    {
        var api = Interlocked.Exchange(ref _api, null);
        if (api is not null) await api.DisposeAsync();
    }
    private WebApplicationFactory<DirectoryDbContext> NewApi() => new WebApplicationFactory<DirectoryDbContext>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = Issuer,
            ["ConnectionStrings:DirectoryDb"] = ConnectionString,
            ["ConnectionStrings:CatalogueDb"] = ConnectionString
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DirectoryDbContext>();
            services.AddScoped(_ => new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(ConnectionString).Options));
            services.RemoveAll<IStaffRoleLookup>(); services.AddSingleton<IStaffRoleLookup>(Roles);
            if (UseTestLocationUsage)
            {
                foreach (var descriptor in services.Where(service => service.IsKeyedService
                    && Equals(service.ServiceKey, "directory") && service.KeyedImplementationType == typeof(LocationTownUsageSource)).ToArray())
                    services.Remove(descriptor);
                services.AddKeyedSingleton<IReferenceUsageSource>("directory", Locations);
            }
            ConfigureAdditionalServices(services, ConnectionString);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer }; configuration.SigningKeys.Add(Key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    });
    public StaffWebsiteFactory CreateWebsite() => new(Api.Server.CreateHandler(), Token());
    public HttpClient CreateApiClient()
    {
        var client = Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token()); return client;
    }
    public async Task ResetAsync()
    {
        Locations.Reset();
        Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        await db.Locations.ExecuteDeleteAsync(); await db.Customers.ExecuteDeleteAsync();
        await db.LocationTypes.ExecuteDeleteAsync(); await db.ContactTypes.ExecuteDeleteAsync();
        await db.Towns.ExecuteDeleteAsync(); await db.Counties.ExecuteDeleteAsync(); await db.Regions.ExecuteDeleteAsync();
    }
    public async Task<(int, int, int)> CountsAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        return (await db.Regions.CountAsync(), await db.Counties.CountAsync(), await db.Towns.CountAsync());
    }
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
    public string Token(string role = StaffRoles.HeadOfficeUser, string scope = "fieldsales.api", string subject = "niamh")
    {
        var token = new JwtSecurityToken(Issuer, "fieldsales-api",
            [new("sub", subject), new("scope", scope), new("role", role)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
