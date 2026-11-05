using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-053 (T-5.1.5, REP-US-012 S19–S22) on T-07: an order with no Call carries the gaps the shop's last Call left, and
/// Record call from the Low tab links the order to a new Call. Agreed scenarios 1–7; 8 and 9 are storage (Tablet.Core
/// <c>CarriedGapsTests</c>), 10 and 12 the server. Where the carried gaps matter, the screen is given what
/// <see cref="CarriedGaps.Of"/> works out from the snapshot's gaps and this tablet's orders.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static ProductSearchResult NappyWipes => new(StockId(2, 25), "P-25", "Nappy Wipes 64s", Remedies.Name, "Each", 2.20m, Remedies.Id);
    private static readonly Guid LastCallId = new("ca11ca11-0000-0000-0000-000000000015");
    private static readonly DateTimeOffset SnapshotBuilt = new(2026, 9, 22, 6, 30, 0, TimeSpan.Zero);

    private static ProductSearchIndex CarriedIndex => new("g1", [.. OrderCatalogue, NappyWipes]);

    /// <summary>The shop at <see cref="MurphysId"/>, named as the scenario names it.</summary>
    private static OfflineLocationDetail Shop(string name) => new(new SnapshotLocation(MurphysId, name, Guid.NewGuid(),
        "Retail", new(Guid.NewGuid(), "Aughrim", "Wicklow"), null, null), true, false);

    /// <summary>The gaps <see cref="CarriedGaps.Of"/> carries for the order on screen: the snapshot's gaps from the shop's last Call
    /// on <paramref name="calledOn"/>, less what this tablet's <paramref name="otherOrders"/> have ordered since.</summary>
    private static Func<string, Guid, Guid, CarriedLow?> Carrying(DateOnly calledOn, ProductSearchResult[] gaps,
        params TabletOrder[] otherOrders) => (_, shop, onScreen) => CarriedGaps.Of(shop, onScreen,
            new LastCallGaps(LastCallId, calledOn, [.. gaps.Select(product => product.Id)]), SnapshotBuilt, [], otherOrders, CarriedIndex);

    /// <summary>Another order at the shop on this tablet, taken on <paramref name="takenOn"/>, already sent.</summary>
    private static TabletOrder OrderedOn(DateOnly takenOn, params ProductSearchResult[] products) =>
        ShopOrders.Holding(null, [.. products.Select(product => (product, 6m))]) with { TakenOn = takenOn };

    /// <summary>Order entry at the shop for <paramref name="order"/> (a new order when null), with no Call.</summary>
    private static async Task<(Screen Screen, ShopOrders Orders)> OrderWithNoCallAsync(TabletOrder? order,
        Func<string, Guid, Guid, CarriedLow?> carried, string shop = "Murphy's Pharmacy")
    {
        Screen screen = new(typeof(NewOrder));
        screen.Tablet.Read = (_, _) => Shop(shop);
        screen.Tablet.ReadIndex = _ => CarriedIndex;
        screen.Tablet.FindCarried = carried;
        ShopOrders orders = new(order);
        orders.Wire(screen.Tablet);
        Dictionary<string, object?> route = new() { [nameof(NewOrder.LocationId)] = MurphysId };
        if (order is not null) route[nameof(NewOrder.OrderId)] = (Guid?)order.Id;
        await screen.Open(route);
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("On this order"));
        return (screen, orders);
    }

    private static string? RecordCallLink(Screen screen) => screen.Renderer.AttributeValues(screen.Id, "href")
        .SingleOrDefault(href => href.EndsWith("/calls/new", StringComparison.Ordinal) && href.Contains("/orders/", StringComparison.Ordinal));

    // Scenario 1 (S19). Aftersun's CAN'T ADD row waits for E8 (D6): a product the rep can't order isn't in the snapshot's gaps.
    [Fact]
    public async Task Should_ReadStillOpenFromYourCallAtCareysTue15SepAndListNappyWipesOnly_When_SudocremWasOrderedThereOn18Sep()
    {
        (Screen screen, _) = await OrderWithNoCallAsync(null,
            Carrying(new(2026, 9, 15), [NappyWipes, Sudocrem125], OrderedOn(new(2026, 9, 18), Sudocrem125)), shop: "Carey's");
        await using Screen disposing = screen;

        Assert.Equal("Low (1)", LowTabLabel(screen));
        await OpenLowTabAsync(screen);

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("No stock check with this order.", text);
        Assert.Contains("Still open from your call at Carey's - Tue 15 Sep", text);
        Assert.Equal(["Nappy Wipes 64s: not added"], LowRows(screen));
        Assert.Contains("marked Low 15 Sep", text);
        Assert.StartsWith($"/locations/{MurphysId}/orders/", RecordCallLink(screen));
    }

    // Scenario 2 (S20): no age limit (D4).
    [Fact]
    public async Task Should_CarryTheGapsLabelledWithThatCallsDate_When_TheMostRecentCallWas10WeeksAgo()
    {
        (Screen screen, _) = await OrderWithNoCallAsync(null, Carrying(new(2026, 7, 14), [Lozenges, Plasters]));
        await using Screen disposing = screen;

        await OpenLowTabAsync(screen);

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Still open from your call at Murphy's Pharmacy - Tue 14 Jul", text);
        Assert.Equal(["Throat Lozenges 36s: not added", "Plasters 20s: not added"], LowRows(screen));
        Assert.Contains("marked Low 14 Jul", text);
        Assert.Equal("Low (2)", LowTabLabel(screen));
    }

    // Scenario 3 (S21).
    [Fact]
    public async Task Should_ShowLow0NoStockCheckWithThisOrderAndRecordCall_When_TheShopHasNoEarlierCall()
    {
        (Screen screen, _) = await OrderWithNoCallAsync(null, (_, _, _) => null);
        await using Screen disposing = screen;

        await AssertNothingCarriedAsync(screen);
    }

    // Scenario 4 (S21): every gap is on another order at the shop since the Call.
    [Fact]
    public async Task Should_ShowLow0NoStockCheckWithThisOrderAndRecordCall_When_EveryGapWasOrderedSince()
    {
        (Screen screen, _) = await OrderWithNoCallAsync(null,
            Carrying(new(2026, 9, 15), [Lozenges, Plasters], OrderedOn(new(2026, 9, 15), Lozenges), OrderedOn(new(2026, 9, 20), Plasters)));
        await using Screen disposing = screen;

        await AssertNothingCarriedAsync(screen);
    }

    private static async Task AssertNothingCarriedAsync(Screen screen)
    {
        Assert.Equal("Low (0)", LowTabLabel(screen));
        await OpenLowTabAsync(screen);
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("No stock check with this order.", text);
        Assert.DoesNotContain("Still open from your call", text);
        Assert.Empty(LowRows(screen));
        Assert.Contains("Record call", screen.Renderer.Text(screen.Id));
        Assert.NotNull(RecordCallLink(screen));
    }

    // Scenario 7 (T7.13): a carried item goes on the order through the same popover, as a plain add (the order has no Call).
    [Fact]
    public async Task Should_ShowADDED_When_ACarriedItemIsAddedThroughThePopover()
    {
        TabletOrder order = ShopOrders.Holding(null, (ColdFlu, 2m));
        (Screen screen, ShopOrders orders) = await OrderWithNoCallAsync(order, Carrying(new(2026, 9, 15), [Lozenges, Plasters]));
        await using Screen disposing = screen;
        await OpenLowTabAsync(screen);

        await screen.ClickLabelled($"Add {Lozenges.Name} to order");
        Assert.True(PopoverOpen(screen));
        Assert.Contains("marked Low 15 Sep", screen.Renderer.Text(screen.Id));
        await screen.Type("popover-quantity", "12");
        await screen.ClickLabelled("Add to order");
        await screen.Until(() => !PopoverOpen(screen));

        Assert.Equal((order.Id, Lozenges.Id, 12m, (Guid?)null), orders.Adds.Single());
        Assert.Equal(["Throat Lozenges 36s: ADDED - 12", "Plasters 20s: not added"], LowRows(screen));
        Assert.Equal("Low (1)", LowTabLabel(screen));
        Assert.Contains("Still open from your call at Murphy's Pharmacy - Tue 15 Sep", screen.Renderer.Text(screen.Id));
    }

    /// <summary>Record call opened from <paramref name="order"/>'s Low tab (the order's id on the route), with its links recorded.</summary>
    private static async Task<(Screen Screen, List<(Guid Order, Guid Call)> Links)> RecordCallFromOrderAsync(SavedCalls calls,
        TabletOrder? order, Guid orderId)
    {
        Screen screen = new(typeof(RecordCall));
        List<(Guid Order, Guid Call)> links = [];
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.SaveWork = calls.Save;
        screen.Tablet.ReadStock = (_, _) => MurphysStock([ColdFlu]);
        screen.Tablet.FindOrder = (_, id) => order is { } saved && saved.Id == id ? saved : null;
        screen.Tablet.FindCallOrder = (_, _, _) => throw new InvalidOperationException("The order comes from the route.");
        screen.Tablet.LinkWork = (_, id, call) =>
        {
            links.Add((id, call));
            return order is { } saved && saved.Id == id ? new(saved with { CallId = call }, null) : new(null, OrderStore.NotFound);
        };
        await screen.Open(new() { [nameof(RecordCall.Id)] = MurphysId, [nameof(RecordCall.OrderId)] = (Guid?)orderId });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Stock check"));
        return (screen, links);
    }

    // Scenario 5 (S22, D5).
    [Fact]
    public async Task Should_LinkTheOrderAndShowThatCallsLowItems_When_ACallIsRecordedAndSavedFromTheLowTab()
    {
        TabletOrder order = ShopOrders.Holding(null, (Plasters, 2m));
        SavedCalls calls = new();
        (Screen screen, List<(Guid Order, Guid Call)> links) = await RecordCallFromOrderAsync(calls, order, order.Id);
        await using (screen)
        {
            Assert.Contains("Back to order", screen.Renderer.Text(screen.Id));
            await screen.Click("In person");
            await CountAsync(screen, ColdFlu, "3");
            await screen.ClickLabelled($"Low {ColdFlu.Name}");
            await screen.Click("Review and save");
            await screen.Click("Save");
            await screen.Until(() => screen.Navigation.Destinations.Count > 0);
        }

        (_, Guid callId, _, CallContent content) = calls.Saves.Single();
        Assert.Equal([(order.Id, callId)], links);
        Assert.Equal([$"/locations/{MurphysId}/calls/{callId}/orders/{order.Id}"], screen.Navigation.Destinations);

        // Order entry for that Call, as the navigation opens it: the order now linked, the Call's own Low items listed.
        TabletCall saved = new(CallContract.Schema, 1, callId, MurphysId, "colm", new(2026, 9, 22), SnapshotBuilt.AddHours(4),
            CallChannel.InPerson, new("v1", SnapshotBuilt), null, content.StockEntries);
        (Screen entry, _) = await OrderEntryAsync(null, order with { CallId = callId }, callId, saved);
        await using Screen disposing = entry;
        await OpenLowTabAsync(entry);
        Assert.Equal(["Cold & Flu Relief 16s: not added"], LowRows(entry));
        Assert.Contains("From your stock check at Murphy's Pharmacy", entry.Renderer.Text(entry.Id));
        Assert.DoesNotContain("No stock check with this order.", entry.Renderer.Text(entry.Id));
    }

    // Scenario 6 (D5): Back, unsaved, returns to the order; an order with no lines yet isn't saved, so to a new order.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_ReturnToTheOrder_When_BackIsTappedOnRecordCallOpenedFromTheLowTab(bool orderHasLines)
    {
        TabletOrder order = ShopOrders.Holding(null, (Plasters, 2m));
        SavedCalls calls = new();
        (Screen screen, List<(Guid Order, Guid Call)> links) = await RecordCallFromOrderAsync(calls, orderHasLines ? order : null, order.Id);
        await using Screen disposing = screen;

        await screen.ClickLink("Back to order");

        Assert.Equal([orderHasLines ? $"/locations/{MurphysId}/orders/{order.Id}" : $"/locations/{MurphysId}/orders/new"],
            screen.Navigation.Destinations);
        Assert.Empty(calls.Saves);
        Assert.Empty(links);
    }
}
