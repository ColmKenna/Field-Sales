using System.Globalization;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>A product's quantity on one of a shop's recent accepted orders, in the unit it was ordered in (T-5.1.6).</summary>
public sealed record OrderedQuantity(decimal Quantity, string Unit);

/// <summary>The out-of-pattern quantity marker on Review (T-5.1.6, US-012 S14–S16, T7.11): a fallback that puts the shop's usual
/// quantity next to a line that is far above it. It informs and never blocks.</summary>
/// <remarks>The rule (WI-054 D1, the developer's choice for MI-49): the history is the product's quantity on whichever of the shop's
/// last 3 accepted orders contained it. With fewer than 2 such orders there is no pattern and no marker. The usual quantity is their
/// median (with two, their average), and a line is marked when it is more than 3× that. High only, with no absolute floor. Past
/// quantities in another unit than the line's are left out (D3).</remarks>
public static class QuantityMarker
{
    /// <summary>A line more than this many times the usual quantity is marked.</summary>
    public const decimal Multiple = 3m;

    /// <summary>How many recent orders must contain the product before it has a pattern.</summary>
    public const int MinimumOrders = 2;

    /// <summary>"Usually about 48 here" (or "Usually about 2.5 kg here") when <paramref name="line"/> is far above the shop's usual
    /// quantity of its product, else null. <paramref name="step"/> is the product's quantity step, which the usual quantity of a
    /// measured product is rounded to.</summary>
    public static string? For(OrderLine line, IReadOnlyList<OrderedQuantity> history, decimal? step)
    {
        decimal[] quantities = [.. history.Where(each => each.Unit == line.Unit).Select(each => each.Quantity).Order()];
        if (quantities.Length < MinimumOrders) return null;
        int middle = quantities.Length / 2;
        decimal usual = quantities.Length % 2 == 1 ? quantities[middle] : (quantities[middle - 1] + quantities[middle]) / 2m;
        return line.Quantity > Multiple * usual ? $"Usually about {Shown(usual, line.Unit, step)} here" : null;
    }

    // A whole number for Each; a measured product to its step, with its unit. Halves round up.
    private static string Shown(decimal usual, string unit, decimal? step)
    {
        if (unit == "Each") return Math.Round(usual, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        decimal rounded = step is > 0m and decimal by ? Math.Round(usual / by, 0, MidpointRounding.AwayFromZero) * by : usual;
        return $"{rounded.ToString("0.0#####", CultureInfo.InvariantCulture)} {unit}";
    }
}
