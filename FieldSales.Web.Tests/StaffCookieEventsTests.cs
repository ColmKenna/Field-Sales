using System.Net;
using System.Security.Claims;
using System.Text;
using FieldSales.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace FieldSales.Web.Tests;

public sealed class StaffCookieEventsTests
{
    [Fact]
    public async Task ExpiringApiTokenIsRefreshedInsideServerSession()
    {
        string? posted = null;
        using HttpClient client = new(new ReplyHandler(async request =>
        {
            Assert.Equal("https://localhost:7201/connect/token", request.RequestUri?.ToString());
            posted = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600}")
            };
        }));
        StaffCookieEvents events = new(new OneClientFactory(client), new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://localhost:7201",
                ["Authentication:ClientSecret"] = "server-secret"
            }).Build(), TimeProvider.System);
        AuthenticationProperties properties = new();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "old-access" },
            new AuthenticationToken { Name = "refresh_token", Value = "old-refresh" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("o") }
        ]);
        AuthenticationTicket ticket = new(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "staff-1")], "Cookies")),
            properties, CookieAuthenticationDefaults.AuthenticationScheme);
        CookieValidatePrincipalContext context = new(new DefaultHttpContext(),
            new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null,
                typeof(CookieAuthenticationHandler)), new CookieAuthenticationOptions(), ticket);

        await events.ValidatePrincipal(context);

        Assert.True(context.ShouldRenew);
        Assert.Equal("new-access", context.Properties.GetTokenValue("access_token"));
        Assert.Equal("new-refresh", context.Properties.GetTokenValue("refresh_token"));
        Assert.True(DateTimeOffset.Parse(context.Properties.GetTokenValue("expires_at")!) > DateTimeOffset.UtcNow);
        Assert.Contains("grant_type=refresh_token", posted);
        Assert.Contains("refresh_token=old-refresh", posted);
        Assert.Contains("client_secret=server-secret", posted);
    }

    private sealed class OneClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class ReplyHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => reply(request);
    }
}
