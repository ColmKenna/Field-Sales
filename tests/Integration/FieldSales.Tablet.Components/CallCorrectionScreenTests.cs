using FieldSales.Calls.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-055 agreed scenarios 1–12 (T-5.6.1, REP-US-015 S1–S4) on T-06: a saved Call's page and Edit Call. Edit Call changes and
/// removes what the Call recorded and never adds to it; "Record a follow-up call" stands where adding would be. Storage is
/// Tablet.Core <c>CallCorrectionTests</c> (13–16); the server end to end is 17.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static readonly Guid MurphysCall = new("ca11ca11-0000-0000-0000-000000000022");

    /// <summary>A Call saved at Murphy's on 22 Sep with these rows (count, or null for Not checked, and the Low mark).</summary>
    private static TabletCall SavedAtMurphys(CallChannel channel, string? notes, params (ProductSearchResult Product, decimal? Count, bool Low)[] rows) =>
        new(CallContract.Schema, 1, MurphysCall, MurphysId, "colm", new(2026, 9, 22), new(2026, 9, 22, 9, 15, 0, TimeSpan.Zero), channel,
            new("1.5", new(2026, 9, 22, 6, 30, 0, TimeSpan.Zero)), notes,
            [.. rows.Select(row => new CallStockEntry(row.Product.Id, row.Product.Code, row.Product.Name, row.Product.Unit, row.Count, row.Low))]);

    // In person: Cold & Flu counted 3 and Low, Vitamin D counted 2 (by mistake), Throat Lozenges Not checked; no pitch notes.
    private static TabletCall CountedAtMurphys => SavedAtMurphys(CallChannel.InPerson, null,
        (ColdFlu, 3m, true), (VitaminD, 2m, false), (Lozenges, null, false));

    /// <summary>Stands in for CallStore.Correct, recording each correction the screen asked for.</summary>
    private sealed class Corrections
    {
        public List<(Guid Call, Guid Shop, CallContent Content)> Saves { get; } = [];

        public CallResult Save(string rep, Guid call, Guid shop, CallContent content)
        {
            Saves.Add((call, shop, content));
            return new(null, null);
        }
    }

    /// <summary>Edit Call for <paramref name="call"/>. Nothing on this screen may touch an order (T7.8).</summary>
    private static async Task<Screen> EditCallAsync(TabletCall call, Corrections corrections)
    {
        Screen screen = new(typeof(RecordCall));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.ReadIndex = _ => new ProductSearchIndex("g1", [ColdFlu, VitaminD, Lozenges, Plasters]);
        screen.Tablet.FindCallView = (_, id) => id == call.Id ? new(call, CallStanding.Unsent) : null;
        screen.Tablet.CorrectWork = corrections.Save;
        screen.Tablet.AddFromCall = (_, _, _, _, _) => throw new InvalidOperationException("Edit Call adds nothing to an order.");
        screen.Tablet.AddForCall = (_, _, _, _, _, _) => throw new InvalidOperationException("Edit Call adds nothing to an order.");
        screen.Tablet.RemoveLine = (_, _, _, _) => throw new InvalidOperationException("Edit Call never takes a line off an order.");
        screen.Tablet.ChangeLine = (_, _, _, _) => throw new InvalidOperationException("Edit Call never changes an order line.");
        await screen.Open(new() { [nameof(RecordCall.Id)] = MurphysId, [nameof(RecordCall.CallId)] = (Guid?)call.Id });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Edit call"));
        return screen;
    }

    private static async Task SaveChangesAsync(Screen screen)
    {
        await screen.Click("Review and save");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Save changes?"));
        await screen.Click("Save");
        await screen.Until(() => screen.Navigation.Destinations.Count > 0);
    }

    private static (Guid Product, decimal? Count, bool Low)[] Rows(CallContent content) =>
        [.. (content.StockEntries ?? []).Select(entry => (entry.ProductId, entry.Count, entry.Low))];

    // Scenario 1 (S1).
    [Fact]
    public async Task Should_SaveTheCountAs13_When_ColdFluRelief16sCountedAs3IsChangedTo13InEditCall()
    {
        Corrections corrections = new();
        await using Screen screen = await EditCallAsync(CountedAtMurphys, corrections);

        await CountAsync(screen, ColdFlu, "13");
        await SaveChangesAsync(screen);

        (Guid call, Guid shop, CallContent content) = corrections.Saves.Single();
        Assert.Equal((MurphysCall, MurphysId), (call, shop));
        Assert.Equal([(ColdFlu.Id, 13m, true), (VitaminD.Id, 2m, false), (Lozenges.Id, null, false)], Rows(content));
        Assert.Equal([$"/locations/{MurphysId}/calls/{MurphysCall}"], screen.Navigation.Destinations);
        Assert.True(screen.Tablet.CallDrafts.TakeCorrected("colm", 0, MurphysCall)); // "Changes saved." on the call page.
    }

    // Scenario 2 (S2): channel, notes, counts and Low marks all change.
    [Fact]
    public async Task Should_SaveTheChangedChannelNotesCountsAndLowMarks_When_EditCallIsSaved()
    {
        Corrections corrections = new();
        await using Screen screen = await EditCallAsync(SavedAtMurphys(CallChannel.Phone, "Presented autumn range",
            (ColdFlu, 3m, true), (VitaminD, 2m, false)), corrections);

        await screen.Click("In person");
        await screen.Type("pitch-notes", "Presented autumn cold & flu range");
        await CountAsync(screen, VitaminD, "4");
        await screen.ClickLabelled($"Low {ColdFlu.Name}");
        await screen.ClickLabelled($"Low {VitaminD.Name}");
        await SaveChangesAsync(screen);

        CallContent content = corrections.Saves.Single().Content;
        Assert.Equal((CallChannel.InPerson, "Presented autumn cold & flu range"), (content.Channel, content.Notes));
        Assert.Equal([(ColdFlu.Id, 3m, false), (VitaminD.Id, 4m, true)], Rows(content));
    }

    // Scenario 3 (S2): the checkpoint question — no path in Edit Call adds content.
    [Fact]
    public async Task Should_OfferNoWayToAddContent_When_EditCallIsOpened()
    {
        await using Screen screen = await EditCallAsync(CountedAtMurphys, new());

        string[] buttons = [.. screen.Renderer.Buttons(screen.Id)];
        string[] labels = [.. screen.Renderer.AttributeValues(screen.Id, "aria-label")];
        string[] ids = [.. screen.Renderer.AttributeValues(screen.Id, "id")];
        Assert.DoesNotContain("Add product", buttons);
        Assert.DoesNotContain("Open order", buttons);
        Assert.DoesNotContain(buttons, button => button.StartsWith("Pitch notes", StringComparison.Ordinal)); // No notes to change.
        Assert.DoesNotContain(labels, label => label.EndsWith(" to order", StringComparison.Ordinal));
        Assert.DoesNotContain("stock-search", ids);
        Assert.DoesNotContain($"count-{Lozenges.Id}", ids); // A Not checked line can't be given a count…
        Assert.Contains("not checked", screen.Renderer.Text(screen.Id)); // …it reads Not checked, and can be removed.
        Assert.Contains($"Remove {Lozenges.Name}", labels);

        // On Phone: no Add count, no Out on a row with no count, and the filter never reaches the catalogue.
        await screen.Click("Phone");
        labels = [.. screen.Renderer.AttributeValues(screen.Id, "aria-label")];
        Assert.DoesNotContain(labels, label => label.StartsWith("Add count", StringComparison.Ordinal));
        Assert.DoesNotContain($"Out {Lozenges.Name}", labels);
        Assert.Contains($"Out {ColdFlu.Name}", labels);
        Assert.DoesNotContain(labels, label => label.EndsWith(" to order", StringComparison.Ordinal));
        await screen.Type("stock-filter", "Plasters");
        Assert.Contains("Nothing on this call matches 'Plasters'", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain($"Add {Plasters.Name}", screen.Renderer.AttributeValues(screen.Id, "aria-label"));
    }

    // Scenario 4 (S2): the follow-up stands where Add product, and pitch notes, would be.
    [Fact]
    public async Task Should_OfferRecordAFollowUpCallInPlaceOfAdd_When_EditCallIsOpened()
    {
        await using Screen screen = await EditCallAsync(CountedAtMurphys, new());

        Assert.Equal(2, screen.Renderer.Buttons(screen.Id).Count(button => button == "Record a follow-up call"));
    }

    // Scenario 5 (S3).
    [Fact]
    public async Task Should_RemoveTheLine_When_VitaminD1000IU90sCountedByMistakeIsRemovedAndSaved()
    {
        Corrections corrections = new();
        await using Screen screen = await EditCallAsync(CountedAtMurphys, corrections);

        await screen.ClickLabelled($"Remove {VitaminD.Name}");
        await SaveChangesAsync(screen);

        Assert.Equal([(ColdFlu.Id, 3m, true), (Lozenges.Id, null, false)], Rows(corrections.Saves.Single().Content));
    }

    /// <summary>The saved Call's page, standing as <paramref name="standing"/> says.</summary>
    private static async Task<Screen> CallPageAsync(TabletCall call, CallStanding standing)
    {
        Screen screen = new(typeof(SavedCall));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.FindCallView = (_, id) => id == call.Id
            ? new(call, standing, standing == CallStanding.Sent ? new DateTimeOffset(2026, 9, 22, 18, 0, 0, TimeSpan.Zero) : null) : null;
        await screen.Open(new() { [nameof(SavedCall.Id)] = MurphysId, [nameof(SavedCall.CallId)] = call.Id });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Murphy's Pharmacy"));
        return screen;
    }

    // Scenario 6 (S4).
    [Fact]
    public async Task Should_OfferRecordAFollowUpCallAndNoEditCall_When_ASentCallIsOpened()
    {
        await using Screen screen = await CallPageAsync(CountedAtMurphys, CallStanding.Sent);

        Assert.Equal(["Record a follow-up call"], screen.Renderer.Buttons(screen.Id));
        Assert.Contains("It can't be changed on this tablet.", screen.Renderer.Text(screen.Id));
        Assert.Contains("Cold & Flu Relief 16s: counted 3 · Low", screen.Renderer.AttributeValues(screen.Id, "data-call-line"));
    }

    // Scenario 7 (D1): Sync's review opens the Call's page, where an unsent Call offers Edit call.
    [Fact]
    public async Task Should_OfferEditCall_When_AnUnsentCallIsOpenedFromSync()
    {
        await using (Screen sync = new(typeof(Sync)))
        {
            UnsentView view = new(new(MurphysCall, UnsentKind.Call, DateTimeOffset.Parse("2026-09-22T09:15:00Z"), """{"channel":"InPerson"}"""),
                "Murphy's Pharmacy", null, "In person") { LocationId = MurphysId };
            sync.Tablet.ReadViews = _ => [view];
            sync.Tablet.ReadSummary = _ => new(1, 0, 1);
            await sync.Open(new());
            await sync.ClickLink("Murphy's Pharmacy");

            await sync.Click("Open call");

            Assert.Equal([$"/locations/{MurphysId}/calls/{MurphysCall}"], sync.Navigation.Destinations);
        }
        await using Screen page = await CallPageAsync(CountedAtMurphys, CallStanding.Unsent);
        Assert.Equal(["Edit call", "Record a follow-up call"], page.Renderer.Buttons(page.Id));

        await page.Click("Edit call");

        Assert.Equal([$"/locations/{MurphysId}/calls/{MurphysCall}/edit"], page.Navigation.Destinations);
    }

    // Scenario 8 (D3), amended by WI-056 scenario 6 (T-5.7.1 D3): Edit Call opens the follow-up for the Call being corrected.
    [Fact]
    public async Task Should_OpenTheFollowUpForThatCall_When_RecordAFollowUpCallIsTappedInEditCall()
    {
        await using Screen screen = await EditCallAsync(CountedAtMurphys, new());

        await screen.Click("Record a follow-up call");

        Assert.Equal([$"/locations/{MurphysId}/calls/{MurphysCall}/follow-up"], screen.Navigation.Destinations);
    }

    // Scenario 9 (edge): a Call can't be deleted.
    [Fact]
    public async Task Should_OfferNoDelete_When_EditCallIsOpened()
    {
        await using Screen screen = await EditCallAsync(CountedAtMurphys, new());

        Assert.DoesNotContain(screen.Renderer.Buttons(screen.Id), button => button.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(screen.Renderer.AttributeValues(screen.Id, "aria-label"), label => label.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    // Scenario 10 (T7.8): unticking Low changes the Call only; the order is never touched (the screen's order calls all throw).
    [Fact]
    public async Task Should_LeaveTheOrderLineUnchanged_When_LowIsUntickedInEditCall()
    {
        Corrections corrections = new();
        await using Screen screen = await EditCallAsync(CountedAtMurphys, corrections);

        await screen.ClickLabelled($"Low {ColdFlu.Name}");
        await SaveChangesAsync(screen);

        Assert.Equal((ColdFlu.Id, 3m, false), Rows(corrections.Saves.Single().Content)[0]);
    }

    // Scenario 11 (D6).
    [Fact]
    public async Task Should_KeepTheSavedCallUnchanged_When_EditCallIsLeftWithoutSaving()
    {
        Corrections corrections = new();
        await using Screen screen = await EditCallAsync(CountedAtMurphys, corrections);
        await CountAsync(screen, ColdFlu, "13");

        await screen.ClickLink("Back to call");
        Assert.Contains("Leave without saving these changes?", screen.Renderer.Text(screen.Id));
        await screen.Click("Leave");

        Assert.Empty(corrections.Saves);
        Assert.Equal([$"/locations/{MurphysId}/calls/{MurphysCall}"], screen.Navigation.Destinations);
        Assert.Null(screen.Tablet.CallDrafts.ReadEdit("colm", 0, MurphysCall));
    }

    // Scenario 12 (D2): switching to Phone keeps only what was mentioned, as recording a phone Call does.
    [Fact]
    public async Task Should_SaveOnlyMentionedRows_When_EditCallSwitchesTheChannelToPhone()
    {
        Corrections corrections = new();
        await using Screen screen = await EditCallAsync(CountedAtMurphys, corrections);

        await screen.Click("Phone");
        await SaveChangesAsync(screen);

        CallContent content = corrections.Saves.Single().Content;
        Assert.Equal(CallChannel.Phone, content.Channel);
        Assert.Equal([(ColdFlu.Id, 3m, true), (VitaminD.Id, 2m, false)], Rows(content)); // Throat Lozenges was Not checked.
    }
}
