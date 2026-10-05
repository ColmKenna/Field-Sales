using FieldSales.Api.Coverage;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

// Increment 1 exercises only the approved duplicate/invalid storage,
// reporting-line uniqueness and schema upgrade/restart scenario families.
[Collection(SqlServerCollection.Name)]
public sealed class TerritoryAssignmentPersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly CoverageDatabase database = new(fixture);

    public Task InitializeAsync() => database.InitializeAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task Should_PreserveDirectoryAndCoverageData_When_SchemaIsUpgradedOrRestarted()
    {
        var options = database.Options("CoverageUpgrade_" + Guid.NewGuid().ToString("N"));
        Seed seed;
        Guid assignmentId;
        await using (var db = new DirectoryDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261003104659_AddMainContactReplacement");
            seed = await SeedAsync(db, reporting: false);
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            db.RepReportingLines.Add(RepReportingLine.Create(seed.Rep, seed.Manager));
            var assignment = TerritoryAssignment.Create(seed.Rep, new(TerritoryLevel.County, seed.UnitId));
            assignmentId = assignment.Id;
            db.TerritoryAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }
        await using var restarted = new DirectoryDbContext(options);
        var location = await restarted.Locations.SingleAsync();
        Assert.Equal(seed.UnitId, location.Id);
        Assert.Equal(seed.UnitId, location.TownId);
        Assert.Equal(seed.UnitId, location.CustomerId);
        Assert.Equal("Existing shop", location.Name);
        Assert.Equal("A67 X123", location.Eircode);
        Assert.Equal("Existing customer", (await restarted.Customers.SingleAsync()).Name);
        Assert.Equal("Wicklow", (await restarted.Counties.SingleAsync()).Name);
        Assert.Equal("Rathdrum", (await restarted.Towns.SingleAsync()).Name);
        var saved = await restarted.TerritoryAssignments.SingleAsync();
        Assert.Equal(assignmentId, saved.Id);
        Assert.Equal(seed.Rep, saved.RepSubject);
        Assert.Equal(new(TerritoryLevel.County, seed.UnitId), saved.Target);
        Assert.Equal(8, saved.Version.Length);
        var line = await restarted.RepReportingLines.SingleAsync();
        Assert.Equal(seed.Manager, line.ManagerSubject);
        Assert.Equal(8, line.Version.Length);
    }

    [Theory]
    [InlineData(TerritoryLevel.Region)]
    [InlineData(TerritoryLevel.County)]
    [InlineData(TerritoryLevel.Town)]
    [InlineData(TerritoryLevel.Location)]
    public async Task Should_RejectSameLevelDuplicate_When_DifferentRepsAssignTheSameUnit(TerritoryLevel level)
    {
        await using var db = database.Create();
        var seed = await SeedAsync(db);
        string otherRep = seed.Rep + "-other";
        db.RepReportingLines.Add(RepReportingLine.Create(otherRep, seed.Manager));
        db.TerritoryAssignments.Add(TerritoryAssignment.Create(seed.Rep, new(level, seed.UnitId)));
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<SqlException>(() => InsertAsync(db, otherRep, seed.UnitId, level));
        Assert.Equal(2601, error.Number);
        var original = await db.TerritoryAssignments.SingleAsync(item => item.RepSubject == seed.Rep);
        Assert.Equal(seed.Rep, original.RepSubject);
        Assert.False(await db.TerritoryAssignments.AnyAsync(item => item.RepSubject == otherRep));
    }

    [Fact]
    public async Task Should_AllowOverlappingLevels_When_EachTypedUnitHasOneAssignment()
    {
        await using var db = database.Create();
        var seed = await SeedAsync(db);
        // The same GUID in distinct tables is valid. Uniqueness is per level,
        // so wider assignments must not block a narrower carve-out.
        foreach (var level in Enum.GetValues<TerritoryLevel>())
        {
            string rep = seed.Rep + "-" + level;
            db.RepReportingLines.Add(RepReportingLine.Create(rep, seed.Manager));
            db.TerritoryAssignments.Add(TerritoryAssignment.Create(rep, new(level, seed.UnitId)));
        }
        await db.SaveChangesAsync();
        await using var restarted = database.Create();
        var assignments = await restarted.TerritoryAssignments.Where(item => item.RepSubject.StartsWith(seed.Rep + "-"))
            .ToArrayAsync();
        Assert.Equal(4, assignments.Length);
        Assert.Equal(Enum.GetValues<TerritoryLevel>(), assignments.Select(item => item.Target.Level).Order().ToArray());
    }

    [Theory]
    [InlineData("no-target")]
    [InlineData("two-targets")]
    [InlineData("empty-target")]
    [InlineData("unknown-region")]
    [InlineData("unknown-county")]
    [InlineData("unknown-town")]
    [InlineData("unknown-location")]
    [InlineData("unknown-rep")]
    [InlineData("wrong-subject-case")]
    public async Task Should_RejectInvalidAssignment_When_StorageIsWrittenDirectly(string scenario)
    {
        await using var db = database.Create();
        var seed = await SeedAsync(db);
        Guid? region = null, county = null, town = seed.UnitId, location = null;
        string rep = seed.Rep;
        switch (scenario)
        {
            case "no-target": town = null; break;
            case "two-targets": county = seed.UnitId; break;
            case "empty-target": town = Guid.Empty; break;
            case "unknown-region": town = null; region = Guid.NewGuid(); break;
            case "unknown-county": town = null; county = Guid.NewGuid(); break;
            case "unknown-town": town = Guid.NewGuid(); break;
            case "unknown-location": town = null; location = Guid.NewGuid(); break;
            case "unknown-rep": rep += "-missing"; break;
            case "wrong-subject-case": rep = rep.ToUpperInvariant(); break;
        }
        var error = await Assert.ThrowsAsync<SqlException>(() => InsertRawAsync(db, rep, region, county, town, location));
        Assert.Equal(547, error.Number);
        Assert.False(await db.TerritoryAssignments.AnyAsync(item => item.RepSubject == rep));
    }

    [Fact]
    public async Task Should_KeepSingleManagerAndDetectStaleUpdates_When_ReportingLineChanges()
    {
        await using var db = database.Create();
        var seed = await SeedAsync(db);
        var duplicate = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO RepReportingLines (RepSubject,ManagerSubject) VALUES ({seed.Rep},N'other-manager')
            """));
        Assert.Equal(2627, duplicate.Number);
        await using var stale = database.Create();
        var staleLine = await stale.RepReportingLines.SingleAsync(item => item.RepSubject == seed.Rep);
        byte[] oldVersion = staleLine.Version.ToArray();
        var current = await db.RepReportingLines.SingleAsync(item => item.RepSubject == seed.Rep);
        current.SetManager(seed.Manager + "-new");
        await db.SaveChangesAsync();
        Assert.NotEqual(oldVersion, current.Version);
        staleLine.SetManager(seed.Manager + "-stale");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using var check = database.Create();
        Assert.Equal(seed.Manager + "-new",
            (await check.RepReportingLines.SingleAsync(item => item.RepSubject == seed.Rep)).ManagerSubject);
    }

    [Theory]
    [InlineData(TerritoryLevel.Region)]
    [InlineData(TerritoryLevel.County)]
    [InlineData(TerritoryLevel.Town)]
    [InlineData(TerritoryLevel.Location)]
    public async Task Should_ProtectReferencedUnitAndReportingLine_When_AssignmentExists(TerritoryLevel level)
    {
        await using var db = database.Create();
        var seed = await SeedAsync(db);
        // Add an otherwise unused unit, so it is the assignment FK alone
        // protecting deletion rather than a child or existing Location.
        Guid target = Guid.NewGuid();
        switch (level)
        {
            case TerritoryLevel.Region:
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName,IsArchived) VALUES ({target},{target.ToString()},{target.ToString()},0)"); break;
            case TerritoryLevel.County:
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({target},{target.ToString()},{target.ToString()},{seed.UnitId},0)"); break;
            case TerritoryLevel.Town:
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({target},{target.ToString()},{target.ToString()},{seed.UnitId},0)"); break;
            case TerritoryLevel.Location:
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Locations (Id,CustomerId,Name,NormalizedName,TownId) VALUES ({target},{seed.UnitId},N'Other shop',N'OTHER SHOP',{seed.UnitId})"); break;
        }
        db.TerritoryAssignments.Add(TerritoryAssignment.Create(seed.Rep, new(level, target)));
        await db.SaveChangesAsync();
        var unitError = await Assert.ThrowsAsync<SqlException>(() => level switch
        {
            TerritoryLevel.Region => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Regions WHERE Id={target}"),
            TerritoryLevel.County => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Counties WHERE Id={target}"),
            TerritoryLevel.Town => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Towns WHERE Id={target}"),
            _ => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Locations WHERE Id={target}")
        });
        Assert.Equal(547, unitError.Number);
        var repError = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM RepReportingLines WHERE RepSubject={seed.Rep}"));
        Assert.Equal(547, repError.Number);
        Assert.True(await db.TerritoryAssignments.AnyAsync(item => item.RepSubject == seed.Rep));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_RejectMissingManager_When_ReportingStorageIsWrittenDirectly(string manager)
    {
        await using var db = database.Create();
        string rep = "rep-" + Guid.NewGuid().ToString("N");
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO RepReportingLines (RepSubject,ManagerSubject) VALUES ({rep},{manager})"));
        Assert.Equal(547, error.Number);
    }

    private static Task<int> InsertAsync(DirectoryDbContext db, string rep, Guid id, TerritoryLevel level) =>
        InsertRawAsync(db, rep, level == TerritoryLevel.Region ? id : null, level == TerritoryLevel.County ? id : null,
            level == TerritoryLevel.Town ? id : null, level == TerritoryLevel.Location ? id : null);

    private static Task<int> InsertRawAsync(DirectoryDbContext db, string rep, Guid? region, Guid? county, Guid? town, Guid? location)
    {
        Guid id = Guid.NewGuid();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TerritoryAssignments (Id,RepSubject,RegionId,CountyId,TownId,LocationId)
            VALUES ({id},{rep},{region},{county},{town},{location})
            """);
    }

    private sealed record Seed(Guid UnitId, string Rep, string Manager);
    private static async Task<Seed> SeedAsync(DirectoryDbContext db, bool reporting = true)
    {
        Guid unit = Guid.NewGuid();
        string unique = unit.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName,IsArchived) VALUES ({unit},{unique},{unique},0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({unit},N'Wicklow',N'WICKLOW',{unit},0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({unit},N'Rathdrum',N'RATHDRUM',{unit},0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Customers (Id,Name) VALUES ({unit},N'Existing customer')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Locations (Id,CustomerId,Name,NormalizedName,TownId,Eircode) VALUES ({unit},{unit},N'Existing shop',N'EXISTING SHOP',{unit},N'A67 X123')");
        var seed = new Seed(unit, "rep-" + unique, "manager-" + unique);
        if (reporting)
        {
            db.RepReportingLines.Add(RepReportingLine.Create(seed.Rep, seed.Manager));
            await db.SaveChangesAsync();
        }
        return seed;
    }
}

public sealed class CoverageDatabase(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly string _catalogName = $"ApiTest_Coverage_{Guid.NewGuid():N}";

    public DbContextOptions<DirectoryDbContext> Options(string? name = null)
    {
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = name ?? _catalogName };
        return new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection.ConnectionString).Options;
    }

    public DirectoryDbContext Create() => new(Options());

    public async Task InitializeAsync()
    {
        await using var db = Create();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
