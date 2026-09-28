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
            builder.UseSetting("Clients:RazorClientUri", "https://localhost:5001");
            builder.UseSetting("Clients:BlazorClientUri", "https://localhost:5002");
            builder.UseSetting("Clients:RazorSecret", "dev-secret-razor");
            builder.UseSetting("Clients:BlazorSecret", "dev-secret-blazor");
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
