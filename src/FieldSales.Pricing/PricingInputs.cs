namespace FieldSales.Pricing;

/// <summary>A list (base) price from a date onward: one entry of the product's price history (T-1.3.1).</summary>
public sealed record DatedPrice(DateOnly EffectiveFrom, decimal Amount);

/// <summary>A price that applies from a quantity upward, in the product's unit (Pricing US-004): "from 10: €2.00", "from 10 kg:
/// €4.00".</summary>
public sealed record QuantityBreak(decimal FromQuantity, decimal Price);

/// <summary>What the engine knows about one product: its unit ("Each", "kg", …), its list price history and its quantity breaks.</summary>
public sealed record PricedProduct(Guid Id, string Unit, IReadOnlyList<DatedPrice> ListPrices, IReadOnlyList<QuantityBreak>? Breaks = null)
{
    /// <summary>The list price in effect on <paramref name="date"/>: the newest entry from that day or earlier; null when none has
    /// started yet. Which calendar day "today" is belongs to the caller (MI-53, WI-059).</summary>
    public decimal? ListPriceOn(DateOnly date) =>
        ListPrices.Where(price => price.EffectiveFrom <= date).MaxBy(price => price.EffectiveFrom)?.Amount;

    /// <summary>Whether the product is counted in whole items rather than measured.</summary>
    public bool IsEach => string.Equals(Unit, "Each", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A Price Tier a Customer holds (Pricing US-001, US-003). It is either a percentage off the list price or a price list
/// (WI-058 D2: overrides are per product only, never per category).</summary>
public abstract record PriceTier(string Name);

/// <summary>A percentage off the list price, tracking it as it changes, with optional per-product overrides.</summary>
/// <param name="PercentOff">The default percentage off, e.g. 15 for 15%.</param>
public sealed record PercentageTier(string Name, decimal PercentOff, IReadOnlyDictionary<Guid, TierOverride>? Overrides = null)
    : PriceTier(Name);

/// <summary>A product's own term on a percentage tier: a different percentage off, or a fixed price. Exactly one is set.</summary>
public sealed record TierOverride(decimal? PercentOff = null, decimal? FixedPrice = null)
{
    public static TierOverride Percent(decimal percentOff) => new(PercentOff: percentOff);
    public static TierOverride Fixed(decimal price) => new(FixedPrice: price);
}

/// <summary>Fixed prices for named products; a product it doesn't list gets no price from this tier, so the list price stands.</summary>
public sealed record PriceListTier(string Name, IReadOnlyDictionary<Guid, decimal> Prices) : PriceTier(Name);
