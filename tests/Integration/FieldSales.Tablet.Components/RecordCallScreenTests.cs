using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-047 (T-5.3.1, REP-US-006) on T-06: channel, pitch notes, the save review and leaving. Agreed scenarios 1–6, 8–10.</summary>
public sealed partial class TabletSessionScreenTests
{
    private static readonly Guid MurphysId = Guid.Parse("57000000-0000-0000-0000-000000000001");
    private const string Pitch = "Presented autumn cold & flu range";

    private static OfflineLocationDetail Murphys() => new(new SnapshotLocation(MurphysId, "Murphy's Pharmacy", Guid.NewGuid(),
        "Murphy Retail", new(Guid.NewGuid(), "Aughrim", "Wicklow"), null, null), true, false);

    /// <summary>Stands in for CallStore, recording what the screen asked to save.</summary>
    private sealed class SavedCalls
    {
        public List<(string Rep, Guid Call, Guid Shop, CallContent Content)> Saves { get; } = [];
        public ManualResetEventSlim? Gate { get; set; }

        public CallResult Save(string rep, Guid call, Guid shop, CallContent content)
        {
            Saves.Add((rep, call, shop, content));
            if (Gate is not null && !Gate.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The save was not released.");
            return new(null, null);
        }
    }

    private static async Task<Screen> RecordCallAsync(SavedCalls calls)
    {
        Screen screen = new(typeof(RecordCall));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.SaveWork = calls.Save;
        await screen.Open(new() { [nameof(RecordCall.Id)] = MurphysId });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Murphy's Pharmacy"));
        return screen;
    }

    private static async Task EnterNotesAsync(Screen screen, string notes)
    {
        await screen.Click("Pitch notes");
        await screen.Type("pitch-notes", notes);
    }

    [Fact]
    public async Task Should_ShowChannelAsTwoUnselectedOptions_When_RecordCallOpensAtMurphysPharmacy()
    {
        await using Screen screen = await RecordCallAsync(new());

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Record call", text);
        Assert.Contains("How", text);
        Assert.Equal(("false", "false"), (screen.Renderer.ButtonAttribute(screen.Id, "In person", "aria-pressed"),
            screen.Renderer.ButtonAttribute(screen.Id, "Phone", "aria-pressed")));
        Assert.Contains("Review and save", screen.Renderer.Buttons(screen.Id)); // One page: no steps to page through (T6.1).

        await screen.Click("In person");

        Assert.Equal(("true", "false"), (screen.Renderer.ButtonAttribute(screen.Id, "In person", "aria-pressed"),
            screen.Renderer.ButtonAttribute(screen.Id, "Phone", "aria-pressed")));
    }

    [Fact]
    public async Task Should_SaveTheCallWithDateRepChannelAndNotes_When_AnInPersonPitchAtMurphysOn22September2026IsConfirmed()
    {
        SavedCalls calls = new();
        await using Screen screen = await RecordCallAsync(calls);
        await screen.Click("In person");
        Guid callId = screen.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.CallId;
        await EnterNotesAsync(screen, Pitch);
        await screen.Click("Review and save");
        await screen.Click("Save");
        await screen.Until(() => screen.Navigation.Destinations.Count > 0);

        (string rep, Guid call, Guid shop, CallContent content) = Assert.Single(calls.Saves);
        Assert.Equal(("colm", MurphysId, CallChannel.InPerson, Pitch), (rep, shop, content.Channel, content.Notes));
        Assert.Equal(callId, call); // The id the page chose when it opened (date and rep are stamped by CallStore).
        Assert.Equal([$"/locations/{MurphysId}"], screen.Navigation.Destinations);
        Assert.Null(screen.Tablet.CallDrafts.Read("colm", 0, MurphysId));

        // The shop screen the rep returns to says so, once.
        await using Screen shopScreen = new(typeof(LocationDetail));
        shopScreen.Tablet.Read = (_, _) => Murphys();
        shopScreen.Tablet.CallDrafts.Saved("colm", 0, MurphysId);
        await shopScreen.Open(new() { [nameof(LocationDetail.Id)] = MurphysId });
        await shopScreen.Until(() => shopScreen.Renderer.Text(shopScreen.Id).Contains(LocationDetail.CallSaved));
    }

