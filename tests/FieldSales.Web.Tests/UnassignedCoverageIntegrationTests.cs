extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;

namespace FieldSales.Web.Tests;

public sealed class UnassignedCoverageIntegrationTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    private const string ListUrl = "/Coverage/Unassigned";

    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task Should_GroupAllUnassignedShopsWithCustomersAndTownFirstActions_When_ManagerOpensList(string role)
    {
        var seed = await SeedAsync(4);
        // A shop covered by another manager's rep does not belong in this list.
        using (var api = app.CreateApiClient())
        {
            using var response = await api.PostAsJsonAsync($"/directory/customers/{seed.CustomerId}/locations", new CreateLocationRequest("Covered shop", seed.Town.Id));
            var covered = (await response.Content.ReadFromJsonAsync<LocationDetails>())!;
            await AssignAsync("brian", new(TerritoryLevel.Location, covered.Id));
        }
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web, role);
        SetRole(role); using var readApi = app.CreateApiClient(); app.Reads.Commands.Clear(); app.Staff.Calls = 0;
        var page = (await readApi.GetFromJsonAsync<UnassignedCoveragePage>("/coverage/unassigned"))!;
        Assert.Equal(5, page.Towns.Sum(town => town.Locations.Count));
        Assert.Equal(new[] { "Laragh", "Rathdrum" }, page.Towns.Select(town => town.Name));
        Assert.All(page.Towns.SelectMany(town => town.Locations), row => Assert.Equal("Walsh Wholesale", row.CustomerName));
        Assert.Equal(4, app.Reads.Commands.Count);
        Assert.All(app.Reads.Commands, isolation => Assert.Equal(System.Data.IsolationLevel.Serializable, isolation));
        Assert.Equal(0, app.Staff.Calls);
        string html = await HtmlAsync(browser, ListUrl); Capture("list", html);
        Assert.DoesNotContain("Covered shop", html); Assert.Contains("Customer: Walsh Wholesale", html);
        string row = Row(html, seed.Shop.Id); string decoded = WebUtility.HtmlDecode(row);
        Assert.Contains("4 Locations in Laragh are unassigned", decoded);
        Assert.True(decoded.IndexOf("Assign Laragh (Town) to...", StringComparison.Ordinal) < decoded.IndexOf("Assign just this shop to...", StringComparison.Ordinal));
        Assert.Contains("href=\"/Coverage/Unassigned\"", await HtmlAsync(browser, "/Coverage/Territory"));
        Assert.DoesNotContain("test-access-token", html); Assert.Equal((1, 0), await CountsAsync());
    }

    [Theory]
    [InlineData("Shop", 1, 5)] [InlineData("Town", 5, 1)]
    public async Task Should_ReturnToUpdatedListWithOwnerNotice_When_AssignmentFromRowIsConfirmed(string intent, int changed, int remaining)
    {
        var seed = await SeedAsync(5);
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, ListUrl);
        html = await HtmlAsync(browser, Action(Row(html, seed.Shop.Id), intent));
        Assert.Contains("href=\"/Coverage/Unassigned\">Cancel", html);
        var fields = AssignmentTransferIntegrationTests.Inputs(html); Set(fields, "RepSubject", "colm"); Set(fields, "Reason", "Cover the shops");
        string changeUrl = $"/Coverage/LocationChange/{seed.Shop.Id}";
        using var preview = await browser.PostAsync(changeUrl + "?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); html = await preview.Content.ReadAsStringAsync();
        Capture(intent.ToLowerInvariant() + "-preview", html);
        Assert.Contains("Confirm assignment", html); Assert.Contains("href=\"/Coverage/Unassigned\">Cancel", html);
        Assert.Equal((0, 0), await CountsAsync());
        Assert.Contains(changed + (changed == 1 ? " Location becomes" : " Locations become"), html);
        fields = AssignmentTransferIntegrationTests.Inputs(html); Set(fields, "Confirmed", "true"); Set(fields, "ReturnUrl", "https://untrusted.invalid");
        using var saved = await browser.PostAsync(changeUrl + "?handler=Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode); Assert.Equal(ListUrl, saved.Headers.Location!.OriginalString);
        Assert.Equal((1, changed), await CountsAsync());
        html = await HtmlAsync(browser, ListUrl); Capture(intent.ToLowerInvariant() + "-saved", html);
        Assert.Contains(intent == "Shop" ? "Colm (assigned directly)" : "Colm (via Laragh)", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain($"data-location-id=\"{seed.Shop.Id}\"", html);
        using var api = app.CreateApiClient(); var page = (await api.GetFromJsonAsync<UnassignedCoveragePage>("/coverage/unassigned"))!;
        Assert.Equal(remaining, page.Towns.Sum(town => town.Locations.Count));
        foreach (Guid id in seed.Locations)
        {
            var location = (await api.GetFromJsonAsync<LocationCoveragePage>($"/coverage/locations/{id}/page"))!;
            bool affected = intent == "Town" || id == seed.Shop.Id;
            Assert.Equal(affected ? "colm" : null, location.Owner?.Rep.Subject);
            if (affected) Assert.Equal(intent == "Town" ? TerritoryLevel.Town : TerritoryLevel.Location, location.Owner!.Source.Level);
        }
        var history = (await api.GetFromJsonAsync<LocationCoverageHistoryPage>($"/coverage/locations/{seed.Shop.Id}/page/history"))!;
        var entry = Assert.Single(history.Entries); Assert.Null(entry.PreviousOwner); Assert.Equal("colm", entry.NewOwner!.Rep.Subject);
        Assert.Equal("Cover the shops", entry.Reason); Assert.Equal("niamh", entry.Actor.Subject);
        using var replay = await browser.PostAsync(changeUrl + "?handler=Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal((1, changed), await CountsAsync());
    }

    [Fact]
    public async Task Should_PreserveExistingDirectOverride_When_TownIsAssignedFromList()
    {
        var seed = await SeedAsync(5); Guid carved = seed.Locations.Last(); await AssignAsync("brian", new(TerritoryLevel.Location, carved));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, ListUrl); Assert.DoesNotContain($"data-location-id=\"{carved}\"", html);
        html = await HtmlAsync(browser, Action(Row(html, seed.Shop.Id), "Town"));
        var fields = AssignmentTransferIntegrationTests.Inputs(html); Set(fields, "RepSubject", "colm");
        string url = $"/Coverage/LocationChange/{seed.Shop.Id}";
        using var preview = await browser.PostAsync(url + "?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); html = await preview.Content.ReadAsStringAsync(); Assert.Contains("4 Locations", html);
        fields = AssignmentTransferIntegrationTests.Inputs(html); Set(fields, "Confirmed", "true");
        using var saved = await browser.PostAsync(url + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var api = app.CreateApiClient(); var owner = (await api.GetFromJsonAsync<LocationCoveragePage>($"/coverage/locations/{carved}/page"))!.Owner!;
        Assert.Equal("brian", owner.Rep.Subject); Assert.Equal(TerritoryLevel.Location, owner.Source.Level); Assert.Equal((2, 4), await CountsAsync());
    }

    [Theory]
    [InlineData(TerritoryLevel.Region)] [InlineData(TerritoryLevel.County)]
    [InlineData(TerritoryLevel.Town)] [InlineData(TerritoryLevel.Location)]
    public async Task Should_ShowExactEmptyState_When_EveryShopHasAnOwnerAtAnyLevel(TerritoryLevel level)
    {
        var seed = await SeedAsync(1);
        if (level is TerritoryLevel.Region or TerritoryLevel.County)
            await AssignAsync("colm", new(level, level == TerritoryLevel.Region ? seed.Region.Id : seed.County.Id));
        else
        {
            await AssignAsync("colm", new(level, level == TerritoryLevel.Town ? seed.Town.Id : seed.Shop.Id));
            await AssignAsync("brian", new(level, level == TerritoryLevel.Town ? seed.OtherTown.Id : seed.OtherShop.Id));
        }
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var before = await CountsAsync(); string html = await HtmlAsync(browser, ListUrl); Capture("empty", html);
        Assert.Contains("All Locations have a responsible rep", html); Assert.DoesNotContain("Assign just this shop", html); Assert.Equal(before, await CountsAsync());
    }

    [Theory]
    [InlineData("town")] [InlineData("county")] [InlineData("region")]
    public async Task Should_KeepArchivedUnassignedShopsVisibleWithDisabledActions_When_HierarchyIsArchived(string level)
    {
        var seed = await SeedAsync(1);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            if (level == "town") await db.Towns.Where(row => row.Id == seed.Town.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.IsArchived, true));
            if (level == "county") await db.Counties.Where(row => row.Id == seed.County.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.IsArchived, true));
            if (level == "region") await db.Regions.Where(row => row.Id == seed.Region.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.IsArchived, true));
        }
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web); string row = Row(await HtmlAsync(browser, ListUrl), seed.Shop.Id);
        Assert.Contains("1 Location in Laragh is unassigned", row); Assert.Equal(2, Regex.Matches(row, "<button[^>]*disabled").Count);
        Assert.DoesNotContain("LocationChange", row); Assert.Contains("archived", row); Assert.Equal((0, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_SeparateSameNamedTownsByIdentityAndShowCounty_When_NamesCollide()
    {
        var seed = await SeedAsync(1); using var api = app.CreateApiClient();
        var county = await Place(api, "counties", "Wexford", seed.Region.Id); var town = await Place(api, "towns", "Laragh", county.Id);
        using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Other customer", new("Walsh's Shop", town.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web); string html = await HtmlAsync(browser, ListUrl);
        Assert.Contains("Wexford · Leinster", html); Assert.Contains("Wicklow · Leinster", html);
        Assert.Contains("Customer: Other customer", Row(html, customer.Locations[0].Id));
        Assert.Contains("Customer: Walsh Wholesale", Row(html, seed.Shop.Id));
        var page = (await api.GetFromJsonAsync<UnassignedCoveragePage>("/coverage/unassigned"))!;
        Assert.Equal(2, page.Towns.Count(row => row.Name == "Laragh")); Assert.Equal(3, page.Towns.Count);
    }

    [Theory]
    [InlineData("proof")] [InlineData("wrong-shop")] [InlineData("recipient")] [InlineData("held-town")] [InlineData("csrf")]
    public async Task Should_SaveNothing_When_ListEntryOrRecipientIsTamperedOrStale(string kind)
    {
        var seed = await SeedAsync(2); await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, ListUrl); string url = Action(Row(html, seed.Shop.Id), "Town");
        html = await HtmlAsync(browser, url); var fields = AssignmentTransferIntegrationTests.Inputs(html); Set(fields, "RepSubject", kind == "recipient" ? "brian" : "colm");
        if (kind == "proof") Set(fields, "EntryProof", "forged");
        if (kind == "held-town") await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        if (kind == "csrf") fields.RemoveAll(row => row.Key == "__RequestVerificationToken");
        var before = await CountsAsync(); Guid id = kind == "wrong-shop" ? seed.OtherShop.Id : seed.Shop.Id;
        using var response = await browser.PostAsync($"/Coverage/LocationChange/{id}?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(kind == "recipient" ? HttpStatusCode.OK : kind == "held-town" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, response.StatusCode);
        html = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("Confirm assignment", html); Assert.Equal(before, await CountsAsync());
    }

    [Theory]
    [InlineData(BusinessRoles.FieldSalesperson)] [InlineData("SysAdmin")] [InlineData("")]
    public async Task Should_DenyListAndExposeNoShops_When_ActorHasNoCoverageRole(string role)
    {
        await SeedAsync(1); SetRole(role); using var api = app.CreateApiClient();
        using var response = await api.GetAsync("/coverage/unassigned"); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Walsh", await response.Content.ReadAsStringAsync());
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web, role);
        using var denied = await browser.GetAsync(ListUrl); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.DoesNotContain("Walsh", await denied.Content.ReadAsStringAsync()); Assert.Equal((0, 0), await CountsAsync());
    }

    private void SetRole(string role) { app.Roles.SetRoles("niamh", BusinessRoles.Contains(role) ? [role] : []); app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = BusinessRoles.Contains(role) ? [role] : [] }; }
    private async Task<HttpClient> BrowserAsync(StaffWebsiteFactory web, string role = BusinessRoles.SalesManager)
    {
        SetRole(role); var browser = web.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(string.IsNullOrEmpty(role) ? "SysAdmin" : role));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        web.Roles.SetRoles("niamh", BusinessRoles.Contains(role) ? [role] : []); return browser;
    }
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    { using var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadAsStringAsync(); }
    private static string Row(string html, Guid id) => Regex.Match(html, $"<li data-location-id=\"{id}\">(.*?)</li>", RegexOptions.Singleline).Value;
    private static string Action(string row, string intent) => WebUtility.HtmlDecode(Regex.Matches(row, "<a[^>]*href=\"([^\"]*LocationChange[^\"]*)\"[^>]*>([^<]*)</a>")
        .Single(link => intent == "Town" ? link.Groups[2].Value.Contains("(Town)", StringComparison.Ordinal) : link.Groups[2].Value.Contains("just this shop", StringComparison.Ordinal)).Groups[1].Value);
    private static void Set(List<KeyValuePair<string, string>> fields, string name, string value) { fields.RemoveAll(row => row.Key == name); fields.Add(new(name, value)); }
    private async Task AssignAsync(string rep, TerritoryTarget target)
    { await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); db.TerritoryAssignments.Add(TerritoryAssignment.Create(rep, target)); await db.SaveChangesAsync(); }
    private async Task<(int, int)> CountsAsync()
    { await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); return (await db.TerritoryAssignments.CountAsync(), await db.AssignmentHistory.CountAsync()); }
    private async Task<Seed> SeedAsync(int locations)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var region = await Place(api, "regions", "Leinster");
        var county = await Place(api, "counties", "Wicklow", region.Id); var town = await Place(api, "towns", "Laragh", county.Id); var other = await Place(api, "towns", "Rathdrum", county.Id);
        using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Walsh Wholesale", new("Walsh's Shop", town.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        List<Guid> ids = [customer.Locations[0].Id];
        for (int i = 1; i < locations; i++)
        { using var added = await api.PostAsJsonAsync($"/directory/customers/{customer.Id}/locations", new CreateLocationRequest("Neighbour " + i, town.Id)); Assert.Equal(HttpStatusCode.Created, added.StatusCode); ids.Add((await added.Content.ReadFromJsonAsync<LocationDetails>())!.Id); }
        using var distant = await api.PostAsJsonAsync($"/directory/customers/{customer.Id}/locations", new CreateLocationRequest("Distant shop", other.Id));
        Assert.Equal(HttpStatusCode.Created, distant.StatusCode); var otherShop = (await distant.Content.ReadFromJsonAsync<LocationDetails>())!;
        return new(region, county, town, other, customer.Locations[0], otherShop, customer.Id, ids);
    }
    private static async Task<GeographyItem> Place(HttpClient api, string level, string name, Guid? parent = null)
    { using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent)); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!; }
    private static void Capture(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_UNASSIGNED_ARTIFACT_DIR"); if (string.IsNullOrEmpty(output)) return;
        System.IO.Directory.CreateDirectory(output); string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "src/FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>"); File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem Town, GeographyItem OtherTown,
        LocationSummary Shop, LocationDetails OtherShop, Guid CustomerId, IReadOnlyList<Guid> Locations);
}
