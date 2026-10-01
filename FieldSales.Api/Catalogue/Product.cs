using FieldSales.Quantities;

namespace FieldSales.Api.Catalogue;

public sealed record ProductAttribute(string Name, string Value, bool IsArchived = false);

public sealed class Product
{
    public const int MaximumCodeLength = 100;
    public const int MaximumNameLength = 200;

    private readonly List<ProductBasePrice> _basePrices = [];
    private readonly List<ProductAlternativeBrand> _alternativeBrands = [];
    private readonly List<ProductAttributeValue> _attributeValues = [];

    // EF Core materializes persisted products through this constructor.
    private Product() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Guid? PrimaryBrandId { get; private set; }
    public Guid? ProductProfileId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public IReadOnlyList<ProductAlternativeBrand> AlternativeBrands => _alternativeBrands.AsReadOnly();
    public string Unit { get; private set; } = "Each";
    public decimal? QuantityStep { get; private set; }
    public decimal? MinimumQuantity { get; private set; }
    public Guid? ParentProductId { get; private set; }
    public IReadOnlyList<ProductAttributeValue> AttributeValues => _attributeValues.AsReadOnly();
    // Readers load AttributeValues and their AttributeName navigation.
    public IReadOnlyList<ProductAttribute> Attributes => _attributeValues.OrderBy(value => value.Position)
        .Select(value => new ProductAttribute(value.AttributeName.Name, value.Value, value.AttributeName.IsArchived)).ToArray();
    public IReadOnlyList<ProductBasePrice> BasePrices => _basePrices.AsReadOnly();

    public static Product Create(string code, string name, Guid categoryId,
        decimal basePrice, DateOnly effectiveFrom, QuantityRules? quantityRules = null)
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
        if (quantityRules is not null) product.SetQuantityRules(quantityRules, hasOrders: false);
        return product;
    }

    public void SetQuantityRules(QuantityRules rules, bool hasOrders)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (hasOrders && Unit != rules.Unit.ToCode())
            throw new InvalidOperationException("This product has orders. Its unit cannot be changed yet.");
        Unit = rules.Unit.ToCode();
        QuantityStep = rules.Unit == UnitOfMeasure.Each ? null : rules.Step.Amount;
        MinimumQuantity = rules.Unit == UnitOfMeasure.Each ? null : rules.Minimum.Amount;
    }

    public QuantityRules GetQuantityRules()
    {
        if (!QuantityRules.TryCreate(Unit, QuantityStep, MinimumQuantity, out QuantityRules? rules, out _))
            throw new InvalidOperationException("The saved product has invalid quantity rules.");
        return rules;
    }

    public void SetClassification(ProductProfile? profile, Supplier? supplier)
    {
        if (profile is { IsArchived: true } && ProductProfileId != profile.Id)
            throw new ArgumentException("Archived product profiles cannot be selected for new references.", nameof(profile));
        if (supplier is { IsArchived: true } && SupplierId != supplier.Id)
            throw new ArgumentException("Archived suppliers cannot be selected for new references.", nameof(supplier));
        ProductProfileId = profile?.Id;
        SupplierId = supplier?.Id;
    }

    public void AddAttribute(AttributeName name, string value) => _attributeValues.Add(
        new ProductAttributeValue(Id, name, value, _attributeValues.Count == 0 ? 0 : _attributeValues.Max(value => value.Position) + 1));

    // Caller loads existing links and checks brand state inside the shared transaction.
    public void SetBrands(Brand? primary, IReadOnlyList<Brand> alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);
        if (alternatives.Count > 0 && primary is null)
            throw new ArgumentException("Choose a primary brand when alternative brands are supplied.", nameof(primary));
        if (alternatives.Select(brand => brand.Id).Distinct().Count() != alternatives.Count)
            throw new ArgumentException("Choose each alternative brand once.", nameof(alternatives));
        var previous = _alternativeBrands.Select(link => link.BrandId).ToHashSet();
        if (PrimaryBrandId is Guid previousPrimary) previous.Add(previousPrimary);
        IEnumerable<Brand> selected = primary is null ? alternatives : alternatives.Prepend(primary);
        if (selected.Any(brand => brand.IsArchived && !previous.Contains(brand.Id)))
            throw new ArgumentException("Archived brands cannot be selected for new references.", nameof(alternatives));
        PrimaryBrandId = primary?.Id;
        var next = alternatives.Select(brand => brand.Id).ToHashSet();
        _alternativeBrands.RemoveAll(link => !next.Contains(link.BrandId));
        foreach (Guid id in next.Where(id => !_alternativeBrands.Any(link => link.BrandId == id)))
            _alternativeBrands.Add(new ProductAlternativeBrand(Id, id));
    }

    /// <summary>Returns the latest base price effective on or before the supplied business date.</summary>
    /// <remarks>The caller must load BasePrices. No price exists before the first entry.</remarks>
    public ProductBasePrice? BasePriceOn(DateOnly date) =>
        _basePrices.Where(price => price.EffectiveFrom <= date).MaxBy(price => price.EffectiveFrom);

    /// <summary>Adds a dated entry without changing any existing price.</summary>
    /// <remarks>The caller must load BasePrices. The database key also enforces date uniqueness.</remarks>
    public void AddBasePrice(decimal amount, DateOnly effectiveFrom)
    {
        if (_basePrices.Any(price => price.EffectiveFrom == effectiveFrom))
            throw new InvalidOperationException("A base price already starts on this date.");
        _basePrices.Add(ProductBasePrice.Create(Id, amount, effectiveFrom));
    }

    /// <summary>Derives inclusive ranges in date order; the final entry has no end date.</summary>
    /// <remarks>The caller must load BasePrices. No range exists before the first entry.</remarks>
    public IReadOnlyList<ProductPricePeriod> BasePricePeriods()
    {
        ProductBasePrice[] prices = _basePrices.OrderBy(price => price.EffectiveFrom).ToArray();
        return prices.Select((price, index) => new ProductPricePeriod(price,
            index + 1 < prices.Length ? prices[index + 1].EffectiveFrom.AddDays(-1) : null)).ToArray();
    }
}
