using FieldSales.Api.Catalogue;
using FieldSales.Pricing;

namespace FieldSales.Api.Pricing;

/// <summary>How a tier is set up (Pricing US-001): a percentage off base with Per-Product Overrides, or a list of fixed prices.</summary>
public enum PriceTierShape { Percentage, PriceList }

/// <summary>A Price Tier set up as a percentage off base (Pricing US-001 S1, S2), with optional Per-Product Overrides, or as a price
/// list (S3): fixed prices for named products, everything else at base. It stores the terms only, never a resulting price, so a base
/// price change flows through whenever prices are worked out (WI-060 D2). A price list's entries are fixed-price overrides (WI-061
/// D5), so changing shape keeps every fixed price where it is.</summary>
public sealed class PriceTier
{
    public const int MaximumNameLength = 100;
    public static readonly string NameMessage = $"Enter a tier name of up to {MaximumNameLength} characters.";
    public const string PriceListPercentMessage = "A price-list tier has no default percentage.";
    public const string PriceListTermMessage = "Enter a fixed price; a price list holds fixed prices only.";

    private readonly List<PriceTierOverride> _overrides = [];

    private PriceTier() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PriceTierShape Shape { get; private set; }
    /// <summary>The default percentage off base, e.g. 8 for "8% off base"; null on a price list.</summary>
    public decimal? PercentOff { get; private set; }
    public IReadOnlyList<PriceTierOverride> Overrides => _overrides.AsReadOnly();

    public static PriceTier CreatePercentage(string name, decimal percentOff)
    {
        PriceTier tier = new() { Id = Guid.NewGuid(), Shape = PriceTierShape.Percentage };
        tier.Change(name, percentOff);
        return tier;
    }

    /// <summary>A price list with nothing listed yet: every product is at base until prices are added (WI-061).</summary>
    public static PriceTier CreatePriceList(string name)
    {
        PriceTier tier = new() { Id = Guid.NewGuid(), Shape = PriceTierShape.PriceList };
        tier.Change(name, null);
        return tier;
    }

    /// <summary>Renames the tier and, on a percentage tier, sets its default percentage (WI-060 D5). A price list takes no
    /// percentage.</summary>
    public void Change(string name, decimal? percentOff)
    {
        if (!NameRules.IsValid(name, MaximumNameLength)) throw new ArgumentException(NameMessage, nameof(name));
        if (Shape == PriceTierShape.PriceList && percentOff is not null)
            throw new ArgumentException(PriceListPercentMessage, nameof(percentOff));
        if (Shape == PriceTierShape.Percentage && (percentOff is not decimal percent || !TierTerms.IsValidPercent(percent)))
            throw new ArgumentOutOfRangeException(nameof(percentOff), TierTerms.PercentMessage);
        Name = name.Trim();
        PercentOff = percentOff;
    }

    /// <summary>Makes a percentage tier a price list (WI-061 D1): its fixed-price overrides become the listed prices, its default
    /// percentage stops applying, and its percentage overrides are removed. Returns the products whose percentage overrides went.</summary>
    public IReadOnlyList<Guid> ChangeToPriceList()
    {
        if (Shape == PriceTierShape.PriceList) throw new InvalidOperationException("The tier is already a price list.");
        Guid[] removed = [.. _overrides.Where(entry => entry.PercentOff is not null).Select(entry => entry.ProductId)];
        _overrides.RemoveAll(entry => entry.PercentOff is not null);
        Shape = PriceTierShape.PriceList;
        PercentOff = null;
        return removed;
    }

    /// <summary>Makes a price list a percentage tier at <paramref name="percentOff"/> (WI-061 D4): its listed prices stay as fixed
    /// overrides, and every product it doesn't list gets the percentage.</summary>
    public void ChangeToPercentage(decimal percentOff)
    {
        if (Shape == PriceTierShape.Percentage) throw new InvalidOperationException("The tier is already a percentage tier.");
        if (!TierTerms.IsValidPercent(percentOff)) throw new ArgumentOutOfRangeException(nameof(percentOff), TierTerms.PercentMessage);
        Shape = PriceTierShape.Percentage;
        PercentOff = percentOff;
    }

