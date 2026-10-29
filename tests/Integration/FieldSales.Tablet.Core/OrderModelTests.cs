using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-038 scenarios 1-9: the order and line model, captured values and the rules for adding a line.</summary>
public sealed class OrderModelTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private OrderStore Store(string rep = "colm") => new(tablet.Workspace(rep), tablet.Clock);

    // One shop, Byrne's Chemist, and three Each products priced 2.50 from 1 Oct and 3.50 from 1 Nov.
    private static TabletSnapshot Byrnes()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 3);
        return snapshot with { Locations = [snapshot.Locations[0] with { Name = "Byrne's Chemist" }] };
    }

    private static TabletSnapshot WithPrices(TabletSnapshot snapshot, IEnumerable<SnapshotBasePrice> prices)
    {
        SnapshotBasePrice[] kept = prices.ToArray();
        return snapshot with { BasePrices = kept, Manifest = snapshot.Manifest with { Counts = snapshot.Manifest.Counts with { BasePrices = kept.Length } } };
    }

    private static string Json(TabletOrder order) => JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder);

    [Fact]
    public async Task Should_RecordCapturingRepAndShopWithNoCallLinked_When_TheFirstLineStartsAnOrder()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = Guid.NewGuid();
        Assert.Null(Store().Find(orderId));
        Assert.Empty(new UnsentStore(tablet.Workspace()).Read());

        OrderResult result = Store().AddLine(orderId, snapshot.Locations[0].Id, snapshot.Products[0].Id, 12);

        Assert.True(result.Succeeded, result.Error);
        TabletOrder order = result.Order!;
        Assert.Equal(orderId, order.Id);
        Assert.Equal(OrderState.InProgress, order.State);
        Assert.Equal("colm", order.CapturingRep);
        Assert.Equal(snapshot.Locations[0].Id, order.LocationId);
        Assert.Equal(order.LocationId, order.OrderedById);
        Assert.Null(order.CallId);
        Assert.Equal(new DateOnly(2026, 10, 1), order.TakenOn);
        Assert.Equal((OrderContract.Schema, OrderContract.CurrentVersion), (order.Schema, order.Version));
        UnsentItem saved = Assert.Single(new UnsentStore(tablet.Workspace()).Read());
        Assert.Equal((orderId, UnsentKind.Order), (saved.Id, saved.Kind));
        using JsonDocument body = JsonDocument.Parse(saved.Json);
        Assert.Equal(snapshot.Locations[0].Id.ToString(), body.RootElement.GetProperty("locationId").GetString());
    }

    [Fact]
    public async Task Should_CaptureProductQuantityUnitPriceSourceAndSnapshot_When_ALineIsAdded()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        SnapshotProduct product = snapshot.Products[0];

        TabletOrder order = Store().AddLine(Guid.NewGuid(), snapshot.Locations[0].Id, product.Id, 12).Order!;

        OrderLine line = Assert.Single(order.Lines);
        Assert.Equal((product.Id, product.Code, product.Name), (line.ProductId, line.ProductCode, line.ProductName));
        Assert.Equal((12m, "Each"), (line.Quantity, line.Unit));
        Assert.Equal((2.50m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel), (line.Price.Amount, line.Price.Kind, line.Price.Label));
        OrderPriceCandidate candidate = Assert.Single(line.Price.Candidates);
        Assert.Equal((OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 2.50m, true),
            (candidate.Kind, candidate.Label, candidate.Amount, candidate.IsWinner));
        OrderSnapshotRef captured = new(snapshot.Manifest.Version, snapshot.Manifest.BuiltUtc);
        Assert.Equal(captured, line.Snapshot);
        Assert.Equal(captured, order.Snapshot);
    }

    [Fact]
    public async Task Should_KeepOneLineWithTheSummedQuantity_When_TheSameProductIsAddedTwice()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = Guid.NewGuid(), shop = snapshot.Locations[0].Id;

        Store().AddLine(orderId, shop, snapshot.Products[0].Id, 12);
        Store().AddLine(orderId, shop, snapshot.Products[1].Id, 2);
        TabletOrder order = Store().AddLine(orderId, shop, snapshot.Products[0].Id, 6).Order!;

        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(18m, order.Lines.Single(line => line.ProductId == snapshot.Products[0].Id).Quantity);
        Assert.Equal(2m, order.Lines.Single(line => line.ProductId == snapshot.Products[1].Id).Quantity);
        Assert.Single(new UnsentStore(tablet.Workspace()).Read());
        Assert.Equal(Json(order), Json(Store().Find(orderId)!));
    }

    [Theory]
    [InlineData("Each", null, null, 0, "Enter a quantity of 1 or more")]
    [InlineData("Each", null, null, 1.5, "Enter a quantity of 1 or more")]
    [InlineData("Each", null, null, -3, "Enter a quantity of 1 or more")]
    [InlineData("kg", 0.5, 1.0, 1.2, "Enter at least 1.0 kg in steps of 0.5 kg")]
    [InlineData("kg", 0.5, 1.0, 0.5, "Enter at least 1.0 kg in steps of 0.5 kg")]
    public async Task Should_RejectTheLineAndStoreNothing_When_TheQuantityBreaksTheProductRules(
        string unit, double? step, double? minimum, double quantity, string message)
    {
        TabletSnapshot snapshot = Byrnes();
        snapshot = snapshot with { Products = [snapshot.Products[0] with { Unit = unit, QuantityStep = (decimal?)step, MinimumQuantity = (decimal?)minimum },
            .. snapshot.Products.Skip(1)] };
        await tablet.SyncAsync(snapshot);

        OrderResult result = Store().AddLine(Guid.NewGuid(), snapshot.Locations[0].Id, snapshot.Products[0].Id, (decimal)quantity);

        Assert.False(result.Succeeded);
        Assert.Null(result.Order);
        Assert.Equal(message, result.Error);
        Assert.Empty(new UnsentStore(tablet.Workspace()).Read());
    }

    [Theory]
    [InlineData(0, "Enter a quantity of 1 or more")]
    [InlineData(1.5, "Enter a quantity of 1 or more")]
    [InlineData(-2, "Enter a quantity of 1 or more")]
    public async Task Should_KeepTheLineUnchanged_When_AnAddedQuantityBreaksTheProductStep(double quantity, string message)
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = Guid.NewGuid(), shop = snapshot.Locations[0].Id, product = snapshot.Products[0].Id;
        string before = Json(Store().AddLine(orderId, shop, product, 12).Order!);

        OrderResult result = Store().AddLine(orderId, shop, product, (decimal)quantity);

        Assert.Equal(message, result.Error);
        Assert.Equal(before, Json(Store().Find(orderId)!));
    }

    [Fact]
    public async Task Should_AcceptMeasuredQuantitiesAndCaptureTheUnit_When_TheyFollowTheProductSteps()
    {
        TabletSnapshot snapshot = Byrnes();
        snapshot = snapshot with { Products = [snapshot.Products[0] with { Unit = "kg", QuantityStep = 0.5m, MinimumQuantity = 1.0m },
            .. snapshot.Products.Skip(1)] };
        await tablet.SyncAsync(snapshot);
        Guid orderId = Guid.NewGuid(), shop = snapshot.Locations[0].Id;

        Store().AddLine(orderId, shop, snapshot.Products[0].Id, 1.5m);
        TabletOrder order = Store().AddLine(orderId, shop, snapshot.Products[0].Id, 0.5m).Order!;

        OrderLine line = Assert.Single(order.Lines);
        Assert.Equal((2.0m, "kg"), (line.Quantity, line.Unit));
    }

    [Fact]
    public async Task Should_KeepCapturedValues_When_ASyncReplacesTheSnapshotAndRenamesTheProduct()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = Guid.NewGuid(), shop = snapshot.Locations[0].Id;
        string before = Json(Store().AddLine(orderId, shop, snapshot.Products[0].Id, 12).Order!);

        TabletSnapshot next = snapshot with
        {
            Products = [snapshot.Products[0] with { Name = "Renamed product" }, .. snapshot.Products.Skip(1)],
            BasePrices = snapshot.BasePrices.Select(price => price with { Amount = price.Amount + 10m }).ToArray(),
            Manifest = snapshot.Manifest with { BuiltUtc = snapshot.Manifest.BuiltUtc.AddDays(1) }
        };
        await tablet.SyncAsync(next);

        Assert.Equal(before, Json(Store().Find(orderId)!));
        TabletOrder order = Store().AddLine(orderId, shop, snapshot.Products[1].Id, 3).Order!;
        OrderLine first = order.Lines.Single(line => line.ProductId == snapshot.Products[0].Id);
        OrderLine added = order.Lines.Single(line => line.ProductId == snapshot.Products[1].Id);
        Assert.Equal(("A product 0", 2.50m), (first.ProductName, first.Price.Amount));
        Assert.Equal(new OrderSnapshotRef(snapshot.Manifest.Version, snapshot.Manifest.BuiltUtc), first.Snapshot);
        // A line added after the later Sync follows the newer snapshot.
        Assert.Equal(12.50m, added.Price.Amount);
        Assert.Equal(new OrderSnapshotRef(next.Manifest.Version, next.Manifest.BuiltUtc), added.Snapshot);
    }

    [Fact]
    public async Task Should_ReadBackTheExactBody_When_TheOrderIsReopenedAfterRestart()
    {
        TabletSnapshot snapshot = Byrnes();
        snapshot = snapshot with { Products = [snapshot.Products[0], snapshot.Products[1] with { Unit = "kg", QuantityStep = 0.25m, MinimumQuantity = 0.25m },
            snapshot.Products[2]] };
        snapshot = WithPrices(snapshot, [new(snapshot.Products[0].Id, new DateOnly(2026, 10, 1), 12.50m),
            new(snapshot.Products[1].Id, new DateOnly(2026, 9, 1), 4.80m), new(snapshot.Products[2].Id, new DateOnly(2026, 10, 1), 0.01m)]);
        await tablet.SyncAsync(snapshot);
        Guid orderId = Guid.NewGuid(), shop = snapshot.Locations[0].Id;
        Store().AddLine(orderId, shop, snapshot.Products[0].Id, 12);
        Store().AddLine(orderId, shop, snapshot.Products[1].Id, 0.25m);
        TabletOrder written = Store().AddLine(orderId, shop, snapshot.Products[2].Id, 999_999_999_999m).Order!;

        UnsentItem saved = Assert.Single(new UnsentStore(tablet.Workspace()).Read());
        TabletOrder reopened = JsonSerializer.Deserialize(saved.Json, OrderJsonContext.Default.TabletOrder)!;
        Assert.Equal(saved.Json, Json(reopened));
        Assert.Equal(Json(written), Json(new OrderStore(tablet.Workspace(), tablet.Clock).Find(orderId)!));
        Assert.Equal([12m, 0.25m, 999_999_999_999m], reopened.Lines.Select(line => line.Quantity));
        Assert.Equal([12.50m, 4.80m, 0.01m], reopened.Lines.Select(line => line.Price.Amount));

        // The stored shape is the contract later items rely on: lock its property names.
        using JsonDocument body = JsonDocument.Parse(saved.Json);
        JsonElement root = body.RootElement;
        Assert.Equal(["callId", "capturingRep", "id", "lines", "locationId", "orderedById", "schema", "snapshot", "state", "takenOn", "version"],
            Names(root));
        Assert.Equal("InProgress", root.GetProperty("state").GetString());
        Assert.Equal("2026-10-01", root.GetProperty("takenOn").GetString());
        JsonElement line = root.GetProperty("lines")[0];
        Assert.Equal(["id", "price", "productCode", "productId", "productName", "quantity", "snapshot", "unit"], Names(line));
        Assert.Equal(["amount", "candidates", "kind", "label"], Names(line.GetProperty("price")));
        Assert.Equal(["amount", "isWinner", "kind", "label"], Names(line.GetProperty("price").GetProperty("candidates")[0]));
        Assert.Equal(["builtUtc", "version"], Names(root.GetProperty("snapshot")));

        static string[] Names(JsonElement element) => element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public async Task Should_CaptureTheNewestPriceOnOrBeforeToday_When_AFutureDatedPriceExists()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid shop = snapshot.Locations[0].Id, product = snapshot.Products[0].Id;

        Assert.Equal(2.50m, Store().AddLine(Guid.NewGuid(), shop, product, 1).Order!.Lines[0].Price.Amount);

        tablet.Clock.Advance(TimeSpan.FromDays(32)); // 2 Nov: the 1 Nov price is now the newest one in effect.
        TabletOrder later = Store().AddLine(Guid.NewGuid(), shop, product, 1).Order!;
        Assert.Equal(3.50m, later.Lines[0].Price.Amount);
        Assert.Equal(new DateOnly(2026, 11, 2), later.TakenOn);
    }

    [Fact]
    public async Task Should_RefuseTheLineAndStoreNothing_When_TheProductHasNoPriceInEffectOrIsNotDownloaded()
    {
        TabletSnapshot snapshot = Byrnes();
        // Product 1 never had a price; product 2's only price starts in the future.
        snapshot = WithPrices(snapshot, snapshot.BasePrices.Where(price => price.ProductId == snapshot.Products[0].Id)
            .Concat([new SnapshotBasePrice(snapshot.Products[2].Id, new DateOnly(2026, 11, 1), 9.99m)]));
        await tablet.SyncAsync(snapshot);
        Guid shop = snapshot.Locations[0].Id;

        foreach (SnapshotProduct unpriced in new[] { snapshot.Products[1], snapshot.Products[2] })
        {
            OrderResult result = Store().AddLine(Guid.NewGuid(), shop, unpriced.Id, 1);
            Assert.False(result.Succeeded);
            Assert.Equal("No price yet — this product can't be ordered.", result.Error);
        }
        OrderResult missing = Store().AddLine(Guid.NewGuid(), shop, Guid.NewGuid(), 1);
        Assert.Equal("This product isn't in your downloaded catalogue.", missing.Error);
        Assert.Empty(new UnsentStore(tablet.Workspace()).Read());
    }

    [Fact]
    public async Task Should_RefuseTheLineAndLeaveTheOrderUnchanged_When_TheShopIsNoLongerAssigned()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid shop = snapshot.Locations[0].Id;
        Guid existing = Guid.NewGuid();
        string before = Json(Store().AddLine(existing, shop, snapshot.Products[0].Id, 12).Order!);
        Assert.Equal("This shop isn't assigned to you.", Store().AddLine(Guid.NewGuid(), Guid.NewGuid(), snapshot.Products[0].Id, 1).Error);

        await tablet.SyncAsync(Snapshots.Build("colm", 0, 3)); // The shop is reassigned away.

        OrderResult newOrder = Store().AddLine(Guid.NewGuid(), shop, snapshot.Products[0].Id, 1);
        OrderResult sameOrder = Store().AddLine(existing, shop, snapshot.Products[1].Id, 1);
        Assert.Equal("This shop isn't assigned to you.", newOrder.Error);
        Assert.Equal("This shop isn't assigned to you.", sameOrder.Error);
        Assert.Equal(before, Json(Store().Find(existing)!));
        Assert.Single(new UnsentStore(tablet.Workspace()).Read());
    }
}
