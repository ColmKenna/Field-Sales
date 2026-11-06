using FieldSales.Api.Catalogue;
using FieldSales.Pricing;

namespace FieldSales.Api.Pricing;

/// <summary>A Price Tier set up as a percentage off base (Pricing US-001 S1, S2), with optional Per-Product Overrides. It stores the
/// terms only, never a resulting price, so a base price change flows through whenever prices are worked out (WI-060 D2).</summary>
public sealed class PriceTier
{
    public const int MaximumNameLength = 100;
    public static readonly string NameMessage = $"Enter a tier name of up to {MaximumNameLength} characters.";

    private readonly List<PriceTierOverride> _overrides = [];

    private PriceTier() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    /// <summary>The default percentage off base, e.g. 8 for "8% off base".</summary>
    public decimal PercentOff { get; private set; }
    public IReadOnlyList<PriceTierOverride> Overrides => _overrides.AsReadOnly();

    public static PriceTier CreatePercentage(string name, decimal percentOff)
    {
        PriceTier tier = new() { Id = Guid.NewGuid() };
        tier.Change(name, percentOff);
        return tier;
    }

    /// <summary>Renames the tier and sets its default percentage (WI-060 D5).</summary>
    public void Change(string name, decimal percentOff)
    {
        if (!NameRules.IsValid(name, MaximumNameLength)) throw new ArgumentException(NameMessage, nameof(name));
        if (!TierTerms.IsValidPercent(percentOff)) throw new ArgumentOutOfRangeException(nameof(percentOff), TierTerms.PercentMessage);
        Name = name.Trim();
        PercentOff = percentOff;
    }

    /// <summary>Gives <paramref name="productId"/> its own term, replacing any it had: one override per product (WI-060 D2).
    /// <paramref name="setOn"/> is the business day it was set, kept so drift can later be measured from it (D8).</summary>
    public PriceTierOverride SetOverride(Guid productId, TierTerm term, DateOnly setOn)
    {
        if (productId == Guid.Empty) throw new ArgumentException("A product identity is required.", nameof(productId));
        ArgumentNullException.ThrowIfNull(term);
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

    /// <summary>The tier as the price engine reads it (WI-058's <see cref="PercentageTier"/>).</summary>
    public PercentageTier ToEngine() =>
        new(Name, PercentOff, _overrides.ToDictionary(entry => entry.ProductId, entry => entry.ToEngine()));
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
