using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed class ReferenceUsageUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class ReferenceUsageReader(IEnumerable<IReferenceUsageSource> sources,
    IEnumerable<IReferenceListStore> lists) : IReferenceUsageReader
{
    public async Task<ReferenceUsage> ReadAsync(ReferenceItemKey item, CancellationToken cancellationToken)
    {
        try
        {
            var definition = lists.Single(list => list.Definition.Key == item.ListKey).Definition;
            var applicable = sources.Where(source => source.Supports(item.ListKey)).ToArray();
            if (applicable.Select(source => source.SourceKey).Distinct(StringComparer.Ordinal).Count() != applicable.Length
                || definition.RequiredUsageSources.Any(required => !applicable.Any(source => source.SourceKey == required)))
                throw new InvalidOperationException("Required reference sources are missing or duplicated.");
            List<ReferenceCount> counts = [];
            // Providers share the scoped DbContext; never run concurrent operations on it.
            foreach (var source in applicable)
            {
                var count = await source.CountAsync(item, cancellationToken);
                if (count.SourceKey != source.SourceKey) throw new InvalidOperationException("Unexpected reference source key.");
                counts.Add(count);
            }
            return new ReferenceUsage(counts);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ReferenceUsageUnavailableException("Reference usage could not be checked.", exception);
        }
    }
}

public sealed class ProductBrandUsageSource(CatalogueDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "products";
    public bool Supports(string listKey) => listKey == "brands";
    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken) =>
        new(SourceKey, "product", "products", await db.Products.LongCountAsync(product =>
            product.PrimaryBrandId == item.ItemId || product.AlternativeBrands.Any(link => link.BrandId == item.ItemId),
            cancellationToken));
}

public sealed class ProductReferenceUsageSource(CatalogueDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "products";
    public bool Supports(string listKey) => listKey is "profiles" or "attribute-names" or "suppliers";
    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken)
    {
        long count = item.ListKey switch
        {
            "profiles" => await db.Products.LongCountAsync(product => product.ProductProfileId == item.ItemId, cancellationToken),
            "suppliers" => await db.Products.LongCountAsync(product => product.SupplierId == item.ItemId, cancellationToken),
            "attribute-names" => await db.ProductAttributeValues.Where(value => value.AttributeNameId == item.ItemId)
                .Select(value => value.ProductId).Distinct().LongCountAsync(cancellationToken),
            _ => throw new ArgumentException("Unknown product reference list.", nameof(item))
        };
        return new(SourceKey, "product", "products", count);
    }
}
