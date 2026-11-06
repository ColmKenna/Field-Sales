using System.Globalization;

namespace FieldSales.Web.Pages.HeadOffice.Tiers;

/// <summary>How a tier's terms read on H-20 (WI-060 D6): "8% off base", "fixed €10.50", "€3.40 today (list €4.00)".</summary>
public static class TierText
{
    private static readonly CultureInfo Currency = CultureInfo.GetCultureInfo("en-IE");

    public static string Percent(decimal percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static string Money(decimal amount, string unit) =>
        amount.ToString("C2", Currency) + (string.Equals(unit, "Each", StringComparison.OrdinalIgnoreCase) ? "" : " per " + unit);

    public static string Overrides(int count) => count == 1 ? "1 override" : $"{count} overrides";

    /// <summary>One override row: the term, then today's list price, and for a percentage the price it gives today.</summary>
    public static string Row(PriceTierOverrideItem entry)
    {
        string list = entry.ListPrice is decimal listPrice ? "list " + Money(listPrice, entry.Unit) : "no list price today";
        if (entry.FixedPrice is decimal price) return $"fixed {Money(price, entry.Unit)} · {list}";
        string term = Percent(entry.PercentOff!.Value) + " off";
        return entry.TierPrice is decimal today ? $"{term} · {Money(today, entry.Unit)} today ({list})" : $"{term} · {list}";
    }
}
