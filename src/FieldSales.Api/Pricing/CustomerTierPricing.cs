using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.Pricing;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Pricing;

/// <summary>A Price Tier a Customer holds (Pricing US-003): every Location of the customer prices against it. Stored beside the tiers
/// in the catalogue database; the customer is in the Directory database, so it is checked there on save (WI-063 D1).</summary>
public sealed class CustomerPriceTier
{
    private CustomerPriceTier() { }

    public CustomerPriceTier(Guid customerId, Guid priceTierId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("A customer identity is required.", nameof(customerId));
        if (priceTierId == Guid.Empty) throw new ArgumentException("A tier identity is required.", nameof(priceTierId));
        CustomerId = customerId;
        PriceTierId = priceTierId;
    }

    public Guid CustomerId { get; private set; }
    public Guid PriceTierId { get; private set; }
}

/// <summary>Prices for a Customer and its Locations (WI-063 D4): a Location prices against the tiers its customer holds, through the
/// price engine, so the head office sample and (from WI-064) the tablet agree. No price is stored.</summary>
public sealed class CustomerTierPricing(CatalogueDbContext catalogue, DirectoryDbContext directory)
{
    /// <summary>The ids of the tiers <paramref name="customerId"/> holds.</summary>
    public async Task<Guid[]> TierIdsAsync(Guid customerId, CancellationToken cancellationToken) =>
        await catalogue.CustomerPriceTiers.AsNoTracking().Where(link => link.CustomerId == customerId)
            .Select(link => link.PriceTierId).ToArrayAsync(cancellationToken);

    /// <summary>The tiers <paramref name="tierIds"/> name, as the engine reads them, by name. Unknown ids are left out.</summary>
    public async Task<FieldSales.Pricing.PriceTier[]> EngineTiersAsync(IReadOnlyCollection<Guid> tierIds, CancellationToken cancellationToken)
    {
        if (tierIds.Count == 0) return [];
        List<PriceTier> tiers = await catalogue.PriceTiers.AsNoTracking().Include(tier => tier.Overrides)
            .Where(tier => tierIds.Contains(tier.Id)).ToListAsync(cancellationToken);
        return [.. tiers.OrderBy(tier => tier.Name, StringComparer.Ordinal).Select(tier => tier.ToEngine())];
    }

    /// <summary>The tiers a Location prices against: its customer's. Null when there is no such Location.</summary>
    public async Task<FieldSales.Pricing.PriceTier[]?> TiersForLocationAsync(Guid locationId, CancellationToken cancellationToken)
    {
        Guid? customerId = await directory.Locations.AsNoTracking().Where(location => location.Id == locationId)
            .Select(location => (Guid?)location.CustomerId).SingleOrDefaultAsync(cancellationToken);
        return customerId is Guid customer ? await EngineTiersAsync(await TierIdsAsync(customer, cancellationToken), cancellationToken) : null;
    }

    /// <summary>What <paramref name="locationId"/> pays for <paramref name="quantity"/> of <paramref name="productId"/> on
    /// <paramref name="date"/>: Best Price Wins across the list price, its customer's tiers and the product's breaks. Null when the
    /// Location or product doesn't exist, or nothing prices the product.</summary>
    public async Task<ResolvedPrice?> ResolveForLocationAsync(Guid locationId, Guid productId, decimal quantity, DateOnly date,
        CancellationToken cancellationToken)
    {
        if (await TiersForLocationAsync(locationId, cancellationToken) is not { } tiers) return null;
        Product? product = await PricedProductsQuery().SingleOrDefaultAsync(row => row.Id == productId, cancellationToken);
        return product is null ? null : PriceResolver.Resolve(product.ToPricedProduct(), tiers, quantity, date);
    }

    /// <summary>Products with what the engine needs: base price history and quantity breaks.</summary>
    public IQueryable<Product> PricedProductsQuery() =>
        catalogue.Products.AsNoTracking().Include(product => product.BasePrices).Include(product => product.QuantityBreaks).AsSplitQuery();
}
