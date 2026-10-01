namespace FieldSales.Api.Catalogue;

public sealed class Brand
{
    public const int MaximumNameLength = 200;
    private Brand() { }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public byte[] Version { get; private set; } = [];

    public static Brand Create(string name)
    {
        Brand brand = new() { Id = Guid.NewGuid() };
        brand.Rename(name);
        return brand;
    }
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaximumNameLength)
            throw new ArgumentException("Enter a brand name of up to 200 characters.", nameof(name));
        Name = name.Trim();
    }
    public void Archive() => IsArchived = true;
    public void Unarchive() => IsArchived = false;
}

public sealed class ProductAlternativeBrand
{
    private ProductAlternativeBrand() { }
    internal ProductAlternativeBrand(Guid productId, Guid brandId) => (ProductId, BrandId) = (productId, brandId);
    public Guid ProductId { get; private set; }
    public Guid BrandId { get; private set; }
}
