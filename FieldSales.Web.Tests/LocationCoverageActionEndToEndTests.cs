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

public sealed class LocationCoverageActionEndToEndTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    [Theory]
    [InlineData(false, "Town", 5)] [InlineData(false, "Shop", 1)]
    [InlineData(true, "Shop", 1)] [InlineData(true, "Source", 23)]
    public async Task Should_RouteEachLocationActionToExactImpactWithoutSaving_When_ManagerChoosesEligibleRep(bool covered, string intent, int changed)
    {
        var seed = await SeedAsync(covered ? 23 : 5, covered ? "Murphy's Pharmacy" : "Walsh's Shop", covered ? "Rathdrum" : "Laragh");
        TerritoryAssignment? source = covered ? await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id)) : null;
        // An unrelated assignment must never enter the source transfer.
        if (covered) await AssignAsync("aoife", new(TerritoryLevel.Town, seed.OtherTown.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var before = await CountsAsync(); string html = await HtmlAsync(browser, Page(seed.Shop.Id));
        string state = covered ? "covered" : "unassigned"; SaveRender(state, html);
        string decoded = WebUtility.HtmlDecode(html);
        if (covered)
        {
            Assert.Contains("Aoife (via Rathdrum)", decoded); Assert.Contains("Transfer Rathdrum (Town, 23 Locations) to...", decoded);
            Assert.True(decoded.IndexOf("Change just this shop", StringComparison.Ordinal) < decoded.IndexOf("Transfer Rathdrum", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains("4 other Locations in Laragh are unassigned", decoded);
            Assert.True(decoded.IndexOf("Assign Laragh (Town)", StringComparison.Ordinal) < decoded.IndexOf("Assign just this shop", StringComparison.Ordinal));
        }
        string entryUrl = Action(html, intent); html = await HtmlAsync(browser, entryUrl); SaveRender(state + "-" + intent.ToLowerInvariant() + "-choose", html);
        Assert.Contains("Choose the receiving rep", html); Assert.DoesNotContain("<option value=\"brian\"", html);
        if (covered) Assert.DoesNotContain("<option value=\"aoife\"", html);
        var fields = Inputs(html); Set(fields, "RepSubject", "colm"); Set(fields, "Reason", "Agreed shop coverage");
        using var preview = await browser.PostAsync(Change(seed.Shop.Id) + "?handler=Preview", new FormUrlEncodedContent(fields));
        if (intent == "Source")
        {
            Assert.Equal(HttpStatusCode.Redirect, preview.StatusCode); Assert.Contains(source!.Id.ToString("D"), preview.Headers.Location!.OriginalString);
            html = await HtmlAsync(browser, preview.Headers.Location.OriginalString); SaveRender("covered-source-review", html);
            Assert.Single(Inputs(html), row => row.Key == "Selected"); Assert.DoesNotContain("Laragh", html);
            fields = Inputs(html); using var transferPreview = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.OK, transferPreview.StatusCode); html = await transferPreview.Content.ReadAsStringAsync();
            Assert.Contains("Confirm transfer", html);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode); html = await preview.Content.ReadAsStringAsync(); Assert.Contains("Confirm assignment", html);
        }
        Assert.Contains(changed + (changed == 1 ? " Location" : " Locations"), html);
        Assert.Contains("Colm", html); Assert.Contains("Agreed shop coverage", WebUtility.HtmlDecode(html));
        Assert.Contains("href=\"" + Page(seed.Shop.Id) + "\">Cancel", html); Assert.DoesNotContain("test-access-token", html);
        Assert.Contains(Inputs(html), row => row.Key == "PreviewProof" && row.Value.Length > 0);
        Assert.Equal(before, await CountsAsync()); SaveRender(state + "-" + intent.ToLowerInvariant() + "-preview", html);
    }

    [Theory]
    [InlineData("held-town")] [InlineData("forged-proof")] [InlineData("wrong-location")] [InlineData("unmanaged-recipient")]
    public async Task Should_SaveNothing_When_LocationEntryOrRecipientIsStaleOrTampered(string change)
    {
        var seed = await SeedAsync(1); await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, Page(seed.Shop.Id)); string entry = Action(html, "Town"); html = await HtmlAsync(browser, entry);
        var fields = Inputs(html); Set(fields, "RepSubject", change == "unmanaged-recipient" ? "brian" : "colm");
        if (change == "held-town") await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        if (change == "forged-proof") Set(fields, "EntryProof", "forged");
        var before = await CountsAsync(); Guid id = change == "wrong-location" ? Guid.NewGuid() : seed.Shop.Id;
        using var response = await browser.PostAsync(Change(id) + "?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(change == "held-town" ? HttpStatusCode.Conflict : change == "unmanaged-recipient" ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("Confirm assignment", body); Assert.DoesNotContain("Confirm transfer", body);
        Assert.Equal(before, await CountsAsync());
    }

    [Theory]
    [InlineData(TerritoryLevel.Town, 5)] [InlineData(TerritoryLevel.County, 6)] [InlineData(TerritoryLevel.Region, 6)] [InlineData(TerritoryLevel.Location, 1)]
    public async Task Should_ReadExactSourceAndFootprint_When_AssignmentProvidesCoverage(TerritoryLevel level, int count)
    {
        var seed = await SeedAsync(5); var source = await AssignAsync("aoife", new(level, level switch
        { TerritoryLevel.Region => seed.Region.Id, TerritoryLevel.County => seed.County.Id, TerritoryLevel.Town => seed.Town.Id, _ => seed.Shop.Id }));
        SetManager(); using var api = app.CreateApiClient(); app.Reads.Commands.Clear();
        var context = (await api.GetFromJsonAsync<LocationCoverageActions>(Actions(seed.Shop.Id)))!;
        Assert.Equal(source.Id, context.SourceAssignmentId); Assert.Equal(count, context.SourceLocations); Assert.True(context.CanChangeShop);
        Assert.False(context.CanAssignTown); Assert.Equal(level != TerritoryLevel.Location, context.CanTransferSource);
        Assert.False(app.Staff.SawTransaction); Assert.All(app.Reads.Commands, isolation => Assert.Equal(System.Data.IsolationLevel.Serializable, isolation));
        Assert.Equal((1, 0), await CountsAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_PreserveExistingSourceAuthority_When_ManagerReadsForeignCoverage(bool direct)
    {
        var seed = await SeedAsync(1); await AssignAsync("brian", new(direct ? TerritoryLevel.Location : TerritoryLevel.Town, direct ? seed.Shop.Id : seed.Town.Id));
        SetManager(); using var api = app.CreateApiClient(); var context = (await api.GetFromJsonAsync<LocationCoverageActions>(Actions(seed.Shop.Id)))!;
        Assert.Equal(!direct, context.CanChangeShop); Assert.False(context.CanTransferSource);
        using var preview = await api.PostAsJsonAsync("/coverage/assignments/preview", new AddTerritoryAssignmentRequest("colm", new(TerritoryLevel.Location, seed.Shop.Id)));
        Assert.Equal(direct ? HttpStatusCode.Forbidden : HttpStatusCode.OK, preview.StatusCode);
        using var transfer = await api.GetAsync("/coverage/transfers/review?sourceRepSubject=brian"); Assert.Equal(HttpStatusCode.Forbidden, transfer.StatusCode);
        Assert.Equal((1, 0), await CountsAsync());
    }

    private void SetManager() { app.Roles.SetRoles("niamh", BusinessRoles.SalesManager); app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [BusinessRoles.SalesManager] }; }
    private async Task<HttpClient> BrowserAsync(StaffWebsiteFactory web)
    {
        SetManager(); web.Roles.SetRoles("niamh", BusinessRoles.SalesManager); var browser = web.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(BusinessRoles.SalesManager)); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode); return browser;
    }
    private static string Page(Guid id) => $"/Coverage/Location/{id}";
    private static string Change(Guid id) => $"/Coverage/LocationChange/{id}";
    private static string Actions(Guid id) => $"/coverage/locations/{id}/page/actions";
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    { using var r = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return await r.Content.ReadAsStringAsync(); }
    private static List<KeyValuePair<string, string>> Inputs(string html) => AssignmentTransferEndToEndTests.Inputs(html);
    private static void Set(List<KeyValuePair<string, string>> fields, string name, string value) { fields.RemoveAll(row => row.Key == name); fields.Add(new(name, value)); }
    private static string Action(string html, string intent)
    {
        var links = Regex.Matches(html, "<a[^>]*href=\"([^\"]*LocationChange[^\"]*)\"[^>]*>([^<]*)</a>");
        string phrase = intent == "Town" ? "Assign Laragh (Town)" : intent == "Source" ? "Transfer Rathdrum" : "just this shop";
        return WebUtility.HtmlDecode(links.Single(link => WebUtility.HtmlDecode(link.Groups[2].Value).Contains(phrase, StringComparison.Ordinal)).Groups[1].Value);
    }
    private static void SaveRender(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_LOCATION_ACTION_ARTIFACT_DIR"); if (string.IsNullOrEmpty(output)) return;
        System.IO.Directory.CreateDirectory(output); string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
    private async Task<TerritoryAssignment> AssignAsync(string rep, TerritoryTarget target)
    { await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); var row = TerritoryAssignment.Create(rep, target); db.TerritoryAssignments.Add(row); await db.SaveChangesAsync(); return row; }
    private async Task<(int, int)> CountsAsync()
    { await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); return (await db.TerritoryAssignments.CountAsync(), await db.AssignmentHistory.CountAsync()); }
    private async Task<Seed> SeedAsync(int locations, string name = "Walsh's Shop", string townName = "Laragh")
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient();
        var region = await Place(api, "regions", "Leinster"); var county = await Place(api, "counties", "Wicklow", region.Id);
        var town = await Place(api, "towns", townName, county.Id); var other = await Place(api, "towns", townName == "Laragh" ? "Rathdrum" : "Laragh", county.Id);
        using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Customer", new(name, town.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        for (int i = 1; i < locations; i++) { using var added = await api.PostAsJsonAsync($"/directory/customers/{customer.Id}/locations", new CreateLocationRequest("Neighbour " + i, town.Id)); Assert.Equal(HttpStatusCode.Created, added.StatusCode); }
        using var distant = await api.PostAsJsonAsync($"/directory/customers/{customer.Id}/locations", new CreateLocationRequest("Distant shop", other.Id)); Assert.Equal(HttpStatusCode.Created, distant.StatusCode);
        return new(region, county, town, other, customer.Locations[0]);
    }
    private static async Task<GeographyItem> Place(HttpClient api, string level, string name, Guid? parent = null)
    { using var r = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent)); Assert.Equal(HttpStatusCode.Created, r.StatusCode); return (await r.Content.ReadFromJsonAsync<GeographyItem>())!; }
    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem Town, GeographyItem OtherTown, LocationSummary Shop);
}
