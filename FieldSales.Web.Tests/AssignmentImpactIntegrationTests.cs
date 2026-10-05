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
using OwnershipReader = CatalogueApi::FieldSales.Api.Coverage.CoverageOwnershipReader;
using DryRun = CatalogueApi::FieldSales.Api.Coverage.AssignmentImpactPreview;

namespace FieldSales.Web.Tests;

public sealed class AssignmentImpactIntegrationTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    private const string Root = "/coverage/assignments";
    private const string Page = "/Coverage/Assignments";

    [Fact]
    public async Task Should_Preview140Then23AndFallback_When_WicklowAndRathdrumChange()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 23, 117);
        var countyRequest = Add("colm", TerritoryLevel.County, seed.County.Id);
        var county = await PreviewAsync(api, countyRequest);
        Assert.Equal("140 Locations become Colm's", Assert.Single(county.Groups).Sentence);
        Assert.Equal(140, county.ChangedLocations); Assert.Equal(140, county.Groups[0].Locations.Count);
        Assert.All(county.Groups[0].Locations, row => { Assert.Null(row.PreviousOwner); Assert.Equal("Wicklow", row.NewOwner!.SourceName); });
        Assert.Equal((0, 0), await CountsAsync());
        await SaveAsync(api, countyRequest, county);
        Assert.Equal((1, 140), await CountsAsync());
        var townRequest = Add("aoife", TerritoryLevel.Town, seed.Town.Id); var town = await PreviewAsync(api, townRequest);
        Assert.Equal("23 Locations move from Colm to Aoife", Assert.Single(town.Groups).Sentence);
        Assert.Equal(seed.Locations.Take(23).Order(), town.Groups[0].Locations.Select(row => row.LocationId).Order());
        var assignment = await SaveAsync(api, townRequest, town);
        Assert.Equal("Aoife", town.Groups[0].Locations.Single(row => row.Name == "Murphy's Pharmacy").NewOwner!.Rep.Name);
        Assert.Equal("colm", (await OwnerAsync(api, seed.Locations[23])).Owner!.RepSubject);
        Assert.Equal("Doyle's Shop", (await OwnerAsync(api, seed.Locations[23])).Name);
        var removal = await RemovalPreviewAsync(api, assignment);
        Assert.Equal("23 Locations move from Aoife to Colm", Assert.Single(removal.Groups).Sentence);
        await RemoveAsync(api, assignment, removal);
        Assert.Equal((1, 186), await CountsAsync());
        foreach (var id in seed.Locations) Assert.Equal("Wicklow", (await OwnerAsync(api, id)).Owner!.Source.Name);
        Assert.False(app.Staff.SawTransaction);
    }

    [Fact]
    public async Task Should_RequireExplicitUnassignedConfirmation_When_BriansWexfordIsRemoved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 96, countyName: "Wexford");
        var request = Add("brian", TerritoryLevel.County, seed.County.Id);
        var assignment = await SaveAsync(api, request, await PreviewAsync(api, request));
        var preview = await RemovalPreviewAsync(api, assignment);
        Assert.Equal("96 Locations become Unassigned", Assert.Single(preview.Groups).Sentence);
        Assert.Equal(96, preview.Groups[0].Locations.Count);
        using var unconfirmed = await api.PostAsJsonAsync($"{Root}/{assignment.Id}/remove",
            new RemoveTerritoryAssignmentRequest(assignment.Version, PreviewProof: preview.Proof));
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode); Assert.Equal((1, 96), await CountsAsync());
        await RemoveAsync(api, assignment, preview); Assert.Equal((0, 192), await CountsAsync());
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        Assert.Null((await OwnerAsync(restarted, seed.Locations[0])).Owner);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_PreviewZeroWithoutOwnerHistory_When_SameRepOrCarveOutShieldsLocations(bool carveOut)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var firstRequest = Add("colm", carveOut ? TerritoryLevel.Location : TerritoryLevel.County,
            carveOut ? seed.Locations[0] : seed.County.Id);
        await SaveAsync(api, firstRequest, await PreviewAsync(api, firstRequest));
        var request = Add(carveOut ? "aoife" : "colm", TerritoryLevel.Town, seed.Town.Id);
        var preview = await PreviewAsync(api, request); Assert.Equal(0, preview.ChangedLocations); Assert.Empty(preview.Groups);
        await SaveAsync(api, request, preview); Assert.Equal((2, 1), await CountsAsync());
    }

    [Fact]
    public async Task Should_KeepCarveOuts_When_ParentIsRemoved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 23, 117);
        var parentRequest = Add("colm", TerritoryLevel.County, seed.County.Id);
        var parent = await SaveAsync(api, parentRequest, await PreviewAsync(api, parentRequest));
        var town = Add("aoife", TerritoryLevel.Town, seed.Town.Id); await SaveAsync(api, town, await PreviewAsync(api, town));
        var preview = await RemovalPreviewAsync(api, parent); Assert.Equal(117, preview.ChangedLocations);
        Assert.Equal("117 Locations become Unassigned", Assert.Single(preview.Groups).Sentence);
        Assert.DoesNotContain(preview.Groups[0].Locations, row => seed.Locations.Take(23).Contains(row.LocationId));
        await RemoveAsync(api, parent, preview); Assert.Equal((1, 280), await CountsAsync());
        Assert.Equal("aoife", (await OwnerAsync(api, seed.Locations[0])).Owner!.RepSubject);
    }

    [Fact]
    public async Task Should_PreviewAllFallbackLevels_When_DirectAssignmentsAreRemoved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        List<TerritoryAssignmentDetails> rows = [];
        foreach (var (rep, level, id) in new[] { ("brian", TerritoryLevel.Region, seed.Region.Id), ("colm", TerritoryLevel.County, seed.County.Id),
            ("aoife", TerritoryLevel.Town, seed.Town.Id), ("colm", TerritoryLevel.Location, seed.Locations[0]) })
        {
            var request = Add(rep, level, id); rows.Add(await SaveAsync(api, request, await PreviewAsync(api, request)));
        }
        foreach (var assignment in rows.AsEnumerable().Reverse())
        {
            var preview = await RemovalPreviewAsync(api, assignment); Assert.Equal(1, preview.ChangedLocations);
            await RemoveAsync(api, assignment, preview);
            Assert.Equal(preview.Groups[0].Locations[0].NewOwner?.Rep.Subject, (await OwnerAsync(api, seed.Locations[0])).Owner?.RepSubject);
        }
        Assert.Equal((0, 8), await CountsAsync());
    }

    [Theory]
    [InlineData(11, true)] [InlineData(22, true)] [InlineData(33, true)] [InlineData(44, true)]
    [InlineData(55, false)] [InlineData(66, false)] [InlineData(77, false)] [InlineData(88, false)]
    public async Task Should_MatchSavedOwnersAndHistory_When_GeneratedValidChangesAreApplied(int randomSeed, bool adding)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 4, 3);
        var random = new Random(randomSeed);
        var targets = new[] { new TerritoryTarget(TerritoryLevel.Region, seed.Region.Id), new(TerritoryLevel.County, seed.County.Id),
            new(TerritoryLevel.Town, seed.Town.Id), new(TerritoryLevel.Town, seed.OtherTown.Id) }
            .Concat(seed.Locations.Select(id => new TerritoryTarget(TerritoryLevel.Location, id))).ToArray();
        List<TerritoryAssignment> baseline = [];
        foreach (var target in targets.Take(targets.Length - 1))
            if (random.Next(2) == 0) baseline.Add(TerritoryAssignment.Create(new[] { "colm", "aoife", "brian" }[random.Next(3)], target));
        if (baseline.Count == 0) baseline.Add(TerritoryAssignment.Create("colm", targets[0]));
        await using (var setup = app.Api.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<DirectoryDbContext>(); db.TerritoryAssignments.AddRange(baseline); await db.SaveChangesAsync();
        }
        await using var scope = app.Api.Services.CreateAsyncScope(); var reader = scope.ServiceProvider.GetRequiredService<OwnershipReader>();
        var before = await reader.ReadAllAsync(default);
        var candidate = adding ? TerritoryAssignment.Create("aoife", targets[^1]) : baseline[random.Next(baseline.Count)];
        var dry = DryRun.Calculate(before.Locations, baseline, adding ? baseline.Append(candidate) : baseline.Where(row => row.Id != candidate.Id));
        AssignmentImpactDetails preview;
        if (adding)
        {
            var request = Add(candidate.RepSubject, candidate.Target.Level, candidate.Target.UnitId);
            preview = await PreviewAsync(api, request); await SaveAsync(api, request, preview);
        }
        else
        {
            var assignment = new TerritoryAssignmentDetails(candidate.Id, candidate.RepSubject, candidate.Target, Convert.ToBase64String(candidate.Version));
            preview = await RemovalPreviewAsync(api, assignment); await RemoveAsync(api, assignment, preview);
        }
        var after = await reader.ReadAllAsync(default);
        var actual = before.Locations.Where(path => before.Owners[path.LocationId]?.RepSubject != after.Owners[path.LocationId]?.RepSubject).ToArray();
        Assert.Equal(dry.Select(row => row.Location.LocationId).Order(), actual.Select(row => row.LocationId).Order());
        Assert.Equal(dry.Count, preview.ChangedLocations);
        Assert.Equal(dry.Select(row => row.Location.LocationId).Order(), preview.Groups.SelectMany(row => row.Locations).Select(row => row.LocationId).Order());
        var dbContext = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); var history = await dbContext.AssignmentHistory.AsNoTracking().ToArrayAsync();
        Assert.Equal(dry.Count, history.Length);
        foreach (var change in dry)
        {
            var savedOwner = after.Owners[change.Location.LocationId];
            Assert.Equal(change.Next?.RepSubject, savedOwner?.RepSubject); Assert.Equal(change.Next?.Source.Target, savedOwner?.Source.Target);
            Assert.Equal(change.Next?.Source.Name, savedOwner?.Source.Name);
            var listed = Assert.Single(preview.Groups.SelectMany(row => row.Locations), row => row.LocationId == change.Location.LocationId);
            Assert.Equal(change.Previous?.RepSubject, listed.PreviousOwner?.Rep.Subject); Assert.Equal(change.Next?.RepSubject, listed.NewOwner?.Rep.Subject);
            var recorded = Assert.Single(history, row => row.LocationId == change.Location.LocationId);
            Assert.Equal(change.Previous?.RepSubject, recorded.PreviousRepSubject); Assert.Equal(change.Next?.RepSubject, recorded.NewRepSubject);
        }
        Assert.False(app.Staff.SawTransaction);
    }

    [Theory]
    [InlineData("assignment")] [InlineData("new-location")] [InlineData("moved-location")] [InlineData("location-name")]
    [InlineData("county-name")] [InlineData("rep-name")] [InlineData("actor-name")] [InlineData("reporting")]
    public async Task Should_RepreviewWithoutWrites_When_AuthoritativeInputsChanged(string change)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var request = Add("colm", TerritoryLevel.County, seed.County.Id); var preview = await PreviewAsync(api, request);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            switch (change)
            {
                case "assignment": db.TerritoryAssignments.Add(TerritoryAssignment.Create("aoife", new(TerritoryLevel.Town, seed.Town.Id))); await db.SaveChangesAsync(); break;
                case "new-location": await InsertLocationAsync(db, seed.CustomerId, seed.Town.Id, "New shop"); break;
                case "moved-location": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Locations] SET [TownId]={seed.OtherTown.Id} WHERE [Id]={seed.Locations[0]}"); break;
                case "location-name": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Locations] SET [Name]={"Renamed shop"} WHERE [Id]={seed.Locations[0]}"); break;
                case "county-name": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Counties] SET [Name]={"Renamed county"} WHERE [Id]={seed.County.Id}"); break;
                case "rep-name": app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { DisplayName = "Renamed Colm" }; break;
                case "actor-name": app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { DisplayName = "Renamed Manager" }; break;
                case "reporting": var line = await db.RepReportingLines.SingleAsync(row => row.RepSubject == "colm"); line.SetManager("another-manager"); await db.SaveChangesAsync(); break;
            }
        }
        var baseline = await CountsAsync();
        using var stale = await api.PostAsJsonAsync(Root, request with { PreviewProof = preview.Proof, Confirmed = true });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var fresh = (await stale.Content.ReadFromJsonAsync<CoverageError>())!.Preview!;
        Assert.NotNull(fresh); Assert.NotEqual(preview.Proof, fresh.Proof); Assert.Equal(baseline, await CountsAsync());
        using var again = await api.PostAsJsonAsync(Root, request with { PreviewProof = fresh.Proof });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode); Assert.Equal(baseline, await CountsAsync());
        await SaveAsync(api, request, fresh);
        Assert.Equal(baseline.Item2 + fresh.ChangedLocations, (await CountsAsync()).Item2);
        if (change == "new-location") Assert.Equal(2, fresh.ChangedLocations);
        if (change == "assignment") Assert.Equal(0, fresh.ChangedLocations);
        if (change == "rep-name") Assert.Contains("Renamed Colm", Assert.Single(fresh.Groups).Sentence);
    }

    [Theory]
    [InlineData("missing")] [InlineData("unconfirmed")] [InlineData("tampered")] [InlineData("command")] [InlineData("actor")]
    public async Task Should_RejectBypassAndTampering_When_ProofDoesNotConfirmThisCommand(string scenario)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var request = Add("colm", TerritoryLevel.County, seed.County.Id); var preview = await PreviewAsync(api, request);
        var submitted = request with { PreviewProof = preview.Proof, Confirmed = true };
        if (scenario == "missing") submitted = submitted with { PreviewProof = null };
        if (scenario == "unconfirmed") submitted = submitted with { Confirmed = false };
        if (scenario == "tampered") submitted = submitted with { PreviewProof = "invalid" };
        if (scenario == "command") submitted = submitted with { RepSubject = "aoife" };
        if (scenario == "actor")
        {
            app.Roles.SetRoles("another-manager", BusinessRoles.HeadOfficeUser);
            app.Staff.Entries["another-manager"] = app.Staff.Entries["another-manager"] with { Roles = [BusinessRoles.HeadOfficeUser] };
            api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(subject: "another-manager"));
        }
        using var response = await api.PostAsJsonAsync(Root, submitted); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((0, 0), await CountsAsync());
    }

    [Theory]
    [InlineData("role", HttpStatusCode.Forbidden)] [InlineData("team", HttpStatusCode.Forbidden)]
    [InlineData("inactive", HttpStatusCode.BadRequest)] [InlineData("archive", HttpStatusCode.BadRequest)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    public async Task Should_DenyWithoutWrites_When_AuthorityEligibilityOrTargetBecameUnavailable(string change, HttpStatusCode expected)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        app.Roles.SetRoles("niamh", BusinessRoles.SalesManager);
        app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [BusinessRoles.SalesManager] };
        var request = Add("colm", TerritoryLevel.County, seed.County.Id); var preview = await PreviewAsync(api, request);
        if (change == "role") app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        if (change == "inactive") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        if (change == "unavailable") app.Staff.Unavailable = true;
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            if (change == "team") await db.RepReportingLines.Where(row => row.RepSubject == "colm").ExecuteDeleteAsync();
            if (change == "archive") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Regions] SET [IsArchived]=1 WHERE [Id]={seed.Region.Id}");
        }
        using var response = await api.PostAsJsonAsync(Root, request with { PreviewProof = preview.Proof, Confirmed = true });
        Assert.Equal(expected, response.StatusCode); Assert.Equal((0, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_RefreshRemovalVersionAndDenyReplay_When_AssignmentChangedOrAlreadySaved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var request = Add("colm", TerritoryLevel.County, seed.County.Id); var addPreview = await PreviewAsync(api, request);
        var assignment = await SaveAsync(api, request, addPreview);
        using var repeat = await api.PostAsJsonAsync(Root, request with { PreviewProof = addPreview.Proof, Confirmed = true });
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode); Assert.Equal((1, 1), await CountsAsync());
        var preview = await RemovalPreviewAsync(api, assignment);
        await using (var scope = app.Api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE [TerritoryAssignments] SET [RepSubject]={"colm"} WHERE [Id]={assignment.Id}");
        var removal = new RemoveTerritoryAssignmentRequest(assignment.Version, PreviewProof: preview.Proof, Confirmed: true);
        using var stale = await api.PostAsJsonAsync($"{Root}/{assignment.Id}/remove", removal);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal((1, 1), await CountsAsync());
        var fresh = (await stale.Content.ReadFromJsonAsync<CoverageError>())!.Preview!;
        Assert.NotEqual(assignment.Version, fresh.AssignmentVersion);
        await RemoveAsync(api, assignment with { Version = fresh.AssignmentVersion! }, fresh);
        using var repeated = await api.PostAsJsonAsync($"{Root}/{assignment.Id}/remove", removal);
        Assert.Equal(HttpStatusCode.NotFound, repeated.StatusCode); Assert.Equal((0, 2), await CountsAsync());
    }

    [Theory]
    [InlineData(BusinessRoles.HeadOfficeUser)] [InlineData(BusinessRoles.SalesManager)]
    public async Task Should_ReviewExpandCancelRefreshAndConfirm_When_AuthorizedStaffUseTheBff(string role)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        app.Roles.SetRoles("niamh", role); app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Roles = [role] };
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser();
        using var signIn = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        using var page = await browser.GetAsync(Page + "?repSubject=colm"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var fields = new Dictionary<string, string> { ["RepSubject"] = "colm", ["TargetKey"] = "County:" + seed.County.Id, ["Action"] = "Add" };
        using var noCsrf = await browser.PostAsync(Page + "?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        fields["__RequestVerificationToken"] = Field(await page.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        using var reviewed = await browser.PostAsync(Page + "?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        string html = WebUtility.HtmlDecode(await reviewed.Content.ReadAsStringAsync());
        Assert.Contains("<details>", html); Assert.Contains("<summary>1 Location becomes Colm's</summary>", html); Assert.Contains("Murphy's Pharmacy", html);
        Assert.Contains("Colm (via Wicklow)", html); Assert.Contains("Assign Wicklow to Colm", html); Assert.Contains(">Cancel</a>", html);
        Assert.Equal((0, 0), await CountsAsync());
        using var cancelled = await browser.GetAsync(Page + "?repSubject=colm"); Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode); Assert.Equal((0, 0), await CountsAsync());
        fields["PreviewProof"] = Field(html, "PreviewProof"); fields["Confirmed"] = "true";
        await using (var scope = app.Api.Services.CreateAsyncScope()) await InsertLocationAsync(scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(), seed.CustomerId, seed.Town.Id, "New shop");
        using var stale = await browser.PostAsync(Page + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var refreshed = WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync()); Assert.Contains("2 Locations become Colm's", refreshed);
        Assert.Contains("Nothing was saved", refreshed); Assert.Equal((0, 0), await CountsAsync());
        Assert.NotEqual(fields["PreviewProof"], Field(refreshed, "PreviewProof")); fields["PreviewProof"] = Field(refreshed, "PreviewProof");
        using var saved = await browser.PostAsync(Page + "?handler=Save", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal((1, 2), await CountsAsync());
        var assigned = Assert.Single((await api.GetFromJsonAsync<AssignedTerritoryDetails[]>("/coverage/reps/colm/assignments"))!).Assignment;
        var removeFields = new Dictionary<string, string> { ["RepSubject"] = "colm", ["Action"] = "Remove", ["AssignmentId"] = assigned.Id.ToString(),
            ["Version"] = assigned.Version, ["__RequestVerificationToken"] = fields["__RequestVerificationToken"] };
        using var remove = await browser.PostAsync(Page + "?handler=Preview", new FormUrlEncodedContent(removeFields)); Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        var removeHtml = WebUtility.HtmlDecode(await remove.Content.ReadAsStringAsync()); Assert.Contains("2 Locations become Unassigned", removeHtml);
        Assert.Contains("no responsible rep", removeHtml); Assert.Contains(">Remove assignment</button>", removeHtml);
        removeFields["PreviewProof"] = Field(removeHtml, "PreviewProof");
        using var noConfirm = await browser.PostAsync(Page + "?handler=Save", new FormUrlEncodedContent(removeFields)); Assert.Equal(HttpStatusCode.OK, noConfirm.StatusCode);
        Assert.Equal((1, 2), await CountsAsync()); removeFields["Confirmed"] = "true";
        using var removed = await browser.PostAsync(Page + "?handler=Save", new FormUrlEncodedContent(removeFields)); Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Equal((0, 4), await CountsAsync());
        website.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        using var denied = await browser.GetAsync(Page); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
    }

    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static AddTerritoryAssignmentRequest Add(string rep, TerritoryLevel level, Guid id) => new(rep, new(level, id));
    private static async Task<AssignmentImpactDetails> PreviewAsync(HttpClient api, AddTerritoryAssignmentRequest request)
    {
        using var response = await api.PostAsJsonAsync(Root + "/preview", request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssignmentImpactDetails>())!;
    }
    private static async Task<TerritoryAssignmentDetails> SaveAsync(HttpClient api, AddTerritoryAssignmentRequest request, AssignmentImpactDetails preview)
    {
        using var response = await api.PostAsJsonAsync(Root, request with { PreviewProof = preview.Proof, Confirmed = true }); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TerritoryAssignmentDetails>())!;
    }
    private static async Task<AssignmentImpactDetails> RemovalPreviewAsync(HttpClient api, TerritoryAssignmentDetails assignment)
    {
        using var response = await api.PostAsJsonAsync($"{Root}/{assignment.Id}/remove/preview", new RemoveTerritoryAssignmentRequest(assignment.Version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<AssignmentImpactDetails>())!;
    }
    private static async Task RemoveAsync(HttpClient api, TerritoryAssignmentDetails assignment, AssignmentImpactDetails preview)
    {
        using var response = await api.PostAsJsonAsync($"{Root}/{assignment.Id}/remove",
            new RemoveTerritoryAssignmentRequest(assignment.Version, PreviewProof: preview.Proof, Confirmed: true)); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(preview.ChangedLocations, (await response.Content.ReadFromJsonAsync<CoverageMutationResult>())!.ChangedLocations);
    }
    private static async Task<LocationCoverageDetails> OwnerAsync(HttpClient api, Guid id) => (await api.GetFromJsonAsync<LocationCoverageDetails>($"/coverage/locations/{id}/owner"))!;
    private async Task<(int, int)> CountsAsync()
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        return (await db.TerritoryAssignments.CountAsync(), await db.AssignmentHistory.CountAsync());
    }
    private async Task<Seed> SeedAsync(HttpClient api, int first = 1, int other = 0, string countyName = "Wicklow")
    {
        var region = await PlaceAsync(api, "regions", "Leinster"); var county = await PlaceAsync(api, "counties", countyName, region.Id);
        var town = await PlaceAsync(api, "towns", "Rathdrum", county.Id); var otherTown = await PlaceAsync(api, "towns", "Laragh", county.Id);
        using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Customer", new("Murphy's Pharmacy", town.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        List<Guid> locations = [customer.Locations[0].Id];
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        for (int i = 1; i < first + other; i++) locations.Add(await InsertLocationAsync(db, customer.Id, i < first ? town.Id : otherTown.Id,
            i == first ? "Doyle's Shop" : "Shop " + i));
        return new(region, county, town, otherTown, customer.Id, locations.ToArray());
    }
    private static async Task<Guid> InsertLocationAsync(DirectoryDbContext db, Guid customer, Guid town, string name)
    {
        Guid id = Guid.NewGuid(); string normalized = name.ToUpperInvariant();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Locations] ([Id], [CustomerId], [TownId], [Name], [NormalizedName]) VALUES ({id}, {customer}, {town}, {name}, {normalized})"); return id;
    }
    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parent = null)
    {
        using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem Town, GeographyItem OtherTown, Guid CustomerId, Guid[] Locations);
}
