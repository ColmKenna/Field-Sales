namespace FieldSales.Api.Catalogue;

public sealed record ProductAttribute(string Name, string Value);

public sealed class Product
{
    public const int MaximumCodeLength = 100;
    public const int MaximumNameLength = 200;

    private readonly List<ProductBasePrice> _basePrices = [];

    // EF Core materializes persisted products through this constructor.
    private Product() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public string Unit { get; private set; } = "Each";
    public Guid? ParentProductId { get; private set; }
    public IReadOnlyList<ProductAttribute> Attributes { get; private set; } = [];
    public IReadOnlyList<ProductBasePrice> BasePrices => _basePrices.AsReadOnly();

    public static Product Create(string code, string name, Guid categoryId,
        decimal basePrice, DateOnly effectiveFrom)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > MaximumCodeLength)
            throw new ArgumentException("Enter a product code of up to 100 characters.", nameof(code));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaximumNameLength)
            throw new ArgumentException("Enter a product name of up to 200 characters.", nameof(name));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Choose a category", nameof(categoryId));

        Product product = new()
        {
            Id = Guid.NewGuid(), Code = code.Trim(), Name = name.Trim(), CategoryId = categoryId
        };
        product._basePrices.Add(ProductBasePrice.Create(product.Id, basePrice, effectiveFrom));
        return product;
    }

    /// <summary>Returns the latest base price effective on or before the supplied business date.</summary>
    /// <remarks>The caller must load BasePrices. No price exists before the first entry.</remarks>
    public ProductBasePrice? BasePriceOn(DateOnly date) =>
        _basePrices.Where(price => price.EffectiveFrom <= date).MaxBy(price => price.EffectiveFrom);
}
