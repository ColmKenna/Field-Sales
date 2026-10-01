using System.Data;
using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed record ReferenceStoredItem(Guid Id, string Name, bool IsArchived);
public interface IReferenceListStore
{
    ReferenceListDefinition Definition { get; }
    Task<IReadOnlyList<ReferenceStoredItem>> ListAsync(CancellationToken cancellationToken);
    Task<ReferenceStoredItem?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<ReferenceStoredItem?> SaveAsync(Guid? id, string name, CancellationToken cancellationToken);
    Task<ReferenceMutationStatus> RetireAsync(Guid id, ReferenceAction action,
        IReferenceUsageReader usage, CancellationToken cancellationToken);
}
public enum ReferenceMutationStatus { Saved, Missing, Conflict }

public class ReferenceListStore<T>(CatalogueDbContext db, ReferenceListDefinition definition,
    Func<string, T> create) : IReferenceListStore where T : NamedReferenceItem
{
    public ReferenceListDefinition Definition { get; } = definition;
    public async Task<IReadOnlyList<ReferenceStoredItem>> ListAsync(CancellationToken cancellationToken) =>
        await db.Set<T>().AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new ReferenceStoredItem(item.Id, item.Name, item.IsArchived)).ToArrayAsync(cancellationToken);
    public async Task<ReferenceStoredItem?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Set<T>().AsNoTracking().Where(item => item.Id == id)
            .Select(item => new ReferenceStoredItem(item.Id, item.Name, item.IsArchived)).SingleOrDefaultAsync(cancellationToken);
    public async Task<ReferenceStoredItem?> SaveAsync(Guid? id, string name, CancellationToken cancellationToken)
    {
        T? item = id is null ? create(name) : await db.Set<T>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (item is null) return null;
        if (id is null) db.Set<T>().Add(item);
        else item.Rename(name);
        await db.SaveChangesAsync(cancellationToken);
        return new(item.Id, item.Name, item.IsArchived);
    }
    public Task<ReferenceMutationStatus> RetireAsync(Guid id, ReferenceAction action,
        IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
        {
            T? item = await db.Set<T>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (item is null) return ReferenceMutationStatus.Missing;
            var currentUsage = await usage.ReadAsync(new(Definition.Key, id), cancellationToken);
            if (ReferenceRetirementPolicy.Decide(item.IsArchived, currentUsage) != action)
                return ReferenceMutationStatus.Conflict;
            switch (action)
            {
                case ReferenceAction.Delete: db.Set<T>().Remove(item); break;
                case ReferenceAction.Archive: item.Archive(); break;
                case ReferenceAction.Unarchive: item.Unarchive(); break;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReferenceMutationStatus.Saved;
        }, cancellationToken);
}

public sealed class BrandListStore(CatalogueDbContext db)
    : ReferenceListStore<Brand>(db, new(ReferenceListKeys.Brands, "Brand", "Brands", ["products"]), Brand.Create);
public sealed class ProductProfileListStore(CatalogueDbContext db)
    : ReferenceListStore<ProductProfile>(db, new(ReferenceListKeys.Profiles, "Product Profile", "Product Profiles", ["products"]), ProductProfile.Create);
public sealed class AttributeNameListStore(CatalogueDbContext db)
    : ReferenceListStore<AttributeName>(db, new(ReferenceListKeys.AttributeNames, "Attribute", "Attribute names", ["products"]), AttributeName.Create);
public sealed class SupplierListStore(CatalogueDbContext db)
    : ReferenceListStore<Supplier>(db, new(ReferenceListKeys.Suppliers, "Supplier", "Suppliers", ["products"]), Supplier.Create);
// Permissions join the required sources when the Coverage area registers its usage source (WI-029).
public sealed class RestrictionGroupListStore(CatalogueDbContext db)
    : ReferenceListStore<RestrictionGroup>(db, new(ReferenceListKeys.RestrictionGroups, "Restriction Group", "Restriction Groups", ["products"]), RestrictionGroup.Create);

/// <summary>Storage boundary for the later assignment editor. Reads and writes share one transaction.</summary>
public sealed class ProductBrandAssignments(CatalogueDbContext db)
{
    public Task SetAsync(Guid productId, Guid? primaryId, IReadOnlyList<Guid> alternativeIds,
        CancellationToken cancellationToken = default) => CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
    {
        var product = await db.Products.Include(product => product.AlternativeBrands)
            .SingleAsync(product => product.Id == productId, cancellationToken);
        var ids = alternativeIds.Concat(primaryId is Guid primary ? [primary] : Array.Empty<Guid>()).Distinct().ToArray();
        var brands = await db.Brands.Where(brand => ids.Contains(brand.Id)).ToDictionaryAsync(brand => brand.Id, cancellationToken);
        if (brands.Count != ids.Length) throw new ArgumentException("A selected brand no longer exists.");
        product.SetBrands(primaryId is Guid id ? brands[id] : null, alternativeIds.Select(id => brands[id]).ToArray());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }, cancellationToken);
}
