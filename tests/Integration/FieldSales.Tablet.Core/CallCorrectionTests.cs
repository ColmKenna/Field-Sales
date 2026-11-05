using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-055 agreed scenarios 13–16 (T-5.6.1, REP-US-015): correcting a saved Call before Sync, in storage. A correction changes
/// or removes what was recorded and never adds to it (D2); a Call being sent or sent can't change (D4, D5).</summary>
[Trait("Category", "Integration")]
public sealed class CallCorrectionTests : IDisposable
{
    private const string Pitch = "Presented autumn cold & flu range";
    private readonly TestTablet tablet = new();
    private readonly FakeReceiver receiver;
    private readonly FakeTimeProvider at = new(new DateTimeOffset(2026, 9, 22, 9, 15, 0, TimeSpan.Zero));
    private TabletSnapshot day = null!;

    public CallCorrectionTests()
    {
        receiver = new(tablet.Server);
        tablet.Server.RespondToUpload = receiver.Respond;
    }

    public void Dispose() => tablet.Dispose();

    private Guid Shop => day.Locations[0].Id;
    private CallStore Calls => new(tablet.Workspace(), at);
    private UnsentStore Unsent => new(tablet.Workspace());

    private CallStockEntry Entry(int product, decimal? count, bool low = false)
    {
        SnapshotProduct item = day.Products[product];
        return new(item.Id, item.Code, item.Name, item.Unit, count, low);
    }

    // Murphy's, in person on 22 Sep: product 0 ("Cold & Flu") counted 3 and Low, product 1 ("Vitamin D") counted 2 by mistake,
    // product 2 Not checked, with no pitch notes.
    private async Task<TabletCall> SavedAsync(string? notes = null)
    {
        day = Snapshots.Build("colm", locations: 2, products: 5);
        await tablet.SyncAsync(day);
        CallResult saved = Calls.Save(Guid.NewGuid(), Shop, new(CallChannel.InPerson, notes, [Entry(0, 3m, low: true), Entry(1, 2m), Entry(2, null)]));
        Assert.True(saved.Succeeded, saved.Error);
        return saved.Call!;
    }

    // Scenario 16 (D4), with S1 and S3 in storage: 3 becomes 13, Vitamin D goes, and the Call is otherwise the one saved.
    [Fact]
    public async Task Should_KeepTheCallsIdDateAndSavedTime_When_ACorrectionIsSaved()
    {
        TabletCall saved = await SavedAsync(Pitch);
        DateTimeOffset queued = Unsent.Find(saved.Id)!.SavedUtc;
        at.Advance(TimeSpan.FromHours(2));

        CallResult corrected = Calls.Correct(saved.Id, Shop,
            new(CallChannel.Phone, "Presented the autumn range", [Entry(0, 13m, low: true)])); // On Phone only mentioned rows go.

        Assert.True(corrected.Succeeded, corrected.Error);
        UnsentItem item = Assert.Single(Unsent.Read());
        TabletCall stored = JsonSerializer.Deserialize(item.Json, CallJsonContext.Default.TabletCall)!;
        Assert.Equal((saved.Id, saved.LocationId, saved.CapturingRep, saved.CalledOn, saved.SavedAt, saved.Snapshot),
            (stored.Id, stored.LocationId, stored.CapturingRep, stored.CalledOn, stored.SavedAt, stored.Snapshot));
        Assert.Equal(queued, item.SavedUtc); // Its place in the Unsent queue.
        Assert.Equal((CallChannel.Phone, "Presented the autumn range"), (stored.Channel, stored.PitchNotes));
        Assert.Equal([(day.Products[0].Id, (decimal?)13m, true)], stored.StockEntries!.Select(entry => (entry.ProductId, entry.Count, entry.Low)));
        Assert.Equal(item.Json, JsonSerializer.Serialize(corrected.Call, CallJsonContext.Default.TabletCall));
    }

