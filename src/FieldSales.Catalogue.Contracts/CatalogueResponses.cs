namespace FieldSales.Catalogue.Contracts;

public sealed record CategoryItem(Guid Id, Guid? ParentId, string Name, int Here = 0, int Beneath = 0);
public sealed record CategoryBreadcrumbSegment(Guid Id, string Name);
public sealed record CategoryDetails(CategoryItem Category,
    IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb,
    IReadOnlyList<CategoryItem> Children, IReadOnlyList<ProductItem> Products);
public sealed record CategorySearchResult(Guid Id, string Path);
public sealed record ProductCategoryChoice(Guid Id, string Path);
public sealed record ProductItem(Guid Id, string Code, string Name, Guid CategoryId, string Unit,
    decimal? QuantityStep = null, decimal? MinimumQuantity = null);
public sealed record ProductPriceItem(DateOnly EffectiveFrom, decimal Amount);
public sealed record ProductAttribute(string Name, string Value, bool IsArchived = false);
public sealed record ProductBrandItem(Guid Id, string Name, bool IsArchived, bool IsPrimary,
    bool IsAlternative = false);
public sealed record ProductReferenceItem(Guid Id, string Name, bool IsArchived);
public sealed record ProductDetails(ProductItem Product, IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb,
    ProductPriceItem? CurrentPrice, IReadOnlyList<ProductPriceItem> PriceHistory,
    IReadOnlyList<ProductAttribute> Attributes, IReadOnlyList<ProductBrandItem>? Brands = null,
    ProductReferenceItem? Profile = null, ProductReferenceItem? Supplier = null,
    ProductReferenceItem? RestrictionGroup = null, ProductClassificationChoices? ClassificationChoices = null,
    IReadOnlyList<ProductReferenceItem>? AttributeNameChoices = null, IReadOnlyList<ProductQuantityBreakItem>? QuantityBreaks = null);

/// <summary>One quantity break on the product record (WI-062, H-22), sorted by quantity. The flags say it will never apply: a cheaper
/// break sits at a smaller quantity, or it is above today's base price. Both are worked out when read; the break saves anyway.</summary>
public sealed record ProductQuantityBreakItem(decimal FromQuantity, decimal Price, bool AboveCheaperBreak, bool AboveBase);

/// <summary>A product's whole set of quantity breaks, replacing the ones it has; an empty list removes them all.</summary>
public sealed record SetProductQuantityBreaksRequest(IReadOnlyList<QuantityBreakRequestItem>? Breaks);

public sealed record QuantityBreakRequestItem(decimal? FromQuantity, decimal? Price);
public sealed record ProductValidationErrors(Dictionary<string, string[]> Errors);
public sealed record ProductSaveError(string Field, string Error);
public sealed record CatalogueError(string Error);
