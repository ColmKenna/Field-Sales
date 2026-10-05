extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;

namespace FieldSales.Web.Tests;

public sealed partial class LocationCoverageActionEndToEndTests
{
    [Theory]
    [InlineData(false, "Town", 4)] [InlineData(false, "Shop", 1)] [InlineData(true, "Shop", 1)]
    public async Task Should_SaveReviewedAddAndCapturedHistory_When_LocationChangeIsConfirmed(bool covered, string intent, int changed)
    {
        var seed = await SeedAsync(5);
        TerritoryAssignment? parent = covered ? await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id)) : null;
        var neighbour = await NeighbourAsync(seed);
        var carve = await AssignAsync("brian", new(TerritoryLevel.Location, neighbour));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var baseline = await CountsAsync(); var review = await PreviewLocationAsync(browser, seed, intent);
        Assert.Equal(baseline, await CountsAsync());
        using var saved = await ConfirmAsync(browser, review); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(Page(seed.Shop.Id), saved.Headers.Location!.OriginalString);
        Assert.Equal((baseline.Item1 + 1, changed), await CountsAsync());
        var rows = await AssignmentRowsAsync(); Assert.Equal("brian", rows.Single(row => row.Id == carve.Id).RepSubject);
        if (parent is not null) Assert.Equal("aoife", rows.Single(row => row.Id == parent.Id).RepSubject);
        var added = rows.Single(row => row.RepSubject == "colm");
        Assert.Equal(new TerritoryTarget(intent == "Town" ? TerritoryLevel.Town : TerritoryLevel.Location,
            intent == "Town" ? seed.Town.Id : seed.Shop.Id), added.Target);
        string html = WebUtility.HtmlDecode(await HtmlAsync(browser, Page(seed.Shop.Id)));
        Assert.Contains(intent == "Town" ? "Colm (via Laragh)" : "Colm (assigned directly)", html);
        Assert.Contains("Assignment change saved.", html);
        Assert.Equal("brian", (await CoveragePageAsync(neighbour)).Owner!.Rep.Subject);
        // A different, non-carved neighbour only changes for whole-Town Add.
        Guid other = await NeighbourAsync(seed, neighbour);
        var otherOwner = (await CoveragePageAsync(other)).Owner;
        Assert.Equal(intent == "Town" ? "colm" : covered ? "aoife" : null, otherOwner?.Rep.Subject);
        await AssertHistoryAsync(seed.Shop.Id, covered ? "aoife" : null,
            intent == "Town" ? OwnershipChangeCause.TerritoryAssignment : OwnershipChangeCause.DirectLocationAssignment);
        var after = await CountsAsync(); using var replay = await ConfirmAsync(browser, review);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal(after, await CountsAsync());
    }

    [Fact]
    public async Task Should_TransferExactTownPreservingCarveOutsAndFutureInheritance_When_SourceChangeIsConfirmed()
    {
        var seed = await SeedAsync(5); var source = await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        var unrelated = await AssignAsync("aoife", new(TerritoryLevel.Town, seed.OtherTown.Id));
        var carve = await AssignAsync("colm", new(TerritoryLevel.Location, await NeighbourAsync(seed)));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, "Source"); Assert.Contains("4 Locations move", review.Html);
        Set(review.Fields, "ReturnUrl", "https://untrusted.invalid/");
        using var saved = await ConfirmAsync(browser, review); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(Page(seed.Shop.Id), saved.Headers.Location!.OriginalString);
        var rows = await AssignmentRowsAsync(); Assert.Equal("colm", rows.Single(row => row.Id == source.Id).RepSubject);
        Assert.Equal("colm", rows.Single(row => row.Id == carve.Id).RepSubject); Assert.Equal("aoife", rows.Single(row => row.Id == unrelated.Id).RepSubject);
        Assert.Equal((3, 4), await CountsAsync());
        Assert.Contains("Colm (via Laragh)", WebUtility.HtmlDecode(await HtmlAsync(browser, Page(seed.Shop.Id))));
        await AssertHistoryAsync(seed.Shop.Id, "aoife", OwnershipChangeCause.TerritoryAssignment);
        var after = await CountsAsync(); using var replay = await ConfirmAsync(browser, review);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal(after, await CountsAsync());
        SetHeadOffice(web); using var api = app.CreateApiClient();
        using var created = await api.PostAsJsonAsync($"/directory/customers/{seed.CustomerId}/locations", new CreateLocationRequest("Future shop", seed.Town.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var future = (await created.Content.ReadFromJsonAsync<LocationDetails>())!;
        Assert.Equal("colm", (await CoveragePageAsync(future.Id)).Owner!.Rep.Subject); Assert.Equal((3, 5), await CountsAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_TransferSameDirectAssignment_When_ShopIsDirectOrGivingRepIsInactiveAndGeographyArchived(bool archived)
    {
        var seed = await SeedAsync(1); var direct = await AssignAsync("aoife", new(TerritoryLevel.Location, seed.Shop.Id));
        if (archived)
        {
            await using var scope = app.Api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Counties.Where(row => row.Id == seed.County.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.IsArchived, true));
            app.Staff.Entries["aoife"] = app.Staff.Entries["aoife"] with { Available = false };
        }
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string page = await HtmlAsync(browser, Page(seed.Shop.Id)); Assert.Contains("Change just this shop", page); Assert.DoesNotContain(">Transfer ", page);
        var review = await PreviewLocationAsync(browser, seed, "Shop"); Assert.Equal("/Coverage/Transfer?handler=Save", review.SaveUrl);
        using var saved = await ConfirmAsync(browser, review); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(Page(seed.Shop.Id), saved.Headers.Location!.OriginalString);
        Assert.Equal(direct.Id, Assert.Single(await AssignmentRowsAsync()).Id); Assert.Equal("colm", Assert.Single(await AssignmentRowsAsync()).RepSubject);
        Assert.Equal((1, 1), await CountsAsync()); await AssertHistoryAsync(seed.Shop.Id, "aoife", OwnershipChangeCause.DirectLocationAssignment);
        Assert.Contains("Colm (assigned directly)", WebUtility.HtmlDecode(await HtmlAsync(browser, Page(seed.Shop.Id))));
    }

    [Theory]
    [InlineData(TerritoryLevel.County, 4)] [InlineData(TerritoryLevel.Region, 5)]
    public async Task Should_KeepExcludedAreaAndNarrowerAssignments_When_LocationSourceTransferIsPartial(TerritoryLevel level, int changed)
    {
        var seed = await SeedAsync(5); Guid excluded = seed.OtherTown.Id;
        if (level == TerritoryLevel.Region)
        {
            using var api = app.CreateApiClient(); var county = await Place(api, "counties", "Wexford", seed.Region.Id);
            var town = await Place(api, "towns", "Gorey", county.Id); excluded = county.Id;
            using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Other", new("Excluded shop", town.Id)));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        var source = await AssignAsync("aoife", new(level, level == TerritoryLevel.County ? seed.County.Id : seed.Region.Id));
        var carve = await AssignAsync("colm", new(TerritoryLevel.Location, await NeighbourAsync(seed)));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, "Source", fields => fields.RemoveAll(row => row.Key == "Selected" && row.Value.EndsWith(excluded.ToString("D"), StringComparison.Ordinal)));
        Assert.Contains(changed + " Locations move", review.Html);
        using var saved = await ConfirmAsync(browser, review); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var rows = await AssignmentRowsAsync(); Assert.Equal("aoife", rows.Single(row => row.Id == source.Id).RepSubject);
        Assert.Equal("colm", rows.Single(row => row.Id == carve.Id).RepSubject);
        Assert.Equal(level == TerritoryLevel.County ? TerritoryLevel.Town : TerritoryLevel.County, rows.Single(row => row.Id != source.Id && row.Id != carve.Id).Target.Level);
        Assert.Equal((3, changed), await CountsAsync()); await AssertHistoryAsync(seed.Shop.Id, "aoife", OwnershipChangeCause.TerritoryAssignment);
    }

    [Theory]
    [InlineData(false, "town-name")] [InlineData(false, "rep-name")] [InlineData(false, "actor-name")] [InlineData(false, "assignment")]
    [InlineData(true, "town-name")] [InlineData(true, "rep-name")] [InlineData(true, "actor-name")] [InlineData(true, "assignment")]
    public async Task Should_RefreshProofAndRequireAnotherConfirmation_When_ReviewedSnapshotChanges(bool transfer, string change)
    {
        var seed = await SeedAsync(3); await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, transfer ? "Source" : "Shop"); string oldProof = review.Fields.Single(row => row.Key == "PreviewProof").Value;
        if (change == "town-name")
        {
            await using var scope = app.Api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Towns.Where(row => row.Id == seed.Town.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.Name, "Renamed Town"));
        }
        if (change == "rep-name") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { DisplayName = "Renamed Colm" };
        if (change == "actor-name") app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { DisplayName = "Renamed Manager" };
        if (change == "assignment") await AssignAsync("colm", new(TerritoryLevel.Location, await NeighbourAsync(seed)));
        var before = await CountsAsync(); using var stale = await ConfirmAsync(browser, review); Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        string html = await stale.Content.ReadAsStringAsync(); Assert.Contains("Nothing was saved.", html);
        var fields = Inputs(html); Assert.NotEqual(oldProof, fields.Single(row => row.Key == "PreviewProof").Value); Assert.DoesNotContain(fields, row => row.Key == "Confirmed");
        Assert.Equal(before, await CountsAsync());
        using var unconfirmed = await browser.PostAsync(review.SaveUrl, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, unconfirmed.StatusCode);
        Assert.Equal(before, await CountsAsync());
        Set(fields, "Confirmed", "true");
        using var saved = await browser.PostAsync(review.SaveUrl, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(before.Item2 + (transfer ? change == "assignment" ? 2 : 3 : 1), (await CountsAsync()).Item2);
    }

    [Theory]
    [InlineData(false, "proof")] [InlineData(false, "missing-proof")] [InlineData(false, "unconfirmed")]
    [InlineData(false, "csrf")] [InlineData(false, "recipient")] [InlineData(false, "reason")]
    [InlineData(true, "proof")] [InlineData(true, "missing-proof")] [InlineData(true, "unconfirmed")]
    [InlineData(true, "csrf")] [InlineData(true, "recipient")] [InlineData(true, "reason")]
    public async Task Should_SaveNothing_When_LocationSaveBypassesOrChangesReviewedCommand(bool transfer, string change)
    {
        var seed = await SeedAsync(2); if (transfer) await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, transfer ? "Source" : "Shop"); Set(review.Fields, "Confirmed", "true");
        if (change == "proof") Set(review.Fields, "PreviewProof", "forged");
        if (change == "missing-proof") review.Fields.RemoveAll(row => row.Key == "PreviewProof");
        if (change == "unconfirmed") review.Fields.RemoveAll(row => row.Key == "Confirmed");
        if (change == "csrf") review.Fields.RemoveAll(row => row.Key == "__RequestVerificationToken");
        if (change == "recipient") Set(review.Fields, transfer ? "ReceivingRepSubject" : "RepSubject", "brian");
        if (change == "reason") Set(review.Fields, "Reason", "Changed after preview");
        var before = await CountsAsync(); using var response = await browser.PostAsync(review.SaveUrl, new FormUrlEncodedContent(review.Fields));
        Assert.Equal(change == "csrf" || transfer && change == "recipient" ? HttpStatusCode.BadRequest : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await CountsAsync());
        if (change is "proof" or "missing-proof" or "unconfirmed" or "reason")
        {
            string expected = change switch { "proof" => "This preview is invalid. Review the impact again.",
                "reason" => "This preview does not confirm this assignment change. Review it again.",
                _ => "Review the impact and explicitly confirm this assignment change." };
            Assert.Contains(expected, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }
    }

    [Theory]
    [InlineData("inactive")] [InlineData("non-rep")] [InlineData("reporting")]
    [InlineData("api-role")] [InlineData("cookie-role")] [InlineData("staff-unavailable")] [InlineData("archive")]
    public async Task Should_DenyWithoutWrites_When_RecipientAuthorityOrTargetChangesBeforeSave(string change)
    {
        var seed = await SeedAsync(1); await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, "Shop");
        if (change == "inactive") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        if (change == "non-rep") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Roles = [BusinessRoles.HeadOfficeUser] };
        if (change == "api-role") app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        if (change == "cookie-role") web.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        if (change == "staff-unavailable") app.Staff.Unavailable = true;
        if (change is "reporting" or "archive")
        {
            await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            if (change == "reporting") await db.RepReportingLines.Where(row => row.RepSubject == "colm").ExecuteUpdateAsync(set => set.SetProperty(row => row.ManagerSubject, "another-manager"));
            else await db.Towns.Where(row => row.Id == seed.Town.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.IsArchived, true));
        }
        var before = await CountsAsync(); using var response = await ConfirmAsync(browser, review);
        var expected = change switch { "api-role" => HttpStatusCode.Forbidden, "cookie-role" => HttpStatusCode.Redirect,
            "staff-unavailable" => HttpStatusCode.ServiceUnavailable, "archive" => HttpStatusCode.Conflict, _ => HttpStatusCode.OK };
        Assert.Equal(expected, response.StatusCode); Assert.Equal(before, await CountsAsync());
        Assert.DoesNotContain("Assignment change saved.", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("source")] [InlineData("assignment")] [InlineData("selection")] [InlineData("entry")]
    public async Task Should_RejectExpandedTransferContext_When_OriginalLocationEntryIsTampered(string change)
    {
        var seed = await SeedAsync(1); await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        var other = await AssignAsync("aoife", new(TerritoryLevel.Town, seed.OtherTown.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, "Source");
        if (change == "source") Set(review.Fields, "RepSubject", "colm");
        if (change == "assignment") Set(review.Fields, "PullAssignmentId", other.Id.ToString("D"));
        if (change == "selection") Set(review.Fields, "ReviewedSelections", $"{other.Id:D}:Town:{seed.OtherTown.Id:D}");
        if (change == "entry") Set(review.Fields, "LocationEntry", "forged");
        var before = await CountsAsync(); using var rejected = await ConfirmAsync(browser, review);
        Assert.Equal(change == "selection" ? HttpStatusCode.OK : HttpStatusCode.BadRequest, rejected.StatusCode); Assert.Equal(before, await CountsAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_RollBackAndRetryOnce_When_LocationSaveFailsAfterHistoryWrite(bool transfer)
    {
        var seed = await SeedAsync(2); if (transfer) await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, transfer ? "Source" : "Shop"); var before = await CountsAsync();
        app.Failures.FailAfterHistorySave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ConfirmAsync(browser, review));
        Assert.True(app.Failures.DidFailAfterSave); Assert.Equal(before, await CountsAsync());
        using var retry = await ConfirmAsync(browser, review); Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        Assert.Equal(before.Item2 + (transfer ? 2 : 1), (await CountsAsync()).Item2);
    }

    [Fact]
    public async Task Should_KeepTownFirstWithoutZeroNeighbours_When_OnlyUnassignedShopHasDirectlyAssignedNeighbours()
    {
        var seed = await SeedAsync(3); var first = await NeighbourAsync(seed); await AssignAsync("aoife", new(TerritoryLevel.Location, first));
        await AssignAsync("brian", new(TerritoryLevel.Location, await NeighbourAsync(seed, first)));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string html = await HtmlAsync(browser, Page(seed.Shop.Id)); Assert.Contains("Assign Laragh (Town)", html);
        Assert.DoesNotContain("other Locations", html); Assert.DoesNotContain("other Location", html);
        var review = await PreviewLocationAsync(browser, seed, "Town"); Assert.Contains("1 Location becomes Colm", review.Html);
        using var saved = await ConfirmAsync(browser, review); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode); Assert.Equal((3, 1), await CountsAsync());
    }

    [Fact]
    public async Task Should_ShowNoEligibleRecipientsAndArchivedGuidance_When_ActionCannotBeUsed()
    {
        var seed = await SeedAsync(1); app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        app.Staff.Entries["aoife"] = app.Staff.Entries["aoife"] with { Available = false };
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        string page = await HtmlAsync(browser, Page(seed.Shop.Id)); string html = await HtmlAsync(browser, Action(page, "Shop"));
        Assert.Contains("No eligible receiving reps", html); Assert.DoesNotContain("<select", html); Assert.Equal((0, 0), await CountsAsync());
        await using (var scope = app.Api.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Towns.Where(row => row.Id == seed.Town.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.IsArchived, true));
        html = await HtmlAsync(browser, Page(seed.Shop.Id)); Assert.Contains("New assignments require active geography", html); Assert.DoesNotContain("href=\"/Coverage/LocationChange", html);
        Assert.Equal((0, 0), await CountsAsync());
    }

    [Theory]
    [InlineData("moved-shop")] [InlineData("direct-created")] [InlineData("source-rep")]
    [InlineData("source-team")] [InlineData("receiving-team")]
    public async Task Should_RequireFreshReviewOrDeny_When_LocationScopeOrTransferAuthorityChanges(string change)
    {
        var seed = await SeedAsync(2); var source = await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web);
        var review = await PreviewLocationAsync(browser, seed, change == "direct-created" ? "Shop" : "Source");
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            if (change == "moved-shop") await db.Locations.Where(row => row.Id == seed.Shop.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.TownId, seed.OtherTown.Id));
            if (change == "source-rep") await db.TerritoryAssignments.Where(row => row.Id == source.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.RepSubject, "colm"));
            if (change is "source-team" or "receiving-team") await db.RepReportingLines.Where(row => row.RepSubject == (change == "source-team" ? "aoife" : "colm"))
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.ManagerSubject, "another-manager"));
        }
        if (change == "direct-created") await AssignAsync("brian", new(TerritoryLevel.Location, seed.Shop.Id));
        var before = await CountsAsync(); using var rejected = await ConfirmAsync(browser, review);
        Assert.Equal(change == "receiving-team" ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict, rejected.StatusCode); Assert.Equal(before, await CountsAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_RequireExistingReportingLine_When_HeadOfficeChoosesLocationRecipient(bool transfer)
    {
        var seed = await SeedAsync(1); if (transfer) await AssignAsync("aoife", new(TerritoryLevel.Town, seed.Town.Id));
        await using var web = app.CreateWebsite(); using var browser = await BrowserAsync(web); SetHeadOffice(web);
        await using (var scope = app.Api.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().RepReportingLines.Where(row => row.RepSubject == "colm").ExecuteDeleteAsync();
        string entry = Action(await HtmlAsync(browser, Page(seed.Shop.Id)), transfer ? "Source" : "Shop");
        string html = await HtmlAsync(browser, entry);
        Assert.Contains("<option value=\"brian\"", html); Assert.DoesNotContain("<option value=\"colm\"", html);
        var fields = Inputs(html); Set(fields, "RepSubject", "colm"); var before = await CountsAsync();
        using var rejected = await browser.PostAsync(Change(seed.Shop.Id) + "?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode); Assert.Contains("Choose an eligible rep", await rejected.Content.ReadAsStringAsync()); Assert.Equal(before, await CountsAsync());

    }

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)] [InlineData("scope", HttpStatusCode.Forbidden)]
    [InlineData("subject", HttpStatusCode.Unauthorized)] [InlineData("field-sales", HttpStatusCode.Forbidden)]
    [InlineData("sysadmin", HttpStatusCode.Forbidden)] [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    [InlineData("missing-location", HttpStatusCode.NotFound)]
    public async Task Should_DenyOrFailReadSafely_When_LocationActionEndpointCannotVerifyStaffOrLocation(string failure, HttpStatusCode expected)
    {
        var seed = await SeedAsync(1); using var api = app.CreateApiClient();
        if (failure == "anonymous") api.DefaultRequestHeaders.Authorization = null;
        if (failure == "scope") api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: "openid"));
        if (failure == "subject") api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(subject: ""));
        if (failure == "field-sales") app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        if (failure == "sysadmin") app.Roles.SetRoles("niamh");
        if (failure == "unavailable") app.Roles.Unavailable = true;
        using var response = await api.GetAsync(Actions(failure == "missing-location" ? Guid.NewGuid() : seed.Shop.Id));
        Assert.Equal(expected, response.StatusCode); Assert.Equal((0, 0), await CountsAsync());
    }

    private void SetHeadOffice(StaffWebsiteFactory web)
    {
        app.Roles.SetRoles("niamh", BusinessRoles.HeadOfficeUser); web.Roles.SetRoles("niamh", BusinessRoles.HeadOfficeUser);
        app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [BusinessRoles.HeadOfficeUser] };
    }
    private async Task<Guid> NeighbourAsync(Seed seed, Guid? except = null)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Locations
            .Where(row => row.TownId == seed.Town.Id && row.Id != seed.Shop.Id && (except == null || row.Id != except)).Select(row => row.Id).FirstAsync();
    }
    private async Task<TerritoryAssignment[]> AssignmentRowsAsync()
    { await using var scope = app.Api.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().TerritoryAssignments.AsNoTracking().ToArrayAsync(); }
    private async Task<LocationCoveragePage> CoveragePageAsync(Guid id)
    { using var api = app.CreateApiClient(); return (await api.GetFromJsonAsync<LocationCoveragePage>($"/coverage/locations/{id}/page"))!; }
    private async Task AssertHistoryAsync(Guid id, string? old, OwnershipChangeCause cause)
    {
        using var api = app.CreateApiClient(); var page = (await api.GetFromJsonAsync<LocationCoverageHistoryPage>($"/coverage/locations/{id}/page/history"))!;
        var row = Assert.Single(page.Entries); Assert.Equal(old, row.PreviousOwner?.Rep.Subject); Assert.Equal("colm", row.NewOwner!.Rep.Subject);
        Assert.Equal("Colm", row.NewOwner.Rep.DisplayName); Assert.Equal("Niamh Byrne", row.Actor.DisplayName);
        Assert.Equal(cause, row.Cause); Assert.Equal("Agreed shop coverage", row.Reason);
    }
    private async Task<ReviewedLocation> PreviewLocationAsync(HttpClient browser, Seed seed, string intent, Action<List<KeyValuePair<string, string>>>? selections = null)
    {
        string entry = Action(await HtmlAsync(browser, Page(seed.Shop.Id)), intent); string html = await HtmlAsync(browser, entry);
        var fields = Inputs(html); Set(fields, "RepSubject", "colm"); Set(fields, "Reason", "Agreed shop coverage");
        using var preview = await browser.PostAsync(Change(seed.Shop.Id) + "?handler=Preview", new FormUrlEncodedContent(fields));
        bool transfer = preview.StatusCode == HttpStatusCode.Redirect;
        if (transfer)
        {
            html = await HtmlAsync(browser, preview.Headers.Location!.OriginalString); fields = Inputs(html); selections?.Invoke(fields);
            using var impact = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, impact.StatusCode); html = await impact.Content.ReadAsStringAsync();
        }
        else { Assert.Equal(HttpStatusCode.OK, preview.StatusCode); html = await preview.Content.ReadAsStringAsync(); }
        Assert.Contains(transfer ? "Confirm transfer" : "Confirm assignment", html);
        return new(html, transfer ? "/Coverage/Transfer?handler=Save" : Change(seed.Shop.Id) + "?handler=Save", Inputs(html));
    }
    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient browser, ReviewedLocation review)
    { var fields = review.Fields.ToList(); Set(fields, "Confirmed", "true"); return browser.PostAsync(review.SaveUrl, new FormUrlEncodedContent(fields)); }
    private sealed record ReviewedLocation(string Html, string SaveUrl, List<KeyValuePair<string, string>> Fields);
}
