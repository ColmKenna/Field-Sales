extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using CoverageUsageSource = CatalogueApi::FieldSales.Api.Directory.TerritoryGeographyUsageSource;
using GeographyUsageReader = CatalogueApi::FieldSales.Api.Directory.GeographyUsageReader;
using LocationTownUsageSource = CatalogueApi::FieldSales.Api.Directory.LocationTownUsageSource;
using ReferenceUsageUnavailableException = CatalogueApi::FieldSales.Api.Catalogue.ReferenceUsageUnavailableException;

namespace FieldSales.Web.Tests;

public sealed class CoverageMutationIntegrationTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    private const string Root = "/coverage";
    private const string Page = "/HeadOffice/Coverage/ReportingLines";

    [Fact]
    public async Task Should_Write140Then23AndFallbackHistory_When_ProductionCountyAndTownAssignmentsChange()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 23, 117);
        var county = await AssignAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        Assert.Equal(140, await HistoryCountAsync());
        Assert.Equal(140, (await api.GetFromJsonAsync<LocationCoverageDetails[]>(Root + "/reps/colm/locations"))!.Length);
        var town = await AssignAsync(api, "aoife", TerritoryLevel.Town, seed.Town.Id);
        Assert.Equal(163, await HistoryCountAsync());
        Assert.Equal(23, (await api.GetFromJsonAsync<LocationCoverageDetails[]>(Root + "/reps/aoife/locations"))!.Length);
        Assert.Equal("aoife", (await OwnerAsync(api, seed.Locations[0])).Owner!.RepSubject);
        Assert.Equal("colm", (await OwnerAsync(api, seed.Locations[23])).Owner!.RepSubject);
        var removed = await RemoveAsync(api, town); Assert.Equal(23, removed.ChangedLocations);
        Assert.Equal(186, await HistoryCountAsync());
        var restored = (await api.GetFromJsonAsync<LocationCoverageDetails[]>(Root + "/reps/colm/locations"))!;
        Assert.Equal(140, restored.Length); Assert.All(restored, row => Assert.Equal(county.Id, row.Owner!.Source.AssignmentId));
        var history = (await api.GetFromJsonAsync<AssignmentHistoryDetails[]>($"{Root}/locations/{seed.Locations[0]}/history"))!;
        Assert.Equal(3, history.Length); Assert.Equal("Aoife", history[0].PreviousOwner!.Rep.DisplayName);
        Assert.Equal("Colm", history[0].NewOwner!.Rep.DisplayName); Assert.Equal("Niamh Byrne", history[0].Actor.DisplayName);
        Assert.False(app.Staff.SawTransaction);
    }

    [Fact]
    public async Task Should_Preserve23CarveOuts_When_ParentCountyAssignmentIsRemoved()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 23, 117);
        var county = await AssignAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync(api, "aoife", TerritoryLevel.Town, seed.Town.Id);
        Assert.Equal(117, (await RemoveAsync(api, county)).ChangedLocations);
        Assert.Equal(280, await HistoryCountAsync());
        Assert.Equal("aoife", (await OwnerAsync(api, seed.Locations[0])).Owner!.RepSubject);
        Assert.Null((await OwnerAsync(api, seed.Locations[23])).Owner);
    }

    [Fact]
    public async Task Should_Leave96LocationsUnassigned_When_BriansWexfordAssignmentIsRemoved()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 96, countyName: "Wexford");
        var assignment = await AssignAsync(api, "brian", TerritoryLevel.County, seed.County.Id);
        Assert.Equal(96, (await RemoveAsync(api, assignment)).ChangedLocations); Assert.Equal(192, await HistoryCountAsync());
        foreach (Guid location in seed.Locations) Assert.Null((await OwnerAsync(api, location)).Owner);
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        Assert.Null((await OwnerAsync(restarted, seed.Locations[0])).Owner);
        Assert.Equal(2, (await restarted.GetFromJsonAsync<AssignmentHistoryDetails[]>($"{Root}/locations/{seed.Locations[0]}/history"))!.Length);
    }

    [Fact]
    public async Task Should_PreferAllFourLevelsAndRecordDirectCause_When_RemovalsWalkFallbackChain()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var region = await AssignAsync(api, "colm", TerritoryLevel.Region, seed.Region.Id);
        var county = await AssignAsync(api, "aoife", TerritoryLevel.County, seed.County.Id);
        var town = await AssignAsync(api, "brian", TerritoryLevel.Town, seed.Town.Id);
        var location = await AssignAsync(api, "colm", TerritoryLevel.Location, seed.Locations[0]);
        Assert.Equal(TerritoryLevel.Location, (await OwnerAsync(api, seed.Locations[0])).Owner!.Source.Target.Level);
        await RemoveAsync(api, location); Assert.Equal("brian", (await OwnerAsync(api, seed.Locations[0])).Owner!.RepSubject);
        await RemoveAsync(api, town); Assert.Equal("aoife", (await OwnerAsync(api, seed.Locations[0])).Owner!.RepSubject);
        await RemoveAsync(api, county); Assert.Equal("colm", (await OwnerAsync(api, seed.Locations[0])).Owner!.RepSubject);
        await RemoveAsync(api, region); Assert.Null((await OwnerAsync(api, seed.Locations[0])).Owner);
        var entries = (await api.GetFromJsonAsync<AssignmentHistoryDetails[]>($"{Root}/locations/{seed.Locations[0]}/history"))!;
        Assert.Equal(8, entries.Length); Assert.Equal(2, entries.Count(row => row.Cause == OwnershipChangeCause.DirectLocationAssignment));
    }

    [Fact]
    public async Task Should_WriteNoHistory_When_SourceChangesButRepDoesNot()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        var town = await AssignAsync(api, "colm", TerritoryLevel.Town, seed.Town.Id);
        Assert.Equal(1, await HistoryCountAsync()); Assert.Equal(0, (await RemoveAsync(api, town)).ChangedLocations);
        Assert.Equal(1, await HistoryCountAsync());
    }

    [Theory]
    [InlineData(TerritoryLevel.Region)] [InlineData(TerritoryLevel.County)] [InlineData(TerritoryLevel.Town)] [InlineData(TerritoryLevel.Location)]
    public async Task Should_RejectDuplicateAbsentArchivedAndConcurrentTargets_When_AddingAssignments(TerritoryLevel level)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        Guid id = Target(seed, level);
        using var missing = await AddAsync(api, "colm", level, Guid.NewGuid()); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var responses = await Task.WhenAll(AddAsync(api, "colm", level, id), AddAsync(api, "aoife", level, id));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Equal(1, await AssignmentCountAsync()); Assert.Equal(1, await HistoryCountAsync());
        using var duplicate = await AddAsync(api, "brian", level, id); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var assignment = (await api.GetFromJsonAsync<AssignedTerritoryDetails[]>($"{Root}/reps/colm/assignments"))!
            .Concat((await api.GetFromJsonAsync<AssignedTerritoryDetails[]>($"{Root}/reps/aoife/assignments"))!).Single().Assignment;
        await RemoveAsync(api, assignment);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            // Ancestor archive also makes a Location ineligible; existing owner removal stays allowed.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Regions] SET [IsArchived]=1 WHERE [Id]={seed.Region.Id}");
        }
        using var archived = await AddAsync(api, "colm", level, id); Assert.Equal(HttpStatusCode.BadRequest, archived.StatusCode);
        Assert.Equal(0, await AssignmentCountAsync()); Assert.Equal(2, await HistoryCountAsync());
    }

    [Fact]
    public async Task Should_RejectMalformedVersionsAndPayloads_When_NoOwnershipChangeIsValid()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        foreach (var request in new[] { new AddTerritoryAssignmentRequest(null, null), new("colm", new((TerritoryLevel)99, seed.Town.Id)),
            new("colm", new(TerritoryLevel.Town, Guid.Empty)), new(" colm", new(TerritoryLevel.Town, seed.Town.Id)),
            new("colm", new(TerritoryLevel.Town, seed.Town.Id), new string('x', 1001)) })
        { using var rejected = await api.PostAsJsonAsync(Root + "/assignments", request); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode); }
        var assigned = await AssignAsync(api, "colm", TerritoryLevel.Town, seed.Town.Id);
        foreach (string? version in new string?[] { null, "invalid", Convert.ToBase64String([1]), Convert.ToBase64String(new byte[8]) })
        {
            using var rejected = await api.PostAsJsonAsync($"{Root}/assignments/{assigned.Id}/remove", new RemoveTerritoryAssignmentRequest(version));
            Assert.Equal(version == Convert.ToBase64String(new byte[8]) ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        Assert.Equal(1, await AssignmentCountAsync()); Assert.Equal(1, await HistoryCountAsync());
        using var absent = await api.PostAsJsonAsync($"{Root}/assignments/{Guid.NewGuid()}/remove", new RemoveTerritoryAssignmentRequest(assigned.Version));
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
    }

    [Fact]
    public async Task Should_RestrictManagersToCurrentTeamAndExistingHolder_When_AddingOrRemovingAssignments()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var foreign = await AssignAsync(api, "brian", TerritoryLevel.County, seed.County.Id);
        app.Roles.SetRoles("niamh", BusinessRoles.SalesManager);
        app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [BusinessRoles.SalesManager] };
        using var held = await AddAsync(api, "colm", TerritoryLevel.County, seed.County.Id); Assert.Equal(HttpStatusCode.Forbidden, held.StatusCode);
        using var foreignAdd = await AddAsync(api, "brian", TerritoryLevel.Town, seed.Town.Id); Assert.Equal(HttpStatusCode.Forbidden, foreignAdd.StatusCode);
        using var foreignRemove = await api.PostAsJsonAsync($"{Root}/assignments/{foreign.Id}/remove", new RemoveTerritoryAssignmentRequest(foreign.Version));
        Assert.Equal(HttpStatusCode.Forbidden, foreignRemove.StatusCode);
        var own = await AssignAsync(api, "colm", TerritoryLevel.Town, seed.Town.Id); await RemoveAsync(api, own);
        var reps = (await api.GetFromJsonAsync<StaffChoice[]>(Root + "/reps"))!; Assert.DoesNotContain(reps, rep => rep.Subject == "brian");
        using var reporting = await api.GetAsync(Root + "/reporting-lines"); Assert.Equal(HttpStatusCode.Forbidden, reporting.StatusCode);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            await db.RepReportingLines.Where(row => row.RepSubject == "colm").ExecuteDeleteAsync();
        }
        using var noTeam = await AddAsync(api, "colm", TerritoryLevel.Town, seed.Town.Id); Assert.Equal(HttpStatusCode.Forbidden, noTeam.StatusCode);
        app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        using var roleRemoved = await AddAsync(api, "colm", TerritoryLevel.Town, seed.Town.Id); Assert.Equal(HttpStatusCode.Forbidden, roleRemoved.StatusCode);
        Assert.Equal(1, await AssignmentCountAsync());
    }

    [Theory]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)] [InlineData("missing", HttpStatusCode.ServiceUnavailable)]
    [InlineData("invalid", HttpStatusCode.ServiceUnavailable)] [InlineData("inactive", HttpStatusCode.BadRequest)]
    [InlineData("non-rep", HttpStatusCode.BadRequest)] [InlineData("case", HttpStatusCode.ServiceUnavailable)]
    public async Task Should_FailClosedWithoutMutations_When_StaffEligibilityIsUncertain(string scenario, HttpStatusCode expected)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        if (scenario == "unavailable") app.Staff.Unavailable = true;
        if (scenario == "missing") app.Staff.Entries.Remove("colm");
        if (scenario == "invalid") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { DisplayName = " " };
        if (scenario == "inactive") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        if (scenario == "non-rep") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Roles = [BusinessRoles.SalesManager] };
        using var rejected = await AddAsync(api, scenario == "case" ? "Colm" : "colm", TerritoryLevel.County, seed.County.Id);
        Assert.Equal(expected, rejected.StatusCode); Assert.Equal(0, await AssignmentCountAsync()); Assert.Equal(0, await HistoryCountAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_RollBackAssignmentAndHistory_When_TheirFinalSaveFails(bool removal)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var previous = removal ? await AssignAsync(api, "colm", TerritoryLevel.County, seed.County.Id) : null;
        app.Failures.FailAfterHistorySave = true;
        Task<HttpResponseMessage> SaveAsync() => removal
            ? RemoveRequestAsync(api, previous!)
            : AddAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(SaveAsync); Assert.True(app.Failures.DidFailAfterSave);
        Assert.Equal(removal ? 1 : 0, await AssignmentCountAsync()); Assert.Equal(removal ? 1 : 0, await HistoryCountAsync());
        using var retry = await SaveAsync(); Assert.Equal(removal ? HttpStatusCode.OK : HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(removal ? 0 : 1, await AssignmentCountAsync()); Assert.Equal(removal ? 2 : 1, await HistoryCountAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    [Theory]
    [InlineData(TerritoryLevel.Region)] [InlineData(TerritoryLevel.County)] [InlineData(TerritoryLevel.Town)]
    public async Task Should_ProtectEmptyGeographyAndRetainOwnership_When_AssignedUnitIsArchived(TerritoryLevel level)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 0);
        var item = level switch { TerritoryLevel.Region => seed.Region, TerritoryLevel.County => seed.County, _ => seed.Town };
        // Town has no children; other levels are also covered by their direct assignment usage count.
        var assigned = await AssignAsync(api, "colm", level, item.Id);
        string plural = Plural(level);
        var read = (await api.GetFromJsonAsync<GeographyItem>($"/directory/geography/{plural}/{item.Id}"))!;
        Assert.Contains("territory assignment", read.Usage!.Description);
        using var deleted = await api.PostAsJsonAsync($"/directory/geography/{plural}/{item.Id}/retire", new RetireGeographyRequest(ReferenceAction.Delete, read.Version));
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        using var archived = await api.PostAsJsonAsync($"/directory/geography/{plural}/{item.Id}/retire", new RetireGeographyRequest(ReferenceAction.Archive, read.Version));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        using var duplicate = await AddAsync(api, "aoife", level, item.Id); Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(1, await AssignmentCountAsync()); Assert.Equal(0, await HistoryCountAsync());
        await RemoveAsync(api, assigned); Assert.Equal(0, await AssignmentCountAsync());
    }

    [Fact]
    public async Task Should_FailClosed_When_RequiredAssignmentUsageSourceIsMissing()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 0);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var reader = new GeographyUsageReader([new LocationTownUsageSource(db)]);
        await Assert.ThrowsAsync<ReferenceUsageUnavailableException>(() => reader.ReadAsync(new("towns", seed.Town.Id), default));
        var count = await new CoverageUsageSource(db).CountAsync(new("towns", seed.Town.Id), default); Assert.Equal(0, count.Count);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)]
    public async Task Should_RejectUntrustedAdditionalUsage_When_PublicZeroCountsWouldBeOmitted(long count)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 0);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var reader = new GeographyUsageReader([new LocationTownUsageSource(db), new InvalidAssignmentUsage(count)]);
        await Assert.ThrowsAsync<ReferenceUsageUnavailableException>(() => reader.ReadAsync(new("towns", seed.Town.Id), default));
    }

    private sealed class InvalidAssignmentUsage(long count) : IReferenceUsageSource
    {
        public string SourceKey => "territory-assignments";
        public bool Supports(string level) => level == "towns";
        public Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) =>
            Task.FromResult(new ReferenceCount(SourceKey, count == 0 ? "" : "assignment", "assignments", count));
        public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string level, IReadOnlyList<Guid> ids, CancellationToken ct)
        {
            var result = new Dictionary<Guid, ReferenceCount>();
            foreach (Guid id in ids) result[id] = await CountAsync(new(level, id), ct);
            return result;
        }
    }

    [Theory]
    [InlineData(TerritoryLevel.Region)] [InlineData(TerritoryLevel.County)] [InlineData(TerritoryLevel.Town)]
    public async Task Should_RetainOwnersAndRestoreChoices_When_AssignedHierarchyIsArchivedAndUnarchived(TerritoryLevel level)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var assignment = await AssignAsync(api, "colm", level, Target(seed, level));
        string plural = Plural(level);
        string url = $"/directory/geography/{plural}/{Target(seed, level)}";
        var before = (await api.GetFromJsonAsync<GeographyItem>(url))!;
        using var archived = await api.PostAsJsonAsync(url + "/retire", new RetireGeographyRequest(ReferenceAction.Archive, before.Version));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Equal(assignment.Id, (await OwnerAsync(api, seed.Locations[0])).Owner!.Source.AssignmentId);
        Assert.Equal(1, await HistoryCountAsync());
        using var forbidden = await AddAsync(api, "aoife", TerritoryLevel.Location, seed.Locations[0]);
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);
        var current = (await api.GetFromJsonAsync<GeographyItem>(url))!;
        using var restored = await api.PostAsJsonAsync(url + "/retire", new RetireGeographyRequest(ReferenceAction.Unarchive, current.Version));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var choices = (await api.GetFromJsonAsync<TownChoice[]>("/directory/geography/town-choices"))!;
        Assert.True(choices.Single(row => row.Id == seed.Town.Id).IsSelectable);
        await AssignAsync(api, "aoife", TerritoryLevel.Location, seed.Locations[0]); Assert.Equal(2, await HistoryCountAsync());
    }

    [Fact]
    public async Task Should_RequireAuthenticationAndScope_When_CoverageWritesAreRequested()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        api.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await AddAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: "openid"));
        using var scope = await AddAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        Assert.Equal(HttpStatusCode.Forbidden, scope.StatusCode);
        Assert.Equal(0, await AssignmentCountAsync()); Assert.Equal(0, await HistoryCountAsync());
    }

    [Fact]
    public async Task Should_RejectEarlierHeadOfficeAuthority_When_StaffLookupReportsOnlyManagerRole()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        // The request's initial role check saw Head Office. The later trusted
        // identity response observes its removal while the Manager role remains.
        app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [BusinessRoles.SalesManager] };
        using var assignment = await AddAsync(api, "brian", TerritoryLevel.County, seed.County.Id);
        Assert.Equal(HttpStatusCode.Forbidden, assignment.StatusCode);
        using var reporting = await api.PutAsJsonAsync(Root + "/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", null));
        Assert.Equal(HttpStatusCode.Forbidden, reporting.StatusCode);
        Assert.Equal(0, await AssignmentCountAsync()); Assert.Equal(0, await HistoryCountAsync());
    }

    [Fact]
    public async Task Should_ManageReportingLinesOnlyAsHeadOffice_When_VersionsAndStaffAreEligible()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var assigned = await AssignAsync(api, "colm", TerritoryLevel.County, seed.County.Id);
        var page = (await api.GetFromJsonAsync<ReportingLinesPage>(Root + "/reporting-lines"))!;
        var before = page.Lines.Single(row => row.Line.RepSubject == "colm").Line;
        using var saved = await api.PutAsJsonAsync(Root + "/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", before.Version));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode); var after = (await saved.Content.ReadFromJsonAsync<RepReportingLineDetails>())!;
        Assert.NotEqual(before.Version, after.Version); Assert.Equal(1, await HistoryCountAsync());
        Assert.Equal(assigned.Id, (await OwnerAsync(api, seed.Locations[0])).Owner!.Source.AssignmentId);
        using var stale = await api.PutAsJsonAsync(Root + "/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", before.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await api.PutAsJsonAsync(Root + "/reporting-lines/colm", new SetRepReportingLineRequest("aoife", after.Version));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        app.Staff.Entries["new-rep"] = new("new-rep", "New Rep", [BusinessRoles.FieldSalesperson], true);
        using var newLine = await api.PutAsJsonAsync(Root + "/reporting-lines/new-rep", new SetRepReportingLineRequest("another-manager", null));
        Assert.Equal(HttpStatusCode.OK, newLine.StatusCode);
        using var duplicate = await api.PutAsJsonAsync(Root + "/reporting-lines/new-rep", new SetRepReportingLineRequest("another-manager", null));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        app.Roles.SetRoles("niamh", BusinessRoles.SalesManager);
        using var team = await api.GetAsync(Root + "/reps/colm/locations"); Assert.Equal(HttpStatusCode.Forbidden, team.StatusCode);
        using var denied = await api.PutAsJsonAsync(Root + "/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", after.Version));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Should_EnforceAntiforgeryAndShowStaleInput_When_HeadOfficeUsesReportingPage()
    {
        await ResetAsync(); await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser();
        using var signIn = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(BusinessRoles.HeadOfficeUser));
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        string url = Page + "?repSubject=colm";
        using var loaded = await browser.GetAsync(url); Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        string html = await loaded.Content.ReadAsStringAsync(); Assert.Contains("Reporting line for Colm", html);
        string version = WebUtility.HtmlDecode(Regex.Match(html, "name=\"Version\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        string csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(version); Assert.NotEmpty(csrf);
        var fields = new Dictionary<string, string> { ["RepSubject"] = "colm", ["ManagerSubject"] = "another-manager", ["Version"] = version };
        using var noCsrf = await browser.PostAsync(Page, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        fields["__RequestVerificationToken"] = csrf;
        using var saved = await browser.PostAsync(Page, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var stale = await browser.PostAsync(Page, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        string error = await stale.Content.ReadAsStringAsync(); Assert.Contains("This item changed", error); Assert.Contains("Reload reporting line", error);
        Assert.Contains("Other Manager", error); Assert.Equal(0, await HistoryCountAsync());
        website.Roles.SetRoles("niamh", BusinessRoles.SalesManager);
        using var denied = await browser.GetAsync(Page); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
    }

    private async Task ResetAsync() => await app.ResetCoverageAsync();
    private async Task<Seed> SeedAsync(HttpClient api, int first = 1, int other = 0, string countyName = "Wicklow")
    {
        var region = await PlaceAsync(api, "regions", "Leinster"); var county = await PlaceAsync(api, "counties", countyName, region.Id);
        var town = await PlaceAsync(api, "towns", "Rathdrum", county.Id); var laragh = await PlaceAsync(api, "towns", "Laragh", county.Id);
        List<Guid> locations = [];
        for (int i = 0; i < first + other; i++)
        {
            using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Customer " + i,
                new("Shop " + i, i < first ? town.Id : laragh.Id)));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            locations.Add((await created.Content.ReadFromJsonAsync<CustomerDetails>())!.Locations[0].Id);
        }
        return new(region, county, town, locations.ToArray());
    }
    private static Guid Target(Seed seed, TerritoryLevel level) => level switch
    { TerritoryLevel.Region => seed.Region.Id, TerritoryLevel.County => seed.County.Id, TerritoryLevel.Town => seed.Town.Id, _ => seed.Locations[0] };
    private static string Plural(TerritoryLevel level) => level == TerritoryLevel.County ? "counties" : level.ToString().ToLowerInvariant() + "s";
    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parent = null)
    {
        using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
    private static async Task<HttpResponseMessage> AddAsync(HttpClient api, string rep, TerritoryLevel level, Guid id)
    {
        var request = new AddTerritoryAssignmentRequest(rep, new(level, id));
        var preview = await api.PostAsJsonAsync(Root + "/assignments/preview", request);
        if (!preview.IsSuccessStatusCode) return preview;
        var impact = (await preview.Content.ReadFromJsonAsync<AssignmentImpactDetails>())!; preview.Dispose();
        return await api.PostAsJsonAsync(Root + "/assignments", request with { PreviewProof = impact.Proof, Confirmed = true });
    }
    private static async Task<HttpResponseMessage> RemoveRequestAsync(HttpClient api, TerritoryAssignmentDetails assignment)
    {
        var request = new RemoveTerritoryAssignmentRequest(assignment.Version);
        var preview = await api.PostAsJsonAsync($"{Root}/assignments/{assignment.Id}/remove/preview", request);
        if (!preview.IsSuccessStatusCode) return preview;
        var impact = (await preview.Content.ReadFromJsonAsync<AssignmentImpactDetails>())!; preview.Dispose();
        return await api.PostAsJsonAsync($"{Root}/assignments/{assignment.Id}/remove", request with { PreviewProof = impact.Proof, Confirmed = true });
    }
    private static async Task<TerritoryAssignmentDetails> AssignAsync(HttpClient api, string rep, TerritoryLevel level, Guid id)
    {
        using var response = await AddAsync(api, rep, level, id); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TerritoryAssignmentDetails>())!;
    }
    private static async Task<CoverageMutationResult> RemoveAsync(HttpClient api, TerritoryAssignmentDetails assignment)
    {
        using var response = await RemoveRequestAsync(api, assignment);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CoverageMutationResult>())!;
    }
    private static async Task<LocationCoverageDetails> OwnerAsync(HttpClient api, Guid id) => (await api.GetFromJsonAsync<LocationCoverageDetails>($"{Root}/locations/{id}/owner"))!;
    private async Task<int> HistoryCountAsync()
    { await using var scope = app.Api.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().AssignmentHistory.CountAsync(); }
    private async Task<int> AssignmentCountAsync()
    { await using var scope = app.Api.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().TerritoryAssignments.CountAsync(); }
    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem Town, Guid[] Locations);
}
