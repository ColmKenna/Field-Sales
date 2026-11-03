using FieldSales.Calls.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

public sealed class HomeRecordCharacterizationTests
{
    [Fact]
    public async Task Should_KeepTheCapturedCallDateTimeAndBody_When_SyncConfirmsIt()
    {
        using TestTablet tablet = new();
        var snapshot = Snapshots.Build("colm", 1, 1);
        await tablet.SyncAsync(snapshot);
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 1, 9, 40, 0, TimeSpan.Zero));
        var result = new CallStore(tablet.Workspace(), clock).Save(Guid.NewGuid(), snapshot.Locations[0].Id,
            new(CallChannel.InPerson, "Presented autumn range"));
        Assert.True(result.Succeeded, result.Error);
        UnsentStore store = new(tablet.Workspace());
        UnsentItem saved = Assert.Single(store.Read());
        Assert.Equal(new DateOnly(2026, 10, 1), result.Call!.CalledOn);
        Assert.Equal(clock.GetUtcNow(), result.Call.SavedAt);
        Assert.True(store.MarkSent(saved.Id, clock.GetUtcNow().AddHours(2)));
        Assert.Equal(saved, Assert.Single(store.ReadSent()).Item);
        Assert.Equal(0, store.Summarise().Total);
    }

    [Fact]
    public async Task Should_KeepTheOrderBusinessDateButUpdateSavedTime_When_AnOrderIsEditedLater()
    {
        using TestTablet tablet = new();
        var snapshot = Snapshots.Build("colm", 1, 1);
        await tablet.SyncAsync(snapshot);
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 1, 11, 15, 0, TimeSpan.Zero));
        OrderStore orders = new(tablet.Workspace(), clock);
        Guid id = Guid.NewGuid();
        var created = orders.AddLine(id, snapshot.Locations[0].Id, snapshot.Products[0].Id, 1);
        Assert.True(created.Succeeded, created.Error);
        UnsentStore store = new(tablet.Workspace());
        Assert.Equal(clock.GetUtcNow(), store.Find(id)!.SavedUtc);
        clock.Advance(TimeSpan.FromDays(1));
        var changed = orders.AddLine(id, snapshot.Locations[0].Id, snapshot.Products[0].Id, 1);
        Assert.True(changed.Succeeded, changed.Error);
        Assert.Equal(new DateOnly(2026, 10, 1), changed.Order!.TakenOn);
        Assert.Equal(clock.GetUtcNow(), store.Find(id)!.SavedUtc);
    }
}
