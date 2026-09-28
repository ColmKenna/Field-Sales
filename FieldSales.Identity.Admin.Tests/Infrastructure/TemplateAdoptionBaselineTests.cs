using System.Net;
using System.Text.Json;
using Duende.IdentityServer.EntityFramework.DbContexts;
using FieldSales.Identity.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

[Collection(Task02SqlServerCollection.Name)]
public sealed class TemplateAdoptionBaselineTests
{
    [Fact]
    public async Task Should_StartTheIdentityHost_When_ThePinnedTemplateIsConfigured()
    {
        await using var databases = new FreshDatabases();
        await using WebApplicationFactory<Program> host = CreateHost(databases, "Development");
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument discovery = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(discovery.RootElement.GetProperty("issuer").GetString()));

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Database.GetPendingMigrationsAsync());
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>()
            .Database.GetPendingMigrationsAsync());
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>()
            .Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Should_SeedTheStaffClientAndApi_When_AFreshDevelopmentHostStarts()
    {
        await using var databases = new FreshDatabases();
        await using WebApplicationFactory<Program> host = CreateHost(databases, "Development");
        using HttpClient client = host.CreateClient();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        ConfigurationDbContext configuration = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        Duende.IdentityServer.EntityFramework.Entities.Client staffClient = await configuration.Clients
            .AsNoTracking()
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .Include(c => c.ClientSecrets)
            .SingleAsync();

        Assert.Equal(Config.StaffWebClientId, staffClient.ClientId);
        Assert.True(staffClient.RequirePkce);
        Assert.True(staffClient.RequireClientSecret);
        Assert.True(staffClient.AllowOfflineAccess);
        Assert.Equal("authorization_code", Assert.Single(staffClient.AllowedGrantTypes).GrantType);
        Assert.Equal("https://localhost:7203/signin-oidc", Assert.Single(staffClient.RedirectUris).RedirectUri);
        Assert.Equal("https://localhost:7203/signout-callback-oidc",
            Assert.Single(staffClient.PostLogoutRedirectUris).PostLogoutRedirectUri);
        Assert.Equal("https://localhost:7203/signout-oidc", staffClient.FrontChannelLogoutUri);
        Assert.Equal(new[] { "fieldsales.api", "openid", "profile", "roles" },
            staffClient.AllowedScopes.Select(s => s.Scope).OrderBy(s => s));
        Assert.NotEqual("dev-secret-staff-web", Assert.Single(staffClient.ClientSecrets).Value);

        Assert.Equal(Config.ApiScopeName, (await configuration.ApiScopes.SingleAsync()).Name);
        Duende.IdentityServer.EntityFramework.Entities.ApiResource api = await configuration.ApiResources
            .Include(r => r.Scopes)
            .Include(r => r.UserClaims)
            .SingleAsync();
        Assert.Equal(Config.ApiResourceName, api.Name);
        Assert.Equal(Config.ApiScopeName, Assert.Single(api.Scopes).Scope);
        Assert.Contains(api.UserClaims, claim => claim.Type == "role");

        Duende.IdentityServer.EntityFramework.Entities.IdentityResource roles = await configuration.IdentityResources
            .Include(r => r.UserClaims)
            .SingleAsync(r => r.Name == "roles");
        Assert.Contains(roles.UserClaims, claim => claim.Type == "role");
    }

    [Fact]
    public async Task Should_EmitTheRequestedRoleClaim_When_AStaffRoleIsAssigned()
    {
        await using var databases = new FreshDatabases();
        await using WebApplicationFactory<Program> host = CreateHost(databases, "Development");
        using HttpClient client = host.CreateClient();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserClaimsPrincipalFactory<ApplicationUser> claims =
            scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();

        Assert.True((await roles.CreateAsync(new IdentityRole("Field Salesperson"))).Succeeded);
        ApplicationUser staff = new() { UserName = "staff@sales.local", Email = "staff@sales.local" };
        Assert.True((await users.CreateAsync(staff, "Password123!")).Succeeded);
        Assert.True((await users.AddToRoleAsync(staff, "Field Salesperson")).Succeeded);

        var principal = await claims.CreateAsync(staff);
        Assert.Contains(principal.Claims, claim => claim.Type == "role" && claim.Value == "Field Salesperson");
    }

    [Fact]
    public async Task Should_KeepRevokedAdministrationRevoked_When_TheHostRestarts()
    {
        await using var databases = new FreshDatabases();

        await using (WebApplicationFactory<Program> firstHost = CreateHost(databases, "Development"))
        {
            using HttpClient client = firstHost.CreateClient();
            await using AsyncServiceScope scope = firstHost.Services.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser admin = (await users.FindByEmailAsync(FreshDatabases.AdminEmail))!;
            Assert.NotNull(admin);
            Assert.True(await users.IsInRoleAsync(admin, Config.SysAdminRole));
            Assert.True((await users.RemoveFromRoleAsync(admin, Config.SysAdminRole)).Succeeded);
        }

        await using WebApplicationFactory<Program> restartedHost = CreateHost(databases, "Development");
        using HttpClient restartedClient = restartedHost.CreateClient();
        await using AsyncServiceScope restartedScope = restartedHost.Services.CreateAsyncScope();
        UserManager<ApplicationUser> restartedUsers =
            restartedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser persistedAdmin = (await restartedUsers.FindByEmailAsync(FreshDatabases.AdminEmail))!;
        Assert.NotNull(persistedAdmin);
        Assert.False(await restartedUsers.IsInRoleAsync(persistedAdmin, Config.SysAdminRole));
    }

    [Fact]
    public async Task Should_RecreateTheSeededAdministrator_When_DeletedAndTheDevelopmentHostRestarts()
    {
        await using var databases = new FreshDatabases();

        await using (WebApplicationFactory<Program> firstHost = CreateHost(databases, "Development"))
        {
            using HttpClient client = firstHost.CreateClient();
            await DeleteSeededAdministratorAsync(firstHost);
        }

        await using (WebApplicationFactory<Program> restartedHost = CreateHost(databases, "Development"))
        {
            using HttpClient client = restartedHost.CreateClient();
            await using AsyncServiceScope scope = restartedHost.Services.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser recreatedAdmin = (await users.FindByEmailAsync(FreshDatabases.AdminEmail))!;
            Assert.NotNull(recreatedAdmin);
            Assert.True(await users.IsInRoleAsync(recreatedAdmin, Config.SysAdminRole));
            Assert.True((await users.DeleteAsync(recreatedAdmin)).Succeeded);
        }

        await using WebApplicationFactory<Program> nonDevelopmentHost = CreateHost(databases, "Testing");
        using HttpClient nonDevelopmentClient = nonDevelopmentHost.CreateClient();
        await using AsyncServiceScope nonDevelopmentScope = nonDevelopmentHost.Services.CreateAsyncScope();
        UserManager<ApplicationUser> nonDevelopmentUsers =
            nonDevelopmentScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await nonDevelopmentUsers.FindByEmailAsync(FreshDatabases.AdminEmail));
    }

    private static async Task DeleteSeededAdministratorAsync(WebApplicationFactory<Program> host)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser admin = (await users.FindByEmailAsync(FreshDatabases.AdminEmail))!;
        Assert.NotNull(admin);
        Assert.True((await users.DeleteAsync(admin)).Succeeded);
    }

    private static WebApplicationFactory<Program> CreateHost(FreshDatabases databases, string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:IdentityDb", databases.IdentityConnectionString);
            builder.UseSetting("ConnectionStrings:IdentityConfigDb", databases.ConfigurationConnectionString);
            builder.UseSetting("ConnectionStrings:IdentityOperationalDb", databases.OperationalConnectionString);
            builder.UseSetting("Clients:StaffWebUri", "https://localhost:7203");
            builder.UseSetting("Clients:StaffWebSecret", "dev-secret-staff-web");
            builder.UseSetting("Seed:SysAdminEmail", FreshDatabases.AdminEmail);
            builder.UseSetting("Seed:SysAdminPassword", "Password123!");
            builder.UseSetting("Seed:TestUserPassword", "Password123!");
        });

    private sealed class FreshDatabases : IAsyncDisposable
    {
        public const string AdminEmail = "admin@sales.local";

        private readonly string _suffix = Guid.NewGuid().ToString("N");

        public string IdentityConnectionString =>
            Task02SqlServerFactory.BuildConnectionString($"Adoption_Identity_{_suffix}");

        public string ConfigurationConnectionString =>
            Task02SqlServerFactory.BuildConnectionString($"Adoption_Configuration_{_suffix}");

        public string OperationalConnectionString =>
            Task02SqlServerFactory.BuildConnectionString($"Adoption_Operational_{_suffix}");

        public async ValueTask DisposeAsync()
        {
            await DeleteDatabaseAsync(IdentityConnectionString);
            await DeleteDatabaseAsync(ConfigurationConnectionString);
            await DeleteDatabaseAsync(OperationalConnectionString);
        }

        private static async Task DeleteDatabaseAsync(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            string database = builder.InitialCatalog;
            builder.InitialCatalog = "master";
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID(@database) IS NOT NULL BEGIN " +
                                  $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                                  $"DROP DATABASE [{database}]; END";
            command.Parameters.AddWithValue("@database", database);
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class TemplateAdoptionAdminAuthorizationTests(AdminWebFactory adminFactory)
    : IClassFixture<AdminWebFactory>
{
    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Users")]
    [InlineData("/Admin/Clients")]
    [InlineData("/Admin/Roles")]
    public async Task Should_DenySecurityAdministration_When_TheUserLacksSysAdmin(string path)
    {
        using HttpClient nonAdmin = adminFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        nonAdmin.DefaultRequestHeaders.Add("X-Test-Auth", "non-admin");
        using HttpResponseMessage denied = await nonAdmin.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal("/Account/AccessDenied", denied.Headers.Location?.OriginalString);

        using HttpClient anonymous = adminFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        anonymous.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");
        using HttpResponseMessage challenged = await anonymous.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        Assert.StartsWith("/Account/Login?returnUrl=", challenged.Headers.Location?.OriginalString);
    }
}
