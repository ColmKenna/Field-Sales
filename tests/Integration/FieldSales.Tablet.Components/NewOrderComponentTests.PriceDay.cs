using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-059 agreed scenario 6 (D4): Order entry left open over midnight in Ireland shows the new day's prices.</summary>
public sealed partial class NewOrderComponentTests
{
    [Fact]
    public async Task Should_ShowTodaysPrices_When_OrderEntryIsLeftOpenOverMidnight()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");
        await screen.Type("line-quantity", "12");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €150.00"));

        // Midnight passes: the downloaded €13.20 is now the price in effect, with no Sync.
        ProductSearchResult[] tomorrow = [.. Catalogue.Select(product => product.Code == "VITD-1000-90" ? product with { Price = 13.20m } : product)];
        screen.Tablet.ReadIndex = _ => new ProductSearchIndex("generation", tomorrow);
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.ChangePriceDay());
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €150.00"));
        await screen.Type("product-search", "vitamin d");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 matching product"));

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("€13.20", text);
        Assert.Contains("1 line · €150.00", text); // The order and its €12.50 line are kept.
        Assert.Equal(orders.Order!.Id, Assert.Single(orders.Calls.Select(call => call.Order).Distinct()));
    }
}
