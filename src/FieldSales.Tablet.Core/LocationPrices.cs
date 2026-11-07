using FieldSales.Pricing;

namespace FieldSales.Tablet.Core;

/// <summary>What a shop pays, offline (WI-064 D2): the tiers its customer holds and every product's quantity breaks, from the active
/// snapshot, on the price day. Every price on the tablet comes through <see cref="PriceResolver"/> here; there is no second rule.</summary>
public sealed class LocationPrices
{
    private readonly IReadOnlyList<PriceTier> tiers;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<QuantityBreak>> breaks;

    public LocationPrices(DateOnly day, IReadOnlyList<PriceTier> tiers, IReadOnlyDictionary<Guid, IReadOnlyList<QuantityBreak>> breaks)
    {
        Day = day;
        this.tiers = tiers;
        this.breaks = breaks;
    }

    /// <summary>No tiers and no breaks: every product at its list price, as before customer pricing.</summary>
    public static LocationPrices None { get; } = new(default, [], new Dictionary<Guid, IReadOnlyList<QuantityBreak>>());

    /// <summary>The business day the prices are for.</summary>
    public DateOnly Day { get; }

    /// <summary>The engine's answer for <paramref name="quantity"/> (in the product's unit) of a product whose list price on the day
    /// is <paramref name="listPrice"/>; null when nothing prices it.</summary>
    public ResolvedPrice? Resolve(Guid productId, string unit, decimal? listPrice, decimal quantity) =>
        PriceResolver.Resolve(new PricedProduct(productId, unit, listPrice is decimal list ? [new DatedPrice(Day, list)] : [],
            breaks.TryGetValue(productId, out IReadOnlyList<QuantityBreak>? reached) ? reached : null), tiers, quantity, Day);

    /// <summary>The engine's answer for <paramref name="quantity"/> of a product from the index.</summary>
    public ResolvedPrice? Resolve(ProductSearchResult product, decimal quantity) =>
        Resolve(product.Id, product.Unit, product.ListPrice ?? product.Price, quantity);

    /// <summary>The smallest quantity the product can be ordered in: 1 for Each, its minimum for a measure (D3).</summary>
    public static decimal Smallest(ProductSearchResult product) => product.MinimumQuantity ?? 1m;

    /// <summary>The index with each product's price for this shop at its smallest orderable quantity (D3), keeping its list price.
    /// A product nothing prices has no price, so it can't be ordered.</summary>
    public ProductSearchIndex Apply(ProductSearchIndex index) => index.WithPrices(product =>
        product with { ListPrice = product.ListPrice ?? product.Price, Price = Resolve(product, Smallest(product))?.Winner.Amount });
}