    // Scenario 13 (D2): nothing can be added, and a count can't be cleared.
    [Theory]
    [InlineData("a product it didn't have")]
    [InlineData("a count on a Not checked line")]
    [InlineData("notes where it had none")]
    [InlineData("a cleared count")]
    public async Task Should_RefuseTheCorrection_When_ItAddsAProductACountOrNotesOrClearsACount(string change)
    {
        TabletCall saved = await SavedAsync();
        string before = Unsent.Find(saved.Id)!.Json;
        CallStockEntry[] entries = change switch
        {
            "a product it didn't have" => [Entry(0, 3m, true), Entry(1, 2m), Entry(2, null), Entry(3, 6m)],
            "a count on a Not checked line" => [Entry(0, 3m, true), Entry(1, 2m), Entry(2, 4m)],
            "a cleared count" => [Entry(0, null, true), Entry(1, 2m), Entry(2, null)],
            _ => [Entry(0, 3m, true), Entry(1, 2m), Entry(2, null)]
        };
        string? notes = change == "notes where it had none" ? Pitch : null;

        CallResult result = Calls.Correct(saved.Id, Shop, new(CallChannel.InPerson, notes, entries));

        Assert.Equal(CallStore.AddsContent, result.Error);
        Assert.Equal(before, Unsent.Find(saved.Id)!.Json);
    }

    // Scenario 14 (D4, D5): only an unsent Call nobody is sending can change.
    [Theory]
    [InlineData("being sent", CallStore.BeingSent)]
    [InlineData("sent", CallStore.AlreadySent)]
    [InlineData("refused", CallStore.NotCorrectable)]
    [InlineData("another shop", CallStore.NotCorrectable)]
    public async Task Should_RefuseTheCorrection_When_TheCallIsBeingSentOrHasBeenSent(string state, string error)
    {
        TabletCall saved = await SavedAsync();
        UnsentItem item = Unsent.Find(saved.Id)!;
        Guid shop = Shop;
        switch (state)
        {
            case "being sent": Assert.True(Unsent.Claim(item, at.GetUtcNow())); break;
            case "sent": Assert.True(Unsent.MarkSent(saved.Id, at.GetUtcNow())); break;
            case "refused": Unsent.MarkNeedsAttention(item, new("incomplete", "Bad call", 400, at.GetUtcNow())); break;
            case "another shop": shop = day.Locations[1].Id; break;
        }

        CallResult result = Calls.Correct(saved.Id, shop, new(CallChannel.InPerson, null, [Entry(0, 13m, true), Entry(1, 2m), Entry(2, null)]));

        Assert.Equal(error, result.Error);
        Assert.Equal(item.Json, (Unsent.Find(saved.Id) ?? Unsent.FindSent(saved.Id)!.Item).Json);
    }

    // Scenario 15 (D5): Sync claims the Call before sending it, so a correction while it is on its way is refused, and what the
    // server holds is exactly what the tablet keeps as Sent.
    [Fact]
    public async Task Should_ClaimTheCallBeforeSendingIt_When_SyncUploadsACall()
    {
        TabletCall saved = await SavedAsync();
        string body = Unsent.Find(saved.Id)!.Json;
        Assert.Equal(CallStanding.Unsent, Calls.ReadView(saved.Id)!.Standing);
        CallResult? during = null;
        CallStanding? standing = null;
        receiver.Interfere = (id, _) =>
        {
            standing = Calls.ReadView(id)!.Standing;
            during = Calls.Correct(id, Shop, new(CallChannel.InPerson, null, [Entry(0, 13m, true), Entry(1, 2m), Entry(2, null)]));
            return null;
        };
        tablet.Server.Serve(EncodedSnapshot.From(day));

        SyncOutcome outcome = await tablet.Sync().SyncAsync();

        Assert.Equal(new UploadSummary(1, 1), outcome.Upload);
        Assert.Equal(CallStanding.Sending, standing);
        Assert.Equal(CallStore.BeingSent, during!.Error);
        Assert.Equal(body, receiver.Stored[saved.Id]);
        Assert.Equal(body, Assert.Single(Unsent.ReadSent()).Item.Json);
        Assert.Equal(CallStanding.Sent, Calls.ReadView(saved.Id)!.Standing);
        Assert.False(Calls.ReadView(saved.Id)!.CanCorrect);
    }
}
