using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-050 (T-5.3.2, REP-US-006 S8–S12) on T-06: "Stock mentioned" on a phone Call.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static readonly Guid DoylesId = Guid.Parse("57000000-0000-0000-0000-000000000002");

    private static OfflineLocationDetail Doyles() => new(new SnapshotLocation(DoylesId, "Doyle's Chemist", Guid.NewGuid(),
        "Doyle Retail", new(Guid.NewGuid(), "Rathdrum", "Wicklow"), null, null), true, false);

    private static readonly ProductSearchResult Spf30 = Stocked(17, "SPF30 Sun Lotion 200ml", Remedies);
    private static readonly ProductSearchResult Sudocrem = Stocked(18, "Sudocrem 125g", Remedies);

    // Doyle's Suggested List of 16: the 14 numbered products, SPF30 and Sudocrem.
    private static StockCheckStart DoylesStock()
    {
        ProductSearchIndex products = new("g1", [.. Numbered, VitaminD, Oats, Spf30, Sudocrem], [Health, Remedies, Vitamins, Food]);
        return new(products, [.. Numbered.Append(Spf30).Append(Sudocrem)
            .Select(product => new SuggestedProduct(product, SuggestionSources.LastVisit))
            .OrderBy(row => row.Product.Name, StringComparer.CurrentCultureIgnoreCase)]);
    }

    private static async Task<Screen> PhoneCallAsync(SavedCalls calls)
    {
        Screen screen = new(typeof(RecordCall));
        screen.Tablet.Read = (_, _) => Doyles();
        screen.Tablet.SaveWork = calls.Save;
        screen.Tablet.ReadStock = (_, _) => DoylesStock();
        await screen.Open(new() { [nameof(RecordCall.Id)] = DoylesId });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Stock check"));
        await screen.Click("Phone");
        return screen;
    }

    private static string? Pressed(Screen screen, string label) => screen.Renderer.LabelledAttribute(screen.Id, label, "aria-pressed");

    [Fact]
    public async Task Should_ReviewPhone2MarkedLow1OutAndRecordOnlyThe2_When_SPF30IsLowSudocremOutAnd14Untouched()
    {
        SavedCalls calls = new();
        await using Screen screen = await PhoneCallAsync(calls);
        Assert.Contains("Stock mentioned", screen.Renderer.Text(screen.Id));

        await screen.ClickLabelled($"Low {Spf30.Name}");
        await screen.ClickLabelled($"Out {Sudocrem.Name}");

        Assert.Equal(("true", "false", "true", "true"), (Pressed(screen, $"Low {Spf30.Name}"), Pressed(screen, $"Out {Spf30.Name}"),
            Pressed(screen, $"Low {Sudocrem.Name}"), Pressed(screen, $"Out {Sudocrem.Name}")));
        Assert.Contains("2 marked Low (1 out)", screen.Renderer.Text(screen.Id));
        await screen.Click("Review and save");
        Assert.Contains("Phone · 2 marked Low (1 out)", screen.Renderer.Text(screen.Id));
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);

        CallContent saved = calls.Saves[0].Content;
        Assert.Equal(CallChannel.Phone, saved.Channel);
        // Only the 2 mentioned products are recorded; the 14 untouched rows are not saved as Not checked.
        Assert.Equal(
        [
            new CallStockEntry(Spf30.Id, Spf30.Code, Spf30.Name, "Each", null, true),
            new CallStockEntry(Sudocrem.Id, Sudocrem.Code, Sudocrem.Name, "Each", 0m, true)
        ], saved.StockEntries);
        Assert.Equal("Phone · 2 marked Low (1 out)", saved.Summary);
    }
}
