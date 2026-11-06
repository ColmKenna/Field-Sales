namespace FieldSales.Catalogue.Contracts;

/// <summary>A percentage tier on the tier list (H-20): its default percentage off base and how many products have overrides.</summary>
public sealed record PriceTierListItem(Guid Id, string Name, decimal PercentOff, int OverrideCount);

/// <summary>A percentage tier and its Per-Product Overrides, each with today's list price and the price the tier gives today,
/// both worked out when read and never stored (WI-060 D6).</summary>
public sealed record PriceTierDetails(Guid Id, string Name, decimal PercentOff, IReadOnlyList<PriceTierOverrideItem> Overrides);

/// <summary>One override: exactly one of <see cref="PercentOff"/> or <see cref="FixedPrice"/>. <see cref="ListPrice"/> and
/// <see cref="TierPrice"/> are for today (the business day) and null when the product has no base price yet.</summary>
public sealed record PriceTierOverrideItem(Guid ProductId, string Code, string Name, string Unit, decimal? PercentOff,
    decimal? FixedPrice, decimal? ListPrice, decimal? TierPrice, DateOnly SetOn);

public sealed record SavePriceTierRequest(string? Name, decimal? PercentOff);

/// <summary>An override's term: send exactly one of the two.</summary>
public sealed record SetPriceTierOverrideRequest(decimal? PercentOff, decimal? FixedPrice);
