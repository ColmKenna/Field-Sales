namespace FieldSales.Tablet.Core.Tests;

public sealed class LocationReadCharacterizationTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    [Fact]
    public async Task Should_ReadOnlyTheActiveRepsLocations_When_SnapshotIsReplacedOffline()
    {
        var first = Snapshots.Build("colm", 312, 0);
        await tablet.SyncAsync(first);
        var store = new SnapshotStore(tablet.Workspace());
        Assert.Equal(312, store.ReadLocations().Count);
        Assert.Equal(first.Locations.Select(x => x.Id).Order(), store.ReadLocations().Select(x => x.Id).Order());
        Assert.Empty(new SnapshotStore(tablet.Workspace("aoife")).ReadLocations());
        var second = Snapshots.Build("colm", 2, 0, label: "B");
        await tablet.SyncAsync(second);
        tablet.Server.Respond = _ => throw new InvalidOperationException("No online location reads");
        Assert.Equal(second.Locations.Select(x => x.Id).Order(), store.ReadLocations().Select(x => x.Id).Order());
        Assert.All(store.ReadLocations(), row => Assert.Equal("Arklow", row.TownName));
    }
}
