using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-053 agreed scenarios 8 and 9: which of a shop's last Call gaps this tablet still carries to an order with no Call.
/// Any other order at the shop dated on or after the Call's date, in any state, drops a gap (D2: "any order since the date"); a
/// Call saved here after the snapshot was built is the newer one, and its own Low items carry (D3).</summary>
[Trait("Category", "Integration")]
public sealed class CarriedGapsTests : IDisposable
{
    private static readonly Guid SnapshotCall = new("5e1f0000-0000-0000-0000-000000000015");
    private static readonly DateOnly Sep15 = new(2026, 9, 15);
    private readonly TestTablet tablet = new();
    private TabletSnapshot snapshot = null!;

    public void Dispose() => tablet.Dispose();

    private Guid Shop => snapshot.Locations[0].Id;
    private Guid Product(int index) => snapshot.Products[index].Id;

    // Shop 0's last Call, on 15 Sep, left products 1, 2 and 3 unordered (the snapshot's 1.4 gaps).
    private async Task SyncWithGapsAsync()
    {
        TabletSnapshot built = Snapshots.Build("colm", locations: 2, products: 5, version: "1.4");
        SnapshotLastCallLowLine[] gaps = [.. new[] { 1, 2, 3 }.Select(index =>
            new SnapshotLastCallLowLine(built.Locations[0].Id, SnapshotCall, Sep15, built.Products[index].Id))];
        snapshot = built with
        {
            LastCallLowLines = gaps,
            Manifest = built.Manifest with { Counts = built.Manifest.Counts with { LastCallLowLines = gaps.Length } }
        };
        await tablet.SyncAsync(snapshot);
    }

    private CarriedLow? Carried(Guid orderOnScreen)
    {
        ProductSearchIndex index = new SnapshotStore(tablet.Workspace(), tablet.Clock).ReadProductIndex(new DateOnly(2026, 10, 1));
        return CarriedGaps.Read(tablet.Workspace(), Shop, orderOnScreen, index);
    }

    private TabletOrder Order(Guid shop, params int[] products)
    {
        OrderStore orders = new(tablet.Workspace(), tablet.Clock);
        Guid id = Guid.NewGuid();
        OrderResult result = new(null, null);
        foreach (int product in products)
        {
            result = orders.AddLine(id, shop, Product(product), 1m);
            Assert.True(result.Succeeded, result.Error);
        }
        return result.Order!;
    }

    [Fact]
    public async Task Should_DropAGap_When_ItIsOnAnotherOrderOnThisTabletSinceTheCall()
    {
        await SyncWithGapsAsync();
        Guid onScreen = Order(Shop, 3).Id; // The order on screen keeps its gap: there it reads ADDED.
        Order(Shop, 1); // In Progress, today.
        TabletOrder before = Order(Shop, 2);
        // Dated the day before the Call, so it doesn't count as ordered since.
        new UnsentStore(tablet.Workspace()).Save(new(before.Id, UnsentKind.Order, tablet.Clock.GetUtcNow(),
            JsonSerializer.Serialize(before with { TakenOn = Sep15.AddDays(-1) }, OrderJsonContext.Default.TabletOrder)));
        Order(snapshot.Locations[1].Id, 2); // Another shop's order never counts.

        CarriedLow carried = Carried(onScreen)!;

        Assert.Equal((SnapshotCall, Sep15), (carried.CallId, carried.CalledOn));
        Assert.Equal(new[] { Product(2), Product(3) }.Order(), carried.Entries.Select(entry => entry.ProductId).Order());
        Assert.All(carried.Entries, entry => Assert.True(entry.Low));
        Assert.Equal("A product 2", carried.Entries.Single(entry => entry.ProductId == Product(2)).ProductName);

        // Any state counts: a sent order on the Call's date drops product 2, and then the order on screen's product 3 is all
        // that is open — on any other order it would be gone too.
        TabletOrder sent = Order(Shop, 2);
        Assert.True(new UnsentStore(tablet.Workspace()).MarkSent(sent.Id, tablet.Clock.GetUtcNow()));
        Assert.Equal([Product(3)], Carried(onScreen)!.Entries.Select(entry => entry.ProductId));
        Assert.Null(Carried(Guid.NewGuid()));
    }

    [Fact]
    public async Task Should_CarryTheNewerCallsGaps_When_TheTabletHoldsAnUnsyncedCallAtTheShop()
    {
        await SyncWithGapsAsync();
        ProductSearchIndex index = new SnapshotStore(tablet.Workspace(), tablet.Clock).ReadProductIndex(new DateOnly(2026, 10, 1));
        CallStockEntry Entry(int product, decimal count, bool low)
        {
            ProductSearchResult item = index.Find(Product(product))!;
            return new(item.Id, item.Code, item.Name, item.Unit, count, low);
        }
        Guid newer = Guid.NewGuid();
        // Saved after the snapshot was built, so it can't be in it: today's Call marked product 4 Low and counted product 1.
        Assert.True(new CallStore(tablet.Workspace(), tablet.Clock).Save(newer, Shop,
            new(CallChannel.InPerson, null, [Entry(4, 1m, true), Entry(1, 9m, false)])).Succeeded);

        CarriedLow carried = Carried(Guid.NewGuid())!;

        Assert.Equal((newer, new DateOnly(2026, 10, 1)), (carried.CallId, carried.CalledOn));
        Assert.Equal([(Product(4), (decimal?)1m)], carried.Entries.Select(entry => (entry.ProductId, entry.Count)));

        // Its gaps drop on an order since, as the snapshot's do; once all are ordered nothing carries, not the older Call's.
        Order(Shop, 4);
        Assert.Null(Carried(Guid.NewGuid()));
    }
}
