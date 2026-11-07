using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-064 characterisation scenarios C1 and C3: how the tablet prices a shop whose customer holds no tier, pinned before
/// tiers and breaks reach it. They pass before and after the change: with no tier and no break the engine's winner is the base
/// price, and it is captured exactly as before.</summary>
[Trait("Category", "Integration")]
public sealed class CustomerPriceCharacterisationTests : IDisposable
{
    private static readonly Guid Vitamins = Guid.Parse("21000000-0000-0000-0000-000000000001");
    private static readonly Guid VitaminD = Guid.Parse("31000000-0000-0000-0000-000000000001");
    private static readonly Guid VitaminC = Guid.Parse("31000000-0000-0000-0000-000000000002");

    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private static TabletSnapshot Catalogue()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 0);
        SnapshotCategory[] categories = [new(Vitamins, null, "Vitamins")];
        SnapshotProduct[] products = [new(VitaminD, "VITD-1000-90", "Vitamin D 1000IU 90s", Vitamins, "Each", null, null, null),
            new(VitaminC, "VITC-500", "Vitamin C 500mg", Vitamins, "Each", null, null, null)];
        SnapshotBasePrice[] prices = [new(VitaminD, new DateOnly(2026, 9, 1), 12.50m)];
        snapshot = snapshot with { Categories = categories, Products = products, BasePrices = prices };
        return snapshot with { Manifest = snapshot.Manifest with { Counts = new(1, categories.Length, products.Length, prices.Length) } };
    }

    [Fact]
    public async Task Should_ShowTheBasePriceOnThePad_When_TheCustomerHoldsNoTier()
    {
        await tablet.SyncAsync(Catalogue());

        ProductSearchIndex index = new SnapshotStore(tablet.Workspace(), tablet.Clock).ReadProductIndex(new DateOnly(2026, 10, 1));

        Assert.Equal(12.50m, Assert.Single(index.Search("vitamin d").Results).Price);
        Assert.Null(Assert.Single(index.Search("vitamin c").Results).Price); // No price today: can't be ordered.
    }

    [Fact]
    public async Task Should_CaptureTheBasePriceAndItsSource_When_ALineIsAdded()
    {
        TabletSnapshot snapshot = Catalogue();
        await tablet.SyncAsync(snapshot);
        OrderStore orders = new(tablet.Workspace(), tablet.Clock);

        OrderResult added = orders.AddLine(Guid.NewGuid(), snapshot.Locations[0].Id, VitaminD, 12m);
        OrderResult refused = orders.AddLine(Guid.NewGuid(), snapshot.Locations[0].Id, VitaminC, 1m);

        OrderLine line = Assert.Single(added.Order!.Lines);
        Assert.Equal((12.50m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel), (line.Price.Amount, line.Price.Kind, line.Price.Label));
        Assert.Equal([new OrderPriceCandidate(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 12.50m, true)], line.Price.Candidates);
        Assert.Equal(OrderStore.NoPrice, refused.Error);
    }
}
