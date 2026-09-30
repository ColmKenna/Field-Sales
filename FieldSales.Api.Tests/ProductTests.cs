using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Tests;

public sealed class ProductTests
{
    [Fact]
    public void Should_SeedInitialPriceAndNormalizeText_When_MinimumProductIsCreated()
    {
        DateOnly today = new(2026, 9, 30);
        Product product = Product.Create(" SUN-0342 ", " SPF30 Sun Lotion v2 200ml ",
            Guid.NewGuid(), 12.50m, today);

        Assert.Equal("SUN-0342", product.Code);
        Assert.Equal("SPF30 Sun Lotion v2 200ml", product.Name);
        Assert.Equal("Each", product.Unit);
        Assert.Null(product.ParentProductId);
        Assert.Empty(product.Attributes);
        ProductBasePrice price = Assert.Single(product.BasePrices);
        Assert.Equal(product.Id, price.ProductId);
        Assert.Equal(today, price.EffectiveFrom);
        Assert.Equal(12.50m, price.Amount);
    }

    [Fact]
    public void Should_ReturnApplicablePrice_When_EffectiveDateIsReached()
    {
        DateOnly start = new(2026, 9, 30);
        Product product = Product.Create("SUN-0342", "Lotion", Guid.NewGuid(), 12.50m, start);

        Assert.Null(product.BasePriceOn(start.AddDays(-1)));
        Assert.Equal(12.50m, product.BasePriceOn(start)!.Amount);
        Assert.Equal(12.50m, product.BasePriceOn(start.AddDays(1))!.Amount);
    }

    [Theory]
    [InlineData("", "Lotion")]
    [InlineData(" ", "Lotion")]
    [InlineData("SUN-0342", "")]
    [InlineData("SUN-0342", " ")]
    public void Should_RejectSave_When_RequiredTextIsMissing(string code, string name) =>
        Assert.Throws<ArgumentException>(() => Product.Create(code, name, Guid.NewGuid(),
            12.50m, new DateOnly(2026, 9, 30)));

    [Fact]
    public void Should_RejectSave_When_CodeOrNameIsTooLong()
    {
        Assert.Throws<ArgumentException>(() => Product.Create(new string('C', 101), "Lotion",
            Guid.NewGuid(), 12.50m, new DateOnly(2026, 9, 30)));
        Assert.Throws<ArgumentException>(() => Product.Create("SUN-0342", new string('N', 201),
            Guid.NewGuid(), 12.50m, new DateOnly(2026, 9, 30)));
        Product accepted = Product.Create(new string('C', 100), new string('N', 200),
            Guid.NewGuid(), 12.50m, new DateOnly(2026, 9, 30));
        Assert.Single(accepted.BasePrices);
    }

    [Fact]
    public void Should_RejectSave_When_CategoryIsMissing()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Product.Create(
            "SUN-0342", "Lotion", Guid.Empty, 12.50m, new DateOnly(2026, 9, 30)));
        Assert.StartsWith("Choose a category", exception.Message);
    }

    [Fact]
    public void Should_ApplyPriceValidation_When_AmountIsZeroNegativeOrOverPrecision()
    {
        DateOnly today = new(2026, 9, 30);
        Guid category = Guid.NewGuid();
        Assert.Equal(0m, Product.Create("ZERO", "Free sample", category, 0m, today)
            .BasePriceOn(today)!.Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => Product.Create("NEG", "Lotion", category, -1m, today));
        Assert.Throws<ArgumentOutOfRangeException>(() => Product.Create("PREC", "Lotion", category, 12.501m, today));
        Assert.Throws<ArgumentOutOfRangeException>(() => Product.Create("MAX", "Lotion", category,
            ProductBasePrice.MaximumAmount + 0.01m, today));
    }
}
