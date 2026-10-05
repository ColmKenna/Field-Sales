using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FieldSales.Identity.Data;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

[Collection(Task02SqlServerCollection.Name)]
public sealed class StaffDirectoryEndpointTests(Task02SqlServerFactory databases) : IAsyncLifetime
{
    private const string Issuer = "https://staff-directory.test";
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("test-only-staff-directory-signing-32bytes"));
    private readonly List<string> seededSubjects = [];

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => databases.RunInScopeAsync(async services =>
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (string subject in seededSubjects)
            if (await users.FindByIdAsync(subject) is { } user)
                Assert.True((await users.DeleteAsync(user)).Succeeded);
    });

    [Fact]
    public async Task Should_ExposeOnlyTrustedStaffFields_When_CurrentHeadOfficeOrManagerReadsDirectory()
    {
        var actor = await SeedAsync(BusinessRoles.HeadOfficeUser, "Niamh Byrne");
        var rep = await SeedAsync(BusinessRoles.FieldSalesperson, "Colm");
        var inactive = await SeedAsync(BusinessRoles.FieldSalesperson, null);
        var admin = await SeedAsync("SysAdmin", "Administrator");
        await databases.RunInScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await users.FindByIdAsync(inactive))!;
            Assert.True((await users.SetLockoutEndDateAsync(account, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
        });
        await using var host = Host(); using var client = Client(host, Token(actor));
        var candidates = (await client.GetFromJsonAsync<StaffDirectoryEntry[]>("/staff/directory"))!;
        Assert.Equal("Colm", candidates.Single(row => row.Subject == rep).DisplayName);
        var locked = candidates.Single(row => row.Subject == inactive);
        Assert.False(locked.Available); Assert.Equal("staff-" + inactive, locked.DisplayName);
        Assert.DoesNotContain(candidates, row => row.Subject == admin);
        using var response = await client.PostAsJsonAsync("/staff/directory/lookup", new StaffDirectoryLookupRequest([rep, "missing", rep]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var entry = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(new[] { "available", "displayName", "roles", "subject" },
            entry.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        await databases.RunInScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await users.FindByIdAsync(rep))!;
            account.FullName = "Renamed Colm"; Assert.True((await users.UpdateAsync(account)).Succeeded);
            Assert.True((await users.RemoveFromRoleAsync(account, BusinessRoles.FieldSalesperson)).Succeeded);
            var manager = (await users.FindByIdAsync(actor))!;
            Assert.True((await users.RemoveFromRoleAsync(manager, BusinessRoles.HeadOfficeUser)).Succeeded);
            Assert.True((await users.AddToRoleAsync(manager, BusinessRoles.SalesManager)).Succeeded);
        });
        // Label lookup includes historical identities even after their business role is removed.
        using var after = await client.PostAsJsonAsync("/staff/directory/lookup", new StaffDirectoryLookupRequest([rep]));
        var oldRep = Assert.Single((await after.Content.ReadFromJsonAsync<StaffDirectoryEntry[]>())!);
        Assert.Equal("Renamed Colm", oldRep.DisplayName); Assert.Empty(oldRep.Roles);
    }

    [Fact]
    public async Task Should_FailClosed_When_CurrentCallerRoleAccountScopeOrAudienceIsInvalid()
    {
        var actor = await SeedAsync(BusinessRoles.HeadOfficeUser, "Niamh");
        await using var host = Host(); using var client = Client(host, Token(actor));
        foreach (var (token, expected) in new[]
        {
            ((string?)null, HttpStatusCode.Unauthorized),
            (Token(actor, scope: "openid"), HttpStatusCode.Forbidden),
            (Token(actor, audience: "another-api"), HttpStatusCode.Unauthorized),
            (Token(actor.ToLowerInvariant()), HttpStatusCode.Forbidden)
        })
        {
            client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);
            using var rejected = await client.GetAsync("/staff/directory"); Assert.Equal(expected, rejected.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(actor));
        await databases.RunInScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(actor))!;
            Assert.True((await users.RemoveFromRoleAsync(user, BusinessRoles.HeadOfficeUser)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, BusinessRoles.FieldSalesperson)).Succeeded);
        });
        using var stale = await client.PostAsJsonAsync("/staff/directory/lookup", new StaffDirectoryLookupRequest([actor]));
        Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
        await databases.RunInScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await users.FindByIdAsync(actor))!;
            Assert.True((await users.AddToRoleAsync(user, BusinessRoles.HeadOfficeUser)).Succeeded);
            Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
        });
        using var locked = await client.GetAsync("/staff/directory"); Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
    }

    [Fact]
    public async Task Should_RejectMalformedAndCaseSpoofedSubjects_When_ReadOnlyLookupIsRequested()
    {
        var actor = await SeedAsync(BusinessRoles.HeadOfficeUser, "Niamh");
        var rep = await SeedAsync(BusinessRoles.FieldSalesperson, "Aoife");
        await using var host = Host(); using var client = Client(host, Token(actor));
        foreach (var subjects in new string[]?[] { null, [], [" "], [" rep"], ["rep\n"], [new string('x', 451)], Enumerable.Repeat(rep, 2049).ToArray() })
        {
            using var response = await client.PostAsJsonAsync("/staff/directory/lookup", new StaffDirectoryLookupRequest(subjects));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var wrongCase = await client.PostAsJsonAsync("/staff/directory/lookup", new StaffDirectoryLookupRequest([rep.ToLowerInvariant()]));
        Assert.Empty((await wrongCase.Content.ReadFromJsonAsync<StaffDirectoryEntry[]>())!);
    }

    private async Task<string> SeedAsync(string role, string? name)
    {
        string subject = "STAFF-" + Guid.NewGuid().ToString("N");
        await databases.RunInScopeAsync(async services =>
        {
            var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string required in new[] { role, BusinessRoles.FieldSalesperson, BusinessRoles.SalesManager, BusinessRoles.HeadOfficeUser })
                if (!await roles.RoleExistsAsync(required)) Assert.True((await roles.CreateAsync(new IdentityRole(required))).Succeeded);
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = subject, UserName = "staff-" + subject,
                Email = "staff-" + subject + "@sales.local", FullName = name, LockoutEnabled = true };
            var created = await users.CreateAsync(user, "Password123!");
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
            seededSubjects.Add(subject);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        });
        return subject;
    }

    private WebApplicationFactory<Program> Host() => databases.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        services.PostConfigure<JwtBearerOptions>("StaffRoleLookup", options =>
        {
            var config = new OpenIdConnectConfiguration { Issuer = Issuer }; config.SigningKeys.Add(Key);
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
        })));
    private static HttpClient Client(WebApplicationFactory<Program> host, string token)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", token); return client;
    }
    private static string Token(string subject, string scope = "fieldsales.api", string audience = "fieldsales-api") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, audience,
            [new Claim("sub", subject), new Claim("scope", scope), new Claim("role", BusinessRoles.HeadOfficeUser)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)));
}
