namespace FieldSales.Catalogue.Contracts;

public sealed record SetProductClassificationRequest(Guid? ProfileId, Guid? PrimaryBrandId,
    IReadOnlyList<Guid>? AlternativeBrandIds, Guid? SupplierId, Guid? RestrictionGroupId);

public sealed record ProductClassificationChoices(IReadOnlyList<ProductReferenceItem> Profiles,
    IReadOnlyList<ProductReferenceItem> Brands, IReadOnlyList<ProductReferenceItem> Suppliers,
    IReadOnlyList<ProductReferenceItem> RestrictionGroups);
