using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-065 agreed scenarios 6 and 8 (Pricing US-005 S4; conventions §1): an order line's (i) opens the provenance sheet —
/// APPLIED, then ALSO CONSIDERED in the engine's order — read from what the line captured.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static readonly OrderSnapshotRef Captured = new("1.6", DateTimeOffset.UnixEpoch);

    // Murphy's order with a Lozenges line captured as a break beating Tier B, and a Plasters line captured at its list price only.
    private static TabletOrder PricedOrder() => new(OrderContract.Schema, 1, Guid.NewGuid(), OrderState.InProgress, MurphysId, MurphysId,
        "colm", new(2026, 9, 22), null, Captured,
    [
        new(Guid.NewGuid(), Lozenges.Id, Lozenges.Code, Lozenges.Name, 12m, "Each",
            new(2.50m, "Break", "Break: 10+", [new("Break", "Break: 10+", 2.50m, true), new("Tier", "Tier B", 2.76m, false),
                new(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 3.00m, false)]), Captured),
        new(Guid.NewGuid(), Plasters.Id, Plasters.Code, Plasters.Name, 2m, "Each",
            new(2.00m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel,
                [new(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 2.00m, true)]), Captured)
    ]);

    private static string SheetText(Screen screen, string product)
    {
        string text = screen.Renderer.Text(screen.Id);
        int sheet = text.IndexOf($"Price for {product}", StringComparison.Ordinal);
        // This renderer's text keeps static markup; only the words count here.
        return sheet < 0 ? "" : System.Text.RegularExpressions.Regex.Replace(text[sheet..], "<[^>]+>", " ");
    }

    [Fact]
    public async Task Should_OpenTheSheetWithAppliedAndAlsoConsidered_When_TheMarkerIsTapped()
    {
        TabletOrder order = PricedOrder();
        (Screen screen, _) = await OrderEntryAsync(null, order, null);
        await using Screen disposing = screen;
        Assert.Contains("€2.50 — Break: 10+ (better than your tier price €2.76)", screen.Renderer.Text(screen.Id));

        await screen.ClickLabelled($"Price details for {Lozenges.Name}");

        string sheet = SheetText(screen, Lozenges.Name);
        int applied = sheet.IndexOf("APPLIED", StringComparison.Ordinal), considered = sheet.IndexOf("ALSO CONSIDERED", StringComparison.Ordinal);
        Assert.True(applied >= 0 && considered > applied, sheet);
        Assert.Matches(@"€2\.50\s*Break: 10\+", sheet[applied..considered]);
        Assert.Matches(@"€2\.76\s*Tier B\s*€3\.00\s*List price", sheet[considered..]); // In the engine's order, cheapest first.

        await screen.ClickLabelled("Close");
        Assert.Equal("", SheetText(screen, Lozenges.Name));
        Assert.Contains("On this order", screen.Renderer.Text(screen.Id)); // Back on the order.
    }

    [Fact]
    public async Task Should_SayNothingElseWasConsidered_When_TheLineHadOneCandidate()
    {
        (Screen screen, _) = await OrderEntryAsync(null, PricedOrder(), null);
        await using Screen disposing = screen;

        await screen.ClickLabelled($"Price details for {Plasters.Name}");

        Assert.Matches(@"APPLIED\s*€2\.00\s*List price\s*ALSO CONSIDERED\s*Nothing else was considered\.", SheetText(screen, Plasters.Name));
    }
}
