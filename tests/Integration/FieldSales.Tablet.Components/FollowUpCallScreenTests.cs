using FieldSales.Calls.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-056 agreed scenarios 7–9 and 11 (T-5.7.1, REP-US-016) on T-06: a follow-up Call, a new Call at the shop linked to the
/// one it follows up. Scenario 6 (from Edit Call) amends WI-055 scenario 8 in <c>CallCorrectionScreenTests</c>; storage is Tablet.Core
/// <c>FollowUpCallTests</c> (1, 5, 10).</summary>
public sealed partial class TabletSessionScreenTests
{
    private static ProductSearchResult NasalSpray => new(StockId(2, 26), "P-26", "Nasal Spray 15ml", Remedies.Name, "Each", 6.50m, Remedies.Id);

    // Murphy's Suggested List has two rows; a follow-up starts without them.
    private static StockCheckStart FollowUpStock => new(
        new ProductSearchIndex("g1", [ColdFlu, VitaminD, Lozenges, Plasters, NasalSpray], [Health, Remedies, Vitamins, Food]),
        [new SuggestedProduct(ColdFlu, SuggestionSources.LastVisit), new SuggestedProduct(VitaminD, SuggestionSources.LastVisit)]);

    /// <summary>Stands in for CallStore.Save with a link, recording each follow-up the screen asked to save.</summary>
    private sealed class FollowUps
    {
        public List<(Guid Call, Guid Shop, CallContent Content, Guid Original)> Saves { get; } = [];

        public CallResult Save(string rep, Guid call, Guid shop, CallContent content, Guid original)
        {
            Saves.Add((call, shop, content, original));
            return new(null, null);
        }
    }

    /// <summary>The follow-up to <paramref name="original"/>. A plain save, without the link, fails the test.</summary>
    private static async Task<Screen> FollowUpAsync(TabletCall original, FollowUps followUps, Action<Screen>? arrange = null)
    {
        Screen screen = new(typeof(RecordCall));
        screen.Tablet.Read = (_, _) => Murphys();
        screen.Tablet.ReadStock = (_, _) => FollowUpStock;
        screen.Tablet.FindCallView = (_, id) => id == original.Id ? new(original, CallStanding.Unsent) : null;
        screen.Tablet.FollowUpWork = followUps.Save;
        screen.Tablet.SaveWork = (_, _, _, _) => throw new InvalidOperationException("A follow-up saves with its link.");
        arrange?.Invoke(screen);
        await screen.Open(new() { [nameof(RecordCall.Id)] = MurphysId, [nameof(RecordCall.FollowUpOf)] = (Guid?)original.Id });
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Follow-up call"));
        return screen;
    }

    // Scenario 7 (D3): a sent Call's page opens the follow-up for that Call.
    [Fact]
    public async Task Should_OpenTheFollowUpForThatCall_When_RecordAFollowUpCallIsTappedOnASentCall()
    {
        await using Screen screen = await CallPageAsync(CountedAtMurphys, CallStanding.Sent);

        await screen.Click("Record a follow-up call");

        Assert.Equal([$"/locations/{MurphysId}/calls/{MurphysCall}/follow-up"], screen.Navigation.Destinations);
    }

