namespace FieldSales.Pricing;

/// <summary>One price that could apply to a line (Pricing glossary: Candidate Price): where it comes from and its unit price.</summary>
/// <param name="Kind">The kind of source: <see cref="PriceKinds"/>, or a later source's own kind (E18 promotions).</param>
/// <param name="Label">The Winning Source as the rep reads it: "List price", "Tier B", "Break: 10+".</param>
/// <param name="Amount">The unit price (per item, or per unit of measure), in whole cents.</param>
public sealed record PriceCandidate(string Kind, string Label, decimal Amount);

/// <summary>The kinds of candidate this engine makes. <see cref="List"/> is "Base", the kind order lines already carry, so a
/// resolved price maps onto <c>OrderLinePrice</c> unchanged.</summary>
public static class PriceKinds
{
    public const string List = "Base";
    public const string Tier = "Tier";
    public const string Break = "Break";

    public const string ListLabel = "List price";
}

/// <summary>Money arithmetic (WI-058 D1, MI-48): to the cent, halves away from zero.</summary>
public static class Money
{
    public static decimal ToCent(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>A line's value: its rounded unit price times its quantity, rounded to the cent.</summary>
    public static decimal LineValue(decimal unitPrice, decimal quantity) => ToCent(unitPrice * quantity);
}

/// <summary>The candidate prices a Customer's tiers give one product (WI-058 increment 1, delegable sliver). Pure; it doesn't choose
/// a winner.</summary>
public static class TierCandidates
{
    /// <summary>One candidate for each tier that prices the product on <paramref name="date"/>. A percentage comes off the list price
    /// in effect that day and is rounded to the cent at once (D1); a product override's percentage or fixed price replaces the
    /// default. A price-list tier gives its fixed price, or nothing for a product it doesn't list. With no list price that day a
    /// percentage gives nothing, but a fixed price still does.</summary>
    public static IEnumerable<PriceCandidate> For(PricedProduct product, IEnumerable<PriceTier> tiers, DateOnly date)
    {
        decimal? list = product.ListPriceOn(date);
        foreach (PriceTier tier in tiers)
        {
            decimal? price = tier switch
            {
                PercentageTier percentage => Percentage(percentage, product.Id, list),
                PriceListTier priceList => priceList.Prices.TryGetValue(product.Id, out decimal listed) ? listed : null,
                _ => null
            };
            if (price is decimal amount) yield return new(PriceKinds.Tier, tier.Name, amount);
        }
    }

    private static decimal? Percentage(PercentageTier tier, Guid productId, decimal? list)
    {
        TierOverride? own = tier.Overrides?.GetValueOrDefault(productId);
        if (own?.FixedPrice is decimal fixedPrice) return fixedPrice;
        decimal percentOff = own?.PercentOff ?? tier.PercentOff;
        return list is decimal amount ? Money.ToCent(amount * (100m - percentOff) / 100m) : null;
    }
}

/// <summary>The quantity break a line reaches (WI-058 increment 1, delegable sliver). Pure; it doesn't choose a winner.</summary>
public static class BreakCandidate
{
    /// <summary>The break candidate at <paramref name="quantity"/> (in the product's unit), or null when no break is reached. Every
    /// break from that quantity or below is reached and the lowest-priced of them applies (D4), so a break priced above one beneath
    /// it never does; between equal prices, the lower threshold.</summary>
    public static PriceCandidate? For(PricedProduct product, decimal quantity) =>
        (product.Breaks ?? []).Where(entry => entry.FromQuantity <= quantity)
            .OrderBy(entry => entry.Price).ThenBy(entry => entry.FromQuantity).FirstOrDefault() is { } reached
            ? new(PriceKinds.Break, Label(product, reached), reached.Price)
            : null;

    /// <summary>"Break: 10+" for an Each product, "Break: 10 kg+" for a measured one.</summary>
    public static string Label(PricedProduct product, QuantityBreak entry)
    {
        string from = entry.FromQuantity.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        return product.IsEach ? $"Break: {from}+" : $"Break: {from} {product.Unit}+";
    }
}
