namespace FieldSales.Catalogue.Contracts;

/// <summary>The tiers a customer holds (Pricing US-003, H-21) and every tier there is to choose from, by name.</summary>
public sealed record CustomerTiersDetails(Guid CustomerId, IReadOnlyList<Guid> HeldTierIds, IReadOnlyList<PriceTierListItem> Tiers);

/// <summary>The customer's whole set of tiers, replacing the ones it holds; an empty list leaves it with none. A tier sent twice is
/// held once.</summary>
public sealed record SetCustomerTiersRequest(IReadOnlyList<Guid>? TierIds);

/// <summary>What a set of tiers would mean for a customer (H-21): its most-ordered products and any products searched for, each
/// priced by the engine for today. <see cref="FoundCount"/> counts every search match; <see cref="Found"/> holds the first few.</summary>
public sealed record CustomerPriceSample(IReadOnlyList<CustomerPriceSampleRow> MostOrdered, IReadOnlyList<CustomerPriceSampleRow> Found,
    int FoundCount);

/// <summary>One product in the sample, at the smallest quantity it can be ordered in: today's base price, the price this customer
/// would get with the tiers chosen, and where that price comes from ("Tier B", "List price", "Break: 10+"). The prices are null when
/// nothing prices the product today.</summary>
public sealed record CustomerPriceSampleRow(Guid ProductId, string Code, string Name, string Unit, decimal Quantity, decimal? BasePrice,
    decimal? CustomerPrice, string? From);
