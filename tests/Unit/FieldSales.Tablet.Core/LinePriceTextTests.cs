using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-065 agreed scenarios 1–5 and 7 (Pricing US-005 S2, S3; MI-54): an order line's price text, read only from what the
/// line captured.</summary>
[Trait("Category", "Unit")]
public sealed class LinePriceTextTests
{
    private static OrderPriceCandidate Tier(decimal amount, bool winner = false) => new("Tier", "Tier B", amount, winner);
    private static OrderPriceCandidate Break(decimal amount, bool winner = false, string label = "Break: 10+") => new("Break", label, amount, winner);
    private static OrderPriceCandidate List(decimal amount, bool winner = false) =>
        new(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, amount, winner);

    // A captured price: the winner first, then the others cheapest first, as the tablet captures them (WI-064 D5).
    private static OrderLinePrice Price(params OrderPriceCandidate[] candidates) =>
        new(candidates[0].Amount, candidates[0].Kind, candidates[0].Label, candidates);

    [Fact]
    public void Should_ReadTheTierWithTheListPrice_When_TierBWins() =>
        Assert.Equal("€11.20 — Tier B (list €12.50)", LinePriceText.Summary(Price(Tier(11.20m, true), List(12.50m)), "Each"));

    [Fact]
    public void Should_ReadListPriceWithNoRunnerUp_When_BaseWins()
    {
        Assert.Equal("€12.50 — list price", LinePriceText.Summary(Price(List(12.50m, true)), "Each")); // As every line before WI-064.
        Assert.Equal("€12.50 — list price", LinePriceText.Summary(Price(List(12.50m, true), Tier(13.00m)), "Each"));
    }

    [Fact]
    public void Should_ShowTheBreakAndTheTierAsRunnerUp_When_ABreakBeatsTheTier() =>
        Assert.Equal("€11.00 — Break: 10+ (better than your tier price €11.50)",
            LinePriceText.Summary(Price(Break(11.00m, true), Tier(11.50m), List(12.50m)), "Each"));

    [Fact]
    public void Should_ShowTheBreakWithTheListPrice_When_NoTierPricesTheProduct() =>
        Assert.Equal("€11.00 — Break: 10+ (list €12.50)", LinePriceText.Summary(Price(Break(11.00m, true), List(12.50m)), "Each"));

    [Fact]
    public void Should_ShowPerUnitPrices_When_TheProductIsMeasured() =>
        Assert.Equal("€4.00 per kg — Break: 10 kg+ (list €4.80 per kg)",
            LinePriceText.Summary(Price(Break(4.00m, true, "Break: 10 kg+"), List(4.80m)), "kg"));

    [Fact]
    public void Should_ListTheEqualCandidateUnderAlsoConsidered_When_TwoCandidatesTie()
    {
        OrderLinePrice tie = Price(Tier(12.50m, true), List(12.50m)); // On a tie the tier is applied (WI-058 D3).

        Assert.Equal("€12.50 — Tier B (list €12.50)", LinePriceText.Summary(tie, "Each"));
        Assert.Equal(["Tier B", "List price"], tie.Candidates.Select(LinePriceText.Name));
        Assert.Equal("€10.50 — Tier B", LinePriceText.Summary(Price(Tier(10.50m, true)), "Each")); // No list price that day.
    }
}
