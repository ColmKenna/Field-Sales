extern alias CatalogueApi;

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using RepReportingLine = CatalogueApi::FieldSales.Api.Coverage.RepReportingLine;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;
using AssignmentHistory = CatalogueApi::FieldSales.Api.Coverage.AssignmentHistory;

namespace FieldSales.Web.Tests;

public sealed class CoverageReadApplication : GeographyApplication
{
    protected override bool UseTestLocationUsage => false;
    public ReadProbe Reads { get; } = new();
    public DateTimeOffset? ClockOverride { get; set; }
    private sealed class CoverageClock(CoverageReadApplication app) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => app.ClockOverride ?? DateTimeOffset.UtcNow; }

    protected override void ConfigureAdditionalServices(IServiceCollection services, string connectionString)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new CoverageClock(this));
        services.RemoveAll<DirectoryDbContext>();
        services.AddScoped(_ => new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(Reads, Failures).Options));
        services.RemoveAll<IStaffDirectory>();
        services.AddScoped<IStaffDirectory>(provider => new CoverageStaffDirectory(
            provider.GetRequiredService<DirectoryDbContext>(), Staff));
    }

    public async Task ResetCoverageAsync()
    {
        Roles.Unavailable = false; ClockOverride = null;
        await using (var scope = Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            // Privileged test-database reset; DML append-only guards stay enabled.
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [AssignmentHistory]");
            await db.TerritoryAssignments.ExecuteDeleteAsync();
            await db.RepReportingLines.ExecuteDeleteAsync();
            await db.LocationPositionHistory.ExecuteDeleteAsync();
        }
        await ResetAsync();
        Staff.Reset();
        Failures.FailAfterHistorySave = false; Failures.DidFailAfterSave = false;
        await using var seedScope = Api.Services.CreateAsyncScope();
        var seed = seedScope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        seed.RepReportingLines.AddRange(RepReportingLine.Create("colm", "niamh"),
            RepReportingLine.Create("aoife", "niamh"), RepReportingLine.Create("brian", "another-manager"));
        await seed.SaveChangesAsync();
        Reads.Commands.Clear();
    }

    public CoverageStaffState Staff { get; } = new();
    public HistoryFailureProbe Failures { get; } = new();

    public sealed class HistoryFailureProbe : SaveChangesInterceptor
    {
        public bool FailAfterHistorySave { get; set; }
        public bool DidFailAfterSave { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (FailAfterHistorySave && eventData.Context!.ChangeTracker.Entries<AssignmentHistory>().Any())
            {
                FailAfterHistorySave = false; DidFailAfterSave = true;
                throw new InvalidOperationException("Injected failure after history save.");
            }
            return base.SavedChangesAsync(eventData, result, ct);
        }
    }

    public sealed class CoverageStaffState
    {
        public Dictionary<string, StaffDirectoryEntry> Entries { get; } = new(StringComparer.Ordinal);
        public bool Unavailable { get; set; }
        public bool SawTransaction { get; set; }
        public int Calls { get; set; }
        public void Reset()
        {
            Entries.Clear(); Unavailable = false; SawTransaction = false; Calls = 0;
            Entries.Add("niamh", new("niamh", "Niamh Byrne", [BusinessRoles.HeadOfficeUser], true));
            Entries.Add("colm", new("colm", "Colm", [BusinessRoles.FieldSalesperson], true));
            Entries.Add("aoife", new("aoife", "Aoife", [BusinessRoles.FieldSalesperson], true));
            Entries.Add("brian", new("brian", "Brian", [BusinessRoles.FieldSalesperson], true));
            Entries.Add("another-manager", new("another-manager", "Other Manager", [BusinessRoles.SalesManager], true));
        }
    }

    private sealed class CoverageStaffDirectory(DirectoryDbContext db, CoverageStaffState staff) : IStaffDirectory
    {
        public Task<IReadOnlyList<StaffDirectoryEntry>> ListAsync(string accessToken, CancellationToken ct) =>
            LookupAsync(accessToken, staff.Entries.Keys.ToArray(), ct);
        public Task<IReadOnlyList<StaffDirectoryEntry>> LookupAsync(string accessToken, IReadOnlyCollection<string> subjects, CancellationToken ct)
        {
            staff.Calls++; staff.SawTransaction |= db.Database.CurrentTransaction is not null;
            if (staff.Unavailable) throw new HttpRequestException("Test staff directory unavailable.");
            return Task.FromResult<IReadOnlyList<StaffDirectoryEntry>>(subjects.Where(staff.Entries.ContainsKey)
                .Select(subject => staff.Entries[subject]).ToArray());
        }
    }

    public sealed class ReadProbe : DbCommandInterceptor
    {
        public ConcurrentQueue<IsolationLevel?> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.Transaction?.IsolationLevel);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

