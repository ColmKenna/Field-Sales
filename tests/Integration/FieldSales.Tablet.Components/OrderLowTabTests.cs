using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-052 (T-5.1.4, REP-US-012 S3, S7–S9, S12, S13; REP-US-008 S13) on T-07: the Low tab in order entry. Agreed
/// scenarios 1–12 and 14; 13 is storage (Tablet.Core <c>OrderFromCallTests</c>). Each item's state is worked out from the Call's
/// Low marks and the order as it is now; only "taken off" is kept, on the Call's draft.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static ProductSearchResult Lozenges => new(StockId(2, 22), "P-22", "Throat Lozenges 36s", Remedies.Name, "Each", 3.00m, Remedies.Id);
    private static ProductSearchResult Sudocrem125 => new(StockId(2, 23), "P-23", "Sudocrem 125g", Remedies.Name, "Each", 4.50m, Remedies.Id);
    private static ProductSearchResult Plasters => new(StockId(2, 24), "P-24", "Plasters 20s", Remedies.Name, "Each", 2.00m, Remedies.Id);

    private static ProductSearchResult[] OrderCatalogue => [ColdFlu, Lozenges, Sudocrem125, Plasters, Tea];

    /// <summary>A Call being recorded at Murphy's, In person, with these rows (count as typed, and the Low mark).</summary>
    private static CallDraft Recording(params (ProductSearchResult Product, string Count, bool Low)[] rows) =>
        new(Guid.NewGuid(), CallChannel.InPerson, "", false, [.. rows.Select(row => new StockLine(row.Product, SuggestionSources.LastVisit, row.Count, row.Low))]);

    /// <summary>Stands in for OrderStore on one order at Murphy's, recording what order entry asked for.</summary>
    private sealed class ShopOrders(TabletOrder? order)
    {
        public TabletOrder? Order { get; private set; } = order;
        public List<(Guid Order, Guid Product, decimal Quantity, Guid? Call)> Adds { get; } = [];
        public List<Guid> Removals { get; } = [];

        public static TabletOrder Holding(Guid? call, params (ProductSearchResult Product, decimal Quantity)[] lines) =>
            new(OrderContract.Schema, 1, Guid.NewGuid(), OrderState.InProgress, MurphysId, MurphysId, "colm", new(2026, 9, 22), call,
                new("v1", DateTimeOffset.UnixEpoch), [.. lines.Select(each => Line(each.Product, each.Quantity))]);

        private static OrderLine Line(ProductSearchResult product, decimal quantity) => new(Guid.NewGuid(), product.Id, product.Code,
            product.Name, quantity, product.Unit, new(product.Price!.Value, OrderContract.BasePriceKind, OrderContract.BasePriceLabel, []),
            new("v1", DateTimeOffset.UnixEpoch));

        public void Wire(TabletApp tablet)
        {
            tablet.FindView = (_, id) => Order is { } saved && saved.Id == id ? new(saved, OrderStanding.InProgress) : null;
            tablet.AddWork = (rep, id, shop, product, quantity) => Add(id, product, quantity, null);
            tablet.AddForCall = (rep, id, shop, product, quantity, call) => Add(id, product, quantity, call);
            tablet.RemoveLine = (_, _, line, confirmed) =>
            {
                Removals.Add(line);
                if (Order!.Lines.Count == 1) return confirmed ? new(null, null) { Deleted = true } : new(null, OrderStore.LastLinePrompt) { NeedsConfirmation = true };
                Order = Order with { Lines = [.. Order.Lines.Where(each => each.Id != line)] };
                return new(Order, null);
            };
            tablet.ChangeLine = (_, _, line, quantity) =>
            {
                Order = Order! with { Lines = [.. Order.Lines.Select(each => each.Id == line ? each with { Quantity = quantity } : each)] };
                return new(Order, null);
            };
        }

        private OrderResult Add(Guid id, Guid product, decimal quantity, Guid? call)
        {
            Adds.Add((id, product, quantity, call));
            OrderLine line = Line(OrderCatalogue.Single(each => each.Id == product), quantity);
            Order = Order is null
                ? new(OrderContract.Schema, 1, id, OrderState.InProgress, MurphysId, MurphysId, "colm", new(2026, 9, 22), call, line.Snapshot, [line])
                : Order with { Lines = [.. Order.Lines, line], CallId = Order.CallId ?? call };
            return new(Order, null);
        }
    }

    /// <summary>Order entry at Murphy's: <paramref name="order"/> opened by id (or a new order), from <paramref name="openedFrom"/>'s
    /// Record call when given, with <paramref name="draft"/> as the Call being recorded.</summary>
    private static async Task<(Screen Screen, ShopOrders Orders)> OrderEntryAsync(CallDraft? draft, TabletOrder? order, Guid? openedFrom,
        TabletCall? saved = null, LocationPrices? prices = null)
    {
        Screen screen = new(typeof(NewOrder));
        screen.Tablet.Read = (_, _) => Murphys();
        if (prices is not null) screen.Tablet.ReadPrices = (_, _) => prices;
        screen.Tablet.ReadIndex = _ => new ProductSearchIndex("g1", OrderCatalogue);
        screen.Tablet.FindCall = (_, id) => saved is { } call && call.Id == id ? call : null;
        if (draft is not null) screen.Tablet.CallDrafts.Keep("colm", 0, MurphysId, draft);
        ShopOrders orders = new(order);
        orders.Wire(screen.Tablet);
        Dictionary<string, object?> route = new() { [nameof(NewOrder.LocationId)] = MurphysId };
        if (order is not null) route[nameof(NewOrder.OrderId)] = (Guid?)order.Id;
        if (openedFrom is not null) route[nameof(NewOrder.CallId)] = openedFrom;
        await screen.Open(route);
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("On this order"));
        return (screen, orders);
    }

    private static string? LowTabLabel(Screen screen) =>
        screen.Renderer.Buttons(screen.Id).FirstOrDefault(button => button.StartsWith("Low (", StringComparison.Ordinal));

    private static Task OpenLowTabAsync(Screen screen) => screen.ClickContaining("Low (");

    /// <summary>The Low tab's rows as "name: state", in the Call's order.</summary>
    private static string[] LowRows(Screen screen) => [.. screen.Renderer.AttributeValues(screen.Id, "data-low-item")];

    // Scenario 1 (S3).
    [Fact]
    public async Task Should_ShowTheInProgressOrderWith1LineAndTheLowTabWithOneAddedOneNot_When_OrderEntryIsOpenedFromACallWith2LowLines1Added()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "1", true), (Plasters, "5", false));
        TabletOrder order = ShopOrders.Holding(draft.CallId, (ColdFlu, 24m));
        (Screen screen, _) = await OrderEntryAsync(draft, order, draft.CallId);
        await using Screen disposing = screen;

        Assert.Contains("1 line ·", screen.Renderer.Text(screen.Id));
        Assert.Equal("Low (1)", LowTabLabel(screen));
        Assert.Contains("Back to call", screen.Renderer.Text(screen.Id));
        Assert.Contains($"/locations/{MurphysId}/calls/new", screen.Renderer.AttributeValues(screen.Id, "href"));

        await OpenLowTabAsync(screen);

        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 24", "Throat Lozenges 36s: not added"], LowRows(screen));
        Assert.Contains("From your stock check at Murphy's Pharmacy", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Plasters 20s", screen.Renderer.Text(screen.Id)); // Counted, not Low.
        Assert.Equal(draft.CallId, order.CallId);
    }

    // Scenario 2 (S7).
    [Fact]
    public async Task Should_ListEachLowItemWithExactlyOneState_When_TheCallHasItemsNotAddedAddedAndRemoved()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "", true), (Sudocrem125, "0", true)).TakeOff([Sudocrem125.Id]);
        (Screen screen, _) = await OrderEntryAsync(draft, ShopOrders.Holding(draft.CallId, (ColdFlu, 24m)), draft.CallId);
        await using Screen disposing = screen;

        await OpenLowTabAsync(screen);

        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 24", "Throat Lozenges 36s: not added", "Sudocrem 125g: REMOVED"],
            LowRows(screen));
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("counted 3", text);
        Assert.Contains("not counted", text);
        Assert.Contains("you took this off the order", text);
        Assert.False(Labelled(screen, $"Add {ColdFlu.Name} to order")); // ADDED is changed on the order's line.
        Assert.True(Labelled(screen, $"Add {Lozenges.Name} to order"));
        Assert.True(Labelled(screen, $"Add {Sudocrem125.Name} to order"));
    }

    // Scenario 3 (S8).
    [Fact]
    public async Task Should_CountOnlyNotAddedItemsInTheTabLabel_When_ItemsAreAddedRemovedAndNotAdded()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "1", true), (Plasters, "", true), (Sudocrem125, "2", true))
            .TakeOff([Sudocrem125.Id]);
        (Screen screen, _) = await OrderEntryAsync(draft, ShopOrders.Holding(draft.CallId, (ColdFlu, 24m)), draft.CallId);
        await using Screen disposing = screen;

        Assert.Equal("Low (2)", LowTabLabel(screen)); // Lozenges and Plasters; not Cold & Flu (ADDED) or Sudocrem (REMOVED).
    }

    // Scenario 4 (S9).
    [Fact]
    public async Task Should_ShowREMOVEDWithAnAddAction_When_AnAddedLowItemsLineIsRemovedFromTheOrder()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "1", true));
        TabletOrder order = ShopOrders.Holding(draft.CallId, (ColdFlu, 24m), (Plasters, 2m));
        (Screen screen, ShopOrders orders) = await OrderEntryAsync(draft, order, draft.CallId);
        await using Screen disposing = screen;

        await screen.ClickLabelled($"Remove {ColdFlu.Name}");
        await screen.Until(() => orders.Removals.Count == 1 && !screen.Renderer.Text(screen.Id).Contains("2 lines"));
        await OpenLowTabAsync(screen);

        Assert.Equal(["Cold & Flu Relief 16s: REMOVED", "Throat Lozenges 36s: not added"], LowRows(screen));
        Assert.True(Labelled(screen, $"Add {ColdFlu.Name} to order"));
        Assert.Equal("Low (1)", LowTabLabel(screen));
        Assert.Equal([ColdFlu.Id], screen.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.TakenOff);
    }

    // Scenario 5 (US-008 S13, S10, S14).
    [Fact]
    public async Task Should_OpenTheSamePopoverAndShowADDEDWithItsQuantity_When_AddIsTappedOnANotAddedItem()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "1", true));
        (Screen screen, ShopOrders orders) = await OrderEntryAsync(draft, ShopOrders.Holding(draft.CallId, (ColdFlu, 24m)), draft.CallId);
        await using Screen disposing = screen;
        await OpenLowTabAsync(screen);

        await screen.ClickLabelled($"Add {Lozenges.Name} to order");

        Assert.True(PopoverOpen(screen));
        Assert.True(string.IsNullOrEmpty(screen.Renderer.Value(screen.Id, "popover-quantity")));
        Assert.Contains("counted 1", screen.Renderer.Text(screen.Id));
        Assert.Contains("€3.00 · list price", screen.Renderer.Text(screen.Id));
        await screen.Type("popover-quantity", "12");
        await screen.ClickLabelled("Add to order");
        await screen.Until(() => !PopoverOpen(screen));

        Assert.Equal((Lozenges.Id, 12m, (Guid?)draft.CallId), (orders.Adds.Single().Product, orders.Adds[0].Quantity, orders.Adds[0].Call));
        Assert.Contains("Low items", screen.Renderer.AttributeValues(screen.Id, "aria-label")); // Back where it was opened.
        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 24", "Throat Lozenges 36s: ADDED - 12"], LowRows(screen));
        Assert.Equal("Low (0)", LowTabLabel(screen));
        Assert.True(screen.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.AddedToOrder);
    }

    // WI-064 agreed scenario 5 (criterion 3): the popover shows the engine's winner at the quantity typed, naming where it comes from.
    [Fact]
    public async Task Should_ShowTheWinnerAtTheTypedQuantity_When_AQuantityReachesABreak()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "1", true));
        LocationPrices prices = new(new DateOnly(2026, 9, 22), [new FieldSales.Pricing.PercentageTier("Tier B", 8m)],
            new Dictionary<Guid, IReadOnlyList<FieldSales.Pricing.QuantityBreak>> { [Lozenges.Id] = [new(10m, 2.50m)] });
        (Screen screen, _) = await OrderEntryAsync(draft, null, draft.CallId, prices: prices);
        await using Screen disposing = screen;
        await OpenLowTabAsync(screen);

        await screen.ClickLabelled($"Add {Lozenges.Name} to order");
        Assert.Contains("€2.76 · Tier B", screen.Renderer.Text(screen.Id)); // At 1: 8% off €3.00.
        await screen.Type("popover-quantity", "9");
        Assert.Contains("€2.76 · Tier B", screen.Renderer.Text(screen.Id));
        await screen.Type("popover-quantity", "12");
        Assert.Contains("€2.50 · Break: 10+", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("list price", screen.Renderer.Text(screen.Id));
    }

    // Scenario 6.
    [Fact]
    public async Task Should_ShowADDEDAgain_When_AREMOVEDItemIsAddedThroughThePopover()
    {
        CallDraft draft = Recording((ColdFlu, "3", true)).TakeOff([ColdFlu.Id]);
        (Screen screen, ShopOrders orders) = await OrderEntryAsync(draft, ShopOrders.Holding(draft.CallId, (Plasters, 2m)), draft.CallId);
        await using Screen disposing = screen;
        await OpenLowTabAsync(screen);
        Assert.Equal(["Cold & Flu Relief 16s: REMOVED"], LowRows(screen));

        await screen.ClickLabelled($"Add {ColdFlu.Name} to order");
        Assert.True(string.IsNullOrEmpty(screen.Renderer.Value(screen.Id, "popover-quantity")));
        await screen.Type("popover-quantity", "24");
        await screen.ClickLabelled("Add to order");
        await screen.Until(() => !PopoverOpen(screen));

        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 24"], LowRows(screen)); // The order decides; "taken off" no longer shows.
        Assert.Single(orders.Adds);
    }

    // Scenario 7 (S12, T7.6).
    [Fact]
    public async Task Should_OfferNoWayToAddThemAllAtOnce_When_SeveralItemsAreNotAdded()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "1", true), (Plasters, "", true));
        (Screen screen, ShopOrders orders) = await OrderEntryAsync(draft, null, draft.CallId);
        await using Screen disposing = screen;

        await OpenLowTabAsync(screen);

        Assert.Equal("Low (3)", LowTabLabel(screen));
        string[] buttons = [.. screen.Renderer.Buttons(screen.Id)];
        Assert.Equal(3, buttons.Count(button => button == "Add"));
        Assert.DoesNotContain(buttons, button => button.Contains("all", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(orders.Adds);
    }

    // Scenario 8 (S13, D7).
    [Fact]
    public async Task Should_LeaveTheOrderLineAndDropTheItem_When_LowIsUntickedOnTheStockCheck()
    {
        CallDraft draft = Recording((ColdFlu, "3", true), (Lozenges, "1", true));
        (Screen screen, ShopOrders orders) = await OrderEntryAsync(draft, ShopOrders.Holding(draft.CallId, (ColdFlu, 24m)), draft.CallId);
        await using Screen disposing = screen;
        await OpenLowTabAsync(screen);
        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 24", "Throat Lozenges 36s: not added"], LowRows(screen));

        // Back on the stock check the rep unticks Low on Cold & Flu, then returns to order entry (a rebuilt page, as on any return).
        screen.Tablet.CallDrafts.Keep("colm", 0, MurphysId, draft with { Stock = [.. draft.Lines.Select(line => line.Product.Id == ColdFlu.Id ? line.Tick() : line)] });
        await screen.Rebuild();
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("On this order"));
        await OpenLowTabAsync(screen);

        Assert.Equal(["Throat Lozenges 36s: not added"], LowRows(screen));
        Assert.Equal((ColdFlu.Id, 24m), (orders.Order!.Lines.Single().ProductId, orders.Order.Lines[0].Quantity));
        Assert.Empty(orders.Removals);
        Assert.Contains("1 line ·", screen.Renderer.Text(screen.Id));
    }

    // Scenario 9.
    [Fact]
    public async Task Should_ShowTheNewQuantity_When_AnAddedItemsLineIsChangedOnTheOrder()
    {
        CallDraft draft = Recording((ColdFlu, "3", true));
        (Screen screen, _) = await OrderEntryAsync(draft, ShopOrders.Holding(draft.CallId, (ColdFlu, 24m)), draft.CallId);
        await using Screen disposing = screen;

        await screen.ClickLabelled($"Change {ColdFlu.Name}");
        await screen.Type("edit-quantity", "36");
        await screen.Click("Save");
        await screen.Until(() => !screen.Renderer.AttributeValues(screen.Id, "id").Contains("edit-quantity"));
        await OpenLowTabAsync(screen);

        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 36"], LowRows(screen));
    }

    // Scenario 10 (D5): one source. Added from the pad's search, not the Low tab, it still reads ADDED; and the new order is the Call's.
    [Fact]
    public async Task Should_ShowADDED_When_ALowProductIsAddedFromTheOrderPadInstead()
    {
        CallDraft draft = Recording((Lozenges, "1", true));
        (Screen screen, ShopOrders orders) = await OrderEntryAsync(draft, null, draft.CallId);
        await using Screen disposing = screen;
        Assert.Equal("Low (1)", LowTabLabel(screen));

        await screen.Type("product-search", "lozenges");
        await screen.Until(() => screen.Renderer.Buttons(screen.Id).Any(button => button.Contains(Lozenges.Name, StringComparison.Ordinal)));
        await screen.ClickContaining(Lozenges.Name);
        await screen.Type("line-quantity", "6");
        await screen.Click("Add to order");
        await screen.Until(() => orders.Adds.Count == 1);
        await OpenLowTabAsync(screen);

        Assert.Equal(["Throat Lozenges 36s: ADDED - 6"], LowRows(screen));
        Assert.Equal("Low (0)", LowTabLabel(screen));
        Assert.Equal((Guid?)draft.CallId, orders.Adds[0].Call);
        Assert.Equal([draft.CallId], screen.Tablet.AddedForCalls);
    }

    // Scenario 11 (D4).
    [Fact]
    public async Task Should_ShowLow0AndNoStockCheckWithThisOrder_When_TheOrderHasNoCall()
    {
        (Screen screen, _) = await OrderEntryAsync(null, ShopOrders.Holding(null, (ColdFlu, 24m)), null);
        await using Screen disposing = screen;
        Assert.Equal("Low (0)", LowTabLabel(screen));
        Assert.Contains("Back to shop", screen.Renderer.Text(screen.Id));

        await OpenLowTabAsync(screen);

        Assert.Contains("No stock check with this order.", screen.Renderer.Text(screen.Id));
        Assert.Equal("Low (0)", LowTabLabel(screen));
    }

    // Scenario 12 (D4, and D5's limit): after the Call is saved, the tab reads the saved Call; a line taken off then reads not added.
    [Fact]
    public async Task Should_ListTheSavedCallsLowItems_When_TheOrderIsOpenedAfterTheCallWasSaved()
    {
        Guid callId = Guid.NewGuid();
        TabletCall saved = new(CallContract.Schema, 1, callId, MurphysId, "colm", new(2026, 9, 22), DateTimeOffset.UnixEpoch,
            CallChannel.InPerson, new("v1", DateTimeOffset.UnixEpoch), null,
            [new(ColdFlu.Id, ColdFlu.Code, ColdFlu.Name, "Each", 3m, Low: true), new(Lozenges.Id, Lozenges.Code, Lozenges.Name, "Each", 1m, Low: true),
             new(Plasters.Id, Plasters.Code, Plasters.Name, "Each", 5m)]);
        // Opened from Unsent: by id, with no Call route; the order is linked to the saved Call.
        (Screen screen, ShopOrders orders) = await OrderEntryAsync(null, ShopOrders.Holding(callId, (ColdFlu, 24m), (Plasters, 2m)), null, saved);
        await using Screen disposing = screen;
        await OpenLowTabAsync(screen);
        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 24", "Throat Lozenges 36s: not added"], LowRows(screen));

        await screen.ClickContaining("Order pad");
        await screen.ClickLabelled($"Remove {ColdFlu.Name}");
        await screen.Until(() => orders.Removals.Count == 1);
        await OpenLowTabAsync(screen);

        Assert.Equal(["Cold & Flu Relief 16s: not added", "Throat Lozenges 36s: not added"], LowRows(screen));
        Assert.Equal("Low (2)", LowTabLabel(screen));
    }

    // Scenario 14 (D2).
    [Fact]
    public async Task Should_OpenTheCallsOrderAndComeBackToTheDraft_When_OpenOrderThenBackToCallAreTapped()
    {
        CallOrders orders = new(CallOrders.From((ColdFlu, 24m)));
        await using Screen call = await CallAtMurphysAsync(new(), orders, ColdFlu);
        await call.Click("In person");
        await call.ClickLabelled($"Low {ColdFlu.Name}");
        Guid callId = call.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.CallId;

        await call.Click("Open order");

        Assert.Equal([$"/locations/{MurphysId}/calls/{callId}/orders/{orders.Order!.Id}"], call.Navigation.Destinations);
        await call.Rebuild(); // Back to call: the page opens again and the draft comes back.
        await call.Until(() => call.Renderer.Text(call.Id).Contains("Stock check"));
        Assert.Equal(callId, call.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.CallId);
        Assert.Equal("true", Pressed(call, $"Low {ColdFlu.Name}"));

        // With nothing on an order yet, and before anything is entered, Open order starts a new one for the same Call.
        await using Screen fresh = await CallAtMurphysAsync(new(), new(), ColdFlu);
        await fresh.Click("Open order");
        Guid freshId = fresh.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.CallId;
        Assert.Equal([$"/locations/{MurphysId}/calls/{freshId}/orders/new"], fresh.Navigation.Destinations);

        // Order entry opened from the Call goes back to it.
        (Screen entry, _) = await OrderEntryAsync(Recording((ColdFlu, "3", true)), null, freshId);
        await using Screen disposingEntry = entry;
        await entry.Click("Save and close");
        Assert.Equal([$"/locations/{MurphysId}/calls/new"], entry.Navigation.Destinations);
    }
}
