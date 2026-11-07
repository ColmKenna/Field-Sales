using System.Globalization;
using FieldSales.Ordering.Contracts;
using FieldSales.Pricing;

namespace FieldSales.Tablet.Core;

/// <summary>An order line's price as the rep reads it (WI-065 D2), from what the line captured and nothing else: the price and
/// where it came from, then either the list price or, when something other than a tier won, the customer's tier price — the
/// runner-up, on the line (MI-54). "€11.20 — Tier B (list €12.50)", "€12.50 — list price", "€11.00 — Break: 10+ (better than
/// your tier price €11.50)".</summary>
public static class LinePriceText
{
    /// <summary>How the provenance sheet names a list price (D4); other candidates keep the name the line captured.</summary>
    public const string ListPriceName = "List price";

    public static string Summary(OrderLine line) => Summary(line.Price, line.Unit);

    public static string Summary(OrderLinePrice price, string unit)
    {
        if (IsList(price.Kind)) return $"{Money(price.Amount, unit)} — list price";
        // Candidates are captured cheapest first, so the first tier among the others is the customer's best tier price.
        OrderPriceCandidate[] others = [.. price.Candidates.Where(candidate => !candidate.IsWinner)];
        string because = price.Kind != PriceKinds.Tier && others.FirstOrDefault(candidate => candidate.Kind == PriceKinds.Tier) is { } tier
            ? $" (better than your tier price {Money(tier.Amount, unit)})"
            : others.FirstOrDefault(candidate => IsList(candidate.Kind)) is { } list ? $" (list {Money(list.Amount, unit)})" : "";
        return $"{Money(price.Amount, unit)} — {price.Label}{because}";
    }

    /// <summary>A candidate's name in the provenance sheet: as the line captured it, a list price as "List price".</summary>
    public static string Name(OrderPriceCandidate candidate) => IsList(candidate.Kind) ? ListPriceName : candidate.Label;

    /// <summary>"€12.50", or "€4.80 per kg" for a measured product.</summary>
    public static string Money(decimal amount, string unit) =>
        "€" + amount.ToString("N2", CultureInfo.InvariantCulture) + (string.Equals(unit, "Each", StringComparison.OrdinalIgnoreCase) ? "" : $" per {unit}");

    // A list price is captured as the base price it always was (WI-064 D5).
    private static bool IsList(string kind) => kind == OrderContract.BasePriceKind;
}
