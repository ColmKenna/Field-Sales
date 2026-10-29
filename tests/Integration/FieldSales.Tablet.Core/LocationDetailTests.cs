using System.Text.Json;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

public sealed class LocationDetailTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private static TabletSnapshot Doyle()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 0);
        return snapshot with { Locations = [snapshot.Locations[0] with { Name = "Doyle's Shop" }] };
    }

    private UnsentStore SaveOrder(Guid locationId)
    {
        UnsentStore work = new(tablet.Workspace());
        work.Save(new(Guid.NewGuid(), UnsentKind.Order, tablet.Clock.GetUtcNow(),
            JsonSerializer.Serialize(new { locationId, state = "Ready to Send", lines = new[] { new { product = "A-00001", quantity = 6 } } })));
        return work;
    }

    [Fact]
    public async Task Should_ReadAddressAndContact_When_AssignedShopIsOpenedOffline()
    {
        TabletSnapshot snapshot = Doyle();
        await tablet.SyncAsync(snapshot);
        tablet.Server.Respond = _ => throw new InvalidOperationException("No online Location reads");
        var store = new SnapshotStore(tablet.Workspace());
        OfflineLocationDetail detail = Assert.IsType<OfflineLocationDetail>(store.ReadLocation(snapshot.Locations[0].Id));
        Assert.Equal(snapshot.Locations[0], detail.Location);
        Assert.True(detail.IsAssigned); // The component verifies the two rendered actions.
        Assert.Null(new SnapshotStore(tablet.Workspace("aoife")).ReadLocation(detail.Location.Id));
    }

    [Fact]
    public async Task Should_ShowMissingContactState_When_NoMainContactIsDownloaded()
    {
        TabletSnapshot snapshot = Doyle();
        snapshot = snapshot with { Locations = [snapshot.Locations[0] with { MainContact = null, Eircode = null }] };
        await tablet.SyncAsync(snapshot);
        OfflineLocationDetail detail = Assert.IsType<OfflineLocationDetail>(new SnapshotStore(tablet.Workspace()).ReadLocation(snapshot.Locations[0].Id));
        Assert.Null(detail.Location.MainContact);
        Assert.Null(detail.Location.Eircode);
        Assert.True(detail.IsAssigned);
    }

    [Fact]
    public async Task Should_RetainReferencedShopDetails_When_RepeatedSyncsCleanupAndRestartOccur()
    {
        TabletSnapshot snapshot = Doyle();
        SnapshotLocation shop = snapshot.Locations[0];
        await tablet.SyncAsync(snapshot);
        UnsentStore work = SaveOrder(shop.Id);
        string fingerprint = work.Fingerprint();
        string bytes = tablet.UnsentFileHash();

        // The fallback must be the most recently downloaded details, not the capture-time contact.
        SnapshotLocation changed = shop with { Eircode = "Y14 Z999", MainContact = shop.MainContact! with { Name = "Updated Contact" } };
        await tablet.SyncAsync(snapshot with { Locations = [changed] });
        for (int refresh = 0; refresh < 4; refresh++)
        {
            await tablet.SyncAsync(Snapshots.Build("colm", 0, 0));
            await tablet.Sync().CleanupAsync();
        }

        tablet.Server.Respond = _ => throw new InvalidOperationException("No online Location reads");
        var reopened = new SnapshotStore(tablet.Workspace());
        OfflineLocationDetail detail = Assert.IsType<OfflineLocationDetail>(reopened.ReadLocation(shop.Id));
        Assert.Equal(changed, detail.Location);
        Assert.False(detail.IsAssigned);
        Assert.True(detail.HasUnsentOrder);
        Assert.Empty(reopened.ReadLocationIndex().Search("Doyle"));
        Assert.Empty(reopened.ReadLocations());
        Assert.Null(new SnapshotStore(tablet.Workspace("aoife")).ReadLocation(shop.Id));
        Assert.Equal(bytes, tablet.UnsentFileHash());
        Assert.Equal(fingerprint, work.Fingerprint());

        await tablet.SyncAsync(snapshot); // Current assignment always wins over retained context.
        OfflineLocationDetail assigned = Assert.IsType<OfflineLocationDetail>(reopened.ReadLocation(shop.Id));
        Assert.True(assigned.IsAssigned);
        Assert.Equal(shop, assigned.Location);
    }

    [Fact]
    public async Task Should_PreserveSavedWork_When_AnExistingPayloadHasNoLocationId()
    {
        TabletSnapshot snapshot = Doyle();
        await tablet.SyncAsync(snapshot);
        UnsentStore work = tablet.SaveWork();
        UnsentItem[] before = work.Read().ToArray();
        string fingerprint = work.Fingerprint();
        await tablet.SyncAsync(Snapshots.Build("colm", 0, 0));
        Assert.Null(new SnapshotStore(tablet.Workspace()).ReadLocation(snapshot.Locations[0].Id));
        Assert.Equal(before, work.Read());
        Assert.Equal(fingerprint, work.Fingerprint());
    }
}
