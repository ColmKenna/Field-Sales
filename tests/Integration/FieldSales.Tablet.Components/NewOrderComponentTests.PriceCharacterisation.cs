using System.Text.RegularExpressions;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-064 characterisation scenario C2: the price shown while choosing a quantity, pinned before tiers and breaks reach
/// the tablet. With no tier and no break it is the base price, whatever quantity is typed. (The Low tab's and Record Call's
/// quantity popover read "€3.00 · list price" and are pinned by OrderLowTabTests and RecordCallLowToOrderTests.)</summary>
public sealed partial class NewOrderComponentTests
{
    // The quantity entry's own price, between the product's breadcrumb and the Quantity label.
    private static readonly Regex QuantityEntryPrice = new(@"VITD-1000-90 · Health > Vitamins\s*(?<price>€[0-9.]+)\s*Quantity");

    [Fact]
    public async Task Should_ShowTheBasePriceInThePopover_When_AddingAProduct()
    {
        await using Screen screen = Open(new());
        await screen.Show();
        await screen.Type("product-search", "vitamin d");
        await screen.Pick("Vitamin D 1000IU 90s");

        Assert.Equal("€12.50", QuantityEntryPrice.Match(screen.Renderer.Text(screen.Id)).Groups["price"].Value);
        await screen.Type("line-quantity", "30");
        Assert.Equal("€12.50", QuantityEntryPrice.Match(screen.Renderer.Text(screen.Id)).Groups["price"].Value);
    }
}
