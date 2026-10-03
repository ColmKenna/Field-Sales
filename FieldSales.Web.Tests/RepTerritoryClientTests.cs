using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FieldSales.Web.Tests;

public sealed class RepTerritoryClientTests
{
    [Theory]
    [InlineData("valid")] [InlineData("wrong-rep")] [InlineData("negative")]
    [InlineData("duplicate-town")] [InlineData("duplicate-assignment")] [InlineData("null-town")]
    [InlineData("invalid-target")] [InlineData("inflated-primary")] [InlineData("inflated-carveout")]
    [InlineData("incorrect-town-total")]
    public async Task Should_RejectMalformedTerritoryWithoutRendering_When_ApiContractIsInvalid(string kind)
    {
        using var services = new ServiceCollection().AddLogging().AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TokenHandler>("Test", _ => { }).Services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var town = new TerritoryTown(Guid.NewGuid(), "Rathdrum", false, 23, new("aoife", "Aoife"), [new(new("aoife", "Aoife"), 23)]);
        var row = new RepTerritoryAssignment(new(new(Guid.NewGuid(), "colm", new(TerritoryLevel.County, Guid.NewGuid()), "AAAA"), "Wicklow"),
            "Leinster", false, 23, [new(new("aoife", "Aoife"), 23)], [town]);
        var page = new RepTerritoryPage(new("colm", "Colm"), new("niamh", "Niamh"), 0, [row]);
        page = kind switch
        {
            "wrong-rep" => page with { Rep = new("brian", "Brian"), Assignments = [] },
            "negative" => page with { PrimaryLocations = -1 },
            "duplicate-town" => page with { Assignments = [row with { Towns = [town, town] }] },
            "duplicate-assignment" => page with { Assignments = [row, row] },
            "null-town" => page with { Assignments = [row with { Towns = [null!] }] },
            "invalid-target" => page with { Assignments = [row with { Assignment = row.Assignment with
                { Assignment = row.Assignment.Assignment with { Target = new((TerritoryLevel)999, Guid.NewGuid()) } } }] },
            "inflated-primary" => page with { PrimaryLocations = 24 },
            "inflated-carveout" => page with { Assignments = [row with { CarveOuts = [new(new("aoife", "Aoife"), 24)] }] },
            "incorrect-town-total" => page with { Assignments = [row with { Towns = [town with { Locations = 24 }] }] },
            _ => page
        };
        using var handler = new ReplyHandler(page); using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var client = new CoverageApiClient(http, new HttpContextAccessor { HttpContext = context });
        var result = await client.TerritoryAsync("colm", default);
        Assert.Equal(kind == "valid" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.Equal(kind == "valid", result.Success);
        if (kind != "valid") Assert.Null(result.Value);
    }

    private sealed class ReplyHandler(RepTerritoryPage page) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("/coverage/reps/colm/territory", request.RequestUri!.AbsolutePath);
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("server-held-access-token", request.Headers.Authorization!.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(page) });
        }
    }
    private sealed class TokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            AuthenticationProperties properties = new(); properties.StoreTokens([new() { Name = "access_token", Value = "server-held-access-token" }]);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity("Test")), properties, "Test")));
        }
    }
}
