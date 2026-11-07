using FieldSales.Pricing;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-065 characterisation scenarios C1 and C2: how an order line shows its price before the source and the provenance
/// sheet arrive. The captured unit price (per unit for a measure) and the line total read the same afterwards, and the line shows
/// what it captured, never today's price.</summary>
public sealed partial class NewOrderComponentTests
{
    [Fact]
    public async Task Should_ShowTheCapturedUnitPriceAndTotal_When_ALineIsOnTheOrder()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();
        await AddAsync(screen, "vitamin d", "Vitamin D 1000IU 90s", "12");
        await AddAsync(screen, "loose herbal", "Loose Herbal Tea", "2");

        string vitaminD = LineText(screen, "Vitamin D 1000IU 90s"), tea = LineText(screen, "Loose Herbal Tea");
        Assert.Contains("€12.50", vitaminD);
        Assert.Contains("€150.00", vitaminD);
        Assert.Contains("€4.80 per kg", tea);
        Assert.Contains("€9.60", tea);
    }

    [Fact]
    public async Task Should_ShowTheCapturedPrice_When_TheShopsPriceHasChangedSince()
    {
        FakeOrders orders = new();
        await using Screen screen = Open(orders);
        await screen.Show();
        await AddAsync(screen, "vitamin d", "Vitamin D 1000IU 90s", "12");

        // The shop now gets 8% off: the pad moves to €11.50, the line keeps the €12.50 it captured.
        screen.Tablet.ReadPrices = (_, _) => new LocationPrices(new DateOnly(2026, 10, 29), [new PercentageTier("Tier B", 8m)],
            new Dictionary<Guid, IReadOnlyList<QuantityBreak>>());
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.ChangePriceDay());
        await screen.Until(() => screen.Renderer.AttributeValues(screen.Id, "id").Contains("product-search")
            && LineTextOrNull(screen, "Vitamin D 1000IU 90s") is not null);
        await screen.Type("product-search", "vitamin d");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("€11.50"));

        string line = LineText(screen, "Vitamin D 1000IU 90s");
        Assert.Contains("€12.50", line);
        Assert.Contains("€150.00", line);
        Assert.DoesNotContain("€11.50", line);
    }

    private static async Task AddAsync(Screen screen, string search, string product, string quantity)
    {
        await screen.Type("product-search", search);
        await screen.Pick(product);
        await screen.Type("line-quantity", quantity);
        await screen.Click("Add to order");
        await screen.Until(() => LineTextOrNull(screen, product) is not null);
    }

    // The text of the order line for this product, from its name to the next line (or the end of the order).
    private static string LineText(Screen screen, string product) =>
        LineTextOrNull(screen, product) ?? throw new Xunit.Sdk.XunitException($"No order line for {product}.");

    private static string? LineTextOrNull(Screen screen, string product)
    {
        string text = screen.Renderer.Text(screen.Id);
        int order = text.IndexOf("On this order", StringComparison.Ordinal);
        if (order < 0) return null;
        int start = text.IndexOf(product, order, StringComparison.Ordinal);
        if (start < 0) return null;
        int end = text.IndexOf("Remove", start, StringComparison.Ordinal);
        return text[start..(end < 0 ? text.Length : end)];
    }
}
