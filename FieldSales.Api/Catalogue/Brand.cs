namespace FieldSales.Api.Catalogue;

public sealed class Brand : NamedReferenceItem
{
    private Brand() { }
    protected override string ItemLabel => "brand";

    public static Brand Create(string name)
    {
        Brand brand = new();
        brand.Initialize(name);
        return brand;
    }
}

public sealed class ProductAlternativeBrand
{
    private ProductAlternativeBrand() { }
    internal ProductAlternativeBrand(Guid productId, Guid brandId) => (ProductId, BrandId) = (productId, brandId);
    public Guid ProductId { get; private set; }
    public Guid BrandId { get; private set; }
}
