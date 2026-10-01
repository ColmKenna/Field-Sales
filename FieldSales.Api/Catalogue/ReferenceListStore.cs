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

public sealed class BrandListStore(CatalogueDbContext db) : IReferenceListStore
{
    public ReferenceListDefinition Definition { get; } = new("brands", "Brand", "Brands", ["products"]);
    public async Task<IReadOnlyList<ReferenceStoredItem>> ListAsync(CancellationToken cancellationToken) =>
        await db.Brands.AsNoTracking().OrderBy(brand => brand.Name)
            .Select(brand => new ReferenceStoredItem(brand.Id, brand.Name, brand.IsArchived)).ToArrayAsync(cancellationToken);
    public async Task<ReferenceStoredItem?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Brands.AsNoTracking().Where(brand => brand.Id == id)
            .Select(brand => new ReferenceStoredItem(brand.Id, brand.Name, brand.IsArchived)).SingleOrDefaultAsync(cancellationToken);
    public async Task<ReferenceStoredItem?> SaveAsync(Guid? id, string name, CancellationToken cancellationToken)
    {
        Brand? brand = id is null ? Brand.Create(name) : await db.Brands.SingleOrDefaultAsync(brand => brand.Id == id, cancellationToken);
        if (brand is null) return null;
        if (id is null) db.Brands.Add(brand);
        else brand.Rename(name);
        await db.SaveChangesAsync(cancellationToken);
        return new(brand.Id, brand.Name, brand.IsArchived);
    }
    public Task<ReferenceMutationStatus> RetireAsync(Guid id, ReferenceAction action,
        IReferenceUsageReader usage, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            Brand? brand = await db.Brands.SingleOrDefaultAsync(brand => brand.Id == id, cancellationToken);
            if (brand is null) return ReferenceMutationStatus.Missing;
            var currentUsage = await usage.ReadAsync(new(Definition.Key, id), cancellationToken);
            if (ReferenceRetirementPolicy.Decide(brand.IsArchived, currentUsage) != action)
                return ReferenceMutationStatus.Conflict;
            switch (action)
            {
                case ReferenceAction.Delete: db.Brands.Remove(brand); break;
                case ReferenceAction.Archive: brand.Archive(); break;
                case ReferenceAction.Unarchive: brand.Unarchive(); break;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReferenceMutationStatus.Saved;
        });
}

/// <summary>Storage boundary for the later assignment editor. Reads and writes share one transaction.</summary>
public sealed class ProductBrandAssignments(CatalogueDbContext db)
{
    public Task SetAsync(Guid productId, Guid? primaryId, IReadOnlyList<Guid> alternativeIds,
        CancellationToken cancellationToken = default) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var product = await db.Products.Include(product => product.AlternativeBrands)
            .SingleAsync(product => product.Id == productId, cancellationToken);
        var ids = alternativeIds.Concat(primaryId is Guid primary ? [primary] : Array.Empty<Guid>()).Distinct().ToArray();
        var brands = await db.Brands.Where(brand => ids.Contains(brand.Id)).ToDictionaryAsync(brand => brand.Id, cancellationToken);
        if (brands.Count != ids.Length) throw new ArgumentException("A selected brand no longer exists.");
        product.SetBrands(primaryId is Guid id ? brands[id] : null, alternativeIds.Select(id => brands[id]).ToArray());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    });
}
