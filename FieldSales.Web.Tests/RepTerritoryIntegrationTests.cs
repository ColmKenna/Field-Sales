extern alias CatalogueApi;

using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;

namespace FieldSales.Web.Tests;

public sealed class RepTerritoryIntegrationTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    private const string Page = "/Coverage/Territory";
    private const string Review = "/Coverage/Assignments";

    [Fact]
    public async Task Should_Show140TotalAnd117Primary_When_23TownLocationsAreCarvedOut()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 23, 117);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync("aoife", TerritoryLevel.Town, seed.Town.Id);
        app.Reads.Commands.Clear(); var page = await ReadAsync(api);
        Assert.Equal("Colm", page.Rep.Name); Assert.Equal("Niamh Byrne", page.Manager!.Name);
        Assert.Equal(117, page.PrimaryLocations);
        var county = Assert.Single(page.Assignments); Assert.Equal(140, county.Locations);
        Assert.Equal("Leinster", county.Context); Assert.Equal("Wicklow", county.Assignment.Name);
        var loss = Assert.Single(county.CarveOuts); Assert.Equal(("Aoife", 23), (loss.Rep.Name, loss.Locations));
        var town = Assert.Single(county.Towns, row => row.Id == seed.Town.Id);
        Assert.Equal(("Rathdrum", 23, "Aoife"), (town.Name, town.Locations, town.AssignedTo!.Name));
        Assert.Equal(117, Assert.Single(county.Towns, row => row.Id == seed.OtherTown.Id).Locations);
        Assert.False(app.Staff.SawTransaction);
        Assert.All(app.Reads.Commands, isolation => Assert.Equal(IsolationLevel.Serializable, isolation));
        Assert.Equal((2, 0), await CountsAsync());
        Assert.Equal(117, (await api.GetFromJsonAsync<LocationCoverageDetails[]>("/coverage/reps/colm/locations"))!.Length);
    }

    [Fact]
    public async Task Should_ListAllFourLevelsWithoutDoubleCounting_When_SameRepAssignmentsOverlap()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 23, 117);
        await AssignAsync("colm", TerritoryLevel.Region, seed.Region.Id);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync("colm", TerritoryLevel.Town, seed.OtherTown.Id);
        await AssignAsync("aoife", TerritoryLevel.Town, seed.Town.Id);
        await AssignAsync("colm", TerritoryLevel.Location, seed.Locations[0]);
        var page = await ReadAsync(api); Assert.Equal(118, page.PrimaryLocations);
        Assert.Equal(new[] { TerritoryLevel.Region, TerritoryLevel.County, TerritoryLevel.Town, TerritoryLevel.Location },
            page.Assignments.Select(row => row.Assignment.Assignment.Target.Level));
        Assert.Equal(22, Assert.Single(page.Assignments[1].CarveOuts).Locations);
        Assert.Empty(page.Assignments[2].CarveOuts);
        Assert.Equal(("Murphy's Pharmacy", "Rathdrum · Wicklow · Leinster", 1),
            (page.Assignments[3].Assignment.Name, page.Assignments[3].Context, page.Assignments[3].Locations));
        Assert.Equal("colm", (await api.GetFromJsonAsync<LocationCoverageDetails>($"/coverage/locations/{seed.Locations[0]}/owner"))!.Owner!.RepSubject);
        Assert.Equal((5, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_MarkOnlyOneLocationLost_When_DirectOverrideDoesNotCarveOutTheTown()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 3, 2);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync("brian", TerritoryLevel.Location, seed.Locations[0]);
        var page = await ReadAsync(api); Assert.Equal(4, page.PrimaryLocations);
        var town = Assert.Single(Assert.Single(page.Assignments).Towns, row => row.Id == seed.Town.Id);
        Assert.Null(town.AssignedTo); Assert.Equal(3, town.Locations);
        Assert.Equal(("Brian", 1), (Assert.Single(town.CarveOuts).Rep.Name, town.CarveOuts[0].Locations));
        SetManager(); var scoped = await ReadAsync(api);
        Assert.Equal("Brian", Assert.Single(Assert.Single(scoped.Assignments).CarveOuts).Rep.Name);
        using var foreign = await api.GetAsync("/coverage/reps/brian/territory"); Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    public async Task Should_PreserveEmptyArchivedTownsAndContext_When_AssignedGeographyHasNoShops()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 0, 0);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync("aoife", TerritoryLevel.Town, seed.Town.Id);
        using var retired = await api.PostAsJsonAsync($"/directory/geography/counties/{seed.County.Id}/retire",
            new RetireGeographyRequest(ReferenceAction.Archive, seed.County.Version));
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var page = await ReadAsync(api); Assert.Equal(0, page.PrimaryLocations);
        var county = Assert.Single(page.Assignments); Assert.True(county.Archived); Assert.Equal(0, county.Locations);
        Assert.Equal(2, county.Towns.Count); Assert.All(county.Towns, row => { Assert.True(row.Archived); Assert.Equal(0, row.Locations); });
        Assert.Equal("Aoife", Assert.Single(county.Towns, row => row.Name == "Rathdrum").AssignedTo!.Name);
        Assert.Empty(county.CarveOuts); Assert.Equal((2, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_DisambiguateAssignments_When_TownNamesMatchAcrossCounties()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 0, 0);
        var county = await PlaceAsync(api, "counties", "Wexford", seed.Region.Id);
        var town = await PlaceAsync(api, "towns", "Rathdrum", county.Id);
        await AssignAsync("colm", TerritoryLevel.Town, seed.Town.Id); await AssignAsync("colm", TerritoryLevel.Town, town.Id);
        var rows = (await ReadAsync(api)).Assignments;
        Assert.Equal(2, rows.Count); Assert.All(rows, row => Assert.Equal("Rathdrum", row.Assignment.Name));
        Assert.Equal(new[] { "Wexford · Leinster", "Wicklow · Leinster" }, rows.Select(row => row.Context).Order());
        Assert.Equal(2, rows.Select(row => row.Assignment.Assignment.Target.UnitId).Distinct().Count());
    }

    [Theory]
    [InlineData("unavailable")] [InlineData("missing")] [InlineData("renamed")]
    public async Task Should_UseTrustedCurrentNamesOrFailClosed_When_IdentityChanges(string change)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id); await AssignAsync("aoife", TerritoryLevel.Town, seed.Town.Id);
        if (change == "unavailable") app.Staff.Unavailable = true;
        if (change == "missing") app.Staff.Entries.Remove("aoife");
        if (change == "renamed") app.Staff.Entries["aoife"] = app.Staff.Entries["aoife"] with { DisplayName = "Aoife Doyle" };
        using var response = await api.GetAsync("/coverage/reps/colm/territory");
        Assert.Equal(change == "renamed" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        if (change == "renamed") Assert.Equal("Aoife Doyle", Assert.Single(Assert.Single((await response.Content.ReadFromJsonAsync<RepTerritoryPage>())!.Assignments).CarveOuts).Rep.Name);
        Assert.Equal((2, 0), await CountsAsync()); Assert.False(app.Staff.SawTransaction);
    }

    [Fact]
    public async Task Should_DenyNextRead_When_ReportingLineOrRoleChanges()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); await SeedAsync(api);
        SetManager(); Assert.Equal("Colm", (await ReadAsync(api)).Rep.Name);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            await db.RepReportingLines.Where(row => row.RepSubject == "colm").ExecuteUpdateAsync(set => set.SetProperty(row => row.ManagerSubject, "another-manager"));
        }
        using var teamDenied = await api.GetAsync("/coverage/reps/colm/territory"); Assert.Equal(HttpStatusCode.Forbidden, teamDenied.StatusCode);
        app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        using var roleDenied = await api.GetAsync("/coverage/reps/aoife/territory"); Assert.Equal(HttpStatusCode.Forbidden, roleDenied.StatusCode);
    }

    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task Should_RenderM06AndFilterCountyChildren_When_ManagerOrHeadOfficeOpensRep(string role)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 23, 117);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id); await AssignAsync("aoife", TerritoryLevel.Town, seed.Town.Id);
        await AssignAsync("colm", TerritoryLevel.Location, seed.Locations[0]); SetRole(role);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser, role);
        string html = await HtmlAsync(browser, Page + "?repSubject=colm"); SavePreview("sample-" + (role == BusinessRoles.SalesManager ? "manager" : "head-office"), html);
        string decoded = WebUtility.HtmlDecode(html);
        Assert.Contains("Reports to: Niamh Byrne", decoded); Assert.Contains("118 Locations as Primary", decoded);
        Assert.Contains("Wicklow (County)", decoded); Assert.Contains("140 Locations", decoded); Assert.Contains("carved out → Aoife", decoded);
        Assert.Contains("Murphy's Pharmacy (Location)", decoded); Assert.Contains("assigned directly", decoded);
        Assert.Contains("<details>", decoded); Assert.Contains("<caption>Towns in Wicklow</caption>", decoded);
        Assert.Contains("returnToTerritory=True", html, StringComparison.OrdinalIgnoreCase);
        html = await HtmlAsync(browser, Page + "?repSubject=colm&filter=rathdrum");
        Assert.Contains("<details open=", html); Assert.Contains("Rathdrum", html); Assert.DoesNotContain(">Laragh", html);
        Assert.Contains("118 Locations as Primary", html); SavePreview("filtered", html);
        html = await HtmlAsync(browser, Page + "?repSubject=colm&filter=missing"); Assert.Contains("No assignments match this filter.", html);
        Assert.Contains("118 Locations as Primary", html);
        Assert.Equal((3, 0), await CountsAsync());
        html = await HtmlAsync(browser, Page); Assert.Contains("repSubject=colm", html);
        Assert.Equal(role == BusinessRoles.HeadOfficeUser, html.Contains("repSubject=brian", StringComparison.Ordinal));
        using var foreign = await browser.GetAsync(Page + "?repSubject=brian");
        Assert.Equal(role == BusinessRoles.HeadOfficeUser ? HttpStatusCode.OK : HttpStatusCode.Forbidden, foreign.StatusCode);
        website.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        using var denied = await browser.GetAsync(Page + "?repSubject=colm"); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
    }

    [Fact]
    public async Task Should_RenderEmptyRep_When_NoAssignmentsExist()
    {
        await app.ResetCoverageAsync(); SetManager();
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser, BusinessRoles.SalesManager);
        string html = await HtmlAsync(browser, Page + "?repSubject=colm");
        Assert.Contains("0 Locations as Primary", html); Assert.Contains("Colm has no territory assignments.", html);
        Assert.Contains("Add assignment", html); SavePreview("empty", html); Assert.Equal((0, 0), await CountsAsync());
    }

    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task Should_ReturnToTerritoryAfterExplicitPreviewAndSave_When_AddAndRemoveStartThere(string role)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api); SetRole(role);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser, role);
        string html = await HtmlAsync(browser, Review + "?repSubject=colm&returnToTerritory=true");
        var fields = new Dictionary<string, string> { ["RepSubject"] = "colm", ["Action"] = "Add", ["TargetKey"] = "County:" + seed.County.Id,
            ["ReturnToTerritory"] = "true", ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") };
        using var reviewed = await browser.PostAsync(Review + "?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        html = await reviewed.Content.ReadAsStringAsync(); Assert.Contains("href=\"/Coverage/Territory?repSubject=colm\">Cancel", html);
        fields["PreviewProof"] = Field(html, "PreviewProof");
        Assert.Equal((0, 0), await CountsAsync()); await HtmlAsync(browser, Page + "?repSubject=colm"); Assert.Equal((0, 0), await CountsAsync());
        // A new Location invalidates the reviewed snapshot, retaining return context.
        await InsertAsync(seed.CustomerId, seed.Town.Id, "New shop"); fields["Confirmed"] = "true";
        using var stale = await browser.PostAsync(Review + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        html = await stale.Content.ReadAsStringAsync(); Assert.Contains("2 Locations become", html); Assert.Contains("Nothing was saved", html);
        Assert.Equal((0, 0), await CountsAsync()); Assert.NotEqual(fields["PreviewProof"], Field(html, "PreviewProof"));
        fields["PreviewProof"] = Field(html, "PreviewProof");
        using var saved = await browser.PostAsync(Review + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(Page + "?repSubject=colm", saved.Headers.Location!.OriginalString); Assert.Equal((1, 2), await CountsAsync());
        html = await HtmlAsync(browser, saved.Headers.Location.OriginalString); Assert.Contains("2 Locations as Primary", html);
        Assert.Contains("Assignment change saved.", html);
        var assignment = Assert.Single((await ReadAsync(api)).Assignments).Assignment.Assignment;
        fields = new() { ["RepSubject"] = "colm", ["Action"] = "Remove", ["AssignmentId"] = assignment.Id.ToString(), ["Version"] = assignment.Version,
            ["ReturnToTerritory"] = "true" };
        using var noCsrf = await browser.PostAsync(Review + "?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        fields["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken");
        using var removal = await browser.PostAsync(Review + "?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, removal.StatusCode);
        html = await removal.Content.ReadAsStringAsync(); Assert.Contains("2 Locations become Unassigned", html);
        fields["PreviewProof"] = Field(html, "PreviewProof");
        using var unconfirmed = await browser.PostAsync(Review + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, unconfirmed.StatusCode);
        Assert.Equal((1, 2), await CountsAsync()); fields["Confirmed"] = "true";
        using var removed = await browser.PostAsync(Review + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Equal(Page + "?repSubject=colm", removed.Headers.Location!.OriginalString); Assert.Equal((0, 4), await CountsAsync());
        Assert.Contains("0 Locations as Primary", await HtmlAsync(browser, Page + "?repSubject=colm"));
    }

    [Fact]
    public async Task Should_RejectRemovalRepMismatch_When_ManagerForgesTerritoryContext()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var assignment = await AssignAsync("aoife", TerritoryLevel.County, seed.County.Id); SetManager();
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser, BusinessRoles.SalesManager);
        string html = await HtmlAsync(browser, Page + "?repSubject=colm");
        // Empty territory has no POST form; obtain a token from the existing review.
        html = await HtmlAsync(browser, Review + "?repSubject=colm&returnToTerritory=true");
        var fields = new Dictionary<string, string> { ["RepSubject"] = "colm", ["Action"] = "Remove", ["AssignmentId"] = assignment.Id.ToString(),
            ["Version"] = Convert.ToBase64String(assignment.Version), ["ReturnToTerritory"] = "true", ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") };
        using var denied = await browser.PostAsync(Review + "?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        Assert.Contains("Reload the rep&#x27;s assignments", await denied.Content.ReadAsStringAsync()); Assert.Equal((1, 0), await CountsAsync());
        fields["RepSubject"] = "brian";
        using var foreign = await browser.PostAsync(Review + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal((1, 0), await CountsAsync());
    }

    private void SetManager() => SetRole(BusinessRoles.SalesManager);
    private void SetRole(string role)
    {
        app.Roles.SetRoles("niamh", role); app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [role] };
    }
    private static async Task SignInAsync(HttpClient browser, string role)
    {
        using var response = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadAsStringAsync();
    }
    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static async Task<RepTerritoryPage> ReadAsync(HttpClient api)
    {
        using var response = await api.GetAsync("/coverage/reps/colm/territory"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RepTerritoryPage>())!;
    }
    private async Task<TerritoryAssignment> AssignAsync(string rep, TerritoryLevel level, Guid id)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var assignment = TerritoryAssignment.Create(rep, new(level, id)); db.TerritoryAssignments.Add(assignment); await db.SaveChangesAsync(); return assignment;
    }
    private async Task<(int, int)> CountsAsync()
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        return (await db.TerritoryAssignments.CountAsync(), await db.AssignmentHistory.CountAsync());
    }
    private async Task<Seed> SeedAsync(HttpClient api, int first = 1, int other = 0)
    {
        var region = await PlaceAsync(api, "regions", "Leinster"); var county = await PlaceAsync(api, "counties", "Wicklow", region.Id);
        var town = await PlaceAsync(api, "towns", "Rathdrum", county.Id); var otherTown = await PlaceAsync(api, "towns", "Laragh", county.Id);
        if (first + other == 0) return new(region, county, town, otherTown, Guid.Empty, []);
        using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Customer", new("Murphy's Pharmacy", town.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        List<Guid> locations = [customer.Locations[0].Id];
        for (int i = 1; i < first + other; i++) locations.Add(await InsertAsync(customer.Id, i < first ? town.Id : otherTown.Id, "Shop " + i));
        return new(region, county, town, otherTown, customer.Id, locations.ToArray());
    }
    private async Task<Guid> InsertAsync(Guid customer, Guid town, string name)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Guid id = Guid.NewGuid(); string normalized = name.ToUpperInvariant();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Locations] ([Id], [CustomerId], [TownId], [Name], [NormalizedName]) VALUES ({id}, {customer}, {town}, {name}, {normalized})"); return id;
    }
    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parent = null)
    {
        using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
    private static void SavePreview(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_TERRITORY_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output);
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem Town, GeographyItem OtherTown, Guid CustomerId, Guid[] Locations);
}
