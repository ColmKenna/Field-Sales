using FieldSales.Pricing;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-064 agreed scenario 4 on the page (S6, criterion 2), and quantity entry's half of scenario 5 (criterion 3): the Order
/// Pad and search show each product at this shop's price, and quantity entry shows the winner at the quantity typed.</summary>
public sealed partial class NewOrderComponentTests
{
    // Tier B (8% off) for the shop's customer, and a break on Vitamin D from 10 at €11.00.
    private static LocationPrices TierBWithBreak => new(new DateOnly(2026, 10, 29), [new PercentageTier("Tier B", 8m)],
        new Dictionary<Guid, IReadOnlyList<QuantityBreak>> { [Catalogue[0].Id] = [new(10m, 11.00m)] });

    [Fact]
    public async Task Should_ShowTheCustomersPriceOnThePad_When_Offline()
    {
        await using Screen screen = Open(new());
        screen.Tablet.ReadPrices = (_, shop) => shop == ShopId ? TierBWithBreak : LocationPrices.None;
        await screen.Show();

        await screen.Type("product-search", "vitamin");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("2 matching products"));

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("€11.50", text); // Vitamin D at Tier B: 8% off €12.50, not the base.
        Assert.DoesNotContain("€12.50", text);
        Assert.Contains("No price yet", text); // Vitamin C: nothing prices it.
    }

    [Fact]
    public async Task Should_ShowTheWinnerAtTheTypedQuantity_When_TheQuantityEnteredReachesABreak()
    {
        await using Screen screen = Open(new());
        screen.Tablet.ReadPrices = (_, _) => TierBWithBreak;
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");

        Assert.Equal("€11.50", QuantityEntryPrice.Match(screen.Renderer.Text(screen.Id)).Groups["price"].Value);
        await screen.Type("line-quantity", "12");
        Assert.Equal("€11.00", QuantityEntryPrice.Match(screen.Renderer.Text(screen.Id)).Groups["price"].Value);
    }
}
