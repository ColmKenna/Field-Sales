namespace FieldSales.SnapshotSpike;

// Capacity fixtures only. These records do not define the production Sync contract.
public sealed record SpikeCounts(int Products, int Locations, int Categories, int Attributes, int BasePrices,
    int QuantityBreaks, int Promotions, int Replacements, int HistoricalOrders, int HistoricalLines,
    int Thresholds, int Leads, int Masters, int BranchLinks);
public sealed record SpikeCategory(Guid Id, Guid? ParentId, string Name);
public sealed record SpikeAttribute(string Name, string Value);
public sealed record SpikePrice(DateOnly EffectiveFrom, decimal Amount);
public sealed record SpikeBreak(decimal MinimumQuantity, decimal UnitPrice);
public sealed record SpikeTier(string Name, IReadOnlyList<SpikeBreak> Breaks);
public sealed record SpikeProduct(Guid Id, string Code, string Name, Guid CategoryId,
    IReadOnlyList<SpikeCategory> Breadcrumbs, IReadOnlyList<SpikeAttribute> Attributes,
    IReadOnlyList<SpikePrice> BasePrices, string Unit, decimal QuantityStep, decimal MinimumQuantity,
    IReadOnlyList<SpikeTier> PriceTiers);
public sealed record SpikeContact(Guid Id, string Name, string Phone, string Email);
public sealed record SpikeLocation(Guid Id, Guid CustomerId, string CustomerName, string Name,
    string Town, string Address, SpikeContact MainContact);
public sealed record SpikePromotion(Guid ProductId, DateOnly StartsOn, DateOnly EndsOn, decimal PercentOff);
public sealed record SpikeReplacement(Guid ProductId, Guid ReplacementProductId);
public sealed record SpikeOrderLine(Guid ProductId, decimal Quantity, decimal UnitPrice);
public sealed record SpikeHistoricalOrder(Guid Id, Guid LocationId, DateOnly CapturedOn,
    IReadOnlyList<SpikeOrderLine> Lines);
public sealed record SpikeThreshold(Guid LocationId, decimal MinimumOrderValue);
public sealed record SpikeLead(Guid Id, Guid NearbyLocationId, string Name, string Notes);
public sealed record SpikeMaster(Guid Id, Guid CustomerId, IReadOnlyList<Guid> BranchLocationIds);
public sealed record SpikeUnsentItem(Guid Id, string Kind, Guid LocationId, string CapturedUnderSnapshot,
    string Note, IReadOnlyList<SpikeOrderLine> Lines);
public sealed record SpikeUnsent(string Schema, string OwnerSubject, IReadOnlyList<SpikeUnsentItem> Items);
public sealed record SpikeFile(string FileName, long Bytes, string Sha256);
public sealed record SpikeUnsentEvidence(string Schema, string OwnerSubject, long Bytes, string Sha256);
public sealed record SpikeManifest(string Schema, string Profile, int Seed, DateOnly AsOf, string OwnerSubject,
    SpikeScale Scale, SpikeCounts Counts, SpikeFile Json, SpikeFile Gzip, SpikeUnsentEvidence Unsent);
public sealed record SpikeProgress(string Phase, int Records);
public sealed record SpikeExport(string OutputDirectory, string UnsentPath, SpikeManifest Manifest);
