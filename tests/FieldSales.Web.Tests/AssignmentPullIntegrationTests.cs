extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using RepReportingLine = CatalogueApi::FieldSales.Api.Coverage.RepReportingLine;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;

namespace FieldSales.Web.Tests;

public sealed class AssignmentPullIntegrationTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    [Theory]
    [InlineData(TerritoryLevel.Region)] [InlineData(TerritoryLevel.County)]
    [InlineData(TerritoryLevel.Town)] [InlineData(TerritoryLevel.Location)]
    public async Task Should_TransferOnlyChosenAssignmentWithHistory_When_ReceiverAddsHeldArea(TerritoryLevel level)
    {
        var seed = await SeedAsync(); using var api = app.CreateApiClient();
        var target = Target(seed, level); var held = await AssignAsync("aoife", target.Target);
        var preserved = await AssignAsync("brian", new(TerritoryLevel.Location, seed.OtherLocation));
        var unrelated = await AssignAsync("colm", new(TerritoryLevel.County, seed.OtherCounty.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, "/Coverage/Assignments?repSubject=receiver&returnToTerritory=true");
        Assert.Contains(target.Name + " — Aoife's", WebUtility.HtmlDecode(html));
        string key = Option(html, target.Target.UnitId);
        var fields = Inputs(html); Set(fields, "Action", "Add"); Set(fields, "TargetKey", key); Set(fields, "Reason", "Permanent replacement");
        using var start = await browser.PostAsync("/Coverage/Assignments?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        html = await HtmlAsync(browser, start.Headers.Location!.OriginalString);
        Assert.Contains($"Transfer {target.Name} from Aoife to Niamh?", WebUtility.HtmlDecode(html));
        Assert.Contains("repSubject=receiver", html); Assert.Equal((3, 0), await CountsAsync());
        fields = Inputs(html); Assert.All(fields.Where(row => row.Key == "Selected"), row => Assert.StartsWith(held.Id.ToString("D"), row.Value));
        using var preview = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); html = await preview.Content.ReadAsStringAsync();
        Assert.Contains("Confirm transfer", html); Assert.DoesNotContain("test-access-token", html); Assert.Equal((3, 0), await CountsAsync());
        AssignmentTransferIntegrationTests.SaveRender("pull-preview-" + level, html);
        fields = Inputs(html); fields.Add(new("Confirmed", "true"));
        using var saved = await browser.PostAsync("/Coverage/Transfer?handler=Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode); Assert.Equal("/Coverage/Territory?repSubject=receiver", saved.Headers.Location!.OriginalString);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var assignments = await db.TerritoryAssignments.AsNoTracking().ToArrayAsync();
        Assert.Equal("receiver", assignments.Single(row => row.Id == held.Id).RepSubject);
        Assert.Equal("brian", assignments.Single(row => row.Id == preserved.Id).RepSubject);
        Assert.Equal("colm", assignments.Single(row => row.Id == unrelated.Id).RepSubject); Assert.Equal(3, assignments.Length);
        var expected = level == TerritoryLevel.Location ? new[] { seed.FirstLocation } : seed.TownLocations;
        var entries = await db.AssignmentHistory.AsNoTracking().ToArrayAsync(); Assert.Equal(expected.Order(), entries.Select(row => row.LocationId).Order());
        Assert.All(entries, row => { Assert.Equal("aoife", row.PreviousRepSubject); Assert.Equal("receiver", row.NewRepSubject); Assert.Equal("Permanent replacement", row.Reason); });
        Assert.Single(entries.Select(row => row.OperationId).Distinct());
        foreach (var id in expected)
        {
            var owner = await api.GetFromJsonAsync<LocationCoverageDetails>("/coverage/locations/" + id + "/owner");
            Assert.Equal("receiver", owner!.Owner!.RepSubject); Assert.Equal(held.Id, owner.Owner.Source.AssignmentId); Assert.Equal(target.Target, owner.Owner.Source.Target);
        }
        Assert.False(app.Staff.SawTransaction);
    }

    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task Should_ScopeSourcesPrefillReceiverAndPreserveExclusions_When_TakeOverStarts(string role)
    {
        var seed = await SeedAsync(); var county = await AssignAsync("colm", new(TerritoryLevel.County, seed.County.Id));
        await AssignAsync("brian", new(TerritoryLevel.County, seed.OtherCounty.Id));
        SetRole(role); app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web, role);
        string html = await HtmlAsync(browser, "/Coverage/Territory?repSubject=receiver"); Assert.Contains("Take over from another rep", html);
        html = await HtmlAsync(browser, "/Coverage/TakeOver?repSubject=receiver"); Assert.Contains("Colm", html);
        Assert.Equal(role == BusinessRoles.HeadOfficeUser, Regex.IsMatch(html, "<option value=\"brian\""));
        AssignmentTransferIntegrationTests.SaveRender("takeover-" + role, html);
        var fields = Inputs(html); Set(fields, "GivingRepSubject", "colm");
        using var start = await browser.PostAsync("/Coverage/TakeOver", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        html = await HtmlAsync(browser, start.Headers.Location!.OriginalString); Assert.Contains("Receiving rep: <strong>Niamh</strong>", html);
        Assert.Contains("repSubject=receiver", html); fields = Inputs(html);
        Assert.Equal("receiver", fields.Single(row => row.Key == "ReceivingRepSubject").Value);
        Assert.Equal(3, fields.Count(row => row.Key == "Selected"));
        fields.RemoveAll(row => row.Key == "Selected" && row.Value.EndsWith(seed.OtherTown.Id.ToString("D"), StringComparison.Ordinal));
        using var preview = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        html = await preview.Content.ReadAsStringAsync(); Assert.Contains("Rathdrum (Town)", WebUtility.HtmlDecode(html)); Assert.Equal((2, 0), await CountsAsync());
        fields = Inputs(html); fields.Add(new("Confirmed", "true"));
        using var save = await browser.PostAsync("/Coverage/Transfer?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal("/Coverage/Territory?repSubject=receiver", save.Headers.Location!.OriginalString); Assert.Equal((3, 2), await CountsAsync());
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal("colm", (await db.TerritoryAssignments.SingleAsync(row => row.Id == county.Id)).RepSubject);
    }

    [Fact]
    public async Task Should_KeepAddAndSelfAssignmentsDistinct_When_TargetIsInheritedFreeOrAlreadyHeld()
    {
        var seed = await SeedAsync(); await AssignAsync("colm", new(TerritoryLevel.County, seed.County.Id));
        var own = await AssignAsync("receiver", new(TerritoryLevel.County, seed.OtherCounty.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, "/Coverage/Assignments?repSubject=receiver");
        Assert.Contains("Already assigned", html); Assert.Matches("<option[^>]*disabled[^>]*>[^<]*Wexford", WebUtility.HtmlDecode(html));
        var fields = Inputs(html); Set(fields, "Action", "Add"); Set(fields, "TargetKey", Option(html, seed.Town.Id));
        using var preview = await browser.PostAsync("/Coverage/Assignments?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains("Assign Rathdrum to Niamh?", await preview.Content.ReadAsStringAsync()); Assert.Equal((2, 0), await CountsAsync());
        Set(fields, "TargetKey", Option(html, own.Target.UnitId));
        using var self = await browser.PostAsync("/Coverage/Assignments?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, self.StatusCode);
        Assert.Contains("already holds", await self.Content.ReadAsStringAsync()); Assert.Equal((2, 0), await CountsAsync());
        Set(fields, "TargetKey", Option(html, seed.Region.Id));
        using var free = await browser.PostAsync("/Coverage/Assignments?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        Assert.Contains("Assign South East to Niamh?", await free.Content.ReadAsStringAsync());
        using var api = app.CreateApiClient(); using var duplicate = await api.PostAsJsonAsync("/coverage/assignments/preview", new AddTerritoryAssignmentRequest("receiver", new(TerritoryLevel.County, seed.County.Id)));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); string error = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("review a transfer", error); Assert.DoesNotContain("Remove it", error); Assert.Equal((2, 0), await CountsAsync());
    }

    [Theory]
    [InlineData("holder")] [InlineData("removed")] [InlineData("foreign")] [InlineData("forged")]
    public async Task Should_RejectStaleHeldChoiceWithoutSwitchingSource_When_ListChanges(string change)
    {
        var seed = await SeedAsync(); var held = await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, "/Coverage/Assignments?repSubject=receiver"); var fields = Inputs(html);
        Set(fields, "Action", "Add"); string key = Option(html, seed.Town.Id); Set(fields, "TargetKey", change == "forged" ? "Town:" + Guid.NewGuid() : key);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            if (change == "removed") await db.TerritoryAssignments.Where(row => row.Id == held.Id).ExecuteDeleteAsync();
            if (change == "holder") await db.TerritoryAssignments.Where(row => row.Id == held.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.RepSubject, "colm"));
            if (change == "foreign") { SetRole(BusinessRoles.SalesManager); await db.RepReportingLines.Where(row => row.RepSubject == "aoife").ExecuteUpdateAsync(set => set.SetProperty(row => row.ManagerSubject, "another-manager")); }
        }
        using var rejected = await browser.PostAsync("/Coverage/Assignments?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Reload the choices", await rejected.Content.ReadAsStringAsync()); Assert.Equal(change == "removed" ? (0, 0) : (1, 0), await CountsAsync());
    }

    [Theory]
    [InlineData("holder")] [InlineData("removed")] [InlineData("receiver")] [InlineData("source")]
    [InlineData("csrf")] [InlineData("roles")] [InlineData("inactive")] [InlineData("identity")]
    public async Task Should_DenyPullWithoutWrites_When_EntryContextIsInvalid(string change)
    {
        var seed = await SeedAsync(); var held = await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string url = $"/Coverage/Transfer?repSubject=aoife&receivingRepSubject=receiver&pull=true&pullAssignmentId={held.Id:D}";
        string html = await HtmlAsync(browser, url); var fields = Inputs(html);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            if (change == "holder") await db.TerritoryAssignments.Where(row => row.Id == held.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.RepSubject, "colm"));
            if (change == "removed") await db.TerritoryAssignments.Where(row => row.Id == held.Id).ExecuteDeleteAsync();
        }
        if (change == "receiver") Set(fields, "ReceivingRepSubject", "aoife");
        if (change == "source") Set(fields, "RepSubject", "colm");
        if (change == "csrf") fields.RemoveAll(row => row.Key == "__RequestVerificationToken");
        if (change == "roles") app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        if (change == "inactive") app.Staff.Entries["receiver"] = app.Staff.Entries["receiver"] with { Available = false };
        if (change == "identity") app.Staff.Unavailable = true;
        using var rejected = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(change == "roles" ? HttpStatusCode.Forbidden : change == "identity" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(change == "removed" ? (0, 0) : (1, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_RefreshProofAndReturnToReceiver_When_PullIsCancelledOrConfirmedAfterChange()
    {
        var seed = await SeedAsync(); var held = await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, $"/Coverage/Transfer?repSubject=aoife&receivingRepSubject=receiver&pull=true&pullAssignmentId={held.Id:D}");
        Assert.Contains("href=\"/Coverage/Territory?repSubject=receiver\">Cancel", html);
        var fields = Inputs(html); using var preview = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); html = await preview.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/Coverage/Territory?repSubject=receiver\">Cancel", html); await HtmlAsync(browser, "/Coverage/Territory?repSubject=receiver"); Assert.Equal((1, 0), await CountsAsync());
        fields = Inputs(html); string proof = fields.Single(row => row.Key == "PreviewProof").Value; fields.Add(new("Confirmed", "true"));
        await using (var scope = app.Api.Services.CreateAsyncScope())
        { await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Locations.Where(row => row.Id == seed.FirstLocation).ExecuteUpdateAsync(set => set.SetProperty(row => row.Name, "Renamed shop")); }
        using var stale = await browser.PostAsync("/Coverage/Transfer?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        html = await stale.Content.ReadAsStringAsync(); Assert.Contains("Nothing was saved", html); Assert.Equal((1, 0), await CountsAsync());
        fields = Inputs(html); Assert.NotEqual(proof, fields.Single(row => row.Key == "PreviewProof").Value); Assert.DoesNotContain(fields, row => row.Key == "Confirmed");
        Assert.Equal("receiver", fields.Single(row => row.Key == "ReceivingRepSubject").Value); Assert.Equal("true", fields.Single(row => row.Key == "Pull").Value.ToLowerInvariant());
        fields.Add(new("Confirmed", "true")); using var save = await browser.PostAsync("/Coverage/Transfer?handler=Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode); Assert.Equal("/Coverage/Territory?repSubject=receiver", save.Headers.Location!.OriginalString); Assert.Equal((1, 2), await CountsAsync());
    }

    [Fact]
    public async Task Should_ShowEmptyStateAndDenyForgedSource_When_NoAuthorizedBookExists()
    {
        await SeedAsync(); await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, "/Coverage/TakeOver?repSubject=receiver"); Assert.Contains("No other reps", html); Assert.DoesNotContain("Review assignments</button>", html);
        var fields = Inputs(html); Set(fields, "RepSubject", "receiver"); Set(fields, "GivingRepSubject", "receiver");
        using var forged = await browser.PostAsync("/Coverage/TakeOver", new FormUrlEncodedContent(fields));
        // The layout's sign-out form supplies a valid token; the business handler
        // rejects the self source with the normal validation page and no redirect.
        Assert.Equal(HttpStatusCode.OK, forged.StatusCode); Assert.Null(forged.Headers.Location);
        Assert.Contains("Choose a rep whose assignments", await forged.Content.ReadAsStringAsync());
        fields.RemoveAll(row => row.Key == "__RequestVerificationToken");
        using var noCsrf = await browser.PostAsync("/Coverage/TakeOver", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode); Assert.Equal((0, 0), await CountsAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_KeepZeroAndArchivedCoverageTransferable_When_TakeOverOpensExistingBook(bool archived)
    {
        var seed = await SeedAsync(); var county = archived ? seed.County : seed.OtherCounty;
        var held = await AssignAsync("colm", new(TerritoryLevel.County, county.Id));
        if (archived)
        {
            await using var scope = app.Api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Counties.Where(row => row.Id == county.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.IsArchived, true));
        }
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, "/Coverage/TakeOver?repSubject=receiver"); Assert.Contains("Colm", html);
        html = await HtmlAsync(browser, "/Coverage/Transfer?repSubject=colm&receivingRepSubject=receiver&pull=true");
        if (archived) Assert.Contains("Archived", html);
        var fields = Inputs(html); using var preview = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); html = await preview.Content.ReadAsStringAsync();
        if (!archived) Assert.Contains("Future matching Locations follow", html);
        fields = Inputs(html); fields.Add(new("Confirmed", "true"));
        using var saved = await browser.PostAsync("/Coverage/Transfer?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal((1, archived ? 3 : 0), await CountsAsync());
        await using var verify = app.Api.Services.CreateAsyncScope(); var db = verify.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal("receiver", (await db.TerritoryAssignments.SingleAsync(row => row.Id == held.Id)).RepSubject);
        Assert.Equal(archived, (await db.Counties.SingleAsync(row => row.Id == county.Id)).IsArchived);
    }

    private async Task<Seed> SeedAsync()
    {
        await app.ResetCoverageAsync(); app.Staff.Entries["receiver"] = new("receiver", "Niamh", [BusinessRoles.FieldSalesperson], true);
        using var api = app.CreateApiClient(); var region = await PlaceAsync(api, "regions", "South East");
        var county = await PlaceAsync(api, "counties", "Wicklow", region.Id); var otherCounty = await PlaceAsync(api, "counties", "Wexford", region.Id);
        var town = await PlaceAsync(api, "towns", "Rathdrum", county.Id); var otherTown = await PlaceAsync(api, "towns", "Laragh", county.Id);
        using var first = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Customer", new("Murphy's Pharmacy", town.Id)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); var customer = (await first.Content.ReadFromJsonAsync<CustomerDetails>())!;
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        db.RepReportingLines.Add(RepReportingLine.Create("receiver", "niamh")); await db.SaveChangesAsync();
        Guid second = Guid.NewGuid(), other = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Locations] ([Id],[CustomerId],[TownId],[Name],[NormalizedName]) VALUES ({second},{customer.Id},{town.Id},{"Doyle's Shop"},{"DOYLE'S SHOP"})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Locations] ([Id],[CustomerId],[TownId],[Name],[NormalizedName]) VALUES ({other},{customer.Id},{otherTown.Id},{"Laragh Shop"},{"LARAGH SHOP"})");
        return new(region, county, otherCounty, town, otherTown, customer.Locations[0].Id, [customer.Locations[0].Id, second], other);
    }
    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem OtherCounty, GeographyItem Town, GeographyItem OtherTown, Guid FirstLocation, Guid[] TownLocations, Guid OtherLocation);
    private sealed record NamedTarget(TerritoryTarget Target, string Name);
    private static NamedTarget Target(Seed seed, TerritoryLevel level) => level switch
    { TerritoryLevel.Region => new(new(level, seed.Region.Id), seed.Region.Name), TerritoryLevel.County => new(new(level, seed.County.Id), seed.County.Name),
        TerritoryLevel.Town => new(new(level, seed.Town.Id), seed.Town.Name), _ => new(new(level, seed.FirstLocation), "Murphy's Pharmacy") };
    private async Task<TerritoryAssignment> AssignAsync(string rep, TerritoryTarget target)
    { await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); var row = TerritoryAssignment.Create(rep, target); db.TerritoryAssignments.Add(row); await db.SaveChangesAsync(); return row; }
    private async Task<(int, int)> CountsAsync()
    { await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); return (await db.TerritoryAssignments.CountAsync(), await db.AssignmentHistory.CountAsync()); }
    private static List<KeyValuePair<string, string>> Inputs(string html) => AssignmentTransferIntegrationTests.Inputs(html);
    private static void Set(List<KeyValuePair<string, string>> fields, string name, string value) { fields.RemoveAll(row => row.Key == name); fields.Add(new(name, value)); }
    private static string Option(string html, Guid id)
    { var option = Regex.Matches(html, "<option[^>]*>").Cast<Match>().Single(row => row.Value.Contains(id.ToString("D"), StringComparison.Ordinal)); return WebUtility.HtmlDecode(Regex.Match(option.Value, "value=\"([^\"]*)\"").Groups[1].Value); }
    private static async Task<string> HtmlAsync(HttpClient browser, string url)
    { using var page = await browser.GetAsync(url); Assert.Equal(HttpStatusCode.OK, page.StatusCode); return await page.Content.ReadAsStringAsync(); }
    private static async Task<HttpClient> BrowserAsync(StaffWebsiteFactory web, string role = BusinessRoles.HeadOfficeUser)
    { var browser = web.CreateBrowser(); using var login = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode); return browser; }
    private void SetRole(string role) { app.Roles.SetRoles("niamh", role); app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [role] }; }
    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parent = null)
    { using var result = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent)); Assert.Equal(HttpStatusCode.Created, result.StatusCode); return (await result.Content.ReadFromJsonAsync<GeographyItem>())!; }
}
