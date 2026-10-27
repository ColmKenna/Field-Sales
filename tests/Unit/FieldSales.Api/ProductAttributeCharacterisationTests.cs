using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]
public sealed class ProductAttributeCharacterisationTests
{
    [Fact]
    public void Should_PreserveRepeatedNamesOrderAndExactValues_When_AttributesAreAdded()
    {
        Product product = NewProduct();
        AttributeName weight = AttributeName.Create("Weight");
        AttributeName shelfLife = AttributeName.Create("Shelf life");
        string value = "  Quoted \"value\" ·\n" + new string('界', 6000);
        product.AddAttribute(weight, "200 g");
        product.AddAttribute(shelfLife, "");
        product.AddAttribute(weight, value);

        Assert.Equal(["Weight", "Shelf life", "Weight"], product.Attributes.Select(row => row.Name));
        Assert.Equal(["200 g", "", value], product.Attributes.Select(row => row.Value));
        Assert.Equal([0, 1, 2], product.AttributeValues.Select(row => row.Position));
        Assert.Equal(12.50m, Assert.Single(product.BasePrices).Amount);
        Assert.Equal("Each", product.Unit);
    }

    [Fact]
    public void Should_RetainAndLabelExistingValues_When_NameIsRenamedThenArchived()
    {
        Product product = NewProduct();
        AttributeName name = AttributeName.Create("Weight");
        product.AddAttribute(name, "200 g");
        name.Rename("Mass");
        name.Archive();

        Assert.Equal(new ProductAttribute("Mass", "200 g", true), Assert.Single(product.Attributes));
        Assert.Throws<ArgumentException>(() => product.AddAttribute(name, "300 g"));
        Assert.Throws<ArgumentNullException>(() => product.AddAttribute(AttributeName.Create("Barcode"), null!));
        Assert.Single(product.Attributes);
        Assert.Equal(name.Id, Assert.Single(product.AttributeValues).AttributeNameId);
    }

    private static Product NewProduct() => Product.Create("SUN-0342", "Sun Lotion", Guid.NewGuid(),
        12.50m, new DateOnly(2026, 10, 1));
}
