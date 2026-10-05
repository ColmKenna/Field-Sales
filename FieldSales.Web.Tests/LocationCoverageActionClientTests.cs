using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

public sealed class LocationCoverageActionClientTests
{
    [Theory]
    [InlineData("valid")] [InlineData("unassigned")] [InlineData("direct")] [InlineData("wrong-id")]
    [InlineData("null-location")] [InlineData("blank-name")] [InlineData("empty-town")]
    [InlineData("bad-owner")] [InlineData("bad-source")] [InlineData("wrong-town")]
    [InlineData("missing-assignment")] [InlineData("empty-assignment")] [InlineData("negative-count")]
    [InlineData("zero-count")] [InlineData("assigned-town-action")] [InlineData("direct-transfer")]
    [InlineData("direct-count")] [InlineData("unassigned-source")] [InlineData("unassigned-count")]
    [InlineData("unassigned-transfer")] [InlineData("unassigned-flags")] [InlineData("malformed-json")]
    public async Task Should_RejectUntrustedActionContextWithoutShowingCoverage_When_ApiContractIsMalformed(string kind)
    {
        Guid id = Guid.NewGuid(), town = Guid.NewGuid();
        var page = new LocationCoveragePage(id, "Murphy's Pharmacy", town, "Rathdrum",
            new(new("aoife", "Aoife"), new(TerritoryLevel.Town, town), "Rathdrum"), 0);
        var actions = new LocationCoverageActions(page, Guid.NewGuid(), 23, false, true, true);
        if (kind.StartsWith("unassigned", StringComparison.Ordinal)) actions = new(page with { Owner = null }, null, 0, true, true, false);
        if (kind.StartsWith("direct", StringComparison.Ordinal)) actions = new(page with { Owner = page.Owner! with { Source = new(TerritoryLevel.Location, id) } }, Guid.NewGuid(), 1, false, true, false);
        actions = kind switch
        {
            "wrong-id" => actions with { Location = page with { LocationId = Guid.NewGuid() } },
            "null-location" => actions with { Location = null! },
            "blank-name" => actions with { Location = page with { Name = " " } },
            "empty-town" => actions with { Location = page with { TownId = Guid.Empty } },
            "bad-owner" => actions with { Location = page with { Owner = page.Owner! with { Rep = new("aoife", " ") } } },
            "bad-source" => actions with { Location = page with { Owner = page.Owner! with { Source = new((TerritoryLevel)99, town) } } },
            "wrong-town" => actions with { Location = page with { Owner = page.Owner! with { Source = new(TerritoryLevel.Town, Guid.NewGuid()) } } },
            "missing-assignment" => actions with { SourceAssignmentId = null },
            "empty-assignment" => actions with { SourceAssignmentId = Guid.Empty },
            "negative-count" => actions with { SourceLocations = -1 },
            "zero-count" => actions with { SourceLocations = 0 },
            "assigned-town-action" => actions with { CanAssignTown = true },
            "direct-transfer" => actions with { CanTransferSource = true },
            "direct-count" => actions with { SourceLocations = 2 },
            "unassigned-source" => actions with { SourceAssignmentId = Guid.NewGuid() },
            "unassigned-count" => actions with { SourceLocations = 1 },
            "unassigned-transfer" => actions with { CanTransferSource = true },
            "unassigned-flags" => actions with { CanChangeShop = false },
            _ => actions
        };
        using var handler = new Reply(actions, kind); await using var app = new StaffWebsiteFactory(handler); using var browser = app.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(BusinessRoles.SalesManager)); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var response = await browser.GetAsync($"/Coverage/Location/{id}");
        bool valid = kind is "valid" or "unassigned" or "direct";
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync();
        if (valid) Assert.Contains("Primary:", html); else { Assert.DoesNotContain("Primary:", html); Assert.DoesNotContain("Unassigned", html); }
        Assert.Equal($"/coverage/locations/{id}/page/actions", handler.Path); Assert.Equal(1, handler.Calls);
    }
    private sealed class Reply(LocationCoverageActions actions, string kind) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter);
            Calls++; Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = kind == "malformed-json"
                ? new StringContent("{bad", System.Text.Encoding.UTF8, "application/json") : JsonContent.Create(actions) });
        }
    }
}
