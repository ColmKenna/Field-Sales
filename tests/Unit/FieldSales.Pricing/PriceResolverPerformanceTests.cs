using System.Diagnostics;
using FieldSales.Pricing;

namespace FieldSales.Pricing.Tests;

/// <summary>WI-058 agreed scenario 19 (T-6.4.1 increment 5): pricing a whole Order Pad must not delay it. The budget (50 ms for 260
/// products, each against 3 tiers, at quantity 1) is inferred and measured on the build machine, not on the target tablet.</summary>
[Trait("Category", "Unit")]
public sealed class PriceResolverPerformanceTests
{
    [Fact]
    public void Should_Resolve260ProductsWithinBudget_When_TheOrderPadPricesEveryProductAtQuantity1()
    {
        DateOnly day = new(2026, 9, 22);
        PricedProduct[] pad = [.. Enumerable.Range(0, 260).Select(index => new PricedProduct(Guid.NewGuid(), index % 5 == 0 ? "kg" : "Each",
            [new(day.AddDays(-400), 9.99m + index), new(day.AddDays(-30), 10.49m + index), new(day.AddDays(30), 11.29m + index)],
            [new(10m, 9.50m + index), new(24m, 9.10m + index)]))];
        PriceTier[] tiers =
        [
            new PercentageTier("Tier B", 10m, pad.Where((_, index) => index % 3 == 0).ToDictionary(product => product.Id, _ => TierOverride.Percent(15m))),
            new PercentageTier("Symbol Group A", 12.5m, pad.Where((_, index) => index % 7 == 0).ToDictionary(product => product.Id, _ => TierOverride.Fixed(8.00m))),
            new PriceListTier("Suncare Deal 2027", pad.Where((_, index) => index % 4 == 0).ToDictionary(product => product.Id, _ => 9.25m))
        ];
        PriceAll(pad, tiers, day); // The first call pays for loading and JIT, as the app's start does.

        Stopwatch timer = Stopwatch.StartNew();
        ResolvedPrice?[] prices = PriceAll(pad, tiers, day);
        timer.Stop();

        Assert.All(prices, price => Assert.NotNull(price));
        Assert.True(timer.ElapsedMilliseconds < 50, $"Pricing the pad took {timer.ElapsedMilliseconds} ms.");
    }

    private static ResolvedPrice?[] PriceAll(PricedProduct[] pad, PriceTier[] tiers, DateOnly day) =>
        [.. pad.Select(product => PriceResolver.Resolve(product, tiers, 1m, day))];
}
