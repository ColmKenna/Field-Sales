using System.Text;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Core;

namespace FieldSales.Api.Tests;

/// <summary>WI-054 (T-5.1.6, REP-US-012 S14–S18): each recent order product's quantities, sent in snapshot 1.5 for the Review marker.
/// Agreed scenarios S1 (server) and E1 (end to end).</summary>
public sealed partial class SuggestedListTests
{
    // S1.
    [Fact]
    public async Task Should_SendEachRecentAcceptedOrdersQuantity_When_TheSnapshotIsBuilt()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 3);
        await ReceiveOrderWithAsync(murphys, new DateOnly(2026, 8, 1), accepted: true, (product[0], 10m, "Each")); // No longer recent.
        await ReceiveOrderWithAsync(murphys, new DateOnly(2026, 9, 1), accepted: true, (product[0], 48m, "Each"), (product[1], 6m, "Each"));
        await ReceiveOrderWithAsync(murphys, new DateOnly(2026, 9, 8), accepted: true, (product[0], 50m, "Each"), (product[2], 2.5m, "kg"));
        await ReceiveOrderWithAsync(murphys, new DateOnly(2026, 9, 15), accepted: true, (product[0], 44m, "Each"),
            (product[1], 3m, "Each"), (product[1], 3m, "Each")); // On the order twice: summed.
        await ReceiveOrderWithAsync(murphys, new DateOnly(2026, 9, 20), accepted: false, (product[0], 480m, "Each")); // Pending.

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        Dictionary<Guid, IReadOnlyList<SnapshotOrderedQuantity>> quantities =
            snapshot.RecentOrderLines!.ToDictionary(line => line.ProductId, line => line.Quantities!);
        Assert.Equal([new(44m, "Each"), new(50m, "Each"), new(48m, "Each")], quantities[product[0]]); // Newest first.
        Assert.Equal([new(6m, "Each"), new(6m, "Each")], quantities[product[1]]);
        Assert.Equal([new(2.5m, "kg")], quantities[product[2]]);
        Assert.Equal("1.6", snapshot.Manifest.Version);
    }

    // E1, end to end: three orders taken on the tablet are synced and accepted; the next snapshot lets the tablet mark 480 on a new
    // order against the usual 48, with no server involved, and marking it ready asks for nothing.
    [Fact]
    public async Task Should_MarkTheLine_When_TheShopsLast3AcceptedOrdersAreSyncedAndAnOrderOf10TimesIsReviewed()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 2);
        Assert.True((await SyncAsync()).Completed);
        OrderStore orders = new(Workspace, _clock);
        Guid[] taken = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        foreach ((Guid order, decimal quantity) in taken.Zip([48m, 50m, 44m]))
        {
            Assert.True(orders.AddLine(order, murphys, product[0], quantity).Succeeded);
            Assert.True(orders.MarkReadyToSend(order).Succeeded);
        }
        Assert.Equal(new UploadSummary(3, 3), (await SyncAsync()).Upload);
        await AcceptAsync(taken);
        Assert.True((await SyncAsync()).Completed);

        Guid next = Guid.NewGuid();
        Assert.True(orders.AddLine(next, murphys, product[1], 5m).Succeeded); // Never ordered here before: no marker.
        TabletOrder review = orders.AddLine(next, murphys, product[0], 480m).Order!; // 48 typed as 480.
        SnapshotStore store = new(Workspace);
        ProductSearchIndex index = store.ReadProductIndex(new DateOnly(2026, 9, 22));
        IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>> history = store.ReadOrderedQuantities(index.Generation, murphys);

        Assert.Equal([("Product 01", (string?)null), ("Product 00", "Usually about 48 here")], review.Lines.Select(line =>
            (line.ProductName, QuantityMarker.For(line, history.GetValueOrDefault(line.ProductId) ?? [], index.Find(line.ProductId)?.QuantityStep))));
        Assert.Equal(OrderState.ReadyToSend, orders.MarkReadyToSend(next).Order!.State);
    }

    // Received through the same receiver the upload endpoint uses, with each line's quantity and unit.
    private async Task ReceiveOrderWithAsync(Guid location, DateOnly takenOn, bool accepted,
        params (Guid Product, decimal Quantity, string Unit)[] products)
    {
        Guid id = Guid.NewGuid();
        OrderLine[] lines = [.. products.Select(each => new OrderLine(Guid.NewGuid(), each.Product, "CODE", "Name", each.Quantity, each.Unit,
            new OrderLinePrice(2.50m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel,
                [new OrderPriceCandidate(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 2.50m, true)]), OrderMorning))];
        TabletOrder order = new(OrderContract.Schema, OrderContract.CurrentVersion, id, OrderState.ReadyToSend, location, location,
            "colm", takenOn, null, OrderMorning, lines);
        await ReceiveAsync(receiver => receiver.ReceiveOrderAsync(id, "colm",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder)), CancellationToken.None));
        if (accepted) await AcceptAsync(id);
    }
}
