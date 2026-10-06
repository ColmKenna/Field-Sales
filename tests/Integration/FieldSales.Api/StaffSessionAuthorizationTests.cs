using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FieldSales.Api.Tests;

public sealed class StaffSessionAuthorizationTests
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(
        Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));

    [Fact]
    public async Task Should_RequireValidAudienceScopeAndBusinessRole_When_AccessingStaffEndpoint()
    {
        TestRoleLookup roleLookup = new();
        await using WebApplicationFactory<Program> factory = CreateFactory(roleLookup);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/session")).StatusCode);
        roleLookup.SetRoles(BusinessRoles.FieldSalesperson);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await GetWithToken(client, CreateToken("wrong-audience", "fieldsales.api", "Field Salesperson"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetWithToken(client, CreateToken("fieldsales-api", "other.scope", "Field Salesperson"))).StatusCode);
        roleLookup.SetRoles();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetWithToken(client, CreateToken("fieldsales-api", "fieldsales.api", "SysAdmin"))).StatusCode);

        roleLookup.SetRoles(BusinessRoles.FieldSalesperson);
        HttpResponseMessage allowed = await GetWithToken(client,
            CreateToken("fieldsales-api", "fieldsales.api", "Field Salesperson"));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        string body = await allowed.Content.ReadAsStringAsync();
        Assert.Contains("staff-1", body);
        Assert.Contains("Field Salesperson", body);
        Assert.DoesNotContain("SysAdmin", body);
    }

    [Fact]
    public async Task Should_DenyStaleApiToken_When_StaffRoleWasRemoved()
    {
        TestRoleLookup roleLookup = new();
        await using WebApplicationFactory<Program> factory = CreateFactory(roleLookup);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        string token = CreateToken("fieldsales-api", "fieldsales.api", BusinessRoles.HeadOfficeUser);

        roleLookup.SetRoles(BusinessRoles.HeadOfficeUser);
        Assert.Equal(HttpStatusCode.OK, (await GetWithToken(client, token)).StatusCode);

        roleLookup.SetRoles();
        Assert.Equal(HttpStatusCode.Forbidden, (await GetWithToken(client, token)).StatusCode);
    }

    [Fact]
    public async Task Should_ReturnUnavailable_When_CurrentRolesCannotBeChecked()
    {
        TestRoleLookup roleLookup = new() { Unavailable = true };
        await using WebApplicationFactory<Program> factory = CreateFactory(roleLookup);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await GetWithToken(client,
            CreateToken("fieldsales-api", "fieldsales.api", BusinessRoles.HeadOfficeUser));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("Head Office User", await response.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Program> CreateFactory(TestRoleLookup roleLookup) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Authentication:Authority"] = Issuer }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStaffRoleLookup>();
                services.AddSingleton<IStaffRoleLookup>(roleLookup);
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    OpenIdConnectConfiguration configuration = new() { Issuer = Issuer };
                    configuration.SigningKeys.Add(SigningKey);
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        });

    private static async Task<HttpResponseMessage> GetWithToken(HttpClient client, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/staff/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string CreateToken(string audience, string scope, string role)
    {
        JwtSecurityToken token = new(issuer: Issuer, audience: audience,
            claims: [new Claim("sub", "staff-1"), new Claim("scope", scope), new Claim("role", role)],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestRoleLookup : IStaffRoleLookup
    {
        private string[] _roles = [];
        public bool Unavailable { get; set; }

        public void SetRoles(params string[] roles) => _roles = roles;

        public Task<StaffRoleLookupResult> GetRolesAsync(
            string accessToken, string subject, CancellationToken cancellationToken) =>
            Unavailable
                ? throw new HttpRequestException("Test identity host is unavailable.")
                : Task.FromResult(StaffRoleLookupResult.Found(_roles));
    }
}
