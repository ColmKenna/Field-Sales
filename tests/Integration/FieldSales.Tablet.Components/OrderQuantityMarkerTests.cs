using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-054 (T-5.1.6, REP-US-012 S14, S17) on T-07: the out-of-pattern marker on the order's lines, where the rep reviews the
/// order and marks it ready. Agreed scenarios U1–U3. The rule itself is <c>QuantityMarkerTests</c> (R1–R12).</summary>
public sealed partial class TabletSessionScreenTests
{
    // Murphy's last 3 accepted orders had Cold & Flu at 24, 20 and 30 and Throat Lozenges at 6 twice.
    private static IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>> MurphysUsual => new Dictionary<Guid, IReadOnlyList<OrderedQuantity>>
    {
        [ColdFlu.Id] = [new(24m, "Each"), new(20m, "Each"), new(30m, "Each")],
        [Lozenges.Id] = [new(6m, "Each"), new(6m, "Each")]
    };

    /// <summary>Order entry for <paramref name="order"/>, standing as <paramref name="standing"/> says, with Murphy's usual quantities.</summary>
    private static async Task<Screen> ReviewAsync(TabletOrder order, OrderStanding standing)
    {
        Screen screen = new(typeof(NewOrder));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.ReadIndex = _ => new ProductSearchIndex("g1", OrderCatalogue);
        screen.Tablet.FindQuantities = (_, shop) => shop == MurphysId ? MurphysUsual : new Dictionary<Guid, IReadOnlyList<OrderedQuantity>>();
        screen.Tablet.FindView = (_, id) => id == order.Id ? new(order, standing, standing == OrderStanding.Sent ? SnapshotBuilt : null) : null;
        await screen.Open(new() { [nameof(NewOrder.LocationId)] = MurphysId, [nameof(NewOrder.OrderId)] = (Guid?)order.Id });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("On this order"));
        return screen;
    }

    private static string[] Markers(Screen screen) => [.. screen.Renderer.AttributeValues(screen.Id, "data-quantity-marker")];

    // U1 (S14): 240 typed for a usual 24.
    [Fact]
    public async Task Should_ShowUsuallyAboutNHereOnTheLine_When_OrderEntryShowsAnInProgressOrderWithAnUnusuallyHighLine()
    {
        TabletOrder order = ShopOrders.Holding(null, (ColdFlu, 240m), (Lozenges, 6m), (Plasters, 500m));
        await using Screen screen = await ReviewAsync(order, OrderStanding.InProgress);

        // Lozenges are as usual; Plasters have no history at Murphy's, so nothing to compare against (S15).
        Assert.Equal(["Usually about 24 here"], Markers(screen));
        Assert.Contains("Usually about 24 here", screen.Renderer.Text(screen.Id)); // Text on the line, not colour alone.
    }

    // U2 (S17): one tap, no question, and the marker stays while the order is Ready to Send.
    [Fact]
    public async Task Should_MarkTheOrderReadyToSendWithoutAsking_When_ALineIsMarked()
    {
        TabletOrder order = ShopOrders.Holding(null, (ColdFlu, 240m));
        List<Guid> marked = [];
        await using Screen screen = await ReviewAsync(order, OrderStanding.InProgress);
        screen.Tablet.MarkWork = (_, id) =>
        {
            marked.Add(id);
            return new(order with { State = OrderState.ReadyToSend }, null);
        };
        Assert.Equal(["Usually about 24 here"], Markers(screen));

        await screen.Click("Mark Ready to Send");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Ready to Send. It goes to head office at your next Sync."));

        Assert.Equal([order.Id], marked);
        Assert.DoesNotContain("order-confirm-question", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.Equal(["Close", "Reopen", "Delete order"], screen.Renderer.Buttons(screen.Id));
        Assert.Equal(["Usually about 24 here"], Markers(screen));
    }

    // U3 (D5): once sent, the order can't change, so the marker has nothing to catch.
    [Fact]
    public async Task Should_ShowNoMarker_When_TheOrderIsSent()
    {
        TabletOrder order = ShopOrders.Holding(null, (ColdFlu, 240m)) with { State = OrderState.ReadyToSend };
        await using Screen screen = await ReviewAsync(order, OrderStanding.Sent);

        Assert.Contains("Sent", screen.Renderer.Text(screen.Id));
        Assert.Empty(Markers(screen));
    }
}
