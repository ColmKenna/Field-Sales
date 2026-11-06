using System.Globalization;
using FieldSales.Quantities;

namespace FieldSales.Api.Catalogue;

/// <summary>A price that applies from a quantity upward, in the product's unit (Pricing US-004): "from 10: €2.00", "from 10 kg:
/// €4.00". It belongs to the product, not to a tier, and isn't dated: it applies from when it is saved (WI-062 D1).</summary>
public sealed class ProductQuantityBreak
{
    private ProductQuantityBreak() { }

    internal ProductQuantityBreak(Guid productId, decimal fromQuantity, decimal price)
    {
        ProductId = productId;
        FromQuantity = fromQuantity;
        Price = price;
    }

    public Guid ProductId { get; private set; }
    public decimal FromQuantity { get; private set; }
    public decimal Price { get; private set; }

    internal void Reprice(decimal price) => Price = price;

    /// <summary>Whether a break at a smaller quantity is cheaper, so this one never applies: the engine gives a line the lowest-priced
    /// break it reaches (WI-062 D4). Equal prices don't count.</summary>
    public bool IsAboveCheaperBreak(IEnumerable<ProductQuantityBreak> breaks) =>
        breaks.Any(other => other.FromQuantity < FromQuantity && other.Price < Price);

    /// <summary>Whether this break is above <paramref name="basePrice"/> (today's), so the base price always beats it (WI-062 D4).
    /// False when there is no base price to compare with, or when the two are equal.</summary>
    public bool IsAboveBase(decimal? basePrice) => basePrice is decimal price && Price > price;
}

/// <summary>One break as entered: either value may be missing.</summary>
public sealed record QuantityBreakInput(decimal? FromQuantity, decimal? Price);

/// <summary>The rules for a product's breaks (WI-062 D3): a threshold is an orderable quantity of the product above the smallest
/// one (whole for Each; a multiple of the step for measures), each threshold once, and a price more than €0.00 to the cent.</summary>
public static class QuantityBreakRules
{
    public const string PriceMessage = "Enter a price more than €0.00, to the cent.";
    public const string QuantityMessage = "Enter the quantity the break starts from.";
    public const string WholeMessage = "Enter a whole number.";
    public const string SameQuantityMessage = "Each break needs its own quantity.";

    public static bool IsValidPrice(decimal price) =>
        price > 0m && price <= ProductBasePrice.MaximumAmount && decimal.Round(price, 2) == price;

    /// <summary>Why <paramref name="fromQuantity"/> can't start a break under <paramref name="rules"/>, or null when it can.</summary>
    public static string? ThresholdError(QuantityRules rules, decimal fromQuantity)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Unit == UnitOfMeasure.Each && fromQuantity % 1m != 0) return WholeMessage;
        if (fromQuantity <= rules.Minimum.Amount) return $"A break starts above the smallest quantity you can order ({Smallest(rules)}).";
        return rules.ValidateOrder(fromQuantity).Error;
    }

    /// <summary>Field errors for <paramref name="breaks"/>, keyed <c>Breaks[i].FromQuantity</c> and <c>Breaks[i].Price</c>; empty
    /// when every break can be saved.</summary>
    public static Dictionary<string, string[]> Validate(QuantityRules rules, IReadOnlyList<QuantityBreakInput> breaks)
    {
        ArgumentNullException.ThrowIfNull(breaks);
        Dictionary<string, string[]> errors = [];
        for (int index = 0; index < breaks.Count; index++)
        {
            QuantityBreakInput entry = breaks[index];
            string? quantity = entry.FromQuantity is decimal from ? ThresholdError(rules, from) : QuantityMessage;
            if (quantity is null && breaks.Take(index).Any(earlier => earlier.FromQuantity == entry.FromQuantity))
                quantity = SameQuantityMessage;
            if (quantity is not null) errors[$"Breaks[{index}].FromQuantity"] = [quantity];
            if (entry.Price is not decimal price || !IsValidPrice(price)) errors[$"Breaks[{index}].Price"] = [PriceMessage];
        }
        return errors;
    }

    /// <summary>"1" for Each, "1 kg" for a measure: the smallest quantity a line can have.</summary>
    public static string Smallest(QuantityRules rules) => rules.Unit == UnitOfMeasure.Each
        ? "1"
        : $"{rules.Minimum.Amount.ToString("0.######", CultureInfo.InvariantCulture)} {rules.Unit.ToCode()}";
}