    /// <summary>Gives <paramref name="productId"/> its own term, replacing any it had: one override (or listed price) per product
    /// (WI-060 D2). A price list takes fixed prices only. <paramref name="setOn"/> is the business day it was set, kept so drift can
    /// later be measured from it (D8).</summary>
    public PriceTierOverride SetOverride(Guid productId, TierTerm term, DateOnly setOn)
    {
        if (productId == Guid.Empty) throw new ArgumentException("A product identity is required.", nameof(productId));
        ArgumentNullException.ThrowIfNull(term);
        if (Shape == PriceTierShape.PriceList && term.FixedPrice is null) throw new ArgumentException(PriceListTermMessage, nameof(term));
        PriceTierOverride? existing = _overrides.FirstOrDefault(entry => entry.ProductId == productId);
        if (existing is not null)
        {
            existing.Set(term, setOn);
            return existing;
        }
        PriceTierOverride added = new(Id, productId, term, setOn);
        _overrides.Add(added);
        return added;
    }

    /// <summary>Takes the product's override away, so the default percentage applies to it again. False when it had none.</summary>
    public bool RemoveOverride(Guid productId) => _overrides.RemoveAll(entry => entry.ProductId == productId) > 0;

    /// <summary>The tier as the price engine reads it: WI-058's <see cref="PercentageTier"/> or <see cref="PriceListTier"/>.</summary>
    public FieldSales.Pricing.PriceTier ToEngine() => Shape == PriceTierShape.PriceList
        ? new PriceListTier(Name, _overrides.ToDictionary(entry => entry.ProductId, entry => entry.FixedPrice!.Value))
        : new PercentageTier(Name, PercentOff!.Value, _overrides.ToDictionary(entry => entry.ProductId, entry => entry.ToEngine()));
}

/// <summary>One product's own term on a percentage tier: a different percentage off, or a fixed price (Per-Product Override).</summary>
public sealed class PriceTierOverride
{
    private PriceTierOverride() { }

    internal PriceTierOverride(Guid tierId, Guid productId, TierTerm term, DateOnly setOn)
    {
        PriceTierId = tierId;
        ProductId = productId;
        Set(term, setOn);
    }

    public Guid PriceTierId { get; private set; }
    public Guid ProductId { get; private set; }
    /// <summary>Set when the override is a percentage off base; otherwise null.</summary>
    public decimal? PercentOff { get; private set; }
    /// <summary>Set when the override is a fixed price; otherwise null.</summary>
    public decimal? FixedPrice { get; private set; }
    /// <summary>The business day the term was last set (WI-060 D8): a date, not a price.</summary>
    public DateOnly SetOn { get; private set; }

    public TierTerm Term => PercentOff is decimal percent ? TierTerm.Percent(percent) : TierTerm.Fixed(FixedPrice!.Value);

    internal void Set(TierTerm term, DateOnly setOn)
    {
        PercentOff = term.PercentOff;
        FixedPrice = term.FixedPrice;
        SetOn = setOn;
    }

    public TierOverride ToEngine() => PercentOff is decimal percent ? TierOverride.Percent(percent) : TierOverride.Fixed(FixedPrice!.Value);

    /// <summary>Whether this is a fixed price above <paramref name="listPrice"/>, so Best Price Wins never picks it (WI-061 D3). Judged
    /// against the list price the caller passes, today's; false when there is none to compare with, or when the two are equal.</summary>
    public bool IsAboveList(decimal? listPrice) => FixedPrice is decimal price && listPrice is decimal list && price > list;
}

/// <summary>An override's term: exactly one of a percentage off base or a fixed price, each within the limits (WI-060 D4).</summary>
public sealed record TierTerm
{
    private TierTerm(decimal? percentOff, decimal? fixedPrice) { PercentOff = percentOff; FixedPrice = fixedPrice; }

    public decimal? PercentOff { get; }
    public decimal? FixedPrice { get; }

    public static TierTerm Percent(decimal percentOff) => TierTerms.IsValidPercent(percentOff)
        ? new(percentOff, null)
        : throw new ArgumentOutOfRangeException(nameof(percentOff), TierTerms.PercentMessage);

    public static TierTerm Fixed(decimal price) => TierTerms.IsValidFixedPrice(price)
        ? new(null, price)
        : throw new ArgumentOutOfRangeException(nameof(price), TierTerms.FixedPriceMessage);
}

/// <summary>The limits on a tier's terms (WI-060 D4, the developer's answer): a percentage more than 0 and less than 100, to two
/// decimal places; a fixed price more than €0.00, to the cent (a €0.00 line is free goods, which has its own rules).</summary>
public static class TierTerms
{
    public const string PercentMessage = "Enter a percentage more than 0 and less than 100, with up to two decimal places.";
    public const string FixedPriceMessage = "Enter a price more than €0.00, to the cent.";

    public static bool IsValidPercent(decimal percent) => percent > 0m && percent < 100m && decimal.Round(percent, 2) == percent;

    public static bool IsValidFixedPrice(decimal price) =>
        price > 0m && price <= ProductBasePrice.MaximumAmount && decimal.Round(price, 2) == price;
}
