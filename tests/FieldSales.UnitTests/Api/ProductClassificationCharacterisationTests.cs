using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Tests;

public sealed class ProductClassificationCharacterisationTests
{
    [Fact]
    public void Should_KeepOptionalClassification_When_ProductIsCreated()
    {
        Product product = NewProduct();
        Assert.Null(product.ProductProfileId);
        Assert.Null(product.PrimaryBrandId);
        Assert.Empty(product.AlternativeBrands);
        Assert.Null(product.SupplierId);
        Assert.Null(product.RestrictionGroupId);
    }

    [Fact]
    public void Should_AllowSameBrandInBothRoles_When_PrimaryAlsoAppearsAsAlternative()
    {
        Product product = NewProduct();
        Brand brand = Brand.Create("SunCo");
        product.SetBrands(brand, [brand]);
        Assert.Equal(brand.Id, product.PrimaryBrandId);
        Assert.Equal(brand.Id, Assert.Single(product.AlternativeBrands).BrandId);
    }

    [Fact]
    public void Should_PreserveBrands_When_PrimaryIsRemovedOrAlternativeIsRepeated()
    {
        Product product = NewProduct();
        Brand primary = Brand.Create("SunCo");
        Brand alternative = Brand.Create("GlowCo");
        product.SetBrands(primary, [alternative]);
        Assert.Throws<ArgumentException>(() => product.SetBrands(null, [alternative]));
        Assert.Throws<ArgumentException>(() => product.SetBrands(primary, [alternative, alternative]));
        Assert.Equal(primary.Id, product.PrimaryBrandId);
        Assert.Equal(alternative.Id, Assert.Single(product.AlternativeBrands).BrandId);
    }

    [Fact]
    public void Should_RetainArchivedReferences_When_ExistingClassificationIsUnchanged()
    {
        Product product = NewProduct();
        ProductProfile profile = ProductProfile.Create("Frozen");
        Supplier supplier = Supplier.Create("Irish Health Supplies");
        RestrictionGroup group = RestrictionGroup.Create("Pharmacy-only medicines");
        Brand brand = Brand.Create("SunCo");
        product.SetClassification(profile, supplier);
        product.SetRestrictionGroup(group);
        product.SetBrands(brand, [brand]);
        profile.Archive(); supplier.Archive(); group.Archive(); brand.Archive();
        product.SetClassification(profile, supplier);
        product.SetRestrictionGroup(group);
        product.SetBrands(brand, [brand]);
        Assert.Equal(profile.Id, product.ProductProfileId);
        Assert.Equal(supplier.Id, product.SupplierId);
        Assert.Equal(group.Id, product.RestrictionGroupId);
        Assert.Equal(brand.Id, product.PrimaryBrandId);
        Assert.Equal(brand.Id, Assert.Single(product.AlternativeBrands).BrandId);
        Assert.Equal(12.50m, Assert.Single(product.BasePrices).Amount);
        Assert.Equal("Each", product.Unit);
        Product other = NewProduct();
        Assert.Throws<ArgumentException>(() => other.SetClassification(profile, null));
        Assert.Throws<ArgumentException>(() => other.SetClassification(null, supplier));
        Assert.Throws<ArgumentException>(() => other.SetRestrictionGroup(group));
        Assert.Throws<ArgumentException>(() => other.SetBrands(brand, []));
    }

    private static Product NewProduct() => Product.Create("SUN-0342", "Sun Lotion", Guid.NewGuid(),
        12.50m, new DateOnly(2026, 10, 1));
}
