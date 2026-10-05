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

[Trait("Category", "Unit")]

public sealed class LocationCoverageClientTests
{
    [Theory]
    [InlineData("valid")] [InlineData("unassigned")] [InlineData("wrong-id")] [InlineData("blank-name")]
    [InlineData("blank-town")] [InlineData("empty-town-id")] [InlineData("negative-count")]
    [InlineData("blank-rep")] [InlineData("invalid-level")] [InlineData("missing-source")]
    [InlineData("empty-source-id")] [InlineData("wrong-direct-id")] [InlineData("wrong-town-id")]
    [InlineData("malformed-json")] [InlineData("null-json")] [InlineData("outage")]
    public async Task Should_RejectInvalidOwnerWithoutFalseUnassigned_When_CoverageContractIsMalformed(string kind)
    {
        Guid id = Guid.NewGuid(), town = Guid.NewGuid();
        var owner = new ImpactOwnerDetails(new("aoife", "Aoife"), new(TerritoryLevel.Town, town), "Rathdrum");
        var page = new LocationCoveragePage(id, "Murphy's Pharmacy", town, "Rathdrum", owner, 0);
        page = kind switch
        {
            "unassigned" => page with { Owner = null },
            "wrong-id" => page with { LocationId = Guid.NewGuid() },
            "blank-name" => page with { Name = " " },
            "blank-town" => page with { TownName = null! },
            "empty-town-id" => page with { TownId = Guid.Empty },
            "negative-count" => page with { OtherUnassignedLocations = -1 },
            "blank-rep" => page with { Owner = owner with { Rep = new("aoife", " ") } },
            "invalid-level" => page with { Owner = owner with { Source = new((TerritoryLevel)999, town) } },
            "missing-source" => page with { Owner = owner with { Source = null! } },
            "empty-source-id" => page with { Owner = owner with { Source = new(TerritoryLevel.Town, Guid.Empty) } },
            "wrong-direct-id" => page with { Owner = owner with { Source = new(TerritoryLevel.Location, Guid.NewGuid()) } },
            "wrong-town-id" => page with { Owner = owner with { Source = new(TerritoryLevel.Town, Guid.NewGuid()) } },
            _ => page
        };
        using var services = Services(); var context = new DefaultHttpContext { RequestServices = services };
        using var handler = new ReplyHandler(page, kind); using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var client = new CoverageApiClient(http, new HttpContextAccessor { HttpContext = context });
        var result = await client.LocationAsync(id, default); bool valid = kind is "valid" or "unassigned";
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status); Assert.Equal(valid, result.Success);
        if (!valid) Assert.Null(result.Value);
        Assert.Equal($"/coverage/locations/{id}/page", handler.Path); Assert.Equal("server-held-access-token", handler.Token);
    }

    [Theory]
    [InlineData("valid")] [InlineData("empty")] [InlineData("wrong-id")] [InlineData("null-entries")]
    [InlineData("null-entry")] [InlineData("foreign-entry")] [InlineData("duplicate-sequence")]
    [InlineData("duplicate-id")] [InlineData("ascending")] [InlineData("invalid-sequence")]
    [InlineData("missing-actor")] [InlineData("blank-actor")] [InlineData("no-owners")]
    [InlineData("same-owner")] [InlineData("invalid-source")] [InlineData("foreign-direct-source")]
    [InlineData("invalid-cause")] [InlineData("invalid-date")] [InlineData("empty-operation")]
    public async Task Should_RejectMalformedHistory_When_EntriesAreNotTrustedConsistentNewestFirstData(string kind)
    {
        Guid id = Guid.NewGuid(); var row = Entry(id, 2); var older = Entry(id, 1);
        var page = new LocationCoverageHistoryPage(id, "Murphy's Pharmacy", "Rathdrum", [row, older]);
        page = kind switch
        {
            "empty" => page with { Entries = [] },
            "wrong-id" => page with { LocationId = Guid.NewGuid(), Entries = [] },
            "null-entries" => page with { Entries = null! },
            "null-entry" => page with { Entries = [null!] },
            "foreign-entry" => page with { Entries = [row with { LocationId = Guid.NewGuid() }] },
            "duplicate-sequence" => page with { Entries = [row, older with { Sequence = row.Sequence }] },
            "duplicate-id" => page with { Entries = [row, older with { Id = row.Id }] },
            "ascending" => page with { Entries = [older, row] },
            "invalid-sequence" => page with { Entries = [row with { Sequence = 0 }] },
            "missing-actor" => page with { Entries = [row with { Actor = null! }] },
            "blank-actor" => page with { Entries = [row with { Actor = new("niamh", " ") }] },
            "no-owners" => page with { Entries = [row with { PreviousOwner = null, NewOwner = null }] },
            "same-owner" => page with { Entries = [row with { PreviousOwner = row.NewOwner }] },
            "invalid-source" => page with { Entries = [row with { NewOwner = row.NewOwner! with { Source = null! } }] },
            "foreign-direct-source" => page with { Entries = [row with { NewOwner = row.NewOwner! with { Source = row.NewOwner.Source with { Target = new(TerritoryLevel.Location, Guid.NewGuid()) } } }] },
            "invalid-cause" => page with { Entries = [row with { Cause = (OwnershipChangeCause)999 }] },
            "invalid-date" => page with { Entries = [row with { ChangedAt = default }] },
            "empty-operation" => page with { Entries = [row with { OperationId = Guid.Empty }] },
            _ => page
        };
        using var services = Services(); var context = new DefaultHttpContext { RequestServices = services };
        using var handler = new ReplyHandler(page, kind); using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var client = new CoverageApiClient(http, new HttpContextAccessor { HttpContext = context });
        var result = await client.LocationHistoryAsync(id, default); bool valid = kind is "valid" or "empty";
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status); Assert.Equal(valid, result.Success);
        if (!valid) Assert.Null(result.Value);
        Assert.Equal($"/coverage/locations/{id}/page/history", handler.Path);
    }

    [Theory]
    [InlineData(OwnershipChangeCause.TerritoryAssignment, "via Rathdrum assignment")]
    [InlineData(OwnershipChangeCause.DirectLocationAssignment, "direct Location assignment")]
    [InlineData(OwnershipChangeCause.GeographyChange, "geography change — via Rathdrum")]
    public void Should_PresentCapturedCauseAndReason_When_HistoryIsFormatted(OwnershipChangeCause cause, string text)
    {
        var row = Entry(Guid.NewGuid(), 1) with { Cause = cause, Reason = "Agreed cover", PreviousOwner = null };
        Assert.Equal($"Unassigned → Aoife — {text} — by M. Byrne — Agreed cover", LocationHistoryPresentation.Change(row));
        Assert.Equal("17 Sep 2026 14:02", LocationHistoryPresentation.Timestamp(row.ChangedAt));
        row = row with { PreviousOwner = row.NewOwner, NewOwner = null, Reason = null };
        Assert.StartsWith("Aoife → Unassigned", LocationHistoryPresentation.Change(row));
    }

    [Fact]
    public void Should_ShowExampleAndConvertOffsetToUtc_When_HistoryHasCapturedNames()
    {
        var row = Entry(Guid.NewGuid(), 1);
        Assert.Equal("Colm → Aoife — via Rathdrum assignment — by M. Byrne", LocationHistoryPresentation.Change(row));
        Assert.Equal("17 Sep 2026 14:02", LocationHistoryPresentation.Timestamp(new(2026, 9, 17, 15, 2, 0, TimeSpan.FromHours(1))));
    }

    [Fact]
    public async Task Should_PropagateCallerCancellation_When_RequestIsCancelled()
    {
        using var services = Services(); var context = new DefaultHttpContext { RequestServices = services };
        using var handler = new CancelHandler(); using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var client = new CoverageApiClient(http, new HttpContextAccessor { HttpContext = context });
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LocationAsync(Guid.NewGuid(), cancellation.Token));
    }

    internal static AssignmentHistoryDetails Entry(Guid location, long sequence)
    {
        var source = new CoverageSourceDetails(Guid.NewGuid(), new(TerritoryLevel.Town, Guid.NewGuid()), "Rathdrum");
        return new(Guid.NewGuid(), sequence, Guid.NewGuid(), location, "Murphy's Pharmacy",
            new(2026, 9, 17, 14, 2, 0, TimeSpan.Zero), new("niamh", "M. Byrne"),
            new(new("colm", "Colm"), source), new(new("aoife", "Aoife"), source), OwnershipChangeCause.TerritoryAssignment, null, "Captured original display");
    }
    private static ServiceProvider Services() => new ServiceCollection().AddLogging().AddAuthentication("Test")
        .AddScheme<AuthenticationSchemeOptions, TokenHandler>("Test", _ => { }).Services.BuildServiceProvider();
    private sealed class ReplyHandler(object value, string kind) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Token { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method); Path = request.RequestUri!.AbsolutePath; Token = request.Headers.Authorization?.Parameter;
            if (kind == "outage") throw new HttpRequestException("Unavailable");
            HttpContent content = kind switch { "malformed-json" => new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json"), "null-json" => JsonContent.Create<object?>(null), _ => JsonContent.Create(value) };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
    private sealed class CancelHandler : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromCanceled<HttpResponseMessage>(ct); }
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
