using FieldSales.Calls.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-049 (T-5.4.1, REP-US-007) on T-06: the stock check from the Suggested List. Agreed scenarios 6–9, 13–15, 19.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static Guid StockId(int kind, int number) => new($"5a{kind:D6}-0000-0000-0000-{number:D12}");
    private static readonly PadCategory Health = new(StockId(1, 1), null, "Health");
    private static readonly PadCategory Remedies = new(StockId(1, 2), Health.Id, "Remedies");
    private static readonly PadCategory Vitamins = new(StockId(1, 3), Health.Id, "Vitamins");
    private static readonly PadCategory Food = new(StockId(1, 4), null, "Food");

    private static ProductSearchResult Stocked(int number, string name, PadCategory category, string unit = "Each") =>
        new(StockId(2, number), $"P-{number:D2}", name, category.Name, unit, 2.50m, category.Id);

    private static readonly ProductSearchResult[] Numbered =
        [.. Enumerable.Range(1, 14).Select(number => Stocked(number, $"Product {number:D2}", Remedies))];
    private static readonly ProductSearchResult VitaminD = Stocked(15, "Vitamin D 1000IU 90s", Vitamins);
    private static readonly ProductSearchResult Oats = Stocked(16, "Loose Porridge Oats", Food, "kg");

    // Murphy's: products 1-9 from the last visit, 10-12 on both, 13-14 ordered recently (as S1 builds it).
    private static StockCheckStart MurphysStock(params ProductSearchResult[] extraSuggested)
    {
        ProductSearchIndex products = new("g1", [.. Numbered, VitaminD, Oats], [Health, Remedies, Vitamins, Food]);
        SuggestedProduct[] suggested =
        [
            .. Numbered.Select((product, index) => new SuggestedProduct(product, index switch
            {
                < 9 => SuggestionSources.LastVisit,
                < 12 => SuggestionSources.LastVisit | SuggestionSources.OrderedRecently,
                _ => SuggestionSources.OrderedRecently
            })),
            .. extraSuggested.Select(product => new SuggestedProduct(product, SuggestionSources.LastVisit))
        ];
        return new(products, [.. suggested.OrderBy(row => row.Product.Name, StringComparer.CurrentCultureIgnoreCase)]);
    }

    private static async Task<Screen> StockCheckAsync(SavedCalls calls, StockCheckStart stock)
    {
        Screen screen = new(typeof(RecordCall));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.SaveWork = calls.Save;
        screen.Tablet.ReadStock = (_, _) => stock;
        await screen.Open(new() { [nameof(RecordCall.Id)] = MurphysId });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Stock check"));
        return screen;
    }

    /// <summary>The rows on the stock check, in order, by the product each count is for.</summary>
    private static string[] StockRows(Screen screen) => [.. screen.Renderer.AttributeValues(screen.Id, "aria-label")
        .Where(label => label.StartsWith("Count for ", StringComparison.Ordinal)).Select(label => label["Count for ".Length..])];

    private static Task CountAsync(Screen screen, ProductSearchResult product, string count) => screen.Type($"count-{product.Id}", count);

    [Fact]
    public async Task Should_ShowNoSuggestedProductsYetSearchOrBrowseToAdd_When_DoylesHasNoStockCheckAndNoAcceptedOrders()
    {
        await using Screen screen = await StockCheckAsync(new(), new(new ProductSearchIndex("g1", [VitaminD], [Health, Vitamins]), []));

        Assert.Contains("No suggested products yet — search or browse to add", screen.Renderer.Text(screen.Id));
        Assert.Empty(StockRows(screen));
        Assert.Contains("Add product", screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_Have13Products_When_2AreRemovedAndVitaminDIsAddedBySearch()
    {
        await using Screen screen = await StockCheckAsync(new(), MurphysStock());
        Assert.Equal(14, StockRows(screen).Length);
        Assert.Contains("Last visit · Ordered recently", screen.Renderer.Text(screen.Id));

        await screen.ClickLabelled("Remove Product 01");
        await screen.ClickLabelled("Remove Product 13");
        await screen.Click("Add product");
        await screen.Type("stock-search", "vitamin d");
        await screen.Until(() => screen.Renderer.AttributeValues(screen.Id, "aria-label").Contains("Add Vitamin D 1000IU 90s"));
        await screen.ClickLabelled("Add Vitamin D 1000IU 90s");

        string[] rows = StockRows(screen);
        Assert.Equal(13, rows.Length);
        Assert.DoesNotContain("Product 01", rows);
        Assert.DoesNotContain("Product 13", rows);
        Assert.Equal("Vitamin D 1000IU 90s", rows[^1]); // In name order with the suggested rows.
        Assert.True(screen.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.HasWork); // Leaving would lose the changed list.
    }

    [Fact]
    public async Task Should_AddTheProduct_When_ItIsChosenByBrowsingCategories()
    {
        await using Screen screen = await StockCheckAsync(new(), MurphysStock());

        await screen.Click("Add product");
        await screen.Click(screen.Renderer.Buttons(screen.Id).First(button => button.StartsWith("Health", StringComparison.Ordinal)));
        await screen.Click(screen.Renderer.Buttons(screen.Id).First(button => button.StartsWith("Health > Vitamins", StringComparison.Ordinal)));
        await screen.ClickLabelled("Add Vitamin D 1000IU 90s");

        Assert.Equal(15, StockRows(screen).Length);
        Assert.Contains("Vitamin D 1000IU 90s", StockRows(screen));
        // A product already on the list can't be added twice.
        await screen.Click("Add product");
        await screen.Type("stock-search", "product 02");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("On the list"));
        Assert.DoesNotContain("Add Product 02", screen.Renderer.AttributeValues(screen.Id, "aria-label"));
    }

    [Fact]
    public async Task Should_ShowTenCountedThreeNotCheckedInTheReview_When_13ProductsHave3Uncounted()
    {
        SavedCalls calls = new();
        await using Screen screen = await StockCheckAsync(calls, MurphysStock());
        await screen.Click("In person");
        await screen.ClickLabelled("Remove Product 14");
        foreach (ProductSearchResult product in Numbered.Take(9)) await CountAsync(screen, product, "4");
        await screen.ClickLabelled("More Product 10"); // The stepper counts too: Not checked, then + is 1.
        Assert.Equal("1", screen.Renderer.Value(screen.Id, $"count-{Numbered[9].Id}"));
        Assert.Contains("10 counted, 3 not checked", screen.Renderer.Text(screen.Id));

        await screen.Click("Review and save");

        Assert.Contains("Save this call?", screen.Renderer.Text(screen.Id));
        Assert.Contains("In person · 10 counted, 3 not checked", screen.Renderer.Text(screen.Id));
        Assert.Contains("Back", screen.Renderer.Buttons(screen.Id));
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);
        IReadOnlyList<CallStockEntry> entries = calls.Saves[0].Content.StockEntries!;
        Assert.Equal(13, entries.Count);
        Assert.Equal(["Product 11", "Product 12", "Product 13"], entries.Where(entry => !entry.Counted).Select(entry => entry.ProductName));
        Assert.Equal(("P-10", 1m, "Each"), entries.Single(entry => entry.ProductName == "Product 10") is var ten
            ? (ten.ProductCode, ten.Count!.Value, ten.Unit) : default);
    }

    [Fact]
    public async Task Should_RejectTheCountWithEnter0OrMore_When_MinusTwoIsEntered()
    {
        SavedCalls calls = new();
        await using Screen screen = await StockCheckAsync(calls, MurphysStock());
        await screen.Click("In person");

        await CountAsync(screen, Numbered[0], "-2");

        Assert.Contains("Enter 0 or more", screen.Renderer.Text(screen.Id));
        await screen.Click("Review and save");
        Assert.DoesNotContain("Save this call?", screen.Renderer.Text(screen.Id));
        Assert.Empty(calls.Saves);
        await CountAsync(screen, Numbered[0], "0"); // Zero is a count.
        Assert.DoesNotContain("Enter 0 or more", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_AcceptADecimalCountWithItsUnit_When_TheProductIsSoldByMeasure()
    {
        SavedCalls calls = new();
        await using Screen screen = await StockCheckAsync(calls, MurphysStock(Oats));
        await screen.Click("In person");

        await CountAsync(screen, Oats, "3.5");
        await CountAsync(screen, Numbered[0], "2.5");

        Assert.Contains("kg", screen.Renderer.Text(screen.Id));
        Assert.Contains("Enter a whole number", screen.Renderer.Text(screen.Id)); // Only the per-item product is refused.
        await CountAsync(screen, Numbered[0], "2");
        await screen.Click("Review and save");
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);
        CallStockEntry oats = calls.Saves[0].Content.StockEntries!.Single(entry => entry.ProductId == Oats.Id);
        Assert.Equal((3.5m, "kg"), (oats.Count, oats.Unit));
    }

    [Fact]
    public async Task Should_ShowAddPitchNotesAStockCheckOrACompetitorNote_When_EveryLineIsNotChecked()
    {
        SavedCalls calls = new();
        await using Screen screen = await StockCheckAsync(calls, MurphysStock());
        await screen.Click("In person");

        await screen.Click("Review and save");

        Assert.Contains(CallContract.RecordSomething, screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Save this call?", screen.Renderer.Text(screen.Id));
        Assert.Empty(calls.Saves);
    }

    [Fact]
    public async Task Should_HideTheStockCheckAndKeepItsCounts_When_PhoneIsChosenThenInPerson()
    {
        SavedCalls calls = new();
        await using Screen screen = await StockCheckAsync(calls, MurphysStock());
        await screen.Click("In person");
        await CountAsync(screen, Numbered[0], "4");

        await screen.Click("Phone");
        Assert.DoesNotContain("Stock check", screen.Renderer.Text(screen.Id));
        await screen.Click("In person");
        Assert.Equal("4", screen.Renderer.Value(screen.Id, $"count-{Numbered[0].Id}"));

        // A Phone call saves no stock entries (Stock mentioned is T-5.3.2).
        await screen.Click("Phone");
        await EnterNotesAsync(screen, Pitch);
        await screen.Click("Review and save");
        Assert.Contains("Phone · Pitch notes", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("counted", screen.Renderer.Text(screen.Id));
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);
        Assert.Null(calls.Saves[0].Content.StockEntries);
    }
}
