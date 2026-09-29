using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FieldSales.Api.Catalogue;
using FieldSales.StaffAccess;
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

namespace FieldSales.Api.Tests;

public sealed class CataloguePersistenceTests
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(
        Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));

    [Fact]
    public async Task Should_PersistRootAndNestedCategories_When_ApiRestarts()
    {
        await using MsSqlContainer sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        Guid rootId;
        Guid childId;
        await using (WebApplicationFactory<Program> first = CreateFactory(sql.GetConnectionString()))
        {
            await MigrateAsync(first);
            using HttpClient browser = first.CreateClient(new WebApplicationFactoryClientOptions
                { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using HttpResponseMessage root = await PostAsync(browser, "Suncare", null, BusinessRoles.HeadOfficeUser);
            Assert.Equal(HttpStatusCode.Created, root.StatusCode);
            rootId = (await root.Content.ReadFromJsonAsync<CategoryItem>())!.Id;
            using HttpResponseMessage child = await PostAsync(browser, "Lotions", rootId, BusinessRoles.HeadOfficeUser);
            Assert.Equal(HttpStatusCode.Created, child.StatusCode);
            childId = (await child.Content.ReadFromJsonAsync<CategoryItem>())!.Id;
            Assert.Equal(HttpStatusCode.Conflict,
                (await PostAsync(browser, "suncare", null, BusinessRoles.HeadOfficeUser)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await PostAsync(browser, "LOTIONS", rootId, BusinessRoles.HeadOfficeUser)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await PostAsync(browser, "Denied", null, BusinessRoles.FieldSalesperson)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await PostAsync(browser, "Denied", null, BusinessRoles.SalesManager)).StatusCode);
        }

        await using WebApplicationFactory<Program> restarted = CreateFactory(sql.GetConnectionString());
        using HttpClient client = restarted.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        CategoryItem[] roots = (await client.GetFromJsonAsync<CategoryItem[]>("/catalogue/categories/"))!;
        Assert.Single(roots);
        Assert.Equal(rootId, roots[0].Id);
        CategoryDetails detail = (await client.GetFromJsonAsync<CategoryDetails>(
            $"/catalogue/categories/{childId}"))!;
        Assert.Equal(childId, detail.Category.Id);
        Assert.Equal(new[] { "Suncare", "Lotions" }, detail.Breadcrumb.Select(segment => segment.Name));
        await using AsyncServiceScope scope = restarted.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Assert.Equal(2, await db.Categories.CountAsync());
    }

    private static async Task MigrateAsync(WebApplicationFactory<Program> factory)
    {
        _ = factory.Server;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string name,
        Guid? parentId, string currentRole)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/catalogue/categories/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(currentRole));
        request.Content = JsonContent.Create(new { Name = name, ParentId = parentId });
        return await client.SendAsync(request);
    }

    private static string Token(string role = BusinessRoles.HeadOfficeUser)
    {
        JwtSecurityToken token = new(issuer: Issuer, audience: "fieldsales-api",
            claims: [new Claim("sub", "staff-1"), new Claim("scope", "fieldsales.api"), new Claim("role", role)],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class HeadOfficeRoleLookup : IStaffRoleLookup
    {
        public Task<StaffRoleLookupResult> GetRolesAsync(string accessToken, string subject,
            CancellationToken cancellationToken)
        {
            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return Task.FromResult(StaffRoleLookupResult.Found(
                token.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value).ToArray()));
        }
    }
}
