using FieldSales.Calls.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-057 agreed scenarios 1–7, 10, 11 and 13 (T-5.8.1, REP-US-018 S1–S5 and edge cases) on T-06: Competitor Notes, each
/// with one link type, in a collapsed section; their content rules are Tablet.Core unit scenarios 8 and 9, corrections in storage 12,
/// the server 14 and 15.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static PadCategory Suncare => new(StockId(1, 11), Health.Id, "Suncare");
    private static ProductSearchResult Spf30Small => Stocked(30, "SPF30 Sun Lotion 200ml", Suncare);
    private static ProductSearchResult Spf30Large => Stocked(31, "SPF30 Sun Lotion 300ml", Suncare);
    private static ProductSearchResult AfterSun => Stocked(32, "After Sun Gel 200ml", Suncare);

    // Murphy's Suggested List: both SPF30 sizes, After Sun and Cold & Flu.
    private static StockCheckStart SuncareStock
    {
        get
        {
            ProductSearchResult coldFlu = ColdFlu;
            ProductSearchIndex products = new("g1", [Spf30Small, Spf30Large, AfterSun, coldFlu], [Health, Remedies, Suncare]);
            return new(products, [.. new[] { Spf30Small, Spf30Large, AfterSun, coldFlu }
                .Select(product => new SuggestedProduct(product, SuggestionSources.LastVisit))
                .OrderBy(row => row.Product.Name, StringComparer.CurrentCultureIgnoreCase)]);
        }
    }

    private static async Task<Screen> CompetitorCallAsync(SavedCalls calls)
    {
        Screen screen = new(typeof(RecordCall));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.SaveWork = calls.Save;
        screen.Tablet.ReadStock = (_, _) => SuncareStock;
        await screen.Open(new() { [nameof(RecordCall.Id)] = MurphysId });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Stock check"));
        await screen.Click("In person");
        return screen;
    }

    /// <summary>Opens the section if it is closed and adds a note with this text; returns its number.</summary>
    private static async Task<int> AddNoteAsync(Screen screen, string text)
    {
        if (!screen.Renderer.Buttons(screen.Id).Contains("Add competitor note"))
            await screen.Click(screen.Renderer.Buttons(screen.Id).Single(button => button.StartsWith("Competitor notes", StringComparison.Ordinal)));
        await screen.Click("Add competitor note");
        int number = screen.Renderer.AttributeValues(screen.Id, "id").Count(id => id.StartsWith("competitor-note-", StringComparison.Ordinal));
        await screen.Type($"competitor-note-{number}", text);
        return number;
    }

    private static async Task<CallContent> SaveCallAsync(Screen screen, SavedCalls calls, string summary)
    {
        await screen.Click("Review and save");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Save this call?"));
        Assert.Contains(summary, screen.Renderer.Text(screen.Id));
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);
        return calls.Saves[0].Content;
    }

    /// <summary>The product and category picks offered to note <paramref name="number"/>, in the order shown.</summary>
    private static string[] Picks(Screen screen, int number) => [.. screen.Renderer.AttributeValues(screen.Id, "aria-label")
        .Where(label => label.StartsWith("Link ", StringComparison.Ordinal) && label.EndsWith($" to note {number}", StringComparison.Ordinal)
            && !label.StartsWith($"Link note {number} ", StringComparison.Ordinal))
        .Select(label => label["Link ".Length..^$" to note {number}".Length])];

    // Scenario 1 (S1).
    [Fact]
    public async Task Should_SaveTheNoteWith2ProductLinks_When_CompetitorSpf30IsLinkedToBothCountedSpf30Sizes()
    {
        SavedCalls calls = new();
        await using Screen screen = await CompetitorCallAsync(calls);
        await CountAsync(screen, Spf30Small, "2");
        await CountAsync(screen, Spf30Large, "4");

        int note = await AddNoteAsync(screen, "Competitor 250ml SPF30 on shelf");
        await screen.ClickLabelled($"Link note {note} to Products");
        await screen.ClickLabelled($"Link {Spf30Small.Name} to note {note}");
        await screen.ClickLabelled($"Link {Spf30Large.Name} to note {note}");
        CallContent content = await SaveCallAsync(screen, calls, "· 1 competitor note");

        CallCompetitorNote saved = Assert.Single(content.CompetitorNotes!);
        Assert.Equal(("Competitor 250ml SPF30 on shelf", CompetitorLinkType.Products), (saved.Text, saved.LinkTo));
        Assert.Equal([Spf30Small.Id, Spf30Large.Id], saved.Products!.Select(product => product.ProductId));
        Assert.Equal((null, null), (saved.Range, saved.Category));
    }

    // Scenario 2 (S2).
    [Fact]
    public async Task Should_SaveTheNoteWith1CategoryLinkAndNoProducts_When_RivalSuncareStandIsLinkedToSuncare()
    {
        SavedCalls calls = new();
        await using Screen screen = await CompetitorCallAsync(calls);

        int note = await AddNoteAsync(screen, "Rival suncare stand by the till");
        await screen.ClickLabelled($"Link note {note} to Category");
        await screen.Type($"note-{note}-category-search", "suncare");
        string suncare = Assert.Single(Picks(screen, note));
        await screen.ClickLabelled($"Link {suncare} to note {note}");
        CallContent content = await SaveCallAsync(screen, calls, "In person · 4 not checked · 1 competitor note"); // Nothing counted.

        CallCompetitorNote saved = Assert.Single(content.CompetitorNotes!);
        Assert.Equal((CompetitorLinkType.Category, Suncare.Id), (saved.LinkTo, saved.Category?.Id));
        Assert.Equal((null, null), (saved.Products, saved.Range));
    }

    // Scenario 3 (S3).
    [Fact]
    public async Task Should_SaveWithNoLinks_When_LinkToIsLeftAtNone()
    {
        SavedCalls calls = new();
        await using Screen screen = await CompetitorCallAsync(calls);

        int note = await AddNoteAsync(screen, "Rival tester unit by the door");
        Assert.Equal("true", screen.Renderer.LabelledAttribute(screen.Id, $"Link note {note} to None", "aria-pressed"));
        CallContent content = await SaveCallAsync(screen, calls, "1 competitor note");

        CallCompetitorNote saved = Assert.Single(content.CompetitorNotes!);
        Assert.Equal((CompetitorLinkType.None, null, null, null), (saved.LinkTo, saved.Products, saved.Range, saved.Category));
    }

    // Scenario 4 (S4).
    [Fact]
    public async Task Should_AskBeforeRemoving2LinkedProductsAndKeepThemOnCancel_When_LinkToIsSwitchedToRange()
    {
        await using Screen screen = await CompetitorCallAsync(new());
        await CountAsync(screen, Spf30Small, "2");
        await CountAsync(screen, Spf30Large, "4");
        int note = await AddNoteAsync(screen, "Competitor 250ml SPF30 on shelf");
        await screen.ClickLabelled($"Link note {note} to Products");
        await screen.ClickLabelled($"Link {Spf30Small.Name} to note {note}");
        await screen.ClickLabelled($"Link {Spf30Large.Name} to note {note}");

        await screen.ClickLabelled($"Link note {note} to Range");
        Assert.Contains("Switching will remove 2 linked products", screen.Renderer.Text(screen.Id));
        await screen.Click("Cancel");

        Assert.DoesNotContain("Switching will remove", screen.Renderer.Text(screen.Id));
        Assert.Equal("true", screen.Renderer.LabelledAttribute(screen.Id, $"Link note {note} to Products", "aria-pressed"));
        Assert.Equal(2, screen.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.Competitor.Single().Products!.Count);

        await screen.ClickLabelled($"Link note {note} to Range");
        await screen.Click("Switch");
        CallCompetitorNote switched = screen.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.Competitor.Single();
        Assert.Equal((CompetitorLinkType.Range, null), (switched.LinkTo, switched.Products));
    }

    // Scenario 5 (S5).
    [Fact]
    public async Task Should_SaveThreeNotesSeparatelyAndRead3CompetitorNotes_When_ThreeNotesAreAddedToOneCall()
    {
        SavedCalls calls = new();
        await using Screen screen = await CompetitorCallAsync(calls);

        await AddNoteAsync(screen, "Competitor 250ml SPF30 on shelf");
        await AddNoteAsync(screen, "Rival suncare stand by the till");
        await AddNoteAsync(screen, "Rival tester unit by the door");
        Assert.Contains("Competitor notes (3)", screen.Renderer.Buttons(screen.Id));
        CallContent content = await SaveCallAsync(screen, calls, "In person · 4 not checked · 3 competitor notes");

        Assert.Equal(["Competitor 250ml SPF30 on shelf", "Rival suncare stand by the till", "Rival tester unit by the door"],
            content.CompetitorNotes!.Select(note => note.Text));
        Assert.Equal(3, content.CompetitorNotes!.Select(note => note.Id).Distinct().Count());
    }

    // Scenario 6 (D4): this Call's counted and Low products come first, Not checked rows don't; search finds the rest.
    [Fact]
    public async Task Should_ListThisCallsCountedAndLowProductsFirst_When_TheProductPickerOpens()
    {
        await using Screen screen = await CompetitorCallAsync(new());
        await CountAsync(screen, Spf30Small, "2");
        await CountAsync(screen, Spf30Large, "4");
        await screen.ClickLabelled($"Low {ColdFlu.Name}"); // Low without a count; After Sun is left Not checked.

        int note = await AddNoteAsync(screen, "Competitor 250ml SPF30 on shelf");
        await screen.ClickLabelled($"Link note {note} to Products");

        Assert.Equal([ColdFlu.Name, Spf30Small.Name, Spf30Large.Name], Picks(screen, note));
        Assert.Contains("From this call", screen.Renderer.Text(screen.Id));
        await screen.Type($"note-{note}-product-search", "sun");
        Assert.Equal([ColdFlu.Name, Spf30Small.Name, Spf30Large.Name, AfterSun.Name], Picks(screen, note)); // Each product once.
    }

    // Scenario 7 (criterion 7): until E9 there are no Ranges to pick, so a Range note can't be saved.
    [Fact]
    public async Task Should_OfferNoRangesAndRefuseTheNote_When_TheSnapshotHasNoRanges()
    {
        SavedCalls calls = new();
        await using Screen screen = await CompetitorCallAsync(calls);
        int note = await AddNoteAsync(screen, "Rival autumn range on the end bay");

        await screen.ClickLabelled($"Link note {note} to Range");

        Assert.Contains(CompetitorNoteDrafts.NoRanges, screen.Renderer.Text(screen.Id));
        Assert.Empty(Picks(screen, note));
        await screen.Click("Review and save");
        Assert.Contains(CallCompetitorNote.ChooseARange, screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Save this call?", screen.Renderer.Text(screen.Id));
        Assert.Empty(calls.Saves);
    }

    // Scenario 10 (D3): a resume rebuilds the page; the notes, their links and the open section come back.
    [Fact]
    public async Task Should_KeepTheCompetitorNotes_When_RecordCallIsRebuilt()
    {
        SavedCalls calls = new();
        await using Screen screen = await CompetitorCallAsync(calls);
        await CountAsync(screen, Spf30Small, "2");
        int note = await AddNoteAsync(screen, "Competitor 250ml SPF30 on shelf");
        await screen.ClickLabelled($"Link note {note} to Products");
        await screen.ClickLabelled($"Link {Spf30Small.Name} to note {note}");

        await screen.Rebuild();
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Murphy's Pharmacy"));

        Assert.Contains("Competitor 250ml SPF30 on shelf", screen.Renderer.AttributeValues(screen.Id, "value"));
        Assert.Equal("true", screen.Renderer.LabelledAttribute(screen.Id, $"Link {Spf30Small.Name} to note {note}", "aria-pressed"));
        CallContent content = await SaveCallAsync(screen, calls, "1 competitor note");
        Assert.Equal([Spf30Small.Id], content.CompetitorNotes!.Single().Products!.Select(product => product.ProductId));
    }

    // A Call saved at Murphy's recording a count and two notes.
    private static TabletCall NotedAtMurphys => SavedAtMurphys(CallChannel.InPerson, null, (ColdFlu, 3m, false)) with
    {
        CompetitorNotes =
        [
            new(new("c0c0c0c0-0000-0000-0000-000000000001"), "Competitor 250ml SPF30 on shelf", CompetitorLinkType.Products,
                [new(Spf30Small.Id, Spf30Small.Code, Spf30Small.Name), new(Spf30Large.Id, Spf30Large.Code, Spf30Large.Name)]),
            new(new("c0c0c0c0-0000-0000-0000-000000000002"), "Rival suncare stand by the till", CompetitorLinkType.Category,
                Category: new(Suncare.Id, "Health > Suncare"))
        ]
    };

    // Scenario 11 (D6, completing US-015 S2).
    [Fact]
    public async Task Should_ChangeNoteTextAndRemoveNotesButAddNone_When_EditCallHasCompetitorNotes()
    {
        Corrections corrections = new();
        await using Screen screen = await EditCallAsync(NotedAtMurphys, corrections);

        Assert.DoesNotContain("Add competitor note", screen.Renderer.Buttons(screen.Id));
        Assert.DoesNotContain(screen.Renderer.AttributeValues(screen.Id, "aria-label"), label => label.StartsWith("Link ", StringComparison.Ordinal));
        Assert.Contains($"Linked to: {Spf30Small.Name}, {Spf30Large.Name}", screen.Renderer.Text(screen.Id));
        await screen.Type("competitor-note-1", "Competitor 250ml SPF30, 2 facings");
        await screen.ClickLabelled("Remove note 2");
        await SaveChangesAsync(screen);

        CallCompetitorNote kept = Assert.Single(corrections.Saves.Single().Content.CompetitorNotes!);
        CallCompetitorNote before = NotedAtMurphys.CompetitorNotes![0];
        Assert.Equal((before.Id, "Competitor 250ml SPF30, 2 facings"), (kept.Id, kept.Text));
        Assert.True(kept.SameLinks(before));
    }

    // Scenario 13 (D7).
    [Fact]
    public async Task Should_ShowEachCompetitorNoteWithItsLinks_When_ASavedCallIsOpened()
    {
        await using Screen screen = await CallPageAsync(NotedAtMurphys, CallStanding.Sent);

        Assert.Equal(
            [$"Competitor 250ml SPF30 on shelf — {Spf30Small.Name}, {Spf30Large.Name}", "Rival suncare stand by the till — Category: Health > Suncare"],
            screen.Renderer.AttributeValues(screen.Id, "data-competitor-note"));
        Assert.Contains("1 counted · 2 competitor notes", screen.Renderer.Text(screen.Id));
    }
}
