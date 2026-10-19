using System.Data;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Coverage;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Tests;

// WI-029 increment 1: the append-only permission record and the current grants it keeps in step.
[Collection(SqlServerCollection.Name)]
public sealed class RestrictionPermissionPersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const RestrictionPermissionChangeKind Granted = RestrictionPermissionChangeKind.Granted;
    private const RestrictionPermissionChangeKind Removed = RestrictionPermissionChangeKind.Removed;
    private static readonly DateTimeOffset At = new(2026, 9, 15, 15, 2, 30, TimeSpan.FromHours(1));
    private static readonly HistoryStaffSnapshot Staff = new("niamh", [
        new("niamh", "Niamh Byrne", [BusinessRoles.SalesManager, BusinessRoles.HeadOfficeUser], true),
        new("colm", "Colm", [BusinessRoles.FieldSalesperson], true)]);
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _connectionString = fixture.CreateConnectionString("Permissions");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_StoreUtcTimeActorRepGroupAndReason_When_AGrantIsRecorded()
    {
        await using var db = CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        var granted = await ApplyAsync(db, Granted, group.Id, null, "Completed training 15 Sep 2026");

        await using var restarted = CreateContext();
        var entry = await restarted.RestrictionPermissionChanges.AsNoTracking().SingleAsync();
        Assert.Equal(granted.Id, entry.Id);
        Assert.Equal(("colm", "Colm"), (entry.RepSubject, entry.RepName));
        Assert.Equal((group.Id, "Pharmacy-only medicines"), (entry.RestrictionGroupId, entry.RestrictionGroupName));
        Assert.Equal(Granted, entry.Change);
        Assert.Equal(new DateTimeOffset(2026, 9, 15, 14, 2, 30, TimeSpan.Zero), entry.ChangedAt);
        Assert.Equal(TimeSpan.Zero, entry.ChangedAt.Offset);
        Assert.Equal(("niamh", "Niamh Byrne"), (entry.ActorSubject, entry.ActorName));
        Assert.Equal("Completed training 15 Sep 2026", entry.Reason);
        var held = await restarted.RestrictionPermissions.AsNoTracking().SingleAsync();
        Assert.Equal(("colm", group.Id), (held.RepSubject, held.RestrictionGroupId));
    }

    [Fact]
    public async Task Should_KeepEveryChangeInOrder_When_APermissionIsGrantedRemovedAndGrantedAgain()
    {
        await using var db = CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        var first = await ApplyAsync(db, Granted, group.Id, null, "Completed training 15 Sep 2026");
        var second = await ApplyAsync(db, Removed, group.Id, first.Id, "Moved to the hardware team");
        Assert.False(await db.RestrictionPermissions.AnyAsync());
        var third = await ApplyAsync(db, Granted, group.Id, second.Id, null);

        await using var restarted = CreateContext();
        var entries = await restarted.RestrictionPermissionChanges.AsNoTracking().OrderBy(row => row.Sequence).ToArrayAsync();
        Assert.Equal([first.Id, second.Id, third.Id], entries.Select(row => row.Id));
        Assert.Equal([Granted, Removed, Granted], entries.Select(row => row.Change));
        Assert.Equal(["Completed training 15 Sep 2026", "Moved to the hardware team", null], entries.Select(row => row.Reason));
        Assert.Equal(3, entries.Select(row => row.Sequence).Distinct().Count());
        var held = Assert.Single(await restarted.RestrictionPermissions.AsNoTracking().ToArrayAsync());
        Assert.Equal(("colm", group.Id), (held.RepSubject, held.RestrictionGroupId));
    }

    [Fact]
    public async Task Should_RejectTheStatement_When_ARecordedChangeIsUpdatedOrDeleted()
    {
        await using var db = CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        var granted = await ApplyAsync(db, Granted, group.Id, null, "Completed training 15 Sep 2026");

        var row = await db.RestrictionPermissionChanges.SingleAsync();
        db.Entry(row).Property(item => item.ActorName).CurrentValue = "Spoofed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Remove(await db.RestrictionPermissionChanges.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var update = await Assert.ThrowsAsync<SqlException>(() => db.RestrictionPermissionChanges
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Reason, "Changed")));
        Assert.Equal(51029, update.Number);
        var delete = await Assert.ThrowsAsync<SqlException>(() => db.RestrictionPermissionChanges.ExecuteDeleteAsync());
        Assert.Equal(51029, delete.Number);
        var sql = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [RestrictionPermissionChanges] SET [ActorName] = {"Changed"} WHERE [Id] = {granted.Id}"));
        Assert.Equal(51029, sql.Number);

        await using var restarted = CreateContext();
        var stored = await restarted.RestrictionPermissionChanges.AsNoTracking().SingleAsync();
        Assert.Equal(("Niamh Byrne", "Completed training 15 Sep 2026"), (stored.ActorName, stored.Reason));
    }

    [Theory]
    [InlineData("blank")]
    [InlineData("padded")]
    [InlineData("1000")]
    [InlineData("1001")]
    [InlineData("control")]
    public async Task Should_SaveNoReasonOrReject_When_ReasonIsBlankTooLongOrHasControlCharacters(string reasonCase)
    {
        var (input, saved, rejected) = reasonCase switch
        {
            "blank" => (" \t ", null, false),
            "padded" => ("  Completed training 15 Sep 2026  ", "Completed training 15 Sep 2026", false),
            "1000" => (new string('R', 1000), new string('R', 1000), false),
            "1001" => (new string('R', 1001), (string?)null, true),
            "control" => ("Completed\ntraining", null, true),
            _ => throw new ArgumentOutOfRangeException(nameof(reasonCase))
        };
        await using var db = CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");

        if (rejected)
        {
            var error = await Assert.ThrowsAsync<CoverageValidationException>(() => ApplyAsync(db, Granted, group.Id, null, input));
            Assert.Equal("Reason", error.Field);
        }
        else await ApplyAsync(db, Granted, group.Id, null, input);

        await using var restarted = CreateContext();
        var entries = await restarted.RestrictionPermissionChanges.AsNoTracking().ToArrayAsync();
        Assert.Equal(rejected ? 0 : 1, entries.Length);
        Assert.Equal(rejected ? 0 : 1, await restarted.RestrictionPermissions.CountAsync());
        if (!rejected) Assert.Equal(saved, entries[0].Reason);
    }

    [Fact]
    public async Task Should_SaveNeitherStateNorRecord_When_TheWriteFailsPartWay()
    {
        await using var db = CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");

        await Assert.ThrowsAsync<SqlException>(() => ApplyAsync(db, Granted, group.Id, null, "Completed training 15 Sep 2026", fail: true));
        await using (var restarted = CreateContext())
        {
            Assert.False(await restarted.RestrictionPermissionChanges.AnyAsync());
            Assert.False(await restarted.RestrictionPermissions.AnyAsync());
        }

        await ApplyAsync(db, Granted, group.Id, null, "Completed training 15 Sep 2026");
        await using var retried = CreateContext();
        Assert.Single(await retried.RestrictionPermissionChanges.AsNoTracking().ToArrayAsync());
        Assert.Single(await retried.RestrictionPermissions.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Should_BlockDeletingTheGroup_When_ARepCurrentlyHoldsItsPermission()
    {
        await using var db = CreateContext();
        var group = await AddGroupAsync(db, "Pharmacy-only medicines");
        var granted = await ApplyAsync(db, Granted, group.Id, null, "Completed training 15 Sep 2026");

        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
            "DELETE FROM RestrictionGroups WHERE Id = @id", new SqlParameter("@id", group.Id)));
        Assert.Equal(547, error.Number);
        Assert.True(await db.RestrictionGroups.AnyAsync(item => item.Id == group.Id));

        // Once every permission is removed, the group may go; its record keeps the captured name.
        var removed = await ApplyAsync(db, Removed, group.Id, granted.Id, null);
        Assert.Equal(1, await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM RestrictionGroups WHERE Id = @id", new SqlParameter("@id", group.Id)));
        await using var restarted = CreateContext();
        var entries = await restarted.RestrictionPermissionChanges.AsNoTracking().OrderBy(row => row.Sequence).ToArrayAsync();
        Assert.Equal([granted.Id, removed.Id], entries.Select(row => row.Id));
        Assert.All(entries, row => Assert.Equal((group.Id, "Pharmacy-only medicines"), (row.RestrictionGroupId, row.RestrictionGroupName)));
    }

    private CatalogueDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(_connectionString).Options);

    private static async Task<RestrictionPermissionChange> ApplyAsync(CatalogueDbContext db, RestrictionPermissionChangeKind change,
        Guid group, Guid? expectedLastChangeId, string? reason, bool fail = false)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var entry = await new RestrictionPermissionWriter(db).ApplyAsync(change, "colm", group, expectedLastChangeId, Staff, At, reason, default);
        if (fail) await db.Database.ExecuteSqlRawAsync("THROW 51099, 'Injected late transaction failure', 1");
        await transaction.CommitAsync();
        return entry!;
    }

    private static async Task<RestrictionGroup> AddGroupAsync(CatalogueDbContext db, string name)
    {
        var group = RestrictionGroup.Create(name);
        db.RestrictionGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }
}
