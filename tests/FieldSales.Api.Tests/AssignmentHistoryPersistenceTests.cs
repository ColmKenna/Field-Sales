using System.Data;
using FieldSales.Api.Coverage;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FieldSales.Api.Tests;

public sealed class AssignmentHistoryPersistenceTests(CoverageDatabase database) : IClassFixture<CoverageDatabase>
{
    private static readonly DateTimeOffset At = new(2026, 9, 17, 14, 2, 59, TimeSpan.Zero);
    private static readonly HistoryStaffSnapshot Staff = new("actor", [
        new("actor", "M. Byrne", [BusinessRoles.HeadOfficeUser], true),
        new("colm", "Colm", [BusinessRoles.FieldSalesperson], true),
        new("aoife", "Aoife", [BusinessRoles.FieldSalesperson], true),
        new("brian", "Brian", [BusinessRoles.FieldSalesperson], true)]);

    [Fact]
    public async Task Should_RejectMissingTown_When_PreparingLocationOwnershipBeforeTransaction()
    {
        await using var db = database.Create();
        var seed = await SeedAsync(db);
        var candidate = await db.Locations.AsNoTracking().SingleAsync(location => location.TownId == seed.Rathdrum);
        await db.Locations.Where(location => location.Id == candidate.Id).ExecuteDeleteAsync();
        await db.Towns.Where(town => town.Id == seed.Rathdrum).ExecuteDeleteAsync();
        var error = await Assert.ThrowsAsync<CustomerDirectoryValidationException>(() =>
            new CoverageOwnershipReader(db).ResolveAsync(candidate, default, "FirstLocation.TownId"));
        Assert.Equal("FirstLocation.TownId", error.Field);
        Assert.Equal("Choose a town", error.Message);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Empty(db.ChangeTracker.Entries<AssignmentHistory>());
    }

