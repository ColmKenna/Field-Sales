extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;

namespace FieldSales.Web.Tests;

public sealed class AssignmentHistoryEndToEndTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    [Fact]
    public async Task Should_InheritCountyOwnerAndRecordCreation_When_FirstAndAdditionalLocationsAreSaved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        // Extra browser fields cannot supply the actor or historical labels.
        using var created = await api.PostAsJsonAsync("/directory/customers", new
        {
            Name = "New customer", FirstLocation = new CreateLocationRequest("First assigned shop", seed.Town.Id, "A67 X123"),
            ActorSubject = "spoofed", ActorName = "Spoofed", NewRepName = "Spoofed"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        using var additional = await api.PostAsJsonAsync($"/directory/customers/{customer.Id}/locations",
            new CreateLocationRequest("Additional assigned shop", seed.Town.Id));
        Assert.Equal(HttpStatusCode.Created, additional.StatusCode);
        var second = (await additional.Content.ReadFromJsonAsync<LocationDetails>())!;
        foreach (var id in new[] { customer.Locations[0].Id, second.Id })
        {
            var entry = Assert.Single(await HistoryAsync(api, id));
            Assert.Null(entry.PreviousOwner); Assert.Equal("colm", entry.NewOwner!.Rep.Subject);
            Assert.Equal("Colm", entry.NewOwner.Rep.DisplayName); Assert.Equal("Wicklow", entry.NewOwner.Source.Name);
            Assert.Equal("niamh", entry.Actor.Subject); Assert.Equal("Niamh Byrne", entry.Actor.DisplayName);
            Assert.Equal(OwnershipChangeCause.TerritoryAssignment, entry.Cause); Assert.Null(entry.Reason);
            Assert.Equal(TimeSpan.Zero, entry.ChangedAt.Offset); Assert.True(entry.Sequence > 0);
            Assert.Contains("Unassigned → Colm", entry.Display);
        }
        Assert.False(app.Staff.SawTransaction); Assert.Equal(2, app.Staff.Calls);
        Assert.Equal(LocationPositionPrecision.Town, second.Position!.Precision);
        Assert.Equal(2, (await api.GetFromJsonAsync<AssignmentHistoryDetails[]>("/coverage/reps/colm/history"))!.Length);
    }

    [Fact]
    public async Task Should_RecordGeographyChangeAtomically_When_LocationMovesToADifferentlyOwnedTown()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync("aoife", TerritoryLevel.Town, seed.OtherTown.Id);
        var before = await LocationAsync(api, seed.Location.Id);
        using var moved = await EditAsync(api, before, seed.OtherTown.Id); Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var current = (await moved.Content.ReadFromJsonAsync<LocationDetails>())!;
        var entry = Assert.Single(await HistoryAsync(api, current.Id));
        Assert.Equal("Colm", entry.PreviousOwner!.Rep.DisplayName); Assert.Equal("Aoife", entry.NewOwner!.Rep.DisplayName);
        Assert.Equal("Wicklow", entry.PreviousOwner.Source.Name); Assert.Equal("Laragh", entry.NewOwner.Source.Name);
        Assert.Equal(OwnershipChangeCause.GeographyChange, entry.Cause);
        Assert.Equal(54m, current.Position!.Latitude); Assert.Equal(seed.OtherTown.Id, current.Town.Id);
        using var stale = await EditAsync(api, before, seed.Town.Id); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Single(await HistoryAsync(api, current.Id));
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(1, await db.LocationPositionHistory.CountAsync()); Assert.Equal(1, await db.AssignmentHistory.CountAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    [Fact]
    public async Task Should_WriteNoHistoryOrRequestLabels_When_OwnerIsUnchangedOrOnlySourceChanges()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync("colm", TerritoryLevel.Town, seed.OtherTown.Id);
        app.Staff.Unavailable = true;
        using var moved = await EditAsync(api, await LocationAsync(api, seed.Location.Id), seed.OtherTown.Id);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode); Assert.Empty(await HistoryAsync(api, seed.Location.Id));
        var current = (await moved.Content.ReadFromJsonAsync<LocationDetails>())!;
        using var unchanged = await EditAsync(api, current, current.Town.Id); Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Empty(await HistoryAsync(api, current.Id)); Assert.Equal(0, app.Staff.Calls);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("missing-rep")]
    [InlineData("missing-actor")]
    [InlineData("invalid-label")]
    [InlineData("invalid-roles")]
    [InlineData("inactive-actor")]
    public async Task Should_SaveNothing_When_RequiredStaffIdentityDataCannotBeVerified(string failure)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        switch (failure)
        {
            case "unavailable": app.Staff.Unavailable = true; break;
            case "missing-rep": app.Staff.Entries.Remove("colm"); break;
            case "missing-actor": app.Staff.Entries.Remove("niamh"); break;
            case "invalid-label": app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { DisplayName = " " }; break;
            case "invalid-roles": app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Roles = ["SysAdmin"] }; break;
            case "inactive-actor": app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { Available = false }; break;
        }
        using var response = await api.PostAsJsonAsync($"/directory/customers/{seed.CustomerId}/locations", new CreateLocationRequest("Rejected shop", seed.Town.Id));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(1, await db.Locations.CountAsync()); Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Empty(await db.AssignmentHistory.ToArrayAsync()); Assert.Empty(await db.LocationPositionHistory.ToArrayAsync());
    }

    [Fact]
    public async Task Should_KeepReadableStoredLabels_When_StaffAndGeographyAreRenamedAndDatabaseRestarts()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        using var saved = await api.PostAsJsonAsync($"/directory/customers/{seed.CustomerId}/locations", new CreateLocationRequest("Assigned shop", seed.Town.Id));
        var location = (await saved.Content.ReadFromJsonAsync<LocationDetails>())!;
        var original = Assert.Single(await HistoryAsync(api, location.Id));
        app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { DisplayName = "Renamed Colm" };
        app.Staff.Entries["niamh"] = app.Staff.Entries["niamh"] with { DisplayName = "Renamed Manager" };
        using var renamed = await api.PutAsJsonAsync($"/directory/geography/counties/{seed.County.Id}/name", new RenameGeographyRequest("Renamed Wicklow", seed.County.Version));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        app.Staff.Unavailable = true;
        using var edited = await api.PutAsJsonAsync("/directory/locations/" + location.Id,
            new EditLocationRequest("Renamed shop", location.Town.Id, location.Eircode, location.Version));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        Assert.Equal(original, Assert.Single(await HistoryAsync(restarted, location.Id)));
        Assert.Equal("Assigned shop", original.LocationName); Assert.Equal("Wicklow", original.NewOwner!.Source.Name);
        Assert.Equal(1, app.Staff.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_RollBackLocationPositionAndHistory_When_LateSaveFailsThenRetrySucceeds(bool firstLocation)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        app.Failures.FailAfterHistorySave = true;
        Task<HttpResponseMessage> SaveAsync() => firstLocation
            ? api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Retry customer", new("Retry shop", seed.Town.Id)))
            : api.PostAsJsonAsync($"/directory/customers/{seed.CustomerId}/locations", new CreateLocationRequest("Retry shop", seed.Town.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(SaveAsync); Assert.True(app.Failures.DidFailAfterSave);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            Assert.Equal(1, await db.Customers.CountAsync()); Assert.Equal(1, await db.Locations.CountAsync());
            Assert.Empty(await db.AssignmentHistory.ToArrayAsync()); Assert.Empty(await db.LocationPositionHistory.ToArrayAsync());
        }
        using var retry = await SaveAsync(); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        await using var after = app.Api.Services.CreateAsyncScope(); var savedDb = after.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(2, await savedDb.Locations.CountAsync()); Assert.Single(await savedDb.AssignmentHistory.ToArrayAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    [Fact]
    public async Task Should_RestrictHistoryReadsAndRejectMutation_When_CurrentTeamOrRoleDoesNotAllowAccess()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("brian", TerritoryLevel.County, seed.County.Id);
        using var saved = await api.PostAsJsonAsync($"/directory/customers/{seed.CustomerId}/locations", new CreateLocationRequest("Brian's shop", seed.Town.Id));
        var location = (await saved.Content.ReadFromJsonAsync<LocationDetails>())!;
        var original = Assert.Single(await HistoryAsync(api, location.Id));
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, HistoryUrl(location.Id));
            using var rejected = await api.SendAsync(request); Assert.Equal(HttpStatusCode.MethodNotAllowed, rejected.StatusCode);
        }
        app.Roles.SetRoles("niamh", "Sales Manager");
        using var rep = await api.GetAsync("/coverage/reps/brian/history"); Assert.Equal(HttpStatusCode.Forbidden, rep.StatusCode);
        using var foreign = await api.GetAsync(HistoryUrl(location.Id)); Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        app.Roles.SetRoles("niamh", "Head Office User");
        Assert.Equal(original, Assert.Single(await HistoryAsync(api, location.Id)));
        app.Roles.SetRoles("niamh", "Field Salesperson");
        using var role = await api.GetAsync(HistoryUrl(location.Id)); Assert.Equal(HttpStatusCode.Forbidden, role.StatusCode);
    }

    private async Task<Seed> SeedAsync(HttpClient api)
    {
        var region = await PlaceAsync(api, "regions", "Leinster");
        var county = await PlaceAsync(api, "counties", "Wicklow", region.Id);
        var town = await PlaceAsync(api, "towns", "Rathdrum", county.Id, 52.9m);
        var other = await PlaceAsync(api, "towns", "Laragh", county.Id, 54m);
        using var created = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Existing customer", new("Existing shop", town.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        Assert.Empty(await HistoryAsync(api, customer.Locations[0].Id)); Assert.Equal(0, app.Staff.Calls);
        return new(customer.Id, customer.Locations[0], county, town, other);
    }
    private async Task AssignAsync(string rep, TerritoryLevel level, Guid id)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        db.TerritoryAssignments.Add(TerritoryAssignment.Create(rep, new(level, id))); await db.SaveChangesAsync();
    }
    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parent = null, decimal? latitude = null)
    {
        using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent, latitude, latitude is null ? null : -6.3m));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
    private static string HistoryUrl(Guid id) => $"/coverage/locations/{id}/history";
    private static async Task<AssignmentHistoryDetails[]> HistoryAsync(HttpClient api, Guid id) => (await api.GetFromJsonAsync<AssignmentHistoryDetails[]>(HistoryUrl(id)))!;
    private static async Task<LocationDetails> LocationAsync(HttpClient api, Guid id) => (await api.GetFromJsonAsync<LocationDetails>("/directory/locations/" + id))!;
    private static Task<HttpResponseMessage> EditAsync(HttpClient api, LocationDetails before, Guid town) =>
        api.PutAsJsonAsync("/directory/locations/" + before.Id, new EditLocationRequest(before.Name, town, before.Eircode, before.Version));
    private sealed record Seed(Guid CustomerId, LocationSummary Location, GeographyItem County, GeographyItem Town, GeographyItem OtherTown);
}