    // Scenario 8 (D2): no stock rows, no channel chosen, and the Call it follows up named; Add product in person, the one box on Phone.
    [Fact]
    public async Task Should_StartWithNoStockRowsAndNameTheOriginal_When_AFollowUpCallOpens()
    {
        await using Screen screen = await FollowUpAsync(CountedAtMurphys, new());

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Following up your call on Tue 22 Sep", text);
        Assert.Contains("Murphy's Pharmacy", text);
        Assert.Contains(RecordCall.NothingFollowedUp, text);
        Assert.Empty(StockRows(screen));
        Assert.Equal(("false", "false"), (screen.Renderer.ButtonAttribute(screen.Id, "In person", "aria-pressed"),
            screen.Renderer.ButtonAttribute(screen.Id, "Phone", "aria-pressed")));
        Assert.Contains("Add product", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("Open order", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("Pitch notes", screen.Renderer.Buttons(screen.Id));

        await screen.Click("Phone");
        Assert.Contains("stock-filter", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.DoesNotContain(screen.Renderer.AttributeValues(screen.Id, "aria-label"), label => label.StartsWith("Low ", StringComparison.Ordinal));
    }

    // Scenario 9 (D2): by phone, Nasal Spray 15ml found through the one box and counted 2; saved with the link, back to the shop.
    [Fact]
    public async Task Should_RecordTheFollowUpLink_When_AFollowUpIsSavedOnTheScreen()
    {
        FollowUps followUps = new();
        await using Screen screen = await FollowUpAsync(CountedAtMurphys, followUps);

        await screen.Click("Phone");
        await screen.Type("stock-filter", "nasal");
        await screen.Until(() => screen.Renderer.AttributeValues(screen.Id, "aria-label").Contains($"Add {NasalSpray.Name}"));
        await screen.ClickLabelled($"Add {NasalSpray.Name}");
        await screen.ClickLabelled($"Add count for {NasalSpray.Name}");
        await CountAsync(screen, NasalSpray, "2");
        await screen.Click("Review and save");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Save this call?"));
        await screen.Click("Save");
        await screen.Until(() => screen.Navigation.Destinations.Count > 0);

        (Guid call, Guid shop, CallContent content, Guid original) = Assert.Single(followUps.Saves);
        Assert.NotEqual(MurphysCall, call); // A new Call…
        Assert.Equal((MurphysId, MurphysCall, CallChannel.Phone), (shop, original, content.Channel)); // …linked to the one it follows up.
        Assert.Equal([(NasalSpray.Id, 2m, false)], Rows(content));
        Assert.Equal([$"/locations/{MurphysId}"], screen.Navigation.Destinations);
        Assert.True(screen.Tablet.CallDrafts.TakeSaved("colm", 0, MurphysId)); // "Call saved" on the shop.
        Assert.Null(screen.Tablet.CallDrafts.ReadFollowUp("colm", 0, MurphysCall));
    }

    // Scenario 11 (D2): the Call being recorded at the shop isn't the follow-up, and is still there after the follow-up is left or saved.
    [Fact]
    public async Task Should_KeepACallBeingRecordedAtTheShop_When_AFollowUpIsStartedThere()
    {
        CallDraft recording = new(Guid.NewGuid(), CallChannel.InPerson, "Presented autumn range", true,
            [new StockLine(ColdFlu, SuggestionSources.LastVisit, "4")], StockChanged: true);
        FollowUps followUps = new();
        await using (Screen left = await FollowUpAsync(CountedAtMurphys, followUps,
            screen => screen.Tablet.CallDrafts.Keep("colm", 0, MurphysId, recording)))
        {
            Assert.Empty(StockRows(left)); // Not the Call being recorded.
            await left.Click("In person");
            await left.ClickLink("Back to shop");
            await left.Click("Leave");
            Assert.Same(recording, left.Tablet.CallDrafts.Read("colm", 0, MurphysId));
        }

        await using Screen saved = await FollowUpAsync(CountedAtMurphys, followUps,
            screen => screen.Tablet.CallDrafts.Keep("colm", 0, MurphysId, recording));
        await saved.Click("In person");
        await saved.Click("Add product");
        await saved.Type("stock-search", "plasters");
        await saved.Until(() => saved.Renderer.AttributeValues(saved.Id, "aria-label").Contains($"Add {Plasters.Name}"));
        await saved.ClickLabelled($"Add {Plasters.Name}");
        await CountAsync(saved, Plasters, "6");
        await saved.Click("Review and save");
        await saved.Until(() => saved.Renderer.Text(saved.Id).Contains("Save this call?"));
        await saved.Click("Save");
        await saved.Until(() => saved.Navigation.Destinations.Count > 0);

        Assert.NotEqual(recording.CallId, Assert.Single(followUps.Saves).Call);
        Assert.Same(recording, saved.Tablet.CallDrafts.Read("colm", 0, MurphysId));
    }
}
