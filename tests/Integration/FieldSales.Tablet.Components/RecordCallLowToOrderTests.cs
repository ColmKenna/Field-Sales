using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-051 (T-5.5.1, REP-US-008) on T-06: a Low item goes on the order through the quantity popover. Agreed scenarios
/// 1, 2 and 4–9, 12–14. Finding or starting the Call's order is storage (Tablet.Core <c>OrderFromCallTests</c>, scenarios 2, 3,
/// 10, 11); here the screen asks for it with the Call's id and shows what is on it.</summary>
public sealed partial class TabletSessionScreenTests
{
    // Properties: static fields in other parts of this partial class (Remedies, Food) may not be set yet when this part's are.
    private static ProductSearchResult ColdFlu => new(StockId(2, 19), "P-19", "Cold & Flu Relief 16s", Remedies.Name, "Each", 8.95m, Remedies.Id);
    private static ProductSearchResult Tea => new(StockId(2, 20), "P-20", "Loose Herbal Tea", Food.Name, "kg", 24.00m, Food.Id, 0.5m, 1m);
    private static ProductSearchResult Calamine => new(StockId(2, 21), "P-21", "Calamine Lotion 200ml", Remedies.Name, "Each", null, Remedies.Id);

    /// <summary>The Call's order as the store would answer: what it starts with (lines added from order entry), and every add
    /// from the Call recorded and put on it.</summary>
    private sealed class CallOrders(TabletOrder? start = null)
    {
        public List<(Guid Call, Guid Shop, Guid Product, decimal Quantity)> Adds { get; } = [];
        public TabletOrder? Order { get; private set; } = start;

        public OrderResult Add(string rep, Guid call, Guid shop, Guid product, decimal quantity)
        {
            Adds.Add((call, shop, product, quantity));
            OrderLine line = Line(product, quantity);
            Order = Order is null
                ? new(OrderContract.Schema, 1, Guid.NewGuid(), OrderState.InProgress, shop, shop, rep, new(2026, 9, 22), call, line.Snapshot, [line])
                : Order with { Lines = [.. Order.Lines, line] };
            return new(Order, null);
        }

        public static OrderLine Line(Guid product, decimal quantity) => new(Guid.NewGuid(), product, "P", "Product", quantity,
            product == Tea.Id ? "kg" : "Each", new(8.95m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel, []),
            new("v1", DateTimeOffset.UnixEpoch));

        /// <summary>Murphy's In Progress order with these lines, as order entry left it (no Call).</summary>
        public static TabletOrder From(params (ProductSearchResult Product, decimal Quantity)[] lines) =>
            new(OrderContract.Schema, 1, Guid.NewGuid(), OrderState.InProgress, MurphysId, MurphysId, "colm", new(2026, 9, 22), null,
                new("v1", DateTimeOffset.UnixEpoch), [.. lines.Select(each => Line(each.Product.Id, each.Quantity))]);
    }

    private static async Task<Screen> CallAtMurphysAsync(SavedCalls calls, CallOrders orders, params ProductSearchResult[] extra)
    {
        Screen screen = new(typeof(RecordCall));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.SaveWork = calls.Save;
        screen.Tablet.ReadStock = (_, _) => MurphysStock(extra);
        screen.Tablet.FindCallOrder = (_, _, _) => orders.Order;
        screen.Tablet.AddFromCall = orders.Add;
        // Unticking Low or cancelling never takes a line off the order or changes it (S4, T7.8).
        screen.Tablet.RemoveLine = (_, _, _, _) => throw new InvalidOperationException("Nothing is removed from the order here.");
        screen.Tablet.ChangeLine = (_, _, _, _) => throw new InvalidOperationException("No order line is changed here.");
        await screen.Open(new() { [nameof(RecordCall.Id)] = MurphysId });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Stock check"));
        return screen;
    }

    /// <summary>In person at Murphy's: Cold &amp; Flu Relief 16s counted 3 and marked Low (S1's Given).</summary>
    private static async Task<Screen> LowAtMurphysAsync(SavedCalls calls, CallOrders orders)
    {
        Screen screen = await CallAtMurphysAsync(calls, orders, ColdFlu);
        await screen.Click("In person");
        await CountAsync(screen, ColdFlu, "3");
        await screen.ClickLabelled($"Low {ColdFlu.Name}");
        return screen;
    }

