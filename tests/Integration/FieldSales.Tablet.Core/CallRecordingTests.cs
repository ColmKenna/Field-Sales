using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-047 (T-5.3.1, REP-US-006): a recorded Call is saved once as an Unsent Call with its date, rep, channel, notes
/// and snapshot, and goes at the next Sync. Agreed scenarios 1, 3, 4, 5, 7, 10 and 11 at store level.</summary>
[Trait("Category", "Integration")]
public sealed class CallRecordingTests : IDisposable
{
    private const string Pitch = "Presented autumn cold & flu range";
    private readonly TestTablet tablet = new();
    private readonly FakeReceiver receiver;

    public CallRecordingTests()
    {
        receiver = new(tablet.Server);
        tablet.Server.RespondToUpload = receiver.Respond;
    }

    public void Dispose() => tablet.Dispose();

    private CallStore Calls(TimeProvider? clock = null) => new(tablet.Workspace(), clock ?? tablet.Clock);
    private UnsentStore Unsent() => new(tablet.Workspace());

    private static TabletSnapshot Day()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 3, 2);
        return snapshot with { Locations = [snapshot.Locations[0] with { Name = "Murphy's Pharmacy" }, .. snapshot.Locations.Skip(1)] };
    }

    [Fact]
    public async Task Should_SaveTheCallWithDateRepChannelAndNotes_When_AnInPersonPitchAtMurphysOn22September2026IsConfirmed()
    {
        TabletSnapshot day = Day();
        ActiveSnapshot active = await tablet.SyncAsync(day);
        FakeTimeProvider at = new(new DateTimeOffset(2026, 9, 22, 9, 15, 0, TimeSpan.Zero));
        Guid id = Guid.NewGuid(), murphys = day.Locations[0].Id;
        int requests = tablet.Server.Requests.Count;

        CallResult result = Calls(at).Save(id, murphys, new(CallChannel.InPerson, "  " + Pitch + "\n"));

        Assert.True(result.Succeeded, result.Error);
        UnsentItem item = Assert.Single(Unsent().Read());
        Assert.Equal((id, UnsentKind.Call), (item.Id, item.Kind));
        TabletCall saved = JsonSerializer.Deserialize(item.Json, CallJsonContext.Default.TabletCall)!;
        Assert.Equal((CallContract.Schema, CallContract.CurrentVersion, id, murphys, "colm", new DateOnly(2026, 9, 22), CallChannel.InPerson, Pitch),
            (saved.Schema, saved.Version, saved.Id, saved.LocationId, saved.CapturingRep, saved.CalledOn, saved.Channel, saved.PitchNotes));
        Assert.Equal(at.GetLocalNow(), saved.SavedAt);
        Assert.Equal(new CallSnapshotRef(active.Manifest.Version, active.Manifest.BuiltUtc), saved.Snapshot);
        Assert.Equal(saved, result.Call);
        Assert.Equal(requests, tablet.Server.Requests.Count); // Saved on the tablet only; it goes at the next Sync.
        using JsonDocument body = JsonDocument.Parse(item.Json);
        Assert.All(CallContract.ReservedSections, section => Assert.False(body.RootElement.TryGetProperty(section, out _)));
    }

    [Theory]
    [InlineData(CallChannel.InPerson, Pitch, "In person · Pitch notes")]
    [InlineData(CallChannel.Phone, Pitch, "Phone · Pitch notes")]
    public void Should_SummariseInPersonPitchNotesWithSaveAndBack_When_ReviewAndSaveIsTapped(CallChannel channel, string notes, string summary)
    {
        CallContent content = new(channel, notes);

        Assert.Null(content.Problem);
        Assert.Equal(summary, content.Summary);
    }

    [Fact]
    public async Task Should_NotSaveAndShowChooseInPersonOrPhone_When_NotesButNoChannel()
    {
        TabletSnapshot day = Day();
        await tablet.SyncAsync(day);

        CallResult refused = Calls().Save(Guid.NewGuid(), day.Locations[0].Id, new(null, Pitch));

        Assert.Equal("Choose In person or Phone", refused.Error);
        Assert.Empty(Unsent().Read());
    }

    [Theory]
    [InlineData(CallChannel.InPerson, null)]
    [InlineData(CallChannel.InPerson, "")]
    [InlineData(CallChannel.Phone, "   \n ")]
    public async Task Should_ShowAddPitchNotesAStockCheckOrACompetitorNote_When_AChannelButNothingRecorded(CallChannel channel, string? notes)
    {
        TabletSnapshot day = Day();
        await tablet.SyncAsync(day);

        CallResult refused = Calls().Save(Guid.NewGuid(), day.Locations[0].Id, new(channel, notes));

        Assert.Equal("Add pitch notes, a stock check or a competitor note", refused.Error);
        Assert.Empty(Unsent().Read());
    }

    [Fact]
    public async Task Should_SaveAndAcceptTheCall_When_TheShopWasReassignedAfterTheSnapshot()
    {
        TabletSnapshot day = Day();
        await tablet.SyncAsync(day);
        Guid id = Guid.NewGuid(), third = day.Locations[2].Id;
        // A later Sync no longer carries the third shop: it was reassigned while the rep was there.
        await tablet.SyncAsync(Snapshots.Build("colm", 2, 2));

        CallResult saved = Calls().Save(id, third, new(CallChannel.InPerson, Pitch));

        Assert.True(saved.Succeeded, saved.Error);
        tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 2, 2)));
        SyncOutcome outcome = await tablet.Sync().SyncAsync();
        Assert.Equal(new UploadSummary(1, 1), outcome.Upload);
        Assert.Equal([id], receiver.Arrivals);
    }

    [Fact]
    public async Task Should_SaveOnce_When_SaveIsTappedTwice()
    {
        TabletSnapshot day = Day();
        await tablet.SyncAsync(day);
        Guid id = Guid.NewGuid();

        CallResult first = Calls().Save(id, day.Locations[0].Id, new(CallChannel.Phone, Pitch));
        tablet.Clock.Advance(TimeSpan.FromMinutes(1));
        CallResult again = Calls().Save(id, day.Locations[0].Id, new(CallChannel.InPerson, "Something else"));

        Assert.Equal(first.Call, again.Call); // The Call already saved, not a second one or a rewrite.
        UnsentItem item = Assert.Single(Unsent().Read());
        Assert.Equal(first.Call, JsonSerializer.Deserialize(item.Json, CallJsonContext.Default.TabletCall));
    }

    [Fact]
    public async Task Should_StayUnsentThenUploadAndMarkSent_When_TheRepSyncs()
    {
        TabletSnapshot day = Day();
        await tablet.SyncAsync(day);
        Guid id = Guid.NewGuid();
        Assert.True(Calls().Save(id, day.Locations[0].Id, new(CallChannel.InPerson, Pitch)).Succeeded);
        string body = Unsent().Find(id)!.Json;

        Assert.Equal(new UnsentSummary(1, 0, 1), Unsent().Summarise()); // A saved Call is Unsent, ready for the next Sync.
        UnsentView review = Assert.Single(Unsent().ReadViews());
        Assert.Equal(("Call", "Murphy's Pharmacy", "In person", UnsentSection.ReadyToSend),
            (review.KindLabel, review.LocationName, review.CallChannel, review.Section));

        tablet.Server.Serve(EncodedSnapshot.From(day));
        SyncOutcome outcome = await tablet.Sync().SyncAsync();

        Assert.Equal(new UploadSummary(1, 1), outcome.Upload);
        Assert.Equal(body, receiver.Stored[id]);
        Assert.Equal(body, Assert.Single(Unsent().ReadSent()).Item.Json);
        Assert.Empty(Unsent().Read());
        Assert.Equal(new UnsentSummary(0, 0, 0), Unsent().Summarise());
    }
}
