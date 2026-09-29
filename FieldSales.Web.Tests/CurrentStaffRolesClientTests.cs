using System.Net;
using FieldSales.StaffAccess;
using Microsoft.Extensions.Configuration;

namespace FieldSales.Web.Tests;

public sealed class CurrentStaffRolesClientTests
{
    [Fact]
    public async Task Should_ReadCurrentRolesForTheTokenSubject_When_IdentityResponds()
    {
        using HttpClient http = new(new ReplyHandler(request =>
        {
            Assert.Equal("https://localhost:7201/staff/current-roles", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("server-held-token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"subject\":\"staff-1\",\"roles\":[\"Sales Manager\"]}")
            };
        }));

        HttpStaffRoleLookup lookup = new(http, Configuration());
        StaffRoleLookupResult result = await lookup.GetRolesAsync(
            "server-held-token", "staff-1", CancellationToken.None);

        Assert.True(result.TokenAccepted);
        Assert.Equal([BusinessRoles.SalesManager], result.Roles);
    }

    [Fact]
    public async Task Should_RejectARoleResponseForAnotherSubject_When_TokenIsUsed()
    {
        using HttpClient http = new(new ReplyHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"subject\":\"other-staff\",\"roles\":[\"Head Office User\"]}")
        }));
        HttpStaffRoleLookup lookup = new(http, Configuration());

        await Assert.ThrowsAsync<InvalidDataException>(() => lookup.GetRolesAsync(
            "server-held-token", "staff-1", CancellationToken.None));
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://localhost:7201"
        }).Build();

    private sealed class ReplyHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(reply(request));
    }
}
