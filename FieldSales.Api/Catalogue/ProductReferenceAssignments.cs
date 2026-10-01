using System.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public enum ProductClassificationSaveStatus { Saved, Missing, Invalid }
public sealed record ProductClassificationSaveResult(ProductClassificationSaveStatus Status,
    IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>Loads references and updates products inside the same serializable transaction as retirement.</summary>
public sealed class ProductReferenceAssignments(CatalogueDbContext db)
{
    public Task<ProductClassificationSaveResult> SaveClassificationAsync(Guid productId,
        SetProductClassificationRequest request, CancellationToken cancellationToken = default) =>
        CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
    {
        var product = await db.Products.Include(product => product.AlternativeBrands)
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null) return new ProductClassificationSaveResult(ProductClassificationSaveStatus.Missing);

        var alternatives = request.AlternativeBrandIds ?? [];
        var profile = request.ProfileId is Guid p ? await db.ProductProfiles.SingleOrDefaultAsync(item => item.Id == p, cancellationToken) : null;
        var supplier = request.SupplierId is Guid s ? await db.Suppliers.SingleOrDefaultAsync(item => item.Id == s, cancellationToken) : null;
        var group = request.RestrictionGroupId is Guid g ? await db.RestrictionGroups.SingleOrDefaultAsync(item => item.Id == g, cancellationToken) : null;
        var ids = alternatives.Concat(request.PrimaryBrandId is Guid primary ? [primary] : Array.Empty<Guid>()).Distinct().ToArray();
        var brands = await db.Brands.Where(brand => ids.Contains(brand.Id)).ToDictionaryAsync(brand => brand.Id, cancellationToken);
        Dictionary<string, string[]> errors = [];
        ValidateReference("ProfileId", "profile", request.ProfileId, product.ProductProfileId, profile, errors);
        ValidateReference("SupplierId", "supplier", request.SupplierId, product.SupplierId, supplier, errors);
        ValidateReference("RestrictionGroupId", "restriction group", request.RestrictionGroupId, product.RestrictionGroupId, group, errors);
        if (alternatives.Count > 0 && request.PrimaryBrandId is null)
            errors["PrimaryBrandId"] = ["Choose a primary brand first"];
        if (alternatives.Distinct().Count() != alternatives.Count)
            errors["AlternativeBrandIds"] = ["Choose each alternative brand once."];

        var previousBrands = product.AlternativeBrands.Select(link => link.BrandId).ToHashSet();
        if (product.PrimaryBrandId is Guid previousPrimary) previousBrands.Add(previousPrimary);
        if (request.PrimaryBrandId is Guid primaryId)
            ValidateBrand("PrimaryBrandId", primaryId, brands, previousBrands, errors);
        foreach (Guid alternative in alternatives)
            ValidateBrand("AlternativeBrandIds", alternative, brands, previousBrands, errors);
        if (errors.Count != 0) return new ProductClassificationSaveResult(ProductClassificationSaveStatus.Invalid, errors);

        // Validate every selection before mutating any part of the aggregate.
        product.SetClassification(profile, supplier);
        product.SetBrands(request.PrimaryBrandId is Guid brandId ? brands[brandId] : null,
            alternatives.Select(id => brands[id]).ToArray());
        product.SetRestrictionGroup(group);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ProductClassificationSaveResult(ProductClassificationSaveStatus.Saved);
    }, cancellationToken);

    private static void ValidateReference(string field, string label, Guid? requestedId, Guid? currentId,
        NamedReferenceItem? item, Dictionary<string, string[]> errors)
    {
        if (requestedId is not null && item is null)
            errors[field] = [$"The selected {label} no longer exists. Choose another."];
        else if (item is { IsArchived: true } && requestedId != currentId)
            errors[field] = [$"This {label} is archived. Choose an active {label}."];
    }

    private static void ValidateBrand(string field, Guid id, IReadOnlyDictionary<Guid, Brand> brands,
        IReadOnlySet<Guid> previous, Dictionary<string, string[]> errors)
    {
        if (!brands.TryGetValue(id, out var brand))
            errors[field] = ["The selected brand no longer exists. Choose another."];
        else if (brand.IsArchived && !previous.Contains(id))
            errors[field] = ["This brand is archived. Choose an active brand."];
    }

    public Task SetClassificationAsync(Guid productId, Guid? profileId, Guid? supplierId,
        CancellationToken cancellationToken = default) => CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
    {
        var product = await db.Products.SingleAsync(product => product.Id == productId, cancellationToken);
        var profile = profileId is Guid p ? await db.ProductProfiles.SingleOrDefaultAsync(item => item.Id == p, cancellationToken) : null;
        var supplier = supplierId is Guid s ? await db.Suppliers.SingleOrDefaultAsync(item => item.Id == s, cancellationToken) : null;
        if (profileId is not null && profile is null || supplierId is not null && supplier is null)
            throw new ArgumentException("A selected reference no longer exists.");
        product.SetClassification(profile, supplier);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }, cancellationToken);

    public Task SetRestrictionGroupAsync(Guid productId, Guid? groupId,
        CancellationToken cancellationToken = default) => CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
    {
        var product = await db.Products.SingleAsync(product => product.Id == productId, cancellationToken);
        var group = groupId is Guid g ? await db.RestrictionGroups.SingleOrDefaultAsync(item => item.Id == g, cancellationToken) : null;
        if (groupId is not null && group is null) throw new ArgumentException("A selected reference no longer exists.");
        product.SetRestrictionGroup(group);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }, cancellationToken);

    public Task AddAttributeAsync(Guid productId, Guid nameId, string value,
        CancellationToken cancellationToken = default) => CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
    {
        var product = await db.Products.Include(product => product.AttributeValues)
            .SingleAsync(product => product.Id == productId, cancellationToken);
        var name = await db.AttributeNames.SingleAsync(item => item.Id == nameId, cancellationToken);
        product.AddAttribute(name, value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }, cancellationToken);
}
