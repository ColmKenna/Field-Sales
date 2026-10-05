namespace FieldSales.Catalogue.Contracts;

public sealed record ProductSearchItem(Guid Id, string Code, string Name,
    IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb, ProductReferenceItem? PrimaryBrand,
    string State, string Unit, ProductPriceItem? CurrentPrice);
public sealed record ProductSearchResponse(IReadOnlyList<ProductSearchItem> Items,
    IReadOnlyList<ProductCategoryChoice> Categories, IReadOnlyList<ProductReferenceItem> Brands);
