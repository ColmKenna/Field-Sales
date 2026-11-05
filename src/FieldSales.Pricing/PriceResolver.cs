using System.Globalization;

namespace FieldSales.Pricing;

/// <summary>A line's price (Pricing US-005): the winning candidate, the runner-up tier price, and every candidate considered, cheapest
/// first, with the winner first of all.</summary>
/// <param name="RunnerUpTier">The best tier price when the winner isn't a tier (WI-058 D6): what the customer would have paid on their
/// own terms. Null when the winner is a tier or the customer holds no tier that prices the product.</param>
/// <param name="ListPrice">The list price in effect on the day, when there is one.</param>
public sealed record ResolvedPrice(IReadOnlyList<PriceCandidate> Candidates, PriceCandidate? RunnerUpTier, decimal? ListPrice, string Unit)
{
    public PriceCandidate Winner => Candidates[0];

    /// <summary>The line's one sentence, as the stories write it: "€11.20 — Tier B (list €12.50)", "€12.50 — list price",
    /// "€10.08 — Break: 24+ (better than your tier price €11.20)"; a measured product reads "€4.00 per kg — …".</summary>
    public string Summary
    {
        get
        {
            if (Winner.Kind == PriceKinds.List) return $"{Money(Winner.Amount)} — list price";
            string because = RunnerUpTier is { } tier ? $" (better than your tier price {Money(tier.Amount)})"
                : ListPrice is decimal list ? $" (list {Money(list)})" : "";
            return $"{Money(Winner.Amount)} — {Winner.Label}{because}";
        }
    }

    private string Money(decimal amount) =>
        "€" + amount.ToString("0.00", CultureInfo.InvariantCulture)
        + (string.Equals(Unit, "Each", StringComparison.OrdinalIgnoreCase) ? "" : $" per {Unit}");
}

/// <summary>The price engine (T-6.4.1): Best Price Wins, evaluated at the line's quantity, nothing compounds. One implementation, run
/// unchanged by the tablet (offline, from the morning snapshot) and by the server.</summary>
public static class PriceResolver
{
    /// <summary>Resolves one product's price for a Customer holding <paramref name="tiers"/>, at <paramref name="quantity"/> (in the
    /// product's unit) on <paramref name="date"/>. The candidates are the list price that day, each tier's price, the break reached and
    /// any <paramref name="extraCandidates"/> a later source supplies (E18 promotions); the lowest wins. On an equal price the rep sees
    /// the tier, then the break, then the list price, then any later source; equal tiers by name (D3). Null when nothing prices the
    /// product: no list price that day and no fixed tier price (D5).</summary>
    public static ResolvedPrice? Resolve(PricedProduct product, IReadOnlyList<PriceTier> tiers, decimal quantity, DateOnly date,
        IEnumerable<PriceCandidate>? extraCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        decimal? list = product.ListPriceOn(date);
        List<PriceCandidate> candidates = [];
        if (list is decimal amount) candidates.Add(new(PriceKinds.List, PriceKinds.ListLabel, amount));
        candidates.AddRange(TierCandidates.For(product, tiers, date));
        if (BreakCandidate.For(product, quantity) is { } reached) candidates.Add(reached);
        candidates.AddRange(extraCandidates ?? []);
        if (candidates.Count == 0) return null;

        PriceCandidate[] ordered = [.. candidates
            .OrderBy(candidate => candidate.Amount)
            .ThenBy(candidate => Rank(candidate.Kind))
            .ThenBy(candidate => candidate.Kind == PriceKinds.Tier ? candidate.Label : "", StringComparer.Ordinal)];
        PriceCandidate? runnerUp = ordered[0].Kind == PriceKinds.Tier ? null : ordered.FirstOrDefault(candidate => candidate.Kind == PriceKinds.Tier);
        return new(ordered, runnerUp, list, product.Unit);
    }

    // D3: the customer's own terms first, then the quantity they bought, then the plain list price; later sources last (inferred
    // until E18). OrderBy is stable, so extra candidates keep the order they were supplied in.
    private static int Rank(string kind) => kind switch
    {
        PriceKinds.Tier => 0,
        PriceKinds.Break => 1,
        PriceKinds.List => 2,
        _ => 3
    };
}
