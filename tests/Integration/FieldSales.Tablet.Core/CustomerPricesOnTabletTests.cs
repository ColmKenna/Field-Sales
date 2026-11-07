using FieldSales.Ordering.Contracts;
using FieldSales.Pricing;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-064 agreed scenarios 3, 4, 6, 7 and 8 (Pricing US-005 S5, S6): the tablet stores the tiers and breaks snapshot 1.6
/// carries, and prices a shop with its customer's tiers through the engine, offline — on the pad and when a line is captured.</summary>
[Trait("Category", "Integration")]
public sealed class CustomerPricesOnTabletTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 1);
    private static readonly Guid Suncare = Guid.Parse("22000000-0000-0000-0000-000000000001");
    private static readonly Guid Spf30 = Guid.Parse("32000000-0000-0000-0000-000000000001");
    private static readonly Guid HandCream = Guid.Parse("32000000-0000-0000-0000-000000000002");
    private static readonly Guid TierB = Guid.Parse("42000000-0000-0000-0000-000000000001");

    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    // Two shops of two customers; the first customer holds Tier B (8% off, SPF30 fixed at €10.50); Hand Cream has a break from 10.
    private static TabletSnapshot Catalogue(bool tierHeld = true, IReadOnlyList<SnapshotPriceTierTerm>? terms = null)
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 2, 0, version: "1.6");
        SnapshotCategory[] categories = [new(Suncare, null, "Suncare")];
        SnapshotProduct[] products = [new(Spf30, "SUN-030", "SPF30 Sun Lotion 200ml", Suncare, "Each", null, null, null),
            new(HandCream, "HAND-75", "Hand Cream 75ml", Suncare, "Each", null, null, null)];
        SnapshotBasePrice[] prices = [new(Spf30, new DateOnly(2026, 9, 1), 12.50m), new(HandCream, new DateOnly(2026, 9, 1), 4.00m)];
        SnapshotPriceTier[] tiers = tierHeld ? [new(TierB, "Tier B", SnapshotTierShapes.Percentage, 8m)] : [];
        SnapshotPriceTierTerm[] tierTerms = [.. terms ?? (tierHeld ? [new(TierB, Spf30, null, 10.50m)] : [])];
        SnapshotCustomerTier[] held = tierHeld ? [new(snapshot.Locations[0].CustomerId, TierB)] : [];
        SnapshotQuantityBreak[] breaks = [new(HandCream, 10m, 3.50m)];
        snapshot = snapshot with
        {
            Categories = categories, Products = products, BasePrices = prices, PriceTiers = tiers, PriceTierTerms = tierTerms,
            CustomerTiers = held, QuantityBreaks = breaks
        };
        return snapshot with
        {
            Manifest = snapshot.Manifest with
            {
                Counts = new(snapshot.Locations.Count, categories.Length, products.Length, prices.Length, 0, 0, 0, 0, tiers.Length,
                    tierTerms.Length, held.Length, breaks.Length)
            }
        };
    }

    private SnapshotStore Store() => new(tablet.Workspace(), tablet.Clock);

    private static (string Kind, string Label, decimal Amount) Winner(ResolvedPrice? price) =>
        (price!.Winner.Kind, price.Winner.Label, price.Winner.Amount);

    [Fact]
    public async Task Should_StoreTiersAndBreaks_When_ASnapshotIsApplied()
    {
        TabletSnapshot snapshot = Catalogue();
        await tablet.SyncAsync(snapshot);
        LocationPrices hickeys = Store().ReadLocationPrices(snapshot.Locations[0].Id, Today);
        Assert.Equal((PriceKinds.Tier, "Tier B", 10.50m), Winner(hickeys.Resolve(Spf30, "Each", 12.50m, 1m)));
        Assert.Equal((PriceKinds.Break, "Break: 10+", 3.50m), Winner(hickeys.Resolve(HandCream, "Each", 4.00m, 12m)));

        // A term naming a tier the snapshot doesn't carry is refused, and the downloaded day stays as it was.
        TabletSnapshot broken = Catalogue(terms: [new(Guid.NewGuid(), Spf30, null, 9.00m)]);
        tablet.Clock.Advance(TimeSpan.FromMinutes(1));
        tablet.Server.Serve(EncodedSnapshot.From(broken));
        Assert.NotEqual(SyncStatus.Completed, (await tablet.Sync().SyncAsync()).Status);
        Assert.Equal(10.50m, Store().ReadLocationPrices(snapshot.Locations[0].Id, Today).Resolve(Spf30, "Each", 12.50m, 1m)!.Winner.Amount);

        // A 1.5 snapshot still applies, with no tiers or breaks.
        await tablet.SyncAsync(Snapshots.Build("colm", 1, 1, version: "1.5"));
        Assert.Equal(PriceKinds.List, Store().ReadLocationPrices(snapshot.Locations[0].Id, Today).Resolve(Spf30, "Each", 12.50m, 1m)!.Winner.Kind);
    }

    [Fact]
    public async Task Should_ShowTheCustomersPriceOnThePad_When_Offline()
    {
        TabletSnapshot snapshot = Catalogue();
        await tablet.SyncAsync(snapshot);
        tablet.Server.Respond = _ => throw new HttpRequestException("No signal.");

        SnapshotStore store = Store();
        ProductSearchIndex pad = store.ReadLocationPrices(snapshot.Locations[0].Id, Today).Apply(store.ReadProductIndex(Today));

        Assert.Equal((10.50m, 12.50m), (pad.Find(Spf30)!.Price, pad.Find(Spf30)!.ListPrice)); // Tier B's override, not the base.
        Assert.Equal(3.68m, pad.Find(HandCream)!.Price); // 8% off €4.00 at quantity 1; the break needs 10.
    }

    [Fact]
    public async Task Should_CaptureTheWinnerAndItsSource_When_ALineIsAdded()
    {
        TabletSnapshot snapshot = Catalogue();
        await tablet.SyncAsync(snapshot);
        OrderStore orders = new(tablet.Workspace(), tablet.Clock);
        Guid orderId = Guid.NewGuid();

        orders.AddLine(orderId, snapshot.Locations[0].Id, Spf30, 2m);
        TabletOrder order = orders.AddLine(orderId, snapshot.Locations[0].Id, HandCream, 12m).Order!;

        OrderLinePrice spf30 = order.Lines[0].Price, handCream = order.Lines[1].Price;
        Assert.Equal((10.50m, "Tier", "Tier B"), (spf30.Amount, spf30.Kind, spf30.Label));
        Assert.Equal([new("Tier", "Tier B", 10.50m, true), new(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 12.50m, false)],
            spf30.Candidates);
        Assert.Equal((3.50m, "Break", "Break: 10+"), (handCream.Amount, handCream.Kind, handCream.Label));
        Assert.Equal([new("Break", "Break: 10+", 3.50m, true), new("Tier", "Tier B", 3.68m, false),
            new(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 4.00m, false)], handCream.Candidates);
    }

    [Fact]
    public async Task Should_KeepCapturedPrices_When_TheCustomersTiersChangeAtTheNextSync()
    {
        TabletSnapshot snapshot = Catalogue();
        await tablet.SyncAsync(snapshot);
        Guid shop = snapshot.Locations[0].Id;
        OrderStore orders = new(tablet.Workspace(), tablet.Clock);
        Guid orderId = Guid.NewGuid();
        Guid line = orders.AddLine(orderId, shop, Spf30, 2m).Order!.Lines[0].Id;

        await tablet.SyncAsync(Catalogue(tierHeld: false)); // The customer no longer holds Tier B.
        orders.AddLine(orderId, shop, Spf30, 1m);
        orders.ChangeQuantity(orderId, line, 4m);
        TabletOrder order = orders.AddLine(orderId, shop, HandCream, 1m).Order!;

        Assert.Equal((4m, 10.50m, "Tier B"), (order.Lines[0].Quantity, order.Lines[0].Price.Amount, order.Lines[0].Price.Label));
        Assert.Equal((4.00m, OrderContract.BasePriceLabel), (order.Lines[1].Price.Amount, order.Lines[1].Price.Label)); // New lines: no tier.
    }

    [Fact]
    public async Task Should_PriceAtBase_When_TheShopsCustomerHoldsNoTier()
    {
        TabletSnapshot snapshot = Catalogue();
        await tablet.SyncAsync(snapshot);

        LocationPrices other = Store().ReadLocationPrices(snapshot.Locations[1].Id, Today);

        Assert.Equal((PriceKinds.List, "List price", 12.50m), Winner(other.Resolve(Spf30, "Each", 12.50m, 1m)));
        Assert.Equal((PriceKinds.Break, "Break: 10+", 3.50m), Winner(other.Resolve(HandCream, "Each", 4.00m, 12m)));
        TabletOrder order = new OrderStore(tablet.Workspace(), tablet.Clock).AddLine(Guid.NewGuid(), snapshot.Locations[1].Id, Spf30, 1m).Order!;
        Assert.Equal((12.50m, OrderContract.BasePriceKind), (order.Lines[0].Price.Amount, order.Lines[0].Price.Kind));
    }
}
