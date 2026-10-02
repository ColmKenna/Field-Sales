using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

public sealed class LocationPositionPersistenceTests : IClassFixture<LocationPositionDatabase>
{
    private readonly LocationPositionDatabase database;
    private static readonly DateTimeOffset First = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    public LocationPositionPersistenceTests(LocationPositionDatabase database) => this.database = database;

    [Fact]
    public async Task Should_PreserveDirectoryData_When_CoordinateSchemaIsAddedOrAppRestarts()
    {
        var options = database.Options("PositionUpgrade_" + Guid.NewGuid().ToString("N"));
        Guid locationId, contactId = Guid.NewGuid(), typeId = Guid.NewGuid();
        await using (var db = new DirectoryDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261002141232_AddContactsAndMainContact");
            locationId = await SeedAsync(db);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ContactTypes (Id,Name,IsArchived) VALUES ({typeId},N'Pharmacist',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Contacts (Id,Name,ContactTypeId,Status) VALUES ({contactId},N'Mary Walsh',{typeId},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO LocationContacts (LocationId,ContactId) VALUES ({locationId},{contactId})");
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
        }
        await using var restarted = new DirectoryDbContext(options);
        var location = await restarted.Locations.Include(item => item.Contacts).Include(item => item.PositionHistory).SingleAsync();
        Assert.Equal(locationId, location.Id);
        Assert.Equal(contactId, location.MainContactId);
        Assert.Equal(contactId, Assert.Single(location.Contacts).ContactId);
        Assert.Equal("Existing shop", location.Name);
        Assert.Equal("A67 X123", location.Eircode);
        Assert.Null(location.Latitude); Assert.Null(location.Longitude); Assert.Null(location.PositionPrecision);
        Assert.Null(location.PositionedAt); Assert.Empty(location.PositionHistory);
        Assert.Equal(location.CustomerId, (await restarted.Customers.SingleAsync()).Id);
        Assert.Equal(location.TownId, (await restarted.Towns.SingleAsync()).Id);
        var store = Store(restarted);
        Assert.Null((await store.FindLocationAsync(locationId, default))!.Position);
    }

    [Fact]
    public async Task Should_RetainPreviousSourcesAndExposeStoredPrecision_When_PositionsChangeOrClear()
    {
        await using var db = database.Create();
        Guid id = await SeedAsync(db);
        var location = await db.Locations.SingleAsync(item => item.Id == id);
        Assert.True(location.ApplyDefaultPosition(new(52.923456789m, -6.291234567m), LocationPositionPrecision.Town, First));
        await db.SaveChangesAsync();
        Assert.False(location.ApplyDefaultPosition(new(52.923456789m, -6.291234567m), LocationPositionPrecision.Town, First.AddHours(1)));
        Assert.Empty(location.PositionHistory);
        Assert.Equal(First, location.PositionedAt);
        Assert.True(location.ApplyDefaultPosition(new(52.93m, -6.30m), LocationPositionPrecision.Eircode, First.AddHours(2)));
        await db.SaveChangesAsync();

        await using var restarted = database.Create();
        var saved = await restarted.Locations.Include(item => item.PositionHistory).SingleAsync(item => item.Id == id);
        var old = Assert.Single(saved.PositionHistory);
        Assert.Equal(52.9234568m, old.Latitude); Assert.Equal(-6.2912346m, old.Longitude);
        Assert.Equal(LocationPositionPrecision.Town, old.Precision);
        Assert.Equal(saved.TownId, old.SourceTownId); Assert.Null(old.SourceEircode);
        Assert.Equal(First, old.PositionedAt); Assert.Equal(First.AddHours(2), old.ReplacedAt);
        var store = Store(restarted);
        var detail = (await store.FindLocationAsync(id, default))!;
        Assert.Equal(new LocationPosition(52.93m, -6.30m, LocationPositionPrecision.Eircode, First.AddHours(2)), detail.Position);
        Assert.Equal(detail.Position, (await store.FindCustomerAsync(saved.CustomerId, default))!.Locations.Single().Position);

        Assert.True(saved.ApplyDefaultPosition(null, null, First.AddHours(3)));
        await restarted.SaveChangesAsync();
        await using var cleared = database.Create();
        var missing = await cleared.Locations.Include(item => item.PositionHistory).SingleAsync(item => item.Id == id);
        Assert.Null(missing.Latitude); Assert.Null(missing.Longitude); Assert.Null(missing.PositionPrecision);
        Assert.Null(missing.PositionSourceTownId); Assert.Null(missing.PositionSourceEircode); Assert.Null(missing.PositionedAt);
        Assert.Equal(2, missing.PositionHistory.Count);
        Assert.Equal("A67 X123", missing.PositionHistory.Single(item => item.Precision == LocationPositionPrecision.Eircode).SourceEircode);
    }

    [Fact]
    public async Task Should_PreserveConfirmedPosition_When_AutomaticDefaultingIsAttempted()
    {
        await using var db = database.Create();
        Guid id = await SeedAsync(db);
        // Future GPS data is represented in storage; WI-019 exposes no confirmation command.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Locations SET Latitude=52.9, Longitude=-6.3, PositionPrecision=2, PositionedAt={First} WHERE Id={id}");
        var location = await db.Locations.SingleAsync(item => item.Id == id);
        byte[] version = location.Version.ToArray();
        Assert.False(location.ApplyDefaultPosition(new(53, -7), LocationPositionPrecision.Town, First.AddHours(1)));
        Assert.False(location.ApplyDefaultPosition(null, null, First.AddHours(2)));
        Assert.Equal(0, await db.SaveChangesAsync());
        Assert.Equal(52.9m, location.Latitude); Assert.Equal(-6.3m, location.Longitude);
        Assert.Equal(LocationPositionPrecision.ConfirmedOnSite, location.PositionPrecision);
        Assert.Equal(version, location.Version); Assert.Empty(location.PositionHistory);
    }

    [Fact]
    public async Task Should_RollBackHistoryAndPosition_When_AnotherEditorHasChangedTheLocation()
    {
        Guid id;
        await using (var seed = database.Create())
        {
            id = await SeedAsync(seed);
            var location = await seed.Locations.SingleAsync(item => item.Id == id);
            location.ApplyDefaultPosition(new(52, -6), LocationPositionPrecision.Town, First);
            await seed.SaveChangesAsync();
        }
        await using var stale = database.Create();
        var staleLocation = await stale.Locations.SingleAsync(item => item.Id == id);
        await using (var winner = database.Create())
        {
            var current = await winner.Locations.SingleAsync(item => item.Id == id);
            current.ApplyDefaultPosition(new(53, -7), LocationPositionPrecision.Eircode, First.AddHours(1));
            await winner.SaveChangesAsync();
        }
        staleLocation.ApplyDefaultPosition(new(54, -8), LocationPositionPrecision.Town, First.AddHours(2));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using var check = database.Create();
        var saved = await check.Locations.Include(item => item.PositionHistory).SingleAsync(item => item.Id == id);
        Assert.Equal(53m, saved.Latitude); Assert.Equal(LocationPositionPrecision.Eircode, saved.PositionPrecision);
        Assert.Equal(First.AddHours(1), Assert.Single(saved.PositionHistory).ReplacedAt);
    }

    [Theory]
    [InlineData(52, null, null, false)]
    [InlineData(52, -6, null, false)]
    [InlineData(91, -6, 2, false)]
    [InlineData(52, -181, 2, false)]
    [InlineData(52, -6, 3, false)]
    [InlineData(52, -6, 0, false)]
    [InlineData(52, -6, 1, true)]
    public async Task Should_RejectInvalidPositionStorage_When_DatabaseConstraintsAreUsed(
        int? latitude, int? longitude, int? precision, bool hasSourceTown)
    {
        await using var db = database.Create();
        Guid id = await SeedAsync(db);
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE Locations SET Latitude={latitude}, Longitude={longitude}, PositionPrecision={precision},
                PositionSourceTownId=CASE WHEN {hasSourceTown}=1 THEN TownId ELSE NULL END,
                PositionedAt=CASE WHEN {precision} IS NULL THEN NULL ELSE SYSDATETIMEOFFSET() END
            WHERE Id={id}
            """));
        Assert.Equal(547, error.Number);
        Assert.Null((await db.Locations.SingleAsync(item => item.Id == id)).Latitude);
    }

    [Fact]
    public async Task Should_RejectInvalidDefaultSources_WithoutChangingAnExistingPosition()
    {
        await using var db = database.Create();
        Guid id = await SeedAsync(db);
        var location = await db.Locations.SingleAsync(item => item.Id == id);
        location.ApplyDefaultPosition(new(0, 0), LocationPositionPrecision.Town, First);
        await db.SaveChangesAsync();
        Assert.Throws<CustomerDirectoryValidationException>(() => location.ApplyDefaultPosition(new(1, 1), null, First));
        Assert.Throws<CustomerDirectoryValidationException>(() => location.ApplyDefaultPosition(null, LocationPositionPrecision.Town, First));
        Assert.Throws<CustomerDirectoryValidationException>(() => location.ApplyDefaultPosition(new(1, 1), LocationPositionPrecision.ConfirmedOnSite, First));
        Assert.Throws<CustomerDirectoryValidationException>(() => location.ApplyDefaultPosition(new(1, 1), (LocationPositionPrecision)99, First));
        Assert.Equal(0, await db.SaveChangesAsync());
        Assert.Equal(0m, location.Latitude); Assert.Equal(0m, location.Longitude); Assert.Empty(location.PositionHistory);
    }

    private static CustomerStore Store(DirectoryDbContext db) => new(db, new GeographyStore(db, new GeographyUsageReader([])));

    private static async Task<Guid> SeedAsync(DirectoryDbContext db)
    {
        Guid region = Guid.NewGuid(), county = Guid.NewGuid(), town = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid();
        string unique = region.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName,IsArchived) VALUES ({region},{unique},{unique},0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({county},N'Wicklow',N'WICKLOW',{region},0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({town},N'Laragh',N'LARAGH',{county},0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Customers (Id,Name) VALUES ({customer},N'Existing customer')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Locations (Id,CustomerId,Name,NormalizedName,TownId,Eircode) VALUES ({location},{customer},N'Existing shop',N'EXISTING SHOP',{town},N'A67 X123')");
        return location;
    }
}

public sealed class LocationPositionDatabase : IAsyncLifetime
{
    private readonly MsSqlContainer sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public DbContextOptions<DirectoryDbContext> Options(string? database = null)
    {
        var connection = new SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = database ?? "LocationPositions" };
        return new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection.ConnectionString).Options;
    }
    public DirectoryDbContext Create() => new(Options());
    public async Task InitializeAsync()
    {
        await sql.StartAsync();
        await using var db = Create();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => sql.DisposeAsync().AsTask();
}
