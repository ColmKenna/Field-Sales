using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-038 scenarios 12-14: finding products offline in Order entry, and S4, a standalone order.</summary>
public sealed class OrderCaptureTests : IDisposable
{
    private static readonly Guid Health = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Vitamins = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Suncare = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid BodyCare = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid VitaminD = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid VitaminC = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid Creme = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid SunLotion = Guid.Parse("30000000-0000-0000-0000-000000000004");
    private static readonly Guid BodyLotion = Guid.Parse("30000000-0000-0000-0000-000000000005");

    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private static TabletSnapshot Catalogue()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 0);
        SnapshotCategory[] categories = [new(Health, null, "Health"), new(Vitamins, Health, "Vitamins"),
            new(Suncare, Health, "Suncare"), new(BodyCare, Health, "Body Care")];
        SnapshotProduct Product(Guid id, string code, string name, Guid category) => new(id, code, name, category, "Each", null, null, null);
        SnapshotProduct[] products = [Product(VitaminD, "VITD-1000-90", "Vitamin D 1000IU 90s", Vitamins),
            Product(VitaminC, "VITC-500", "Vitamin C 500mg", Vitamins), Product(Creme, "CREME-200", "Crème Hydrante 200ml", BodyCare),
            Product(SunLotion, "SUN-LOT", "Lotion 200ml", Suncare), Product(BodyLotion, "BODY-LOT", "Lotion 200ml", BodyCare)];
        SnapshotBasePrice[] prices = [new(VitaminD, new DateOnly(2026, 10, 1), 12.50m), new(VitaminD, new DateOnly(2026, 11, 1), 13.00m),
            new(VitaminC, new DateOnly(2026, 10, 1), 7.20m), new(Creme, new DateOnly(2026, 10, 1), 5.60m),
            new(SunLotion, new DateOnly(2026, 10, 1), 9.99m), new(BodyLotion, new DateOnly(2026, 10, 1), 8.40m)];
        snapshot = snapshot with { Locations = [snapshot.Locations[0] with { Name = "Byrne's Chemist" }], Categories = categories, Products = products, BasePrices = prices };
        return snapshot with { Manifest = snapshot.Manifest with { Counts = new(1, categories.Length, products.Length, prices.Length) } };
    }

    private ProductSearchIndex Index() => new SnapshotStore(tablet.Workspace(), tablet.Clock)
        .ReadProductIndex(DateOnly.FromDateTime(tablet.Clock.GetLocalNow().DateTime));

    private static string[] Names(ProductSearchOutcome outcome) => outcome.Results.Select(product => product.Name).ToArray();

    [Fact]
    public async Task Should_FindProductsByNameOrCodeIgnoringCaseAndAccents_When_TheRepSearchesOffline()
    {
        await tablet.SyncAsync(Catalogue());
        tablet.Server.Respond = _ => throw new InvalidOperationException("No online product search");
        ProductSearchIndex index = Index();

        Assert.Equal(5, index.Count);
        Assert.Equal(["Vitamin D 1000IU 90s"], Names(index.Search("VITAMIN d 1000")));
        Assert.Equal(["Vitamin D 1000IU 90s"], Names(index.Search("  vitd-1000 ")));
        Assert.Equal(["Crème Hydrante 200ml"], Names(index.Search("creme")));
        Assert.Equal(["Vitamin C 500mg", "Vitamin D 1000IU 90s"], Names(index.Search("vit")));
        ProductSearchOutcome vitaminD = index.Search("vitamin d");
        Assert.Equal(1, vitaminD.Total);
        ProductSearchResult result = Assert.Single(vitaminD.Results);
        Assert.Equal((VitaminD, "VITD-1000-90", "Health > Vitamins", "Each"), (result.Id, result.Code, result.Breadcrumb, result.Unit));
        Assert.Equal(12.50m, result.Price); // The 1 Nov price is not yet in effect.
        Assert.Empty(index.Search("").Results);
        Assert.Empty(index.Search("   ").Results);
        Assert.Equal(0, index.Search("no such product").Total);
    }

    [Fact]
    public async Task Should_ShowThePriceInEffectOnTheDay_When_TheIndexIsBuiltAfterAFutureDatedPriceStarts()
    {
        await tablet.SyncAsync(Catalogue());
        tablet.Clock.Advance(TimeSpan.FromDays(32));

        Assert.Equal(13.00m, Assert.Single(Index().Search("vitamin d").Results).Price);
    }

    [Fact]
    public async Task Should_TellTwoSameNamedProductsApart_When_TheirBreadcrumbsDiffer()
    {
        await tablet.SyncAsync(Catalogue());

        ProductSearchOutcome lotions = Index().Search("lotion");

        Assert.Equal(["Health > Body Care", "Health > Suncare"], lotions.Results.Select(product => product.Breadcrumb));
        Assert.Equal([BodyLotion, SunLotion], lotions.Results.Select(product => product.Id));
    }

    [Fact]
    public async Task Should_ShowTheFirstFiftyAndTheTotal_When_MoreProductsMatch()
    {
        await tablet.SyncAsync(Snapshots.Build("colm", 1, 120));

        ProductSearchOutcome outcome = Index().Search("product");

        Assert.Equal(ProductSearchIndex.MaximumResults, outcome.Results.Count);
        Assert.Equal(120, outcome.Total);
        Assert.Equal("A product 0", outcome.Results[0].Name);
    }

    [Fact]
    public async Task Should_SaveAnInProgressOrderDatedTodayWithNoCallLinked_When_IOpenByrnesChemistChooseNewOrderAddVitaminD1000IU90s()
    {
        TabletSnapshot snapshot = Catalogue();
        await tablet.SyncAsync(snapshot);
        Guid shop = snapshot.Locations[0].Id;
        ProductSearchResult found = Assert.Single(Index().Search("Vitamin D 1000IU 90s").Results);

        OrderResult result = new OrderStore(tablet.Workspace(), tablet.Clock).AddLine(Guid.NewGuid(), shop, found.Id, 12);

        TabletOrder order = result.Order!;
        Assert.Equal(OrderState.InProgress, order.State);
        Assert.Equal(new DateOnly(2026, 10, 1), order.TakenOn);
        Assert.Null(order.CallId);
        OrderLine line = Assert.Single(order.Lines);
        Assert.Equal(("Vitamin D 1000IU 90s", 12m, 12.50m), (line.ProductName, line.Quantity, line.Price.Amount));
        UnsentItem saved = Assert.Single(new UnsentStore(tablet.Workspace()).Read());
        Assert.Equal(UnsentKind.Order, saved.Kind);
    }
}
