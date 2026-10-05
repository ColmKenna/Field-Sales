using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

public sealed class UnassignedCoveragePageTests
{
    [Theory]
    [InlineData("valid")] [InlineData("empty")] [InlineData("null-towns")] [InlineData("null-town")]
    [InlineData("empty-town-id")] [InlineData("blank-town")] [InlineData("blank-county")] [InlineData("blank-region")]
    [InlineData("empty-group")] [InlineData("null-locations")] [InlineData("null-location")]
    [InlineData("empty-customer")] [InlineData("blank-customer")] [InlineData("null-actions")]
    [InlineData("assigned")] [InlineData("wrong-town")] [InlineData("wrong-town-name")] [InlineData("wrong-count")]
    [InlineData("duplicate-town")] [InlineData("duplicate-location")] [InlineData("inconsistent-actions")]
    [InlineData("bad-json")] [InlineData("unavailable")]
    public async Task Should_RejectInvalidListWithoutShowingFalseEmptyState_When_ApiReplyIsUntrusted(string kind)
    {
        Guid townId = Guid.NewGuid(), id = Guid.NewGuid();
        var actions = new LocationCoverageActions(new(id, "Walsh's Shop", townId, "Laragh", null, 0), null, 0, true, true, false);
        var row = new UnassignedLocation(Guid.NewGuid(), "Customer", actions);
        var town = new UnassignedTown(townId, "Laragh", "Wicklow", "Leinster", [row]);
        row = kind switch
        {
            "empty-customer" => row with { CustomerId = Guid.Empty },
            "blank-customer" => row with { CustomerName = " " },
            "null-actions" => row with { Actions = null! },
            "assigned" => row with { Actions = actions with { Location = actions.Location with { Owner = new(new("colm", "Colm"), new(TerritoryLevel.Town, townId), "Laragh") }, SourceAssignmentId = Guid.NewGuid(), SourceLocations = 1, CanAssignTown = false } },
            "wrong-town" => row with { Actions = actions with { Location = actions.Location with { TownId = Guid.NewGuid() } } },
            "wrong-town-name" => row with { Actions = actions with { Location = actions.Location with { TownName = "Rathdrum" } } },
            "wrong-count" => row with { Actions = actions with { Location = actions.Location with { OtherUnassignedLocations = 10 } } },
            "inconsistent-actions" => row with { Actions = actions with { CanAssignTown = false } },
            _ => row
        };
        town = town with { Locations = [row] };
        town = kind switch
        {
            "empty-town-id" => town with { Id = Guid.Empty }, "blank-town" => town with { Name = " " },
            "blank-county" => town with { CountyName = " " }, "blank-region" => town with { RegionName = " " },
            "empty-group" => town with { Locations = [] }, "null-locations" => town with { Locations = null! },
            "null-location" => town with { Locations = [null!] },
            "duplicate-location" => town with { Locations = [row with { Actions = actions with { Location = actions.Location with { OtherUnassignedLocations = 1 } } }, row with { Actions = actions with { Location = actions.Location with { OtherUnassignedLocations = 1 } } }] },
            _ => town
        };
        var page = new UnassignedCoveragePage(kind switch
        { "empty" => [], "null-towns" => null!, "null-town" => [null!], "duplicate-town" => [town, town], _ => [town] });
        using var handler = new ListReply(page, kind); await using var web = new StaffWebsiteFactory(handler); using var browser = await BrowserAsync(web);
        using var response = await browser.GetAsync("/Coverage/Unassigned");
        bool valid = kind is "valid" or "empty";
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync();
        if (kind == "empty") Assert.Contains("All Locations have a responsible rep", html);
        else if (kind == "valid") Assert.Contains("Walsh", html);
        else { Assert.DoesNotContain("All Locations have a responsible rep", html); Assert.DoesNotContain("Walsh", html); }
        // Transport unavailability uses the existing GET resilience policy.
        // The acceptance boundary is an unavailable page, never a false empty list.
        Assert.True(handler.Calls > 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_EncodeLabelsAndRetainKeyboardActionOrder_When_ListHasLongOrMarkupLabels(bool markup)
    {
        string name = markup ? "<script>Walsh</script>" : new('L', 200);
        string townName = markup ? "<img>Laragh" : new('T', 200);
        string customer = markup ? "<script>Customer</script>" : new('C', 200);
        Guid townId = Guid.NewGuid();
        var page = new UnassignedCoveragePage([new(townId, townName, "Wicklow", "Leinster",
            [new(Guid.NewGuid(), customer, new(new(Guid.NewGuid(), name, townId, townName, null, 0), null, 0, true, true, false))])]);
        using var handler = new ListReply(page, "valid"); await using var web = new StaffWebsiteFactory(handler); using var browser = await BrowserAsync(web);
        using var response = await browser.GetAsync("/Coverage/Unassigned"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync(); Capture(markup ? "markup" : "long", html);
        Assert.Contains(name, WebUtility.HtmlDecode(html)); Assert.Contains(customer, WebUtility.HtmlDecode(html));
        var links = Regex.Matches(html, "<a[^>]*href=\"([^\"]*LocationChange[^\"]*)\"[^>]*>([^<]*)</a>");
        Assert.Equal(2, links.Count); Assert.Contains("(Town) to...", links[0].Groups[2].Value); Assert.Contains("just this shop", links[1].Groups[2].Value);
        Assert.StartsWith("/Coverage/LocationChange/", WebUtility.HtmlDecode(links[0].Groups[1].Value));
        Assert.DoesNotContain("test-access-token", html); Assert.DoesNotContain("<script>Walsh", html); Assert.DoesNotContain("<img>Laragh", html);
    }

    private static async Task<HttpClient> BrowserAsync(StaffWebsiteFactory web)
    {
        web.Roles.SetRoles("niamh", BusinessRoles.SalesManager); var browser = web.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(BusinessRoles.SalesManager)); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode); return browser;
    }
    private sealed class ListReply(UnassignedCoveragePage page, string kind) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("/coverage/unassigned", request.RequestUri!.AbsolutePath);
            Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter); Calls++;
            return Task.FromResult(new HttpResponseMessage(kind == "unavailable" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            { Content = kind == "bad-json" ? new StringContent("{bad", System.Text.Encoding.UTF8, "application/json") : JsonContent.Create(page) });
        }
    }
    private static void Capture(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_UNASSIGNED_ARTIFACT_DIR"); if (string.IsNullOrEmpty(output)) return;
        System.IO.Directory.CreateDirectory(output); string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>"); File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
}
