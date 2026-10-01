namespace FieldSales.Web.Catalogue;

public sealed class ProductClassificationInput
{
    public Guid? ProfileId { get; set; }
    public Guid? PrimaryBrandId { get; set; }
    public List<Guid> AlternativeBrandIds { get; set; } = [];
    public Guid? SupplierId { get; set; }
    public Guid? RestrictionGroupId { get; set; }

    public SetProductClassificationRequest ToRequest() => new(ProfileId, PrimaryBrandId,
        AlternativeBrandIds, SupplierId, RestrictionGroupId);

    public static ProductClassificationInput From(ProductDetails details) => new()
    {
        ProfileId = details.Profile?.Id,
        PrimaryBrandId = details.Brands?.SingleOrDefault(brand => brand.IsPrimary)?.Id,
        AlternativeBrandIds = details.Brands?.Where(brand => !brand.IsPrimary || brand.IsAlternative)
            .Select(brand => brand.Id).ToList() ?? [],
        SupplierId = details.Supplier?.Id,
        RestrictionGroupId = details.RestrictionGroup?.Id
    };
}
