using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed class ReferenceUsageUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class ReferenceUsageReader(ReferenceCatalogueRegistry registry) : IReferenceUsageReader
{
    public async Task<ReferenceUsage> ReadAsync(ReferenceItemKey item, CancellationToken cancellationToken)
    {
        try
        {
            List<ReferenceCount> counts = [];
            // The sources share one scoped DbContext; reads inside retirement use the live transaction.
            foreach (var source in registry.SourcesFor(item.ListKey))
            {
                ReferenceCount count = await source.CountAsync(item, cancellationToken);
                if (count.SourceKey != source.SourceKey) throw new InvalidOperationException("Unexpected reference source key.");
                counts.Add(count);
            }
            return new(counts);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new ReferenceUsageUnavailableException("Reference usage could not be checked.", exception); }
    }

    public async Task<IReadOnlyDictionary<Guid, ReferenceUsage>> ReadManyAsync(string listKey,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        try
        {
            var counts = ids.Distinct().ToDictionary(id => id, _ => new List<ReferenceCount>());
            foreach (var source in registry.SourcesFor(listKey))
            {
                if (counts.Count == 0) continue;
                var batch = await source.CountManyAsync(listKey, counts.Keys.ToArray(), cancellationToken);
                if (batch.Count != counts.Count) throw new InvalidOperationException("Incomplete reference usage response.");
                foreach (var entry in counts)
                {
                    if (!batch.TryGetValue(entry.Key, out var count) || count.SourceKey != source.SourceKey)
                        throw new InvalidOperationException("Unexpected reference source key.");
                    entry.Value.Add(count);
                }
            }
            return counts.ToDictionary(entry => entry.Key, entry => new ReferenceUsage(entry.Value));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new ReferenceUsageUnavailableException("Reference usage could not be checked.", exception); }
    }
}

public sealed class ProductBrandUsageSource(CatalogueDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "products";
    public bool Supports(string listKey) => listKey == ReferenceListKeys.Brands;
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var primary = db.Products.Where(product => product.PrimaryBrandId != null && ids.Contains(product.PrimaryBrandId.Value))
            .Select(product => new { ItemId = product.PrimaryBrandId!.Value, ProductId = product.Id });
        var alternatives = db.ProductAlternativeBrands.Where(link => ids.Contains(link.BrandId))
            .Select(link => new { ItemId = link.BrandId, ProductId = link.ProductId });
        var counts = await primary.Concat(alternatives).Distinct().GroupBy(link => link.ItemId)
            .Select(group => new { Id = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(group => group.Id, group => group.Count, cancellationToken);
        return ids.Distinct().ToDictionary(id => id,
            id => new ReferenceCount(SourceKey, "product", "products", counts.GetValueOrDefault(id)));
    }

    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken) =>
        new(SourceKey, "product", "products", await db.Products.LongCountAsync(product =>
            product.PrimaryBrandId == item.ItemId || product.AlternativeBrands.Any(link => link.BrandId == item.ItemId),
            cancellationToken));
}

public sealed class ProductReferenceUsageSource(CatalogueDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "products";
    public bool Supports(string listKey) => listKey is ReferenceListKeys.Profiles or ReferenceListKeys.AttributeNames or ReferenceListKeys.Suppliers;
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        IQueryable<ReferenceLink> links = listKey switch
        {
            ReferenceListKeys.Profiles => db.Products.Where(product => product.ProductProfileId != null && ids.Contains(product.ProductProfileId.Value))
                .Select(product => new ReferenceLink { ItemId = product.ProductProfileId!.Value, ProductId = product.Id }),
            ReferenceListKeys.Suppliers => db.Products.Where(product => product.SupplierId != null && ids.Contains(product.SupplierId.Value))
                .Select(product => new ReferenceLink { ItemId = product.SupplierId!.Value, ProductId = product.Id }),
            ReferenceListKeys.AttributeNames => db.ProductAttributeValues.Where(value => ids.Contains(value.AttributeNameId))
                .Select(value => new ReferenceLink { ItemId = value.AttributeNameId, ProductId = value.ProductId }).Distinct(),
            _ => throw new ArgumentException("Unknown product reference list.", nameof(listKey))
        };
        var counts = await links.GroupBy(link => link.ItemId)
            .Select(group => new { Id = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(group => group.Id, group => group.Count, cancellationToken);
        return ids.Distinct().ToDictionary(id => id,
            id => new ReferenceCount(SourceKey, "product", "products", counts.GetValueOrDefault(id)));
    }

    private sealed class ReferenceLink
    {
        public Guid ItemId { get; init; }
        public Guid ProductId { get; init; }
    }

    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken)
    {
        long count = item.ListKey switch
        {
            ReferenceListKeys.Profiles => await db.Products.LongCountAsync(product => product.ProductProfileId == item.ItemId, cancellationToken),
            ReferenceListKeys.Suppliers => await db.Products.LongCountAsync(product => product.SupplierId == item.ItemId, cancellationToken),
            ReferenceListKeys.AttributeNames => await db.ProductAttributeValues.Where(value => value.AttributeNameId == item.ItemId)
                .Select(value => value.ProductId).Distinct().LongCountAsync(cancellationToken),
            _ => throw new ArgumentException("Unknown product reference list.", nameof(item))
        };
        return new(SourceKey, "product", "products", count);
    }
}
