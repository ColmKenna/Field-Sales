namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-052 checkpoint: order entry as T-5.1.1, T-5.1.2 and T-5.2.1 left it, pinned before the Low tab (T-5.1.4) is added.
/// These must pass unchanged afterwards. Removing the last line (C3) is pinned by
/// <c>Should_AskRemoveTheLastLineAndDeleteThisOrder_When_TheOnlyLineIsRemoved</c>; the quantity popover by WI-051's
/// <c>RecordCallLowToOrderTests</c>.</summary>
public sealed partial class NewOrderComponentTests
{
    // C1: the screen an In Progress order opens to.
    [Fact]
    public async Task Should_ShowTheSearchBoxThePadAndTheOrdersLines_When_AnInProgressOrderIsOpenedById()
    {
        SavedOrder saved = new(Ordering.Contracts.OrderState.InProgress, LineFor(ColdFlu, 24m));
        await using Screen screen = await saved.OpenAsync();

        IReadOnlyList<string> ids = screen.Renderer.AttributeValues(screen.Id, "id");
        IReadOnlyList<string> labels = screen.Renderer.AttributeValues(screen.Id, "aria-label");
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("product-search", ids);
        Assert.Contains("Order pad", labels); // The pad is on screen, not behind a tap.
        Assert.Contains("On this order", text);
        Assert.Contains("Cold & Flu Relief 16s", text);
        Assert.Contains("1 line · €72.00", text);
        Assert.Contains("Change Cold & Flu Relief 16s", labels);
        Assert.Contains("Remove Cold & Flu Relief 16s", labels);
        Assert.Contains("Back to shop", text);
        Assert.Contains($"/locations/{ShopId}", screen.Renderer.AttributeValues(screen.Id, "href"));
    }

    // C2: a New Order from the shop adds every line to the one order on screen, through the plain add with no Call.
    [Fact]
    public async Task Should_AddEveryLineToTheOrderOnScreenWithoutACall_When_ANewOrderIsBuiltFromTheShop()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        screen.Tablet.AddFromCall = (_, _, _, _, _) => throw new InvalidOperationException("A standalone order isn't added from a Call.");
        await screen.Show();

        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");
        await screen.Type("line-quantity", "6");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line"));
        await screen.Type("product-search", "tea");
        await screen.Pick("Loose Herbal Tea");
        await screen.Type("line-quantity", "1.5");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("2 lines"));

        Assert.Equal(2, orders.Calls.Count);
        Assert.Single(orders.Calls.Select(call => call.Order).Distinct());
        Assert.All(orders.Calls, call => Assert.Equal(ShopId, call.Shop));
        Assert.Null(orders.Order!.CallId);
    }

    // C3b: a line that isn't the last goes without a question.
    [Fact]
    public async Task Should_RemoveTheLineWithoutAsking_When_OneOfTwoLinesIsRemoved()
    {
        SavedOrder saved = new(Ordering.Contracts.OrderState.InProgress, LineFor(ColdFlu, 24m), LineFor(Catalogue[0], 6m));
        await using Screen screen = await saved.OpenAsync();
        Guid coldFlu = saved.Order.Lines[0].Id;

        await screen.ClickLabelled("Remove Cold & Flu Relief 16s");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line ·"));

        Assert.Equal([(coldFlu, false)], saved.Removals);
        Assert.DoesNotContain("Remove the last line", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Navigation.Destinations);
    }
}