    [Fact]
    public async Task Should_WriteOneEntryForEachChangedLocation_When_CountyAndTownAssignmentsChange()
    {
        await using var db = database.Create(); var seed = await SeedAsync(db, 23, 117);
        var county = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County));
        var first = await ChangeAsync(db, () => db.TerritoryAssignments.Add(county)); Assert.Equal(140, first.Count);
        var initial = await EntriesAsync(db, first.Id); Assert.Equal(140, initial.Length);
        Assert.All(initial, row => { Assert.Null(row.PreviousOwner); Assert.Equal("Colm", row.NewOwner!.Rep.DisplayName); Assert.Equal("Wicklow", row.NewOwner.Source.Name); });
        var town = TerritoryAssignment.Create("aoife", new(TerritoryLevel.Town, seed.Rathdrum));
        var carved = await ChangeAsync(db, () => db.TerritoryAssignments.Add(town)); Assert.Equal(23, carved.Count);
        Assert.All(await EntriesAsync(db, carved.Id), row =>
        {
            Assert.Equal("colm", row.PreviousOwner!.Rep.Subject); Assert.Equal("aoife", row.NewOwner!.Rep.Subject);
            Assert.Equal("Wicklow", row.PreviousOwner.Source.Name); Assert.Equal("Rathdrum", row.NewOwner.Source.Name);
        });
        var removed = await ChangeAsync(db, () => db.TerritoryAssignments.Remove(town)); Assert.Equal(23, removed.Count);
        Assert.All(await EntriesAsync(db, removed.Id), row =>
        {
            Assert.Equal("aoife", row.PreviousOwner!.Rep.Subject); Assert.Equal("colm", row.NewOwner!.Rep.Subject);
            Assert.Equal(county.Id, row.NewOwner.Source.AssignmentId);
        });
        Assert.Equal(140, (await EntriesAsync(db, first.Id)).Length);
        Assert.Equal(23, (await EntriesAsync(db, carved.Id)).Length);
    }

    [Fact]
    public async Task Should_PreserveCarveOutOwners_When_ParentCountyAssignmentIsRemoved()
    {
        await using var db = database.Create(); var seed = await SeedAsync(db, 23, 117);
        var county = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County));
        var town = TerritoryAssignment.Create("aoife", new(TerritoryLevel.Town, seed.Rathdrum));
        await ChangeAsync(db, () => db.TerritoryAssignments.AddRange(county, town));
        var removed = await ChangeAsync(db, () => db.TerritoryAssignments.Remove(county)); Assert.Equal(117, removed.Count);
        Assert.All(await EntriesAsync(db, removed.Id), row => { Assert.Equal("colm", row.PreviousOwner!.Rep.Subject); Assert.Null(row.NewOwner); });
        var owner = new CoverageOwnershipReader(db);
        var current = await owner.ReadAllAsync(default);
        Assert.All(current.Locations.Where(path => path.TownId == seed.Rathdrum), path => Assert.Equal("aoife", current.Owners[path.LocationId]!.RepSubject));
    }

    [Fact]
    public async Task Should_Write96UnassignedChanges_When_BriansOnlyWexfordAssignmentIsRemoved()
    {
        await using var db = database.Create(); var seed = await SeedAsync(db, 96, countyName: "Wexford");
        var county = TerritoryAssignment.Create("brian", new(TerritoryLevel.County, seed.County));
        await ChangeAsync(db, () => db.TerritoryAssignments.Add(county));
        var removed = await ChangeAsync(db, () => db.TerritoryAssignments.Remove(county)); Assert.Equal(96, removed.Count);
        Assert.All(await EntriesAsync(db, removed.Id), row =>
        {
            Assert.Equal("Brian", row.PreviousOwner!.Rep.DisplayName); Assert.Null(row.NewOwner);
            Assert.Equal("Wexford", row.PreviousOwner.Source.Name); Assert.Contains("Brian → Unassigned", row.Display);
        });
    }

    [Fact]
    public async Task Should_WriteNoHistory_When_OnlySourceChangesOrOperationIsNoOp()
    {
        await using var db = database.Create(); var seed = await SeedAsync(db);
        var county = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County));
        await ChangeAsync(db, () => db.TerritoryAssignments.Add(county));
        var town = TerritoryAssignment.Create("colm", new(TerritoryLevel.Town, seed.Rathdrum));
        var sourceOnly = await ChangeAsync(db, () => db.TerritoryAssignments.Add(town)); Assert.Equal(0, sourceOnly.Count);
        Assert.Empty(await EntriesAsync(db, sourceOnly.Id));
        var noOp = await ChangeAsync(db, () => { }); Assert.Equal(0, noOp.Count);
        Assert.Empty(await EntriesAsync(db, noOp.Id));
    }

    [Fact]
    public async Task Should_PreserveImmutableReadableHistory_When_LabelsChangeAssignmentIsRemovedOrDatabaseRestarts()
    {
        await using var db = database.Create(); var seed = await SeedAsync(db);
        var county = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County));
        var created = await ChangeAsync(db, () => db.TerritoryAssignments.Add(county), reason: "  New rep  ");
        var original = Assert.Single(await EntriesAsync(db, created.Id));
        Assert.Equal("17 Sep 2026 14:02 UTC — Unassigned → Colm — via Wicklow — territory assignment — by M. Byrne — New rep", original.Display);
        Assert.Equal(At, original.ChangedAt); Assert.Equal("New rep", original.Reason);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Counties] SET [Name] = {"Renamed County"} WHERE [Id] = {seed.County}");
        await ChangeAsync(db, () => db.TerritoryAssignments.Remove(county));
        await using var restarted = database.Create();
        Assert.Equal(original, Assert.Single(await EntriesAsync(restarted, created.Id)));
        Assert.Equal(county.Id, original.NewOwner!.Source.AssignmentId);
    }

    [Fact]
    public async Task Should_RejectUpdatesAndDeletes_When_EfBulkOrDirectSqlAttemptsToMutateHistory()
    {
        await using var db = database.Create(); var seed = await SeedAsync(db);
        var operation = await ChangeAsync(db, () => db.TerritoryAssignments.Add(TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County))));
        var row = await db.AssignmentHistory.SingleAsync(row => row.OperationId == operation.Id);
        db.Entry(row).Property(item => item.ActorName).CurrentValue = "Spoofed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        row = await db.AssignmentHistory.SingleAsync(item => item.Id == row.Id); db.Remove(row);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
        var update = await Assert.ThrowsAsync<SqlException>(() => db.AssignmentHistory.Where(item => item.Id == row.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Reason, "Changed")));
        Assert.Equal(51021, update.Number);
        var delete = await Assert.ThrowsAsync<SqlException>(() => db.AssignmentHistory.Where(item => item.Id == row.Id).ExecuteDeleteAsync());
        Assert.Equal(51021, delete.Number);
        var sql = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [AssignmentHistory] SET [ActorName] = {"Changed"} WHERE [Id] = {row.Id}"));
        Assert.Equal(51021, sql.Number);
        Assert.Equal("M. Byrne", (await db.AssignmentHistory.AsNoTracking().SingleAsync(item => item.Id == row.Id)).ActorName);
    }

    [Fact]
    public async Task Should_RollBackAndRetryWithoutDuplicateHistory_When_FailureOccursAfterAssignmentAndHistoryAreSaved()
    {
        await using var db = database.Create(); var seed = await SeedAsync(db); var operationId = Guid.NewGuid();
        await Assert.ThrowsAsync<SqlException>(() => ChangeAsync(db,
            () => db.TerritoryAssignments.Add(TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County))), operationId, fail: true));
        db.ChangeTracker.Clear();
        Assert.False(await db.TerritoryAssignments.AnyAsync(row => row.CountyId == seed.County));
        Assert.Empty(await EntriesAsync(db, operationId));
        var retry = await ChangeAsync(db, () => db.TerritoryAssignments.Add(TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County))), operationId);
        Assert.Equal(1, retry.Count); Assert.Single(await EntriesAsync(db, operationId));
    }

    [Fact]
    public async Task Should_UpgradeWithoutInventingHistory_When_PredecessorCoverageDataExists()
    {
        var options = database.Options("HistoryUpgrade_" + Guid.NewGuid().ToString("N"));
        await using var db = new DirectoryDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261003123731_AddTerritoryAssignments");
        var seed = await SeedAsync(db);
        var county = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, seed.County));
        db.TerritoryAssignments.Add(county); await db.SaveChangesAsync();
        await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.AssignmentHistory.ToArrayAsync());
        var removed = await ChangeAsync(db, () => db.TerritoryAssignments.Remove(county)); Assert.Equal(1, removed.Count);
        await using var restarted = new DirectoryDbContext(options);
        var entry = Assert.Single(await EntriesAsync(restarted, removed.Id));
        Assert.Equal("Colm", entry.PreviousOwner!.Rep.DisplayName); Assert.Null(entry.NewOwner);
        var error = await Assert.ThrowsAsync<SqlException>(() => restarted.AssignmentHistory.ExecuteDeleteAsync()); Assert.Equal(51021, error.Number);
    }

    private static async Task<(Guid Id, int Count)> ChangeAsync(DirectoryDbContext db, Action mutation,
        Guid? operation = null, string? reason = null, bool fail = false)
    {
        db.ChangeTracker.Clear();
        var id = operation ?? Guid.NewGuid();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var reader = new CoverageOwnershipReader(db); var before = await reader.ReadAllAsync(default);
        mutation(); await db.SaveChangesAsync();
        var after = await reader.ReadAllAsync(default);
        var writer = new AssignmentHistoryWriter(db, new UnusedDirectory(), new HttpContextAccessor());
        int count = writer.AppendChanges(before, after, Staff, id, OwnershipChangeCause.TerritoryAssignment, At, reason);
        await db.SaveChangesAsync();
        if (fail) await db.Database.ExecuteSqlRawAsync("THROW 51099, 'Injected late transaction failure', 1");
        await transaction.CommitAsync(); return (id, count);
    }

    private static async Task<AssignmentHistoryDetails[]> EntriesAsync(DirectoryDbContext db, Guid operation) =>
        (await db.AssignmentHistory.AsNoTracking().Where(row => row.OperationId == operation).OrderBy(row => row.Sequence).ToArrayAsync())
            .Select(AssignmentHistoryFormatter.Details).ToArray();

    private static async Task<Seed> SeedAsync(DirectoryDbContext db, int rathdrumCount = 1, int laraghCount = 0, string countyName = "Wicklow")
    {
        var region = Guid.NewGuid(); var county = Guid.NewGuid(); var town = Guid.NewGuid(); var laragh = Guid.NewGuid(); var customer = Guid.NewGuid();
        string regionName = "Leinster-" + region.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Regions] ([Id],[Name],[NormalizedName],[IsArchived]) VALUES ({region},{regionName},{regionName.ToUpperInvariant()},0);
            INSERT INTO [Counties] ([Id],[RegionId],[Name],[NormalizedName],[IsArchived]) VALUES ({county},{region},{countyName},{countyName.ToUpperInvariant()},0);
            INSERT INTO [Towns] ([Id],[CountyId],[Name],[NormalizedName],[IsArchived]) VALUES ({town},{county},N'Rathdrum',N'RATHDRUM',0),({laragh},{county},N'Laragh',N'LARAGH',0);
            INSERT INTO [Customers] ([Id],[Name]) VALUES ({customer},N'Customer');
            """);
        foreach (var (rep, manager) in new[] { ("colm", "actor"), ("aoife", "actor"), ("brian", "actor") })
            if (!await db.RepReportingLines.AnyAsync(row => row.RepSubject == rep)) db.RepReportingLines.Add(RepReportingLine.Create(rep, manager));
        await db.SaveChangesAsync();
        for (int index = 0; index < rathdrumCount + laraghCount; index++)
        {
            var location = Guid.NewGuid(); var townId = index < rathdrumCount ? town : laragh; string name = "Shop " + index;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Locations] ([Id],[CustomerId],[TownId],[Name],[NormalizedName]) VALUES ({location},{customer},{townId},{name},{name.ToUpperInvariant()})");
        }
        return new(county, town);
    }
    private sealed record Seed(Guid County, Guid Rathdrum);
    private sealed class UnusedDirectory : IStaffDirectory
    {
        public Task<IReadOnlyList<StaffDirectoryEntry>> ListAsync(string token, CancellationToken ct) => throw new InvalidOperationException();
        public Task<IReadOnlyList<StaffDirectoryEntry>> LookupAsync(string token, IReadOnlyCollection<string> subjects, CancellationToken ct) => throw new InvalidOperationException();
    }
}
