namespace FieldSales.Catalogue.Contracts;

/// <summary>How a tier is set up (Pricing US-001): a percentage off base with Per-Product Overrides, or a price list of fixed prices
/// for named products with everything else at base (WI-061).</summary>
public static class PriceTierShapes
{
    public const string Percentage = "Percentage";
    public const string PriceList = "PriceList";

    public static bool IsKnown(string? shape) => shape is Percentage or PriceList;
}

/// <summary>A tier on the tier list (H-20): its shape, its default percentage off base (null on a price list) and how many products
/// have overrides or listed prices.</summary>
public sealed record PriceTierListItem(Guid Id, string Name, decimal? PercentOff, int OverrideCount, string Shape);

/// <summary>A tier and its Per-Product Overrides (or, on a price list, its listed prices), each with today's list price and the
/// price the tier gives today, both worked out when read and never stored (WI-060 D6).</summary>
public sealed record PriceTierDetails(Guid Id, string Name, decimal? PercentOff, IReadOnlyList<PriceTierOverrideItem> Overrides,
    string Shape);

/// <summary>One override or listed price: exactly one of <see cref="PercentOff"/> or <see cref="FixedPrice"/> (a price list has
/// fixed prices only). <see cref="ListPrice"/> and <see cref="TierPrice"/> are for today (the business day) and null when the product
/// has no base price yet. <see cref="AboveList"/> is true when a fixed price is above today's list price, so it will never apply
/// (WI-061 D3).</summary>
public sealed record PriceTierOverrideItem(Guid ProductId, string Code, string Name, string Unit, decimal? PercentOff,
    decimal? FixedPrice, decimal? ListPrice, decimal? TierPrice, DateOnly SetOn, bool AboveList);

/// <summary>A tier's name and default percentage. <see cref="Shape"/> is read when the tier is created (null means a percentage
/// tier) and ignored when it is changed: <see cref="ChangePriceTierShapeRequest"/> changes the shape. A price list takes no
/// percentage.</summary>
public sealed record SavePriceTierRequest(string? Name, decimal? PercentOff, string? Shape = null);

/// <summary>An override's term: send exactly one of the two. A price list takes a fixed price only.</summary>
public sealed record SetPriceTierOverrideRequest(decimal? PercentOff, decimal? FixedPrice);

/// <summary>Changes a tier to the other shape (WI-061 D1, D4): to a price list with no percentage, or to a percentage tier with its
/// new default percentage.</summary>
public sealed record ChangePriceTierShapeRequest(string? Shape, decimal? PercentOff);
