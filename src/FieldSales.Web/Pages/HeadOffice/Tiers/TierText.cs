using System.Globalization;

namespace FieldSales.Web.Pages.HeadOffice.Tiers;

/// <summary>How a tier's terms read on H-20 (WI-060 D6, WI-061 D7): "8% off base", "price list", "fixed €10.50", "€3.40 today
/// (list €4.00)", "2 overrides", "20 products".</summary>
public static class TierText
{
    private static readonly CultureInfo Currency = CultureInfo.GetCultureInfo("en-IE");

    public static string Percent(decimal percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>A tier's own term: "8% off base", or "price list" when it has no default percentage (WI-061).</summary>
    public static string Default(decimal? percentOff) => percentOff is decimal percent ? Percent(percent) + " off base" : "price list";

    public static string Money(decimal amount, string unit) =>
        amount.ToString("C2", Currency) + (string.Equals(unit, "Each", StringComparison.OrdinalIgnoreCase) ? "" : " per " + unit);

    public static string Overrides(int count) => count == 1 ? "1 override" : $"{count} overrides";

    /// <summary>How many products a tier names: overrides on a percentage tier, listed products on a price list.</summary>
    public static string Named(int count, string shape) => shape == PriceTierShapes.PriceList
        ? count == 1 ? "1 product" : $"{count} products"
        : Overrides(count);

    /// <summary>Product names as a sentence's list: "A", "A and B", "A, B and C".</summary>
    public static string Names(IEnumerable<string> names)
    {
        string[] all = [.. names];
        return all.Length <= 1 ? string.Concat(all) : string.Join(", ", all[..^1]) + " and " + all[^1];
    }

    /// <summary>One override row: the term, then today's list price, and for a percentage the price it gives today.</summary>
    public static string Row(PriceTierOverrideItem entry)
    {
        string list = entry.ListPrice is decimal listPrice ? "list " + Money(listPrice, entry.Unit) : "no list price today";
        if (entry.FixedPrice is decimal price) return $"fixed {Money(price, entry.Unit)} · {list}";
        string term = Percent(entry.PercentOff!.Value) + " off";
        return entry.TierPrice is decimal today ? $"{term} · {Money(today, entry.Unit)} today ({list})" : $"{term} · {list}";
    }
}
