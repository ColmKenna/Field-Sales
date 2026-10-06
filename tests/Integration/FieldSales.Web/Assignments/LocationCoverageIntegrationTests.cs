extern alias CatalogueApi;

using System.Data;
using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;

namespace FieldSales.Web.Tests;

public sealed class LocationCoverageIntegrationTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    [Theory]
    [InlineData(TerritoryLevel.Region, "Leinster")]
    [InlineData(TerritoryLevel.County, "Wicklow")]
    [InlineData(TerritoryLevel.Town, "Rathdrum")]
    [InlineData(TerritoryLevel.Location, "Murphy's Pharmacy")]
    public async Task Should_ReadSingleResolverAtEveryLevel_When_CoverageIsInheritedOrDirect(TerritoryLevel level, string name)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", level, Target(seed, level));
        app.Reads.Commands.Clear(); var page = await ReadAsync(api, seed.Shop.Id);
        Assert.Equal(("Colm", level, name), (page.Owner!.Rep.Name, page.Owner.Source.Level, page.Owner.SourceName));
        Assert.Equal((seed.Shop.Id, "Murphy's Pharmacy", seed.Town.Id, "Rathdrum"), (page.LocationId, page.Name, page.TownId, page.TownName));
        Assert.Equal(0, page.OtherUnassignedLocations); Assert.False(app.Staff.SawTransaction);
        Assert.All(app.Reads.Commands, isolation => Assert.Equal(IsolationLevel.Serializable, isolation));
        Assert.Equal((1, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_RenderAoifeViaRathdrumAndDirectColm_When_ManagerOpensM07()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        var town = await AssignAsync("aoife", TerritoryLevel.Town, seed.Town.Id);
        using var extra = await api.PostAsJsonAsync($"/directory/customers/{seed.Customer.Id}/locations", new CreateLocationRequest("Byrne's Chemist", seed.Town.Id));
        var chemist = (await extra.Content.ReadFromJsonAsync<LocationDetails>())!;
        await AssignAsync("colm", TerritoryLevel.Location, chemist.Id);
        SetRole(BusinessRoles.SalesManager); await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser();
        await SignInAsync(browser, BusinessRoles.SalesManager); var before = await CountsAsync();
        string html = await HtmlAsync(browser, Page(seed.Shop.Id));
        Assert.Contains("Primary:", html); Assert.Contains("Aoife (via Rathdrum)", WebUtility.HtmlDecode(html));
        Assert.Contains("Specialists:</strong><span></span>", html); Assert.Contains("History", html);
        Assert.DoesNotContain("Assignment history", html); Assert.DoesNotContain("Visit Dues", html); Assert.DoesNotContain("Last Call", html);
        Assert.DoesNotContain("test-access-token", html); Assert.Contains("Colm (assigned directly)", WebUtility.HtmlDecode(await HtmlAsync(browser, Page(chemist.Id))));
        Assert.Equal(before, await CountsAsync());
        // Resolver fallback remains current, without caching the rendered owner.
        await using (var scope = app.Api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().TerritoryAssignments.Where(row => row.Id == town.Id).ExecuteDeleteAsync();
        Assert.Contains("Colm (via Wicklow)", WebUtility.HtmlDecode(await HtmlAsync(browser, Page(seed.Shop.Id))));
        html = await HtmlAsync(browser, "/Coverage/Territory?repSubject=colm");
        Assert.Contains("href=\"" + Page(chemist.Id) + "\"", html);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task Should_ShowUnassignedAndTownFirstAffordances_When_ShopHasNoCoverage(int other)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, "Walsh's Shop");
        for (int i = 0; i < other; i++)
        {
            using var added = await api.PostAsJsonAsync($"/directory/customers/{seed.Customer.Id}/locations", new CreateLocationRequest("Other " + i, seed.Town.Id));
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        // An unassigned shop in a different Town is not counted.
        using var distant = await api.PostAsJsonAsync($"/directory/customers/{seed.Customer.Id}/locations", new CreateLocationRequest("Distant", seed.OtherTown.Id));
        Assert.Equal(HttpStatusCode.Created, distant.StatusCode);
        SetRole(BusinessRoles.SalesManager); Assert.Null((await ReadAsync(api, seed.Shop.Id)).Owner);
        Assert.Equal(other, (await ReadAsync(api, seed.Shop.Id)).OtherUnassignedLocations);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser, BusinessRoles.SalesManager);
        string html = await HtmlAsync(browser, Page(seed.Shop.Id)); string decoded = WebUtility.HtmlDecode(html);
        Assert.Contains("Unassigned", decoded); Assert.DoesNotContain("Assignment actions are not available yet.", decoded);
        Assert.True(decoded.IndexOf("Assign Rathdrum (Town)", StringComparison.Ordinal) < decoded.IndexOf("Assign just this shop", StringComparison.Ordinal));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "href=\"/Coverage/LocationChange/").Count);
        Assert.DoesNotContain("type=\"button\" disabled", html);
        if (other == 0) Assert.DoesNotContain("other Locations", html);
        else Assert.Contains($"{other} other {(other == 1 ? "Location" : "Locations")} in Rathdrum {(other == 1 ? "is" : "are")} unassigned", html);
        Assert.Contains("No assignment history yet.", await HtmlAsync(browser, HistoryPage(seed.Shop.Id)));
        Assert.Equal((0, 0), await CountsAsync());
    }

    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task Should_ReadAnyShopWithoutGrantingRepBookOrWriteAccess_When_CurrentStaffRoleAllowsView(string role)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("brian", TerritoryLevel.Town, seed.Town.Id); SetRole(role);
        Assert.Equal("Brian", (await ReadAsync(api, seed.Shop.Id)).Owner!.Rep.Name);
        using var history = await api.GetAsync(HistoryApi(seed.Shop.Id)); Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        if (role == BusinessRoles.SalesManager)
        {
            foreach (string path in new[] { $"/coverage/locations/{seed.Shop.Id}/owner", $"/coverage/locations/{seed.Shop.Id}/history", "/coverage/reps/brian/territory", "/directory/customers" })
            { using var denied = await api.GetAsync(path); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
            using var mutation = await api.PostAsJsonAsync("/coverage/assignments/preview", new AddTerritoryAssignmentRequest("brian", new(TerritoryLevel.County, seed.County.Id)));
            Assert.Equal(HttpStatusCode.Forbidden, mutation.StatusCode);
        }
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser, role);
        Assert.Contains("Brian (via Rathdrum)", WebUtility.HtmlDecode(await HtmlAsync(browser, Page(seed.Shop.Id))));
        if (role == BusinessRoles.HeadOfficeUser)
            Assert.Contains("Who covers this shop?", await HtmlAsync(browser, $"/HeadOffice/Locations/Detail/{seed.Shop.Id}"));
        // Reporting authority changes cannot broaden the existing book/write boundary.
        await using (var scope = app.Api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().RepReportingLines.Where(row => row.RepSubject == "colm").ExecuteUpdateAsync(set => set.SetProperty(row => row.ManagerSubject, "another-manager"));
        Assert.Equal("Brian", (await ReadAsync(api, seed.Shop.Id)).Owner!.Rep.Name);
        app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson); website.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        using var stale = await api.GetAsync(ReadApi(seed.Shop.Id)); Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
        using var staleHistory = await api.GetAsync(HistoryApi(seed.Shop.Id)); Assert.Equal(HttpStatusCode.Forbidden, staleHistory.StatusCode);
        using var cookie = await browser.GetAsync(Page(seed.Shop.Id)); Assert.Equal(HttpStatusCode.Redirect, cookie.StatusCode);
        Assert.DoesNotContain("Brian (via", await cookie.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("outage")] [InlineData("missing-owner")] [InlineData("invalid-label")]
    [InlineData("renamed")] [InlineData("inactive")]
    public async Task Should_UseTrustedCurrentOwnerOrFailClosed_When_DirectoryChanges(string change)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("aoife", TerritoryLevel.Town, seed.Town.Id);
        if (change == "outage") app.Staff.Unavailable = true;
        if (change == "missing-owner") app.Staff.Entries.Remove("aoife");
        if (change == "invalid-label") app.Staff.Entries["aoife"] = app.Staff.Entries["aoife"] with { DisplayName = " " };
        if (change == "renamed") app.Staff.Entries["aoife"] = app.Staff.Entries["aoife"] with { DisplayName = "Aoife Doyle" };
        if (change == "inactive") app.Staff.Entries["aoife"] = app.Staff.Entries["aoife"] with { Available = false };
        using var response = await api.GetAsync(ReadApi(seed.Shop.Id));
        bool valid = change is "renamed" or "inactive";
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        if (valid) Assert.Equal(change == "renamed" ? "Aoife Doyle" : "Aoife", (await response.Content.ReadFromJsonAsync<LocationCoveragePage>())!.Owner!.Rep.Name);
        Assert.Equal((1, 0), await CountsAsync()); Assert.False(app.Staff.SawTransaction);
    }

    [Fact]
    public async Task Should_RetainNewestFirstCapturedHistory_When_CurrentNamesChangeOrIdentityDirectoryFails()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        app.ClockOverride = new DateTimeOffset(2026, 9, 17, 14, 2, 0, TimeSpan.Zero);
        await SaveAssignmentAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        await SaveAssignmentAsync(api, "aoife", TerritoryLevel.Town, seed.Town.Id);
        var original = (await api.GetFromJsonAsync<LocationCoverageHistoryPage>(HistoryApi(seed.Shop.Id)))!;
        Assert.Equal(2, original.Entries.Count); Assert.True(original.Entries[0].Sequence > original.Entries[1].Sequence);
        Assert.Equal(original.Entries[0].ChangedAt, original.Entries[1].ChangedAt);
        app.Staff.Entries["aoife"] = app.Staff.Entries["aoife"] with { DisplayName = "Renamed Aoife" };
        app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { DisplayName = "Renamed Manager" };
        using var renamed = await api.PutAsJsonAsync($"/directory/geography/towns/{seed.Town.Id}/name", new RenameGeographyRequest("New Rathdrum", seed.Town.Version));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode); app.Staff.Unavailable = true; app.Staff.Calls = 0;
        var read = (await api.GetFromJsonAsync<LocationCoverageHistoryPage>(HistoryApi(seed.Shop.Id)))!;
        Assert.Equal(original.Entries, read.Entries); Assert.Equal("New Rathdrum", read.TownName); Assert.Equal(0, app.Staff.Calls);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser, BusinessRoles.HeadOfficeUser);
        string html = WebUtility.HtmlDecode(await HtmlAsync(browser, HistoryPage(seed.Shop.Id)));
        Assert.Contains("17 Sep 2026 14:02", html); Assert.Contains("Colm → Aoife — via Rathdrum assignment — by Niamh Byrne", html);
        Assert.Contains("Times are UTC", html); Assert.Contains("Back to Murphy's Pharmacy", html);
        Assert.True(html.IndexOf("Colm → Aoife", StringComparison.Ordinal) < html.IndexOf("Unassigned → Colm", StringComparison.Ordinal));
        Assert.DoesNotContain("Renamed Aoife", html); Assert.Equal(0, app.Staff.Calls); Assert.Equal((2, 2), await CountsAsync());
    }

    [Theory]
    [InlineData(BusinessRoles.FieldSalesperson)] [InlineData("SysAdmin")]
    public async Task Should_DenyBothNewReadRoutes_When_CurrentRoleIsNotManagerOrHeadOffice(string role)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        // Identity's current-roles contract returns business roles only. SysAdmin
        // alone therefore returns an empty list, despite the old token's HO role.
        app.Roles.SetRoles("niamh", BusinessRoles.Contains(role) ? [role] : []);
        foreach (string path in new[] { ReadApi(seed.Shop.Id), HistoryApi(seed.Shop.Id) })
        { using var denied = await api.GetAsync(path); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
    }

    [Fact]
    public async Task Should_DenyNewReadRoutes_When_BearerSubjectOrScopeIsMissing()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        foreach (string path in new[] { ReadApi(seed.Shop.Id), HistoryApi(seed.Shop.Id) })
        {
            api.DefaultRequestHeaders.Authorization = null;
            using var anonymous = await api.GetAsync(path); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: "openid"));
            using var scope = await api.GetAsync(path); Assert.Equal(HttpStatusCode.Forbidden, scope.StatusCode);
            api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(subject: ""));
            using var subject = await api.GetAsync(path); Assert.Equal(HttpStatusCode.Unauthorized, subject.StatusCode);
        }
        Assert.Equal((0, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndRejectWrites_When_LocationIsMissingOrReadRouteReceivesMutation()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        foreach (string path in new[] { ReadApi(Guid.NewGuid()), HistoryApi(Guid.NewGuid()) })
        { using var missing = await api.GetAsync(path); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); }
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        foreach (string path in new[] { ReadApi(seed.Shop.Id), HistoryApi(seed.Shop.Id) })
        { using var request = new HttpRequestMessage(method, path); using var denied = await api.SendAsync(request); Assert.Equal(HttpStatusCode.MethodNotAllowed, denied.StatusCode); }
        app.Roles.Unavailable = true;
        foreach (string path in new[] { ReadApi(seed.Shop.Id), HistoryApi(seed.Shop.Id) })
        { using var unavailable = await api.GetAsync(path); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode); }
        Assert.Equal((0, 0), await CountsAsync());
    }

    private void SetRole(string role)
    { app.Roles.SetRoles("niamh", role); app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [role] }; }
    private static string ReadApi(Guid id) => $"/coverage/locations/{id}/page";
    private static string HistoryApi(Guid id) => ReadApi(id) + "/history";
    private static string Page(Guid id) => $"/Coverage/Location/{id}";
    private static string HistoryPage(Guid id) => $"/Coverage/LocationHistory/{id}";
    private static async Task<LocationCoveragePage> ReadAsync(HttpClient api, Guid id) => (await api.GetFromJsonAsync<LocationCoveragePage>(ReadApi(id)))!;
    private static async Task SignInAsync(HttpClient browser, string role)
    { using var response = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); }
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    { using var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadAsStringAsync(); }
    private async Task<TerritoryAssignment> AssignAsync(string rep, TerritoryLevel level, Guid id)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var assignment = TerritoryAssignment.Create(rep, new(level, id)); db.TerritoryAssignments.Add(assignment); await db.SaveChangesAsync(); return assignment;
    }
    private static async Task SaveAssignmentAsync(HttpClient api, string rep, TerritoryLevel level, Guid id)
    {
        var request = new AddTerritoryAssignmentRequest(rep, new(level, id));
        using var previewResponse = await api.PostAsJsonAsync("/coverage/assignments/preview", request); Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<AssignmentImpactDetails>())!;
        using var saved = await api.PostAsJsonAsync("/coverage/assignments", request with { Confirmed = true, PreviewProof = preview.Proof }); Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
    }
    private async Task<(int, int)> CountsAsync()
    { await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); return (await db.TerritoryAssignments.CountAsync(), await db.AssignmentHistory.CountAsync()); }
    private async Task<Seed> SeedAsync(HttpClient api, string name = "Murphy's Pharmacy")
    {
        var region = await PlaceAsync(api, "regions", "Leinster"); var county = await PlaceAsync(api, "counties", "Wicklow", region.Id);
        var town = await PlaceAsync(api, "towns", "Rathdrum", county.Id); var other = await PlaceAsync(api, "towns", "Laragh", county.Id);
        using var response = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Customer", new(name, town.Id))); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = (await response.Content.ReadFromJsonAsync<CustomerDetails>())!; return new(region, county, town, other, customer, customer.Locations[0]);
    }
    private static Guid Target(Seed seed, TerritoryLevel level) => level switch
    { TerritoryLevel.Region => seed.Region.Id, TerritoryLevel.County => seed.County.Id, TerritoryLevel.Town => seed.Town.Id, _ => seed.Shop.Id };
    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parent = null)
    { using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent)); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!; }
    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem Town, GeographyItem OtherTown, CustomerDetails Customer, LocationSummary Shop);
}
