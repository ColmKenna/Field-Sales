using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]

public sealed class ProductPriceHistoryTests
{
    [Fact]
    public void Should_KeepPreviousPrice_When_DayPrecedesChange()
    {
        Product product = Create(new DateOnly(2026, 9, 1), 12.50m);
        ProductBasePrice original = Assert.Single(product.BasePrices);
        product.AddBasePrice(13.20m, new DateOnly(2026, 11, 1));

        Assert.Same(original, product.BasePriceOn(new DateOnly(2026, 10, 31)));
        Assert.Equal(13.20m, product.BasePriceOn(new DateOnly(2026, 11, 1))!.Amount);
        Assert.Equal(12.50m, original.Amount);
        Assert.Equal(2, product.BasePrices.Count);
        Assert.Null(product.BasePriceOn(new DateOnly(2026, 8, 31)));
    }

    [Fact]
    public void Should_ResolveConsecutiveRanges_When_TwoFuturePricesExist()
    {
        Product product = Create(new DateOnly(2026, 9, 1), 12.50m);
        // Insert out of order to prove resolution and ranges use dates, not list position.
        product.AddBasePrice(14m, new DateOnly(2026, 12, 1));
        product.AddBasePrice(13.20m, new DateOnly(2026, 11, 1));

        Assert.Collection(product.BasePricePeriods(),
            period => AssertPeriod(period, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31), 12.50m),
            period => AssertPeriod(period, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), 13.20m),
            period => AssertPeriod(period, new DateOnly(2026, 12, 1), null, 14m));
        Assert.Equal(12.50m, product.BasePriceOn(new DateOnly(2026, 10, 31))!.Amount);
        Assert.Equal(13.20m, product.BasePriceOn(new DateOnly(2026, 11, 30))!.Amount);
        Assert.Equal(14m, product.BasePriceOn(new DateOnly(2026, 12, 1))!.Amount);
    }

    [Fact]
    public void Should_InsertPastPrice_When_DateFallsBetweenExistingEntries()
    {
        Product product = Create(new DateOnly(2026, 8, 1), 11m);
        product.AddBasePrice(13.20m, new DateOnly(2026, 11, 1));
        ProductBasePrice[] originals = product.BasePrices.ToArray();
        product.AddBasePrice(12m, new DateOnly(2026, 9, 1));

        Assert.Collection(product.BasePricePeriods(),
            period => AssertPeriod(period, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 11m),
            period => AssertPeriod(period, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31), 12m),
            period => AssertPeriod(period, new DateOnly(2026, 11, 1), null, 13.20m));
        Assert.Equal(12m, product.BasePriceOn(new DateOnly(2026, 9, 1))!.Amount);
        Assert.Equal(12m, product.BasePriceOn(new DateOnly(2026, 10, 31))!.Amount);
        Assert.Same(originals[0], product.BasePriceOn(new DateOnly(2026, 8, 31)));
        Assert.Same(originals[1], product.BasePriceOn(new DateOnly(2026, 11, 1)));
        Assert.Equal(11m, originals[0].Amount);
        Assert.Equal(13.20m, originals[1].Amount);
    }

    [Fact]
    public void Should_PreserveInterveningPrice_When_EarlierPastPriceIsAdded()
    {
        // The existing September entry limits the retroactive entry's range.
        Product product = Create(new DateOnly(2026, 9, 30), 12.50m);
        product.AddBasePrice(13.20m, new DateOnly(2026, 11, 1));
        product.AddBasePrice(12m, new DateOnly(2026, 9, 1));

        Assert.Collection(product.BasePricePeriods(),
            period => AssertPeriod(period, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 29), 12m),
            period => AssertPeriod(period, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31), 12.50m),
            period => AssertPeriod(period, new DateOnly(2026, 11, 1), null, 13.20m));
        Assert.Equal(12.50m, product.BasePriceOn(new DateOnly(2026, 10, 1))!.Amount);
    }

    [Fact]
    public void Should_UseNewPrice_When_EffectiveDateIsToday()
    {
        DateOnly today = new(2026, 9, 30);
        Product product = Create(today.AddDays(-1), 12.50m);
        product.AddBasePrice(13.20m, today);

        Assert.Equal(12.50m, product.BasePriceOn(today.AddDays(-1))!.Amount);
        Assert.Equal(13.20m, product.BasePriceOn(today)!.Amount);
    }

    [Fact]
    public void Should_RejectDuplicateDate_When_PriceAlreadyStartsThatDay()
    {
        Product product = Create(new DateOnly(2026, 9, 1), 12.50m);
        product.AddBasePrice(13.20m, new DateOnly(2026, 11, 1));
        ProductBasePrice[] original = product.BasePrices.ToArray();

        Assert.Throws<InvalidOperationException>(() =>
            product.AddBasePrice(14m, new DateOnly(2026, 11, 1)));
        Assert.Equal(original, product.BasePrices);
        Assert.Equal(13.20m, product.BasePriceOn(new DateOnly(2026, 11, 1))!.Amount);
    }

    private static Product Create(DateOnly effectiveFrom, decimal amount) =>
        Product.Create("SUN-0342", "Lotion", Guid.NewGuid(), amount, effectiveFrom);

    private static void AssertPeriod(ProductPricePeriod period, DateOnly from, DateOnly? through, decimal amount)
    {
        Assert.Equal(from, period.Price.EffectiveFrom);
        Assert.Equal(through, period.EffectiveThrough);
        Assert.Equal(amount, period.Price.Amount);
    }
}
