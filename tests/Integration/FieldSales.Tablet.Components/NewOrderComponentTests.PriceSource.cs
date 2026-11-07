using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-065 agreed scenarios 1–3 and 9 on the page (Pricing US-005 S1–S4; conventions §1): each order line shows its
/// price and source from what it captured. The sheet its (i) opens (6, 8) is in PriceSheetScreenTests.</summary>
public sealed partial class NewOrderComponentTests
{
    // Vitamin D captured as Tier B wins (with the list price), Loose Herbal Tea as a break beating Tier B.
    private static OrderLinePrice CapturedWithTiers(ProductSearchResult product) => product.Code switch
    {
        "VITD-1000-90" => new(11.20m, "Tier", "Tier B", [new("Tier", "Tier B", 11.20m, true), new("Base", "Base price", 12.50m, false)]),
        _ => new(4.00m, "Break", "Break: 10 kg+", [new("Break", "Break: 10 kg+", 4.00m, true), new("Tier", "Tier B", 4.42m, false),
            new("Base", "Base price", 4.80m, false)])
    };

    [Fact]
    public async Task Should_ReadTheTierWithTheListPrice_When_TierBWins()
    {
        await using Screen screen = Open(new() { Captures = CapturedWithTiers });
        await screen.Show();
        await AddAsync(screen, "vitamin d", "Vitamin D 1000IU 90s", "12");

        Assert.Contains("€11.20 — Tier B (list €12.50)", LineText(screen, "Vitamin D 1000IU 90s"));
    }

    [Fact]
    public async Task Should_ReadListPriceWithNoRunnerUp_When_BaseWins()
    {
        await using Screen screen = Open(new());
        await screen.Show();
        await AddAsync(screen, "vitamin d", "Vitamin D 1000IU 90s", "12");

        string line = LineText(screen, "Vitamin D 1000IU 90s");
        Assert.Contains("€12.50 — list price", line);
        Assert.DoesNotContain("(", line.Replace("(i)", ""));
    }

    [Fact]
    public async Task Should_ShowTheBreakAndTheTierAsRunnerUp_When_ABreakBeatsTheTier()
    {
        await using Screen screen = Open(new() { Captures = CapturedWithTiers });
        await screen.Show();
        await AddAsync(screen, "loose herbal", "Loose Herbal Tea", "12");

        Assert.Contains("€4.00 per kg — Break: 10 kg+ (better than your tier price €4.42 per kg)", LineText(screen, "Loose Herbal Tea"));
    }

    [Fact]
    public async Task Should_NameTheMarkerAndTheSourceInWords_When_ReadWithoutColour()
    {
        await using Screen screen = Open(new() { Captures = CapturedWithTiers });
        await screen.Show();
        await AddAsync(screen, "vitamin d", "Vitamin D 1000IU 90s", "12");

        Assert.Contains("Price details for Vitamin D 1000IU 90s", screen.Renderer.AttributeValues(screen.Id, "aria-label"));
        Assert.Contains("Tier B", LineText(screen, "Vitamin D 1000IU 90s")); // The source is words on the line, not a colour.
    }
}
