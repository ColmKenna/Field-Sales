using System.Globalization;
using FieldSales.Quantities;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]

public sealed class QuantityTests
{
    [Theory]
    [InlineData("1.0")]
    [InlineData("1.5")]
    [InlineData("2.0")]
    public void Should_AcceptSteppedQuantities_When_KgUsesHalfStepAndMinimumOne(string text)
    {
        QuantityRules rules = Rules("kg", 0.5m, 1m);
        decimal amount = Number(text);
        QuantityValidationResult result = rules.ValidateOrder(amount);
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.Equal(Quantity.Create(UnitOfMeasure.Kilogram, amount), result.Quantity);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.5")]
    [InlineData("0.5")]
    [InlineData("0.7")]
    [InlineData("1.2")]
    [InlineData("1.499999")]
    [InlineData("1.500001")]
    public void Should_RejectQuantityWithMeasureHint_When_BelowMinimumOrBetweenSteps(string text)
    {
        QuantityValidationResult result = Rules("kg", 0.5m, 1m).ValidateOrder(Number(text));
        Assert.False(result.IsValid);
        Assert.Null(result.Quantity);
        Assert.Equal("Enter at least 1.0 kg in steps of 0.5 kg", result.Error);
    }

    [Theory]
    [InlineData("kg")]
    [InlineData("litre")]
    [InlineData("metre")]
    public void Should_DefaultMinimumToStep_When_MinimumIsBlank(string unit)
    {
        QuantityRules rules = Rules(unit, 0.5m, null);
        Assert.True(rules.ValidateOrder(0.5m).IsValid);
        Assert.False(rules.ValidateOrder(0m).IsValid);
        Assert.Equal($"Enter at least 0.5 {unit} in steps of 0.5 {unit}", rules.ValidateOrder(0m).Error);
    }

    [Fact]
    public void Should_RejectMinimum_When_NotAMultipleOfStep()
    {
        bool valid = QuantityRules.TryCreate("kg", 0.5m, 0.7m, out QuantityRules? rules, out var errors);
        Assert.False(valid);
        Assert.Null(rules);
        Assert.Single(errors);
        Assert.Equal("Minimum must be a multiple of the step", Assert.Single(errors["MinimumQuantity"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.5")]
    [InlineData("1.5")]
    public void Should_RequireWholePositiveQuantities_When_UnitIsEach(string text)
    {
        QuantityRules rules = Rules("Each", null, null);
        QuantityValidationResult result = rules.ValidateOrder(Number(text));
        Assert.False(result.IsValid);
        Assert.Null(result.Quantity);
        Assert.Equal("Enter a quantity of 1 or more", result.Error);
        Assert.True(rules.ValidateOrder(1m).IsValid);
        Assert.True(rules.ValidateOrder(2m).IsValid);
    }

    [Fact]
    public void Should_PreserveEachRules_When_HiddenMeasureFieldsAreSubmitted()
    {
        QuantityRules rules = Rules("Each", 0.25m, 0m);
        Assert.False(rules.ValidateOrder(0m).IsValid);
        Assert.False(rules.ValidateOrder(1.25m).IsValid);
        Assert.True(rules.ValidateOrder(1m).IsValid);
    }

    [Theory]
    [InlineData("kg", "0.25", "0.25", "0.75", "0.7")]
    [InlineData("litre", "0.1", "0.1", "0.3", "0.31")]
    [InlineData("metre", "0.25", "0.5", "1.25", "1.2")]
    public void Should_ValidateExactly_When_StepsUseQuarterOrTenthUnits(
        string unit, string step, string minimum, string valid, string invalid)
    {
        QuantityRules rules = Rules(unit, Number(step), Number(minimum));
        Assert.True(rules.ValidateOrder(Number(valid)).IsValid);
        Assert.False(rules.ValidateOrder(Number(invalid)).IsValid);
        QuantityRules tenth = Rules("litre", 0.1m, 0.1m);
        Assert.Equal(Quantity.Create(UnitOfMeasure.Litre, 0.3m), tenth.ValidateOrder(0.1m + 0.2m).Quantity);
    }

    [Fact]
    public void Should_EnforceQuantityBounds_When_ValueExceedsSupportedPrecision()
    {
        QuantityRules smallest = Rules("kg", Quantity.SmallestIncrement, Quantity.SmallestIncrement);
        Assert.True(smallest.ValidateOrder(Quantity.MaximumAmount).IsValid);
        Assert.False(smallest.ValidateOrder(Quantity.MaximumAmount + Quantity.SmallestIncrement).IsValid);
        Assert.False(smallest.ValidateOrder(1.0000001m).IsValid);
        Assert.True(smallest.ValidateOrder(0.000001m).IsValid);
        Assert.False(smallest.ValidateOrder(decimal.MaxValue).IsValid);
        QuantityRules each = Rules("Each", null, null);
        Assert.True(each.ValidateOrder(999_999_999_999m).IsValid);
        Assert.False(each.ValidateOrder(1_000_000_000_000m).IsValid);
        Assert.False(Quantity.TryCreate(UnitOfMeasure.Kilogram, -Quantity.SmallestIncrement, out _));
        Assert.False(Quantity.TryCreate((UnitOfMeasure)99, 1m, out _));
    }

    [Theory]
    [InlineData("kg", null, "1", "QuantityStep")]
    [InlineData("kg", "0", "1", "QuantityStep")]
    [InlineData("kg", "-0.5", "1", "QuantityStep")]
    [InlineData("kg", "0.0000001", "1", "QuantityStep")]
    [InlineData("kg", "1000000000000", "1", "QuantityStep")]
    [InlineData("kg", "0.5", "0", "MinimumQuantity")]
    [InlineData("kg", "0.5", "-1", "MinimumQuantity")]
    [InlineData("kg", "0.5", "1.0000001", "MinimumQuantity")]
    [InlineData("kg", "0.5", "1000000000000", "MinimumQuantity")]
    [InlineData("unsupported", "0.5", "1", "Unit")]
    [InlineData(null, "0.5", "1", "Unit")]
    public void Should_RejectQuantityRule_When_ConfigurationIsInvalid(
        string? unit, string? step, string? minimum, string field)
    {
        Assert.False(QuantityRules.TryCreate(unit, step is null ? null : Number(step),
            minimum is null ? null : Number(minimum), out QuantityRules? rules, out var errors));
        Assert.Null(rules);
        Assert.Contains(field, errors.Keys);
    }

    [Theory]
    [InlineData(UnitOfMeasure.Each)]
    [InlineData(UnitOfMeasure.Kilogram)]
    [InlineData(UnitOfMeasure.Litre)]
    [InlineData(UnitOfMeasure.Metre)]
    public void Should_AllowZeroStockCountAndRejectZeroOrder_When_UsingSameQuantityType(UnitOfMeasure unit)
    {
        Assert.True(Quantity.TryCreate(unit, 0m, out Quantity count));
        Assert.Equal(0m, count.Amount);
        Assert.Equal(unit, count.Unit);
        QuantityRules rules = Rules(unit.ToCode(), 0.5m, 1m);
        Assert.False(rules.ValidateOrder(count.Amount).IsValid);
        Assert.False(Quantity.TryCreate(UnitOfMeasure.Each, 1.5m, out _));
    }

    [Fact]
    public void Should_KeepRecordedUnit_When_ProductUsesDifferentRulesLater()
    {
        Quantity recorded = Rules("Each", null, null).ValidateOrder(12m).Quantity!.Value;
        Quantity newOrder = Rules("kg", 0.5m, 1m).ValidateOrder(12m).Quantity!.Value;
        Assert.Equal(UnitOfMeasure.Each, recorded.Unit);
        Assert.Equal(UnitOfMeasure.Kilogram, newOrder.Unit);
        Assert.NotEqual(recorded, newOrder);
        Assert.Equal(recorded.Amount, newOrder.Amount);
    }

    [Fact]
    public void Should_UseSpecifiedWording_When_ServerCultureUsesDecimalComma()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal("Enter at least 1.0 kg in steps of 0.5 kg", Rules("kg", 0.5m, 1m).ValidateOrder(0m).Error);
            Assert.Equal("Enter at least 0.25 litre in steps of 0.25 litre", Rules("litre", 0.25m, null).ValidateOrder(0m).Error);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static QuantityRules Rules(string unit, decimal? step, decimal? minimum)
    {
        Assert.True(QuantityRules.TryCreate(unit, step, minimum, out QuantityRules? rules, out var errors),
            string.Join("; ", errors.SelectMany(error => error.Value)));
        return rules!;
    }

    private static decimal Number(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
}
