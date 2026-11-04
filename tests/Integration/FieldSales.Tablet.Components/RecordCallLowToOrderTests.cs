using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-051 (T-5.5.1, REP-US-008) on T-06: a Low item goes on the order through the quantity popover. Finding or starting
/// the Call's order is storage (Tablet.Core <c>OrderFromCallTests</c>); here the screen asks for it with the Call's id.</summary>
public sealed partial class TabletSessionScreenTests
{
    // A property: static fields in other parts of this partial class (Remedies) may not be set yet when this part's are.
    private static ProductSearchResult ColdFlu => new(StockId(2, 19), "P-19", "Cold & Flu Relief 16s", Remedies.Name, "Each", 8.95m, Remedies.Id);

    /// <summary>Records every add from a Call and answers with an order holding what was added.</summary>
    private sealed class CallOrders
    {
        public List<(Guid Call, Guid Shop, Guid Product, decimal Quantity)> Adds { get; } = [];
        public TabletOrder? Order { get; private set; }

        public OrderResult Add(string rep, Guid call, Guid shop, Guid product, decimal quantity)
        {
            Adds.Add((call, shop, product, quantity));
            OrderSnapshotRef source = new("v1", DateTimeOffset.UnixEpoch);
            OrderLine line = new(Guid.NewGuid(), product, "P", "Product", quantity, "Each",
                new(8.95m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel, []), source);
            Order = Order is null
                ? new(OrderContract.Schema, 1, Guid.NewGuid(), OrderState.InProgress, shop, shop, rep, new(2026, 9, 22), call, source, [line])
                : Order with { Lines = [.. Order.Lines, line] };
            return new(Order, null);
        }
    }

    private static async Task<(Screen Screen, CallOrders Orders)> LowAtMurphysAsync(SavedCalls calls)
    {
        Screen screen = await StockCheckAsync(calls, MurphysStock(ColdFlu));
        CallOrders orders = new();
        screen.Tablet.AddFromCall = orders.Add;
        await screen.Click("In person");
        await CountAsync(screen, ColdFlu, "3");
        await screen.ClickLabelled($"Low {ColdFlu.Name}");
        return (screen, orders);
    }

    private static bool PopoverOpen(Screen screen) => screen.Renderer.AttributeValues(screen.Id, "id").Contains("quantity-popover-heading");

    private static string? AddDisabled(Screen screen) => screen.Renderer.LabelledAttribute(screen.Id, "Add to order", "disabled");

    // Scenario 1 (S7, AC1).
    [Fact]
    public async Task Should_OpenThePopoverWithNameCount3ListPriceAndAnEmptyQuantity_When_ColdFluRelief16sIsCounted3AndMarkedLow()
    {
        await using Screen screen = await StockCheckAsync(new(), MurphysStock(ColdFlu));
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
        (Screen screen, CallOrders orders) = await LowAtMurphysAsync(calls);
        await using Screen _ = screen;
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
}
