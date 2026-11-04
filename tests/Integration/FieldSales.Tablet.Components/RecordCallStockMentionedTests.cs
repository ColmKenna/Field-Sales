using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-050 (T-5.3.2, REP-US-006 S8–S12) on T-06: "Stock mentioned" on a phone Call. Agreed scenarios 1–5 and 7–11.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static readonly Guid DoylesId = Guid.Parse("57000000-0000-0000-0000-000000000002");

    private static OfflineLocationDetail Doyles() => new(new SnapshotLocation(DoylesId, "Doyle's Chemist", Guid.NewGuid(),
        "Doyle Retail", new(Guid.NewGuid(), "Rathdrum", "Wicklow"), null, null), true, false);

    private static readonly ProductSearchResult Spf30 = Stocked(17, "SPF30 Sun Lotion 200ml", Remedies);
    private static readonly ProductSearchResult Sudocrem = Stocked(18, "Sudocrem 125g", Remedies);

    // Doyle's Suggested List of 16: the 14 numbered products, SPF30 and Sudocrem. Vitamin D and the oats are in the catalogue only.
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

    /// <summary>The rows on Stock mentioned, in order, by the product each Low toggle is for.</summary>
    private static string[] MentionedRows(Screen screen) => [.. screen.Renderer.AttributeValues(screen.Id, "aria-label")
        .Where(label => label.StartsWith("Low ", StringComparison.Ordinal)).Select(label => label["Low ".Length..])];

    private static bool Labelled(Screen screen, string label) => screen.Renderer.AttributeValues(screen.Id, "aria-label").Contains(label);

    private static Task FilterAsync(Screen screen, string text) => screen.Type("stock-filter", text);

    private static CallStockEntry Mention(ProductSearchResult product, decimal? count = null, bool low = false) =>
        new(product.Id, product.Code, product.Name, product.Unit, count, low);

    private static async Task<CallContent> ReviewAndSaveAsync(Screen screen, SavedCalls calls, string summary)
    {
        await screen.Click("Review and save");
        Assert.Contains(summary, screen.Renderer.Text(screen.Id));
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);
        Assert.Equal(summary, calls.Saves[0].Content.Summary);
        return calls.Saves[0].Content;
    }

    // Scenario 1 (S8).
    [Fact]
    public async Task Should_ShowStockMentionedWithTheSame16ProductsAndAFilterBox_When_PhoneIsChosenAtDoyles()
    {
        await using Screen screen = await PhoneCallAsync(new());
        string text = screen.Renderer.Text(screen.Id);

        Assert.Contains("Stock mentioned", text);
        Assert.DoesNotContain("Stock check", text);
        Assert.Contains("stock-filter", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.Contains("Filter or search", text);
        string[] rows = MentionedRows(screen);
        Assert.Equal(16, rows.Length);
        foreach (string name in rows)
        {
            Assert.Equal(("false", "false"), (Pressed(screen, $"Low {name}"), Pressed(screen, $"Out {name}")));
            // WI-050 showed Add to order disabled; WI-051 (T-5.5.1 D4) makes it available on every phone row.
            Assert.True(Labelled(screen, $"Add {name} to order"));
            Assert.Null(screen.Renderer.LabelledAttribute(screen.Id, $"Add {name} to order", "disabled"));
            Assert.True(Labelled(screen, $"Add count for {name}"));
            Assert.False(Labelled(screen, $"Less {name}") || Labelled(screen, $"More {name}") || Labelled(screen, $"Count for {name}"));
        }
        Assert.False(Labelled(screen, $"Remove {rows[0]}"));
        Assert.DoesNotContain("Add product", screen.Renderer.Buttons(screen.Id));
        // The same 16 products as the stock check In person, in the same order.
        await screen.Click("In person");
        Assert.Equal(rows, StockRows(screen));
    }

    // Scenario 2 (S9): name or code, every word typed, ignoring case.
    [Fact]
    public async Task Should_NarrowTheList_When_ITypeInTheFilterBox()
    {
        await using Screen screen = await PhoneCallAsync(new());

        await FilterAsync(screen, "sudo");
        Assert.Equal([Sudocrem.Name], MentionedRows(screen));
        await screen.ClickLabelled($"Low {Sudocrem.Name}");

        await FilterAsync(screen, "p-0");
        Assert.Equal([.. Numbered.Take(9).Select(product => product.Name)], MentionedRows(screen));
        await FilterAsync(screen, "200 SUN");
        Assert.Equal([Spf30.Name], MentionedRows(screen));

        await FilterAsync(screen, "");
        Assert.Equal(16, MentionedRows(screen).Length);
        // A mark made while filtered stays.
        Assert.Equal("true", Pressed(screen, $"Low {Sudocrem.Name}"));
        Assert.DoesNotContain("From the catalogue", screen.Renderer.Text(screen.Id));
    }

    // Scenario 3 (S9, D7).
    [Fact]
    public async Task Should_OfferTheCatalogueAndAddTheProductAsARow_When_NothingOnTheListMatches()
    {
        SavedCalls calls = new();
        await using Screen screen = await PhoneCallAsync(calls);

        await FilterAsync(screen, "zzz");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("No products match 'zzz'"));
        await FilterAsync(screen, "vitamin");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Nothing on the list matches 'vitamin'. From the catalogue:"));
        Assert.Empty(MentionedRows(screen));
        await screen.ClickLabelled($"Add {VitaminD.Name}");

        // The product is a row now, showing under the same filter, ready to mark.
        Assert.Equal([VitaminD.Name], MentionedRows(screen));
        Assert.DoesNotContain("From the catalogue", screen.Renderer.Text(screen.Id));
        await screen.ClickLabelled($"Low {VitaminD.Name}");
        await FilterAsync(screen, "");
        Assert.Equal([.. DoylesStock().Suggested.Select(row => row.Product.Name).Append(VitaminD.Name)
            .Order(StringComparer.CurrentCultureIgnoreCase)], MentionedRows(screen));

        CallContent saved = await ReviewAndSaveAsync(screen, calls, "Phone · 1 marked Low");
        Assert.Equal([Mention(VitaminD, low: true)], saved.StockEntries);
    }

    // Scenario 4 (S10, D6).
    [Fact]
    public async Task Should_AlsoMarkLowWithACountOf0_When_SudocremIsMarkedOut()
    {
        SavedCalls calls = new();
        await using Screen screen = await PhoneCallAsync(calls);

        await screen.ClickLabelled($"Out {Sudocrem.Name}");

        Assert.Equal(("true", "true"), (Pressed(screen, $"Low {Sudocrem.Name}"), Pressed(screen, $"Out {Sudocrem.Name}")));
        Assert.Equal("0", screen.Renderer.Value(screen.Id, $"count-{Sudocrem.Id}"));
        Assert.Contains("1 marked Low (1 out)", screen.Renderer.Text(screen.Id));

        // Out off removes the 0 and keeps Low; Low off also clears Out.
        await screen.ClickLabelled($"Out {Sudocrem.Name}");
        Assert.Equal(("true", "false"), (Pressed(screen, $"Low {Sudocrem.Name}"), Pressed(screen, $"Out {Sudocrem.Name}")));
        Assert.True(Labelled(screen, $"Add count for {Sudocrem.Name}"));
        await screen.ClickLabelled($"Out {Sudocrem.Name}");
        await screen.ClickLabelled($"Low {Sudocrem.Name}");
        Assert.Equal(("false", "false"), (Pressed(screen, $"Low {Sudocrem.Name}"), Pressed(screen, $"Out {Sudocrem.Name}")));
        Assert.True(Labelled(screen, $"Add count for {Sudocrem.Name}"));

        await screen.ClickLabelled($"Out {Sudocrem.Name}");
        CallContent saved = await ReviewAndSaveAsync(screen, calls, "Phone · 1 marked Low (1 out)");
        Assert.Equal([Mention(Sudocrem, 0m, low: true)], saved.StockEntries);
    }

    // Scenario 5 (S11).
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
        CallContent saved = await ReviewAndSaveAsync(screen, calls, "Phone · 2 marked Low (1 out)");

        Assert.Equal(CallChannel.Phone, saved.Channel);
        // Only the 2 mentioned products are recorded; the 14 untouched rows are not saved as Not checked.
        Assert.Equal([Mention(Spf30, low: true), Mention(Sudocrem, 0m, low: true)], saved.StockEntries);
    }

    // Scenario 7 (S12).
    [Fact]
    public async Task Should_KeepEverythingEntered_When_SwitchingPhoneToInPersonAndBack()
    {
        await using Screen screen = await PhoneCallAsync(new());
        await screen.ClickLabelled($"Low {Spf30.Name}");
        await screen.ClickLabelled($"Out {Sudocrem.Name}");
        await screen.ClickLabelled($"Add count for {Numbered[0].Name}");
        await CountAsync(screen, Numbered[0], "3");

        await screen.Click("In person");
        Assert.Equal(("3", "0"), (screen.Renderer.Value(screen.Id, $"count-{Numbered[0].Id}"), screen.Renderer.Value(screen.Id, $"count-{Sudocrem.Id}")));
        Assert.Contains("2 counted, 14 not checked · 2 Low", screen.Renderer.Text(screen.Id));
        await CountAsync(screen, Numbered[1], "5");

        await screen.Click("Phone");
        Assert.Equal(("true", "true"), (Pressed(screen, $"Low {Spf30.Name}"), Pressed(screen, $"Out {Sudocrem.Name}")));
        Assert.Equal(("3", "5"), (screen.Renderer.Value(screen.Id, $"count-{Numbered[0].Id}"), screen.Renderer.Value(screen.Id, $"count-{Numbered[1].Id}")));
        Assert.Contains("2 marked Low (1 out) · 2 counted", screen.Renderer.Text(screen.Id));
    }

    // Scenario 8 (D6): Add count records a count only, with the shared count checks.
    [Fact]
    public async Task Should_RecordOnlyTheCount_When_AddCountIsUsedOnAPhoneRow()
    {
        SavedCalls calls = new();
        await using Screen screen = await PhoneCallAsync(calls);

        await screen.ClickLabelled($"Add count for {Numbered[0].Name}");
        Assert.Equal("", screen.Renderer.Value(screen.Id, $"count-{Numbered[0].Id}") ?? "");
        Assert.False(Labelled(screen, $"More {Numbered[0].Name}"));
        await CountAsync(screen, Numbered[0], "-2");
        await screen.Click("Review and save");
        Assert.Contains(CallContract.CountBelowZero, screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Save this call?", screen.Renderer.Text(screen.Id));

        // Clearing the box leaves the row untouched.
        await CountAsync(screen, Numbered[0], "");
        Assert.DoesNotContain("counted", screen.Renderer.Text(screen.Id));
        await CountAsync(screen, Numbered[0], "2");
        Assert.Equal("false", Pressed(screen, $"Low {Numbered[0].Name}"));
        CallContent saved = await ReviewAndSaveAsync(screen, calls, "Phone · 1 counted");
        Assert.Equal([Mention(Numbered[0], 2m)], saved.StockEntries);
    }

    // Scenario 9 (D6).
    [Fact]
    public async Task Should_SetTheCountTo0_When_AddCountThenOut()
    {
        SavedCalls calls = new();
        await using Screen screen = await PhoneCallAsync(calls);
        await screen.ClickLabelled($"Add count for {Sudocrem.Name}");
        await CountAsync(screen, Sudocrem, "5");

        await screen.ClickLabelled($"Out {Sudocrem.Name}");

        Assert.Equal("0", screen.Renderer.Value(screen.Id, $"count-{Sudocrem.Id}"));
        Assert.Equal("true", Pressed(screen, $"Low {Sudocrem.Name}"));
        CallContent saved = await ReviewAndSaveAsync(screen, calls, "Phone · 1 marked Low (1 out)");
        Assert.Equal([Mention(Sudocrem, 0m, low: true)], saved.StockEntries);
    }

    // Scenario 10 (S5, D3).
    [Fact]
    public async Task Should_ShowAddPitchNotesAStockCheckOrACompetitorNote_When_PhoneHasNothingMarked()
    {
        SavedCalls calls = new();
        await using Screen screen = await PhoneCallAsync(calls);
        await screen.ClickLabelled($"Add count for {Spf30.Name}"); // Opened but left blank: still nothing mentioned.

        await screen.Click("Review and save");

        Assert.Contains(CallContract.RecordSomething, screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Save this call?", screen.Renderer.Text(screen.Id));
        Assert.Empty(calls.Saves);
        // A Low mark alone is something recorded.
        await screen.ClickLabelled($"Low {Spf30.Name}");
        await ReviewAndSaveAsync(screen, calls, "Phone · 1 marked Low");
    }

    // Scenario 11 (D5): marks made on Phone are saved on an In-person Call and shown there. WI-050 showed them as a read-only
    // "Low"/"Out" note; WI-051 (T-5.5.1 D3) shows them as the In-person Low tick, with Out's count of 0.
    [Fact]
    public async Task Should_SaveLowMarksAndShowThem_When_TheCallIsSavedInPersonAfterPhone()
    {
        SavedCalls calls = new();
        await using Screen screen = await PhoneCallAsync(calls);
        await screen.ClickLabelled($"Low {Spf30.Name}");
        await screen.ClickLabelled($"Out {Sudocrem.Name}");

        await screen.Click("In person");

        Assert.Contains("Stock check", screen.Renderer.Text(screen.Id));
        Assert.Equal(("true", "true", "false"),
            (Pressed(screen, $"Low {Spf30.Name}"), Pressed(screen, $"Low {Sudocrem.Name}"), Pressed(screen, $"Low {Numbered[0].Name}")));
        Assert.Equal("0", screen.Renderer.Value(screen.Id, $"count-{Sudocrem.Id}"));
        CallContent saved = await ReviewAndSaveAsync(screen, calls, "In person · 1 counted, 15 not checked · 2 Low");
        Assert.Equal(16, saved.StockEntries!.Count);
        Assert.Equal([Mention(Spf30, low: true), Mention(Sudocrem, 0m, low: true)], saved.StockEntries.Where(entry => entry.Low));
        Assert.All(saved.StockEntries.Where(entry => !entry.Low), entry => Assert.Null(entry.Count));
    }
}
