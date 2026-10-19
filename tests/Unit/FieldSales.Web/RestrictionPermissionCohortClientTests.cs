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

// Approved WI-030 scenario 15: an inconsistent API reply cannot masquerade as a successful grant.
[Trait("Category", "Unit")]
public sealed class RestrictionPermissionCohortClientTests
{
    [Theory]
    [InlineData("valid")] [InlineData("empty")] [InlineData("granted")] [InlineData("removed")]
    [InlineData("archived")] [InlineData("inactive-held")]
    [InlineData("wrong-group")] [InlineData("null-group")] [InlineData("blank-group")]
    [InlineData("null-reps")] [InlineData("null-row")] [InlineData("duplicate-rep")]
    [InlineData("blank-rep")] [InlineData("granted-without-record")] [InlineData("foreign-record")]
    [InlineData("opposite-record")] [InlineData("grant-held")] [InlineData("grant-archived")]
    [InlineData("grant-inactive")] [InlineData("malformed-json")] [InlineData("null-json")]
    public async Task Should_FailClosed_When_TheApiReturnsAnInconsistentGroupPage(string kind)
    {
        Guid group = Guid.NewGuid();
        var entry = Change(group, "colm", 1);
        var row = new GroupPermissionRep(new("colm", "Colm"), true, false, null, true);
        var page = new GroupPermissionsPage(new(group, "Pharmacy-only medicines", false), [row]);
        page = kind switch
        {
            "empty" => page with { Reps = [] },
            "granted" => page with { Reps = [row with { Granted = true, LastChange = entry, CanGrant = false }] },
            "removed" => page with { Reps = [row with { LastChange = entry with { Change = RestrictionPermissionChangeKind.Removed } }] },
            "archived" => page with { Group = page.Group with { Archived = true }, Reps = [row with { CanGrant = false }] },
            "inactive-held" => page with { Reps = [row with { Active = false, Granted = true, LastChange = entry, CanGrant = false }] },
            "wrong-group" => page with { Group = page.Group with { RestrictionGroupId = Guid.NewGuid() } },
            "null-group" => page with { Group = null! },
            "blank-group" => page with { Group = page.Group with { Name = " " } },
            "null-reps" => page with { Reps = null! },
            "null-row" => page with { Reps = [null!] },
            "duplicate-rep" => page with { Reps = [row, row] },
            "blank-rep" => page with { Reps = [row with { Rep = new("colm", " ") }] },
            "granted-without-record" => page with { Reps = [row with { Granted = true, CanGrant = false }] },
            "foreign-record" => page with { Reps = [row with { Granted = true, LastChange = entry with { Rep = new("aoife", "Aoife") }, CanGrant = false }] },
            "opposite-record" => page with { Reps = [row with { LastChange = entry }] },
            "grant-held" => page with { Reps = [row with { Granted = true, LastChange = entry }] },
            "grant-archived" => page with { Group = page.Group with { Archived = true } },
            "grant-inactive" => page with { Reps = [row with { Active = false, Granted = true, LastChange = entry }] },
            _ => page
        };
        using var services = Services();
        using var handler = new Reply(page, kind, HttpMethod.Get);
        using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var client = Client(http, services);
        var result = await client.GroupPermissionsAsync(group, default);
        bool valid = kind is "valid" or "empty" or "granted" or "removed" or "archived" or "inactive-held";
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
        if (!valid) Assert.Null(result.Value);
        Assert.Equal("/coverage/permissions/groups/" + group, handler.Path);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("valid")] [InlineData("missing-change")] [InlineData("duplicate-id")]
    [InlineData("duplicate-sequence")] [InlineData("duplicate-rep")] [InlineData("foreign-rep")]
    [InlineData("wrong-group")] [InlineData("foreign-group-change")] [InlineData("removed")]
    [InlineData("wrong-reason")] [InlineData("different-actor")] [InlineData("different-time")]
    [InlineData("null-changes")] [InlineData("null-row")] [InlineData("empty-id")]
    [InlineData("invalid-sequence")] [InlineData("missing-actor")] [InlineData("self-change")]
    [InlineData("malformed-json")] [InlineData("null-json")] [InlineData("outage")]
    public async Task Should_FailClosed_When_TheApiReturnsAnInconsistentCohortResponse(string kind)
    {
        Guid group = Guid.NewGuid();
        var colm = Change(group, "colm", 1);
        var aoife = Change(group, "aoife", 2);
        var response = new CohortPermissionGrantResult(group, [colm, aoife]);
        response = kind switch
        {
            "missing-change" => response with { Changes = [colm] },
            "duplicate-id" => response with { Changes = [colm, aoife with { Id = colm.Id }] },
            "duplicate-sequence" => response with { Changes = [colm, aoife with { Sequence = colm.Sequence }] },
            "duplicate-rep" => response with { Changes = [colm, aoife with { Rep = colm.Rep }] },
            "foreign-rep" => response with { Changes = [colm, aoife with { Rep = new("brian", "Brian") }] },
            "wrong-group" => response with { RestrictionGroupId = Guid.NewGuid() },
            "foreign-group-change" => response with { Changes = [colm, aoife with { RestrictionGroupId = Guid.NewGuid() }] },
            "removed" => response with { Changes = [colm, aoife with { Change = RestrictionPermissionChangeKind.Removed }] },
            "wrong-reason" => response with { Changes = [colm, aoife with { Reason = "Another reason" }] },
            "different-actor" => response with { Changes = [colm, aoife with { Actor = new("orla", "Orla") }] },
            "different-time" => response with { Changes = [colm, aoife with { ChangedAt = aoife.ChangedAt.AddMinutes(1) }] },
            "null-changes" => response with { Changes = null! },
            "null-row" => response with { Changes = [colm, null!] },
            "empty-id" => response with { Changes = [colm, aoife with { Id = Guid.Empty }] },
            "invalid-sequence" => response with { Changes = [colm, aoife with { Sequence = 0 }] },
            "missing-actor" => response with { Changes = [colm, aoife with { Actor = null! }] },
            "self-change" => response with { Changes = [colm, aoife with { Actor = aoife.Rep }] },
            _ => response
        };
        using var services = Services();
        using var handler = new Reply(response, kind, HttpMethod.Post);
        using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var client = Client(http, services);
        var result = await client.GrantCohortPermissionAsync(group, new([new("colm", null), new("aoife", null)], "  Training complete  "), default);
        Assert.Equal(kind == "valid" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
        if (kind != "valid") Assert.Null(result.Value);
        Assert.Equal("/coverage/permissions/groups/" + group + "/grants", handler.Path);
        Assert.Equal(1, handler.Calls); // No retry of a grant, even when its reply cannot be trusted.
    }

    [Theory]
    [InlineData("valid")] [InlineData("empty")] [InlineData("duplicate")]
    [InlineData("archived")] [InlineData("null-row")] [InlineData("empty-id")]
    [InlineData("blank-name")] [InlineData("malformed-json")] [InlineData("null-json")]
    public async Task Should_FailClosed_When_TheApiReturnsAnInconsistentGroupChooser(string kind)
    {
        var row = new PermissionGroupChoice(Guid.NewGuid(), "Pharmacy-only medicines", false);
        PermissionGroupChoice[] groups = kind switch
        {
            "empty" => [], "duplicate" => [row, row], "archived" => [row with { Archived = true }],
            "null-row" => [null!], "empty-id" => [row with { RestrictionGroupId = Guid.Empty }],
            "blank-name" => [row with { Name = " " }], _ => [row]
        };
        using var services = Services();
        using var handler = new Reply(groups, kind, HttpMethod.Get);
        using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var result = await Client(http, services).PermissionGroupsAsync(default);
        Assert.Equal(kind is "valid" or "empty" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.Equal("/coverage/permissions/groups", handler.Path);
    }

    private static RestrictionPermissionChangeDetails Change(Guid group, string rep, long sequence) =>
        new(Guid.NewGuid(), sequence, new(rep, rep), group, "Pharmacy-only medicines", RestrictionPermissionChangeKind.Granted,
            new(2026, 9, 15, 14, 2, 30, TimeSpan.Zero), new("niamh", "Niamh Byrne"), "Training complete", "Granted by Niamh Byrne");
    private static CoverageApiClient Client(HttpClient http, IServiceProvider services) =>
        new(http, new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } });
    private static ServiceProvider Services() => new ServiceCollection().AddLogging().AddAuthentication("Test")
        .AddScheme<AuthenticationSchemeOptions, TokenHandler>("Test", _ => { }).Services.BuildServiceProvider();
    private sealed class Reply(object value, string kind, HttpMethod method) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Path = request.RequestUri!.AbsolutePath;
            Assert.Equal(method, request.Method);
            Assert.Equal("server-held-access-token", request.Headers.Authorization?.Parameter);
            if (kind == "outage") throw new HttpRequestException("Unavailable");
            HttpContent content = kind switch
            {
                "malformed-json" => new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json"),
                "null-json" => JsonContent.Create<object?>(null), _ => JsonContent.Create(value)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
    private sealed class TokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new() { Name = "access_token", Value = "server-held-access-token" }]);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity("Test")), properties, "Test")));
        }
    }
}