    private static async Task AddToOrderAsync(Screen screen, ProductSearchResult product, string quantity)
    {
        await screen.ClickLabelled($"Add {product.Name} to order");
        await screen.Type("popover-quantity", quantity);
        await screen.ClickLabelled("Add to order");
        await screen.Until(() => !PopoverOpen(screen));
    }

    private static bool PopoverOpen(Screen screen) => screen.Renderer.AttributeValues(screen.Id, "id").Contains("quantity-popover-heading");

    private static string? AddDisabled(Screen screen) => screen.Renderer.LabelledAttribute(screen.Id, "Add to order", "disabled");

    // Scenario 1 (S7, AC1).
    [Fact]
    public async Task Should_OpenThePopoverWithNameCount3ListPriceAndAnEmptyQuantity_When_ColdFluRelief16sIsCounted3AndMarkedLow()
    {
        await using Screen screen = await CallAtMurphysAsync(new(), new(), ColdFlu);
        await screen.Click("In person");
        await CountAsync(screen, ColdFlu, "3");
        Assert.False(Labelled(screen, $"Add {ColdFlu.Name} to order")); // In person, only a Low row offers it.
        await screen.ClickLabelled($"Low {ColdFlu.Name}");

        await screen.ClickLabelled($"Add {ColdFlu.Name} to order");

        Assert.True(PopoverOpen(screen));
        string text = screen.Renderer.Text(screen.Id);
        Assert.Equal(2, text.Split(ColdFlu.Name).Length - 1); // On its row and as the popover's heading.
        Assert.Contains("counted 3", text);
        Assert.Contains("€8.95 · list price", text);
        Assert.True(string.IsNullOrEmpty(screen.Renderer.Value(screen.Id, "popover-quantity")));
        Assert.NotNull(AddDisabled(screen));
        Assert.Contains("Cancel", screen.Renderer.Buttons(screen.Id));
        Assert.True(Labelled(screen, "Close"));
    }

