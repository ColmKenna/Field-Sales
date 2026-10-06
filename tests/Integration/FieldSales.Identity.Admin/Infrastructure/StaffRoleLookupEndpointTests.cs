using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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

[Collection(IdentityAdminSqlServerCollection.Name)]
public sealed class StaffRoleLookupEndpointTests(IdentityAdminSqlServerFactory databases)
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(
        Encoding.UTF8.GetBytes("test-only-staff-role-lookup-key-32bytes"));

    [Fact]
    public async Task CurrentRolesFollowIdentityMembership_When_AWebsiteRoleIsRemoved()
    {
        string subject = Guid.NewGuid().ToString("N");
        await databases.RunInScopeAsync(async services =>
        {
            RoleManager<IdentityRole> roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in new[] { BusinessRoles.SalesManager, BusinessRoles.HeadOfficeUser })
                if (!await roleManager.RoleExistsAsync(role))
                    Assert.True((await roleManager.CreateAsync(new IdentityRole(role))).Succeeded);

            UserManager<ApplicationUser> users = services.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = new()
            {
                Id = subject,
                UserName = $"staff-{subject}@sales.local",
                Email = $"staff-{subject}@sales.local"
            };
            Assert.True((await users.CreateAsync(user, "Password123!")).Succeeded);
            Assert.True((await users.AddToRolesAsync(user,
                [BusinessRoles.SalesManager, BusinessRoles.HeadOfficeUser])).Succeeded);
        });

        await using WebApplicationFactory<Program> host = databases.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(
                "StaffRoleLookup", options =>
                {
                    OpenIdConnectConfiguration configuration = new() { Issuer = Issuer };
                    configuration.SigningKeys.Add(SigningKey);
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                })));
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        string token = CreateToken(subject, "fieldsales-api", "fieldsales.api");

        using HttpResponseMessage before = await GetWithTokenAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        CurrentStaffRolesResponse? initial = await before.Content.ReadFromJsonAsync<CurrentStaffRolesResponse>();
        Assert.Equal(subject, initial?.Subject);
        Assert.Contains(BusinessRoles.HeadOfficeUser, initial!.Roles);

        await databases.RunInScopeAsync(async services =>
        {
            UserManager<ApplicationUser> users = services.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = (await users.FindByIdAsync(subject))!;
            Assert.True((await users.RemoveFromRoleAsync(user, BusinessRoles.HeadOfficeUser)).Succeeded);
        });

        using HttpResponseMessage after = await GetWithTokenAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        CurrentStaffRolesResponse? current = await after.Content.ReadFromJsonAsync<CurrentStaffRolesResponse>();
        Assert.NotNull(current);
        Assert.Equal([BusinessRoles.SalesManager], current.Roles);

        using HttpResponseMessage wrongAudience = await GetWithTokenAsync(
            client, CreateToken(subject, "wrong-audience", "fieldsales.api"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        using HttpResponseMessage wrongScope = await GetWithTokenAsync(
            client, CreateToken(subject, "fieldsales-api", "other.scope"));
        Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetWithTokenAsync(HttpClient client, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/staff/current-roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string CreateToken(string subject, string audience, string scope)
    {
        JwtSecurityToken token = new(issuer: Issuer, audience: audience,
            claims:
            [
                new Claim("sub", subject),
                new Claim("scope", scope),
                new Claim("role", BusinessRoles.HeadOfficeUser)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
