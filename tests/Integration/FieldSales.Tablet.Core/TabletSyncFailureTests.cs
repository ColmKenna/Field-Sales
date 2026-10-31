using System.Net;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-040 (T-4.1.3): when a Sync can't finish, the tablet knows why (the signal went, the server answered with an
/// error, or the sign-in has expired) and what was sent and what wasn't. Faults are injected at the fake server and the fake
/// identity host; production Sync has no test hooks.</summary>
public sealed class TabletSyncFailureTests : IDisposable
{
    private readonly TestTablet tablet = new();
    private readonly FakeReceiver receiver;
    private readonly TabletSnapshot morning = Snapshots.Build("colm", 3, 3);

    public TabletSyncFailureTests()
    {
        receiver = new(tablet.Server);
        tablet.Server.RespondToUpload = receiver.Respond;
    }

    public void Dispose() => tablet.Dispose();

    private UnsentStore Unsent() => new(tablet.Workspace());
    private IReadOnlyList<Guid> SentIds() => Unsent().ReadSent().Select(sent => sent.Item.Id).ToArray();
    private int SnapshotRequests() => tablet.Server.Requests.Count(request => request.Path == SnapshotContract.Path);

    // Two saved Calls and a Ready to Send Order, in the order they go.
    private static Guid[] SaveThreeItems(RepWorkspace workspace, TabletSnapshot day, TimeProvider clock)
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), order = Guid.NewGuid();
        foreach ((Guid id, int minutesAgo) in new[] { (first, 30), (second, 20) })
            new UnsentStore(workspace).Save(new(id, UnsentKind.Call, clock.GetUtcNow().AddMinutes(-minutesAgo),
                $$"""{"id":"{{id}}","locationId":"{{day.Locations[0].Id}}","channel":"InPerson"}"""));
        OrderStore orders = new(workspace, clock);
        Assert.True(orders.AddLine(order, day.Locations[1].Id, day.Products[0].Id, 6m).Succeeded);
        Assert.True(orders.MarkReadyToSend(order).Succeeded);
        return [first, second, order];
    }

    private async Task<Guid[]> ThreeItemsAsync()
    {
        await tablet.SyncAsync(morning);
        return SaveThreeItems(tablet.Workspace(), morning, tablet.Clock);
    }

    private Task<SyncOutcome> SyncAsync()
    {
        tablet.Clock.Advance(TimeSpan.FromMinutes(1));
        return tablet.Sync().SyncAsync();
    }

    // I1: the causes the rep's sentences are built from.

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Should_RecordTheLostConnectionAndWhatWasConfirmed_When_SignalDropsAfterTheServerConfirmsSome(int confirmed)
    {
        Guid[] items = await ThreeItemsAsync();
        string remainingBefore = string.Concat(items.Skip(confirmed).Select(id => Unsent().Find(id)!.Json));
        receiver.DropAfter(confirmed);
        tablet.Server.Respond = _ => throw Faults.NoSignal();
        int snapshotsBefore = SnapshotRequests();

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.NoConnection, new UploadSummary(3, confirmed, UploadStop.ConnectionLost)), (outcome.Status, outcome.Upload));
        Assert.Equivalent(items.Take(confirmed), SentIds());
        Assert.Equal(remainingBefore, string.Concat(items.Skip(confirmed).Select(id => Unsent().Find(id)!.Json)));
        Assert.Equal(confirmed + 1, receiver.Arrivals.Count); // Nothing more is sent once the connection is gone.
        Assert.Equal(snapshotsBefore + 1, SnapshotRequests()); // The download is still tried.
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Should_RecordAServerErrorAndStillDownload_When_TheServerAnswersAnUploadWithAnError(HttpStatusCode status)
    {
        Guid[] items = await ThreeItemsAsync();
        receiver.FailFrom(2, status);
        tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 3, 3, label: "N")));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(3, 1, UploadStop.ServerError)), (outcome.Status, outcome.Upload));
        Assert.Equal([items[0]], SentIds());
        Assert.Equal(2, receiver.Arrivals.Count); // Nothing more is sent after the error.
    }

    [Fact]
    public async Task Should_RecordAServerError_When_AConfirmationNamesAnotherItem()
    {
        await ThreeItemsAsync();
        receiver.Interfere = (_, arrival) => arrival == 1 ? FakeReceiver.Receipt(Guid.NewGuid(), already: false) : null;
        tablet.Server.Serve(EncodedSnapshot.From(morning));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal(new UploadSummary(3, 0, UploadStop.ServerError), outcome.Upload);
        Assert.Empty(SentIds());
        Assert.Single(receiver.Arrivals);
    }

    // I2: US-001 S2, a poor connection.

    [Fact]
    public async Task Should_MarkTheConfirmedItemSentKeepTheOther2AndSayPoorConnection_When_SignalDropsAfterTheServerConfirms1Of3()
    {
        Guid[] items = await ThreeItemsAsync();
        ActiveSnapshot day = new SnapshotStore(tablet.Workspace(), tablet.Clock).ReadActive()!;
        string remainingBefore = Unsent().Find(items[1])!.Json + Unsent().Find(items[2])!.Json;
        receiver.DropAfter(1);
        tablet.Server.Respond = _ => throw Faults.NoSignal();
        int snapshotsBefore = SnapshotRequests();

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal([items[0]], SentIds());
        Assert.Equal(remainingBefore, Unsent().Find(items[1])!.Json + Unsent().Find(items[2])!.Json);
        Assert.Equal(new SyncReport("2 items not sent — poor connection. They'll send next Sync.", null), SyncMessages.Explain(outcome));
        Assert.Equal(snapshotsBefore + 1, SnapshotRequests()); // The download was attempted.
        Assert.Equal(day.Generation, outcome.Active!.Generation); // It didn't arrive, so the morning's day stays.
    }

    [Fact]
    public async Task Should_DownloadTheDayAndStillSayPoorConnection_When_SignalAllowsTheDownloadAfterTheDrop()
    {
        Guid[] items = await ThreeItemsAsync();
        receiver.DropAfter(1);
        TabletSnapshot evening = Snapshots.Build("colm", 3, 3, label: "N");
        tablet.Server.Serve(EncodedSnapshot.From(evening));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, evening.Manifest.BuiltUtc), (outcome.Status, outcome.Active!.Manifest.BuiltUtc));
        Assert.Equal([items[0]], SentIds());
        Assert.Equal(new SyncReport("2 items not sent — poor connection. They'll send next Sync.", null), SyncMessages.Explain(outcome));
    }

    [Fact]
    public async Task Should_UseOneItemWording_When_OneItemIsLeftAfterAPoorConnection()
    {
        await ThreeItemsAsync();
        receiver.DropAfter(2);
        tablet.Server.Respond = _ => throw Faults.NoSignal();

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal("1 item not sent — poor connection. It'll send next Sync.", SyncMessages.Explain(outcome).Work);
    }

    [Fact]
    public async Task Should_SayPoorConnection_When_TheFirstUploadFailsButTheDayDownloads()
    {
        await ThreeItemsAsync();
        string before = Unsent().Fingerprint();
        receiver.DropAfter(0);
        tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 3, 3, label: "N")));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal(SyncStatus.Completed, outcome.Status);
        Assert.Equal(before, Unsent().Fingerprint());
        Assert.Equal(new SyncReport("3 items not sent — poor connection. They'll send next Sync.", null), SyncMessages.Explain(outcome));
    }

    [Fact]
    public async Task Should_CountWhatWasWaitingAndSendNothing_When_TheSignInHasExpired()
    {
        using SessionTablet session = new();
        await session.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(SyncStatus.Completed, (await session.SyncAsync("colm")).Status);
        RepWorkspace workspace = session.Device.Workspace("colm");
        SaveThreeItems(workspace, Snapshots.Build("colm", 3, products: 20, label: "C"), session.Clock); // The day SyncAsync served.
        string fingerprint = new UnsentStore(workspace).Fingerprint();
        // Two hours on, the access token needs a refresh, and the identity host refuses it.
        session.Clock.UtcNow = SessionTablet.Morning.AddHours(2);
        session.Identity.Override = Faults.ExpiredSignIn;
        int requests = session.Server.Requests.Count;

        SyncOutcome outcome = await session.Sync("colm").SyncAsync();

        Assert.Equal((SyncStatus.SignInRequired, new UploadSummary(3, 0, UploadStop.SignInRequired)), (outcome.Status, outcome.Upload));
        Assert.Equal(requests, session.Server.Requests.Count); // No upload and no download.
        Assert.Equal(fingerprint, new UnsentStore(workspace).Fingerprint());
    }
}
