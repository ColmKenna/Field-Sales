using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-054 agreed scenarios R1–R12: the rule behind "Usually about [N] here" on Review (T-5.1.6, US-012 S14–S16). The history
/// is the product's quantity on each of the shop's last 3 accepted orders that contained it.</summary>
[Trait("Category", "Unit")]
public sealed class QuantityMarkerTests
{
    private static OrderLine Line(decimal quantity, string unit = "Each") => new(Guid.NewGuid(), Guid.NewGuid(), "P-01", "Product",
        quantity, unit, new(1m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel, []), new("1.5", DateTimeOffset.UnixEpoch));

    private static OrderedQuantity[] Each(params decimal[] quantities) => [.. quantities.Select(quantity => new OrderedQuantity(quantity, "Each"))];

    // R1 (S14): a 10× slip, 48 typed as 480.
    [Fact]
    public void Should_ShowUsuallyAbout48Here_When_480IsOrderedAndTheLast3OrdersHad48_50_44() =>
        Assert.Equal("Usually about 48 here", QuantityMarker.For(Line(480m), Each(48m, 50m, 44m), 1m));

    // R2 (S15).
    [Fact]
    public void Should_ShowNoMarker_When_TheProductIsOnNoneOfTheLast3Orders() =>
        Assert.Null(QuantityMarker.For(Line(480m), [], 1m));

    // R3: one order is no pattern (D1).
    [Fact]
    public void Should_ShowNoMarker_When_TheProductIsOnOnly1OfTheLast3Orders() =>
        Assert.Null(QuantityMarker.For(Line(480m), Each(48m), 1m));

    // R4 (S16): high only.
    [Fact]
    public void Should_ShowNoMarker_When_TheQuantityIsWellBelowUsual() =>
        Assert.Null(QuantityMarker.For(Line(4m), Each(48m, 50m, 44m), 1m));

    // R5: the boundary is "more than 3×".
    [Fact]
    public void Should_ShowNoMarker_When_TheQuantityIsExactly3TimesUsual() =>
        Assert.Null(QuantityMarker.For(Line(144m), Each(48m, 50m, 44m), 1m));

    // R6.
    [Fact]
    public void Should_ShowUsuallyAbout48Here_When_145IsOrderedAgainstAUsual48() =>
        Assert.Equal("Usually about 48 here", QuantityMarker.For(Line(145m), Each(48m, 50m, 44m), 1m));

    // R7: with two orders, the usual quantity is their average.
    [Fact]
    public void Should_UseTheAverageOfTheTwo_When_TheProductIsOn2OfTheLast3Orders()
    {
        Assert.Equal("Usually about 48 here", QuantityMarker.For(Line(145m), Each(40m, 56m), 1m));
        Assert.Null(QuantityMarker.For(Line(144m), Each(40m, 56m), 1m));
    }

    // R8: no absolute floor.
    [Fact]
    public void Should_MarkTheLine_When_TheShopUsuallyTakes1AndThisOrderIs4()
    {
        Assert.Equal("Usually about 1 here", QuantityMarker.For(Line(4m), Each(1m, 1m, 2m), 1m));
        Assert.Null(QuantityMarker.For(Line(3m), Each(1m, 1m, 2m), 1m));
    }

    // R9: a measured product's usual quantity is rounded to its step and shown with its unit.
    [Fact]
    public void Should_RoundToTheStep_When_AMeasuredProductIsMarked() =>
        Assert.Equal("Usually about 2.5 kg here",
            QuantityMarker.For(Line(8m, "kg"), [new(2.0m, "kg"), new(2.5m, "kg")], 0.5m));

    // R10: halves round up.
    [Fact]
    public void Should_RoundToAWholeNumber_When_TheAverageOfTwoIsAHalf() =>
        Assert.Equal("Usually about 48 here", QuantityMarker.For(Line(480m), Each(47m, 48m), 1m));

    // R11 (D3): orders taken before the product's unit changed don't count, so here only one order is left.
    [Fact]
    public void Should_IgnoreOrdersInAnotherUnit_When_TheProductsUnitChanged()
    {
        OrderedQuantity[] history = [new(2.0m, "kg"), new(48m, "Each"), new(50m, "Each")];

        Assert.Null(QuantityMarker.For(Line(30m, "kg"), history, 0.5m));
        Assert.Equal("Usually about 49 here", QuantityMarker.For(Line(480m), history, 1m));
    }

    // R12: the checkpoint question; an ordinary seasonal peak isn't marked.
    [Fact]
    public void Should_ShowNoMarker_When_ASeasonalPeakIs2AndAHalfTimesUsual() =>
        Assert.Null(QuantityMarker.For(Line(120m), Each(48m, 50m, 44m), 1m));
}
