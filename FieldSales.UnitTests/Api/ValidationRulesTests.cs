using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Tests;

public sealed class ValidationRulesTests
{
    public static TheoryData<decimal, bool> Prices => new()
    {
        { -0.01m, false }, { 0m, true }, { 0.001m, false },
        { ProductBasePrice.MaximumAmount, true }, { ProductBasePrice.MaximumAmount + 0.01m, false }
    };

    [Theory]
    [MemberData(nameof(Prices))]
    public void DomainAndRequestValidation_AgreeOnPriceBoundaries(decimal amount, bool valid)
    {
        Assert.Equal(valid, ProductBasePrice.IsValidAmount(amount));
        if (valid) Assert.Equal(amount, ProductBasePrice.Create(Guid.NewGuid(), amount, new(2026, 1, 1)).Amount);
        else Assert.Throws<ArgumentOutOfRangeException>(() => ProductBasePrice.Create(Guid.NewGuid(), amount, new(2026, 1, 1)));
    }

    [Fact]
    public void NameBoundary_UsesTrimmedLengthAndBreadcrumbKeepsAncestry()
    {
        Assert.True(NameRules.IsValid(" " + new string('n', 200) + " ", 200));
        Assert.False(NameRules.IsValid(new string('n', 201), 200));
        Assert.False(NameRules.IsValid(" ", 200));
        CategoryTree tree = new([]);
        Category root = tree.Add("Root");
        Category child = tree.Add("Child", root.Id);
        Category leaf = tree.Add("Leaf", child.Id);
        Assert.Equal("Root > Child > Leaf", tree.Path(leaf.Id));
    }
}