    [Fact]
    public async Task Should_SummariseInPersonPitchNotesWithSaveAndBack_When_ReviewAndSaveIsTapped()
    {
        SavedCalls calls = new();
        await using Screen screen = await RecordCallAsync(calls);
        await screen.Click("In person");
        await EnterNotesAsync(screen, Pitch);

        await screen.Click("Review and save");

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Save this call?", text);
        Assert.Contains("In person · Pitch notes", text);
        Assert.Contains("Back", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("Save", screen.Renderer.Buttons(screen.Id));
        Assert.Empty(calls.Saves); // Nothing is saved until the rep confirms.
    }

    [Fact]
    public async Task Should_NotSaveAndShowChooseInPersonOrPhone_When_NotesButNoChannel()
    {
        SavedCalls calls = new();
        await using Screen screen = await RecordCallAsync(calls);
        await EnterNotesAsync(screen, Pitch);

        await screen.Click("Review and save");

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Choose In person or Phone", text);
        Assert.DoesNotContain("Save this call?", text);
        Assert.Empty(calls.Saves);
    }

    [Theory]
    [InlineData("In person", "")]
    [InlineData("Phone", "   ")]
    public async Task Should_ShowAddPitchNotesAStockCheckOrACompetitorNote_When_AChannelButNothingRecorded(string channel, string notes)
    {
        SavedCalls calls = new();
        await using Screen screen = await RecordCallAsync(calls);
        await screen.Click(channel);
        await EnterNotesAsync(screen, notes);

        await screen.Click("Review and save");

        Assert.Contains("Add pitch notes, a stock check or a competitor note", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Save this call?", screen.Renderer.Text(screen.Id));
        Assert.Empty(calls.Saves);
    }

    [Fact]
    public async Task Should_KeepWhatWasEnteredAndSaveNothing_When_BackIsTappedInTheReview()
    {
        SavedCalls calls = new();
        await using Screen screen = await RecordCallAsync(calls);
        await screen.Click("Phone");
        await EnterNotesAsync(screen, Pitch);
        await screen.Click("Review and save");

        await screen.Click("Back");

        Assert.DoesNotContain("Save this call?", screen.Renderer.Text(screen.Id));
        Assert.Equal("true", screen.Renderer.ButtonAttribute(screen.Id, "Phone", "aria-pressed"));
        Assert.Contains(Pitch, screen.Renderer.AttributeValues(screen.Id, "value"));
        Assert.Empty(calls.Saves);
        Assert.Empty(screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_AskBeforeLeaving_When_TheRepLeavesWithUnsavedWork()
    {
        await using Screen screen = await RecordCallAsync(new());
        await screen.Click("In person");

        await screen.ClickLink("Back to shop");
        Assert.Contains("Leave without saving this call?", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Navigation.Destinations);
        await screen.Click("Stay");
        Assert.DoesNotContain("Leave without saving this call?", screen.Renderer.Text(screen.Id));
        Assert.Equal("true", screen.Renderer.ButtonAttribute(screen.Id, "In person", "aria-pressed"));

        await screen.ClickLink("Back to shop");
        await screen.Click("Leave");

        Assert.Equal([$"/locations/{MurphysId}"], screen.Navigation.Destinations);
        Assert.Null(screen.Tablet.CallDrafts.Read("colm", 0, MurphysId));

        // With nothing entered there is nothing to lose, so Back leaves at once.
        await using Screen empty = await RecordCallAsync(new());
        await empty.ClickLink("Back to shop");
        Assert.Equal([$"/locations/{MurphysId}"], empty.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_RestoreTheDraft_When_TheAppResumesMidCall()
    {
        SavedCalls calls = new();
        await using Screen screen = await RecordCallAsync(calls);
        await screen.Click("Phone");
        await EnterNotesAsync(screen, Pitch);
        Guid before = screen.Tablet.CallDrafts.Read("colm", 0, MurphysId)!.CallId;

        await screen.Rebuild(); // What every resume does, e.g. back from the dialler on a phone call.
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Murphy's Pharmacy"));

        Assert.Equal("true", screen.Renderer.ButtonAttribute(screen.Id, "Phone", "aria-pressed"));
        Assert.Contains(Pitch, screen.Renderer.AttributeValues(screen.Id, "value"));
        await screen.Click("Review and save");
        await screen.Click("Save");
        await screen.Until(() => calls.Saves.Count == 1);
        Assert.Equal(before, calls.Saves[0].Call); // Still the same Call.
    }

    [Fact]
    public async Task Should_SaveOnce_When_SaveIsTappedTwice()
    {
        SavedCalls calls = new() { Gate = new ManualResetEventSlim(false) };
        await using Screen screen = await RecordCallAsync(calls);
        await screen.Click("In person");
        await EnterNotesAsync(screen, Pitch);
        await screen.Click("Review and save");

        Task first = screen.Click("Save"); // Not awaited: the save is held until the second tap.
        await screen.Until(() => calls.Saves.Count == 1);
        await screen.Click("Save"); // Still saving.
        calls.Gate.Set();
        await first;
        await screen.Until(() => screen.Navigation.Destinations.Count > 0);

        Assert.Single(calls.Saves);
    }
}