// Real SQL, real API JWT/current-role boundary; assignment writes are setup only.
// Production add/remove/history operations belong to later approved increments.
public sealed class TerritoryAssignmentEndToEndTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    [Fact]
    public async Task Should_ResolveColmViaWicklow_When_CountyWith140LocationsIsAssigned()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient();
        var seed = await SeedAsync(api, 23, 117);
        var assignment = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        app.Reads.Commands.Clear();
        var locations = await LocationsAsync(api, "colm");
        Assert.Equal(140, locations.Length);
        Assert.Equal(seed.RathdrumLocations.Concat(seed.LaraghLocations).Order(), locations.Select(row => row.LocationId).Order());
        Assert.All(locations, row => AssertOwner(row, "colm", assignment.Id, TerritoryLevel.County, seed.County.Id, "Wicklow"));
        // Increasing the shop count must not introduce a database query per shop.
        Assert.Equal(2, app.Reads.Commands.Count);
        Assert.All(app.Reads.Commands, isolation => Assert.Equal(IsolationLevel.Serializable, isolation));
        var assigned = Assert.Single((await api.GetFromJsonAsync<AssignedTerritoryDetails[]>(AssignmentsUrl("colm")))!);
        Assert.Equal(assignment.Id, assigned.Assignment.Id); Assert.Equal("Wicklow", assigned.Name);
        Assert.Equal(8, Convert.FromBase64String(assigned.Assignment.Version).Length);
    }

    [Fact]
    public async Task Should_Return23LocationsToColm_When_RathdrumCarveOutIsRemoved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient();
        var seed = await SeedAsync(api, 23, 117);
        var county = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        var town = await AssignAsync("aoife", TerritoryLevel.Town, seed.Rathdrum.Id);
        AssertOwner(await OwnerAsync(api, seed.RathdrumLocations[0]), "aoife", town.Id, TerritoryLevel.Town, seed.Rathdrum.Id, "Rathdrum");
        AssertOwner(await OwnerAsync(api, seed.LaraghLocations[0]), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow");
        Assert.Equal(23, (await LocationsAsync(api, "aoife")).Length);
        Assert.Equal(117, (await LocationsAsync(api, "colm")).Length);
        await RemoveSetupAsync(town.Id);
        Assert.Empty(await LocationsAsync(api, "aoife"));
        var restored = await LocationsAsync(api, "colm"); Assert.Equal(140, restored.Length);
        Assert.All(restored, row => AssertOwner(row, "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow"));
    }

    [Fact]
    public async Task Should_PreserveCarveOutOwners_When_ParentCountyAssignmentIsRemoved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient();
        var seed = await SeedAsync(api, 23, 117);
        var county = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        var town = await AssignAsync("aoife", TerritoryLevel.Town, seed.Rathdrum.Id);
        await RemoveSetupAsync(county.Id);
        Assert.Empty(await LocationsAsync(api, "colm"));
        Assert.Equal(23, (await LocationsAsync(api, "aoife")).Length);
        AssertOwner(await OwnerAsync(api, seed.RathdrumLocations[0]), "aoife", town.Id, TerritoryLevel.Town, seed.Rathdrum.Id, "Rathdrum");
        Assert.Null((await OwnerAsync(api, seed.LaraghLocations[0])).Owner);
    }

    [Fact]
    public async Task Should_Leave96LocationsUnassigned_When_BriansOnlyWexfordAssignmentIsRemoved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient();
        var seed = await SeedAsync(api, 96, countyName: "Wexford");
        var county = await AssignAsync("brian", TerritoryLevel.County, seed.County.Id);
        Assert.Equal(96, (await LocationsAsync(api, "brian")).Length);
        await RemoveSetupAsync(county.Id);
        Assert.Empty(await LocationsAsync(api, "brian"));
        // Every removed owner is independently readable as Unassigned, not 404.
        foreach (var id in seed.RathdrumLocations) Assert.Null((await OwnerAsync(api, id)).Owner);
    }

    [Fact]
    public async Task Should_InheritCountyOwner_When_FirstAndAdditionalLocationsAreSavedLater()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var county = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        using var created = await api.PostAsJsonAsync("/directory/customers",
            new CreateCustomerRequest("New customer", new("New first shop", seed.Rathdrum.Id, "A67 X123")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        var first = Assert.Single(customer.Locations);
        using var added = await api.PostAsJsonAsync($"/directory/customers/{customer.Id}/locations",
            new CreateLocationRequest("New additional shop", seed.Laragh.Id));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var additional = (await added.Content.ReadFromJsonAsync<LocationDetails>())!;
        AssertOwner(await OwnerAsync(api, first.Id), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow");
        AssertOwner(await OwnerAsync(api, additional.Id), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow");
        Assert.Equal("A67 X123", first.Eircode);
        Assert.Equal(LocationPositionPrecision.Town, first.Position!.Precision);
        Assert.Equal(LocationPositionPrecision.Town, additional.Position!.Precision);
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        AssertOwner(await OwnerAsync(restarted, first.Id), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow");
        Assert.Equal(3, (await LocationsAsync(restarted, "colm")).Length);
    }

    [Fact]
    public async Task Should_ReadCurrentHierarchy_When_LocationMovesToADifferentlyOwnedTown()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var county = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        var town = await AssignAsync("aoife", TerritoryLevel.Town, seed.Laragh.Id);
        var first = await api.GetFromJsonAsync<LocationDetails>("/directory/locations/" + seed.RathdrumLocations[0]);
        AssertOwner(await OwnerAsync(api, first!.Id), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow");
        using var edited = await api.PutAsJsonAsync("/directory/locations/" + first.Id,
            new EditLocationRequest(first.Name, seed.Laragh.Id, first.Eircode, first.Version));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        AssertOwner(await OwnerAsync(api, first.Id), "aoife", town.Id, TerritoryLevel.Town, seed.Laragh.Id, "Laragh");
        var saved = (await edited.Content.ReadFromJsonAsync<LocationDetails>())!;
        Assert.Equal(seed.Laragh.Id, saved.Town.Id); Assert.Equal(LocationPositionPrecision.Town, saved.Position!.Precision);
        using var stale = await api.PutAsJsonAsync("/directory/locations/" + first.Id,
            new EditLocationRequest("Stale", seed.Rathdrum.Id, null, first.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("aoife", (await OwnerAsync(api, first.Id)).Owner!.RepSubject);
    }

    [Fact]
    public async Task Should_WalkAllFourLevels_When_NarrowerAssignmentsAreRemoved()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var entries = new[]
        {
            (TerritoryLevel.Region, seed.Region.Id, "Leinster", "colm"),
            (TerritoryLevel.County, seed.County.Id, "Wicklow", "brian"),
            (TerritoryLevel.Town, seed.Rathdrum.Id, "Rathdrum", "aoife"),
            (TerritoryLevel.Location, seed.RathdrumLocations[0], "Murphy's Pharmacy", "colm")
        };
        var assignments = new List<TerritoryAssignment>();
        foreach (var (level, id, _, rep) in entries) assignments.Add(await AssignAsync(rep, level, id));
        for (int index = entries.Length - 1; index >= 0; index--)
        {
            var (level, id, name, rep) = entries[index];
            AssertOwner(await OwnerAsync(api, seed.RathdrumLocations[0]), rep, assignments[index].Id, level, id, name);
            var summary = (await api.GetFromJsonAsync<AssignedTerritoryDetails[]>(AssignmentsUrl(rep)))!
                .Single(row => row.Assignment.Id == assignments[index].Id);
            Assert.Equal(name, summary.Name);
            await RemoveSetupAsync(assignments[index].Id);
        }
        Assert.Null((await OwnerAsync(api, seed.RathdrumLocations[0])).Owner);
    }

    [Theory]
    [InlineData("regions")]
    [InlineData("counties")]
    [InlineData("towns")]
    public async Task Should_KeepAssignmentsAndOwners_When_GeographyIsArchived(string level)
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var target = level == "regions" ? seed.Region : level == "counties" ? seed.County : seed.Rathdrum;
        var territoryLevel = level == "regions" ? TerritoryLevel.Region : level == "counties" ? TerritoryLevel.County : TerritoryLevel.Town;
        var assignment = await AssignAsync("colm", territoryLevel, target.Id);
        using var archived = await api.PostAsJsonAsync($"/directory/geography/{level}/{target.Id}/retire",
            new RetireGeographyRequest(ReferenceAction.Archive, target.Version));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        AssertOwner(await OwnerAsync(api, seed.RathdrumLocations[0]), "colm", assignment.Id, territoryLevel, target.Id, target.Name);
        Assert.Single(await LocationsAsync(api, "colm"));
        Assert.Equal(target.Name, Assert.Single((await api.GetFromJsonAsync<AssignedTerritoryDetails[]>(AssignmentsUrl("colm")))!).Name);
    }

    [Fact]
    public async Task Should_UseCurrentSourceNames_When_GeographyIsRenamed()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var county = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        using var renamed = await api.PutAsJsonAsync($"/directory/geography/counties/{seed.County.Id}/name",
            new RenameGeographyRequest("County Wicklow", seed.County.Version));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        AssertOwner(await OwnerAsync(api, seed.RathdrumLocations[0]), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "County Wicklow");
        Assert.Equal("County Wicklow", Assert.Single((await api.GetFromJsonAsync<AssignedTerritoryDetails[]>(AssignmentsUrl("colm")))!).Name);
    }

    [Fact]
    public async Task Should_RestrictManagerReadsToCurrentTeam_When_ReportingLinesOrRolesChange()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, 1, 1);
        var county = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        await AssignAsync("brian", TerritoryLevel.Town, seed.Laragh.Id);
        app.Roles.SetRoles("niamh", BusinessRoles.SalesManager);
        // Existing token says Head Office; the current-role lookup replaces that claim.
        Assert.Single(await LocationsAsync(api, "colm"));
        Assert.Single((await api.GetFromJsonAsync<AssignedTerritoryDetails[]>(AssignmentsUrl("colm")))!);
        AssertOwner(await OwnerAsync(api, seed.RathdrumLocations[0]), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow");
        foreach (string url in new[] { AssignmentsUrl("brian"), LocationsUrl("brian"), OwnerUrl(seed.LaraghLocations[0]),
            AssignmentsUrl("Colm"), LocationsUrl("missing-rep"), OwnerUrl(seed.LaraghLocations[0]) + "?managerSubject=niamh&repSubject=colm" })
        {
            using var denied = await api.GetAsync(url); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var directory = await api.GetAsync("/directory/customers"); Assert.Equal(HttpStatusCode.Forbidden, directory.StatusCode);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            (await db.RepReportingLines.SingleAsync(line => line.RepSubject == "colm")).SetManager("another-manager");
            await db.SaveChangesAsync();
        }
        foreach (string url in new[] { AssignmentsUrl("colm"), LocationsUrl("colm"), OwnerUrl(seed.RathdrumLocations[0]) })
        {
            using var denied = await api.GetAsync(url); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        app.Roles.SetRoles("niamh", BusinessRoles.HeadOfficeUser);
        AssertOwner(await OwnerAsync(api, seed.RathdrumLocations[0]), "colm", county.Id, TerritoryLevel.County, seed.County.Id, "Wicklow");
        app.Roles.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        using var removedRole = await api.GetAsync(OwnerUrl(seed.RathdrumLocations[0]));
        Assert.Equal(HttpStatusCode.Forbidden, removedRole.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnUnassignedOrNotFound_When_NoAssignmentOrLocationExists()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var unassigned = await OwnerAsync(api, seed.RathdrumLocations[0]);
        Assert.Equal("Murphy's Pharmacy", unassigned.Name); Assert.Null(unassigned.Owner);
        Assert.Empty(await LocationsAsync(api, "colm"));
        using var missing = await api.GetAsync(OwnerUrl(Guid.NewGuid())); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        app.Roles.SetRoles("niamh", BusinessRoles.SalesManager);
        using var manager = await api.GetAsync(OwnerUrl(unassigned.LocationId)); Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
    }

    [Fact]
    public async Task Should_FailClosed_When_AuthenticationScopeOrCurrentRolesCannotBeVerified()
    {
        await app.ResetCoverageAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        var urls = new[] { OwnerUrl(seed.RathdrumLocations[0]), LocationsUrl("colm"), AssignmentsUrl("colm") };
        foreach (string url in urls)
        {
            api.DefaultRequestHeaders.Authorization = null;
            using var anonymous = await api.GetAsync(url); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: "openid"));
            using var scope = await api.GetAsync(url); Assert.Equal(HttpStatusCode.Forbidden, scope.StatusCode);
            api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
            app.Roles.Unavailable = true;
            using var unavailable = await api.GetAsync(url); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
            app.Roles.Unavailable = false;
        }
    }

    private async Task<Seed> SeedAsync(HttpClient api, int rathdrumCount = 1, int laraghCount = 0, string countyName = "Wicklow")
    {
        var region = await PlaceAsync(api, "regions", "Leinster");
        var county = await PlaceAsync(api, "counties", countyName, region.Id);
        var rathdrum = await PlaceAsync(api, "towns", "Rathdrum", county.Id);
        var laragh = await PlaceAsync(api, "towns", "Laragh", county.Id);
        using var created = await api.PostAsJsonAsync("/directory/customers",
            new CreateCustomerRequest("Test customer", new("Murphy's Pharmacy", rathdrum.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        List<Guid> rathdrumLocations = [Assert.Single(customer.Locations).Id];
        List<Guid> laraghLocations = [];
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        for (int index = 1; index < rathdrumCount; index++)
            rathdrumLocations.Add(await InsertLocationAsync(db, customer.Id, rathdrum.Id, "Rathdrum shop " + index));
        for (int index = 0; index < laraghCount; index++)
            laraghLocations.Add(await InsertLocationAsync(db, customer.Id, laragh.Id, index == 0 ? "Doyle's Shop" : "Laragh shop " + index));
        return new(region, county, rathdrum, laragh, rathdrumLocations.ToArray(), laraghLocations.ToArray());
    }

    private static async Task<Guid> InsertLocationAsync(DirectoryDbContext db, Guid customerId, Guid townId, string name)
    {
        var id = Guid.NewGuid(); string normalized = name.ToUpperInvariant();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Locations] ([Id], [CustomerId], [TownId], [Name], [NormalizedName])
            VALUES ({id}, {customerId}, {townId}, {name}, {normalized})
            """);
        return id;
    }

    private async Task<TerritoryAssignment> AssignAsync(string rep, TerritoryLevel level, Guid id)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var assignment = TerritoryAssignment.Create(rep, new(level, id)); db.TerritoryAssignments.Add(assignment);
        await db.SaveChangesAsync(); return assignment;
    }

    private async Task RemoveSetupAsync(Guid assignmentId)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(1, await db.TerritoryAssignments.Where(row => row.Id == assignmentId).ExecuteDeleteAsync());
    }

    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parentId = null)
    {
        using var response = await api.PostAsJsonAsync("/directory/geography/" + level,
            new CreateGeographyRequest(name, parentId, level == "towns" ? 52.9m : null, level == "towns" ? -6.3m : null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }

    private static string AssignmentsUrl(string rep) => "/coverage/reps/" + rep + "/assignments";
    private static string LocationsUrl(string rep) => "/coverage/reps/" + rep + "/locations";
    private static string OwnerUrl(Guid id) => $"/coverage/locations/{id}/owner";
    private static async Task<LocationCoverageDetails[]> LocationsAsync(HttpClient api, string rep) =>
        (await api.GetFromJsonAsync<LocationCoverageDetails[]>(LocationsUrl(rep)))!;
    private static async Task<LocationCoverageDetails> OwnerAsync(HttpClient api, Guid id) =>
        (await api.GetFromJsonAsync<LocationCoverageDetails>(OwnerUrl(id)))!;

    private static void AssertOwner(LocationCoverageDetails row, string rep, Guid assignmentId, TerritoryLevel level, Guid id, string name)
    {
        Assert.NotNull(row.Owner); Assert.Equal(rep, row.Owner.RepSubject);
        Assert.Equal(assignmentId, row.Owner.Source.AssignmentId);
        Assert.Equal(new(level, id), row.Owner.Source.Target); Assert.Equal(name, row.Owner.Source.Name);
    }

    private sealed record Seed(GeographyItem Region, GeographyItem County, GeographyItem Rathdrum, GeographyItem Laragh,
        Guid[] RathdrumLocations, Guid[] LaraghLocations);
}