    // Scenario 2 (S1), screen side: the add goes to the store with this Call's id, and the popover closes to the stock check.
    [Fact]
    public async Task Should_CreateAnInProgressOrderForMurphysWithThatLineLinkedToThisCall_When_24IsAdded()
    {
        SavedCalls calls = new();
        CallOrders orders = new();
        await using Screen screen = await LowAtMurphysAsync(calls, orders);
        await screen.ClickLabelled($"Add {ColdFlu.Name} to order");

        await screen.Type("popover-quantity", "24");
        Assert.Null(AddDisabled(screen));
        await screen.ClickLabelled("Add to order");
        await screen.Until(() => !PopoverOpen(screen));

        (Guid call, Guid shop, Guid product, decimal quantity) = Assert.Single(orders.Adds);
        Assert.Equal((MurphysId, ColdFlu.Id, 24m), (shop, product, quantity));
        Assert.Contains("Stock check", screen.Renderer.Text(screen.Id));
        await EnterNotesAsync(screen, Pitch);
        await screen.Click("Review and save");
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);
        Assert.Equal(call, calls.Saves[0].Call); // The order is found or started for the Call that was saved.
    }

    // Scenario 4 (S3), with a line added from order entry before the Call (D5).
    [Fact]
    public async Task Should_ShowOnOrder24InsteadOfAddToOrder_When_ColdFluRelief16sIsOnTheOrderWith24()
    {
        CallOrders orders = new(CallOrders.From((Tea, 2.5m)));
        await using Screen screen = await CallAtMurphysAsync(new(), orders, ColdFlu, Tea);
        await screen.Click("In person");
        await screen.ClickLabelled($"Low {Tea.Name}");
        Assert.Contains("On order (2.5 kg)", screen.Renderer.Text(screen.Id));
        Assert.False(Labelled(screen, $"Add {Tea.Name} to order"));

        await screen.ClickLabelled($"Low {ColdFlu.Name}");
        await AddToOrderAsync(screen, ColdFlu, "24");

        Assert.Contains("On order (24)", screen.Renderer.Text(screen.Id));
        Assert.False(Labelled(screen, $"Add {ColdFlu.Name} to order"));
    }

    // Scenario 5 (S4, T7.8), and D8: leaving says the lines stay on the order.
    [Fact]
    public async Task Should_LeaveTheOrderLineAt24_When_TheLowMarkIsRemoved()
    {
        CallOrders orders = new();
        await using Screen screen = await LowAtMurphysAsync(new(), orders);
        await AddToOrderAsync(screen, ColdFlu, "24");

        await screen.ClickLabelled($"Low {ColdFlu.Name}");

        Assert.Equal("false", Pressed(screen, $"Low {ColdFlu.Name}"));
        Assert.Equal((ColdFlu.Id, 24m), (Assert.Single(orders.Order!.Lines).ProductId, orders.Order.Lines[0].Quantity));
        Assert.Single(orders.Adds); // No remove or change reached the order (they would throw).
        await screen.ClickLabelled($"Low {ColdFlu.Name}");
        Assert.Contains("On order (24)", screen.Renderer.Text(screen.Id));
        await screen.ClickLink("Back to shop");
        Assert.Contains("Lines added to the order stay on it.", screen.Renderer.Text(screen.Id));
    }

    // Scenario 6 (S6).
    [Fact]
    public async Task Should_ShowEnterAQuantityOf1OrMoreAndKeepAddUnavailable_When_0IsEnteredForAnEachProduct()
    {
        CallOrders orders = new();
        await using Screen screen = await LowAtMurphysAsync(new(), orders);
        await screen.ClickLabelled($"Add {ColdFlu.Name} to order");

        await screen.Type("popover-quantity", "0");

        Assert.Contains("Enter a quantity of 1 or more", screen.Renderer.Text(screen.Id));
        Assert.NotNull(AddDisabled(screen));
        await screen.ClickLabelled("Add to order");
        Assert.True(PopoverOpen(screen));
        Assert.Empty(orders.Adds);
    }

    // Scenario 7 (S12).
    [Fact]
    public async Task Should_ShowTheMeasureMessageAndKeepAddUnavailable_When_AKgQuantityIsBelowTheMinimumOrOffTheStep()
    {
        CallOrders orders = new();
        await using Screen screen = await CallAtMurphysAsync(new(), orders, Tea);
        await screen.Click("In person");
        await screen.ClickLabelled($"Low {Tea.Name}");
        await screen.ClickLabelled($"Add {Tea.Name} to order");
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("not counted", text);
        Assert.Contains("€24.00 per kg · list price", text);
        Assert.Contains("Quantity (kg)", text);

        await screen.Type("popover-quantity", "0.5");
        Assert.Contains("Enter at least 1.0 kg in steps of 0.5 kg", screen.Renderer.Text(screen.Id));
        Assert.NotNull(AddDisabled(screen));
        await screen.Type("popover-quantity", "1.2");
        Assert.Contains("Enter multiples of 0.5 kg", screen.Renderer.Text(screen.Id));
        Assert.NotNull(AddDisabled(screen));
        Assert.Empty(orders.Adds);

        await screen.Type("popover-quantity", "1.5");
        Assert.Null(AddDisabled(screen));
        await screen.ClickLabelled("Add to order");
        await screen.Until(() => !PopoverOpen(screen));
        Assert.Equal((Tea.Id, 1.5m), (Assert.Single(orders.Adds).Product, orders.Adds[0].Quantity));
        Assert.Contains("On order (1.5 kg)", screen.Renderer.Text(screen.Id));
    }

    // Scenario 8 (S11).
    [Fact]
    public async Task Should_AddNothingAndKeepTheItemNotAdded_When_CancelIsTappedAfterTyping()
    {
        CallOrders orders = new();
        await using Screen screen = await LowAtMurphysAsync(new(), orders);
        await screen.ClickLabelled($"Add {ColdFlu.Name} to order");
        await screen.Type("popover-quantity", "24");

        await screen.Click("Cancel");

        Assert.False(PopoverOpen(screen));
        Assert.Empty(orders.Adds);
        Assert.True(Labelled(screen, $"Add {ColdFlu.Name} to order"));
        Assert.Equal("true", Pressed(screen, $"Low {ColdFlu.Name}"));
    }

    // Scenario 9 (S8, T7.7): the quantity is never pre-filled, after ✕ or when another row's popover replaces it.
    [Fact]
    public async Task Should_OpenWithAnEmptyQuantity_When_ThePopoverIsReopenedOrOpenedFromAnotherRow()
    {
        CallOrders orders = new();
        await using Screen screen = await CallAtMurphysAsync(new(), orders, ColdFlu);
        await screen.Click("Phone");
        await screen.ClickLabelled($"Add {ColdFlu.Name} to order");
        await screen.Type("popover-quantity", "24");
        await screen.ClickLabelled("Close");
        Assert.False(PopoverOpen(screen));

        await screen.ClickLabelled($"Add {ColdFlu.Name} to order");
        Assert.True(string.IsNullOrEmpty(screen.Renderer.Value(screen.Id, "popover-quantity")));
        await screen.Type("popover-quantity", "5");
        await screen.ClickLabelled($"Add {Numbered[0].Name} to order");

        Assert.True(PopoverOpen(screen));
        Assert.True(string.IsNullOrEmpty(screen.Renderer.Value(screen.Id, "popover-quantity")));
        Assert.Equal(2, screen.Renderer.Text(screen.Id).Split(Numbered[0].Name).Length - 1); // The popover is now Product 01's.
        Assert.Empty(orders.Adds);
    }

    // Scenario 12 (D4), and the order-only rule: lines on the order alone don't make a Call that can be saved.
    [Fact]
    public async Task Should_AddToTheOrderWithoutMarkingLow_When_AddToOrderIsUsedOnAPhoneRow()
    {
        SavedCalls calls = new();
        CallOrders orders = new();
        await using Screen screen = await CallAtMurphysAsync(calls, orders, ColdFlu);
        await screen.Click("Phone");

        await AddToOrderAsync(screen, ColdFlu, "6");

        Assert.Equal((ColdFlu.Id, 6m), (Assert.Single(orders.Adds).Product, orders.Adds[0].Quantity));
        Assert.Equal(("false", "false"), (Pressed(screen, $"Low {ColdFlu.Name}"), Pressed(screen, $"Out {ColdFlu.Name}")));
        Assert.Contains("On order (6)", screen.Renderer.Text(screen.Id));
        await screen.Click("Review and save");
        Assert.Contains(CallContract.RecordSomething, screen.Renderer.Text(screen.Id));
        Assert.Empty(calls.Saves);
        await screen.ClickLabelled($"Low {ColdFlu.Name}");
        await ReviewAndSaveAsync(screen, calls, "Phone · 1 marked Low");
    }

    // Scenario 13 (D3).
    [Fact]
    public async Task Should_KeepTheCount_When_LowIsUntickedInPersonOnARowCounted0()
    {
        SavedCalls calls = new();
        await using Screen screen = await CallAtMurphysAsync(calls, new(), ColdFlu);
        await screen.Click("In person");
        await CountAsync(screen, ColdFlu, "0");
        await screen.ClickLabelled($"Low {ColdFlu.Name}");

        await screen.ClickLabelled($"Low {ColdFlu.Name}");

        Assert.Equal("false", Pressed(screen, $"Low {ColdFlu.Name}"));
        Assert.Equal("0", screen.Renderer.Value(screen.Id, $"count-{ColdFlu.Id}"));
        CallContent saved = await ReviewAndSaveAsync(screen, calls, "In person · 1 counted, 14 not checked");
        Assert.Equal((0m, false), saved.StockEntries!.Where(entry => entry.ProductId == ColdFlu.Id).Select(entry => (entry.Count, entry.Low)).Single());
    }

    // Scenario 14.
    [Fact]
    public async Task Should_ShowNoPriceYetAndKeepAddUnavailable_When_TheLowProductHasNoPrice()
    {
        CallOrders orders = new();
        await using Screen screen = await CallAtMurphysAsync(new(), orders, Calamine);
        await screen.Click("In person");
        await screen.ClickLabelled($"Low {Calamine.Name}");

        await screen.ClickLabelled($"Add {Calamine.Name} to order");

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains(OrderStore.NoPrice, text);
        Assert.DoesNotContain("list price", text);
        Assert.NotNull(AddDisabled(screen));
        await screen.ClickLabelled("Add to order");
        Assert.Empty(orders.Adds);
    }
}
