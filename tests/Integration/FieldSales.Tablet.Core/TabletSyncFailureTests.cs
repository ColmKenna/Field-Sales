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

    // I3: US-001 S3 and Sign-in US-004 S1, the sign-in has expired.

    [Fact]
    public async Task Should_UploadNothingAndAskToSignInToSend3Items_When_MySignInHasExpired()
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
        Assert.Equal(new SyncReport("Sign in again to send 3 items", null), SyncMessages.Explain(outcome));
    }

    [Fact]
    public async Task Should_AskToSignInToSendTheRest_When_TheServerRefusesTheSignInPartWay()
    {
        Guid[] items = await ThreeItemsAsync();
        receiver.FailFrom(2, HttpStatusCode.Unauthorized);
        int snapshotsBefore = SnapshotRequests();

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.SignInRequired, new UploadSummary(3, 1, UploadStop.SignInRequired)), (outcome.Status, outcome.Upload));
        Assert.Equal([items[0]], SentIds());
        Assert.Equal(snapshotsBefore, SnapshotRequests()); // No download without a sign-in.
        Assert.Equal(new SyncReport("Sign in again to send 2 items", null), SyncMessages.Explain(outcome));
    }

    [Fact]
    public async Task Should_SaySignInAgainToSync_When_TheSignInHasExpiredAndNothingIsWaiting()
    {
        await tablet.SyncAsync(morning);
        // An order still being built isn't finished work, so it isn't waiting to send.
        Assert.True(new OrderStore(tablet.Workspace(), tablet.Clock).AddLine(Guid.NewGuid(), morning.Locations[0].Id, morning.Products[0].Id, 2m).Succeeded);
        tablet.Tokens.Token = null;

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal(new SyncReport("Sign in again to sync.", null), SyncMessages.Explain(outcome));
    }

    [Fact]
    public async Task Should_UseOneItemWording_When_OneItemIsLeftAfterTheSignInExpires()
    {
        await tablet.SyncAsync(morning);
        Guid call = Guid.NewGuid();
        Unsent().Save(new(call, UnsentKind.Call, tablet.Clock.GetUtcNow(), $$"""{"id":"{{call}}","locationId":"{{morning.Locations[0].Id}}"}"""));
        tablet.Tokens.Token = null;

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal("Sign in again to send 1 item", SyncMessages.Explain(outcome).Work);
    }

    [Fact]
    public async Task Should_SaySignInAgainToSync_When_TheDownloadNeedsTheSignInAfterEveryItemWent()
    {
        await ThreeItemsAsync();
        tablet.Server.Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.SignInRequired, new UploadSummary(3, 3)), (outcome.Status, outcome.Upload));
        Assert.Equal(new SyncReport("Sign in again to sync.", null), SyncMessages.Explain(outcome));
    }

    // I4: no connection at all, a server error, and work that went while the day didn't.

    [Fact]
    public async Task Should_SayNoConnectionAndKeepEveryItem_When_NothingGetsThrough()
    {
        await ThreeItemsAsync();
        string fingerprint = Unsent().Fingerprint();
        receiver.DropAfter(0);
        tablet.Server.Respond = _ => throw Faults.NoSignal();

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.NoConnection, new UploadSummary(3, 0, UploadStop.ConnectionLost)), (outcome.Status, outcome.Upload));
        Assert.Equal(fingerprint, Unsent().Fingerprint());
        Assert.Equal(new SyncReport("No connection — your work is saved on this tablet", null), SyncMessages.Explain(outcome));
    }

    [Fact]
    public async Task Should_StopUploadingSayFieldSalesDidNotRespondAndStillDownload_When_TheServerAnswersWithAnError()
    {
        Guid[] items = await ThreeItemsAsync();
        receiver.FailFrom(2, HttpStatusCode.ServiceUnavailable);
        TabletSnapshot evening = Snapshots.Build("colm", 3, 3, label: "N");
        tablet.Server.Serve(EncodedSnapshot.From(evening));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, evening.Manifest.BuiltUtc), (outcome.Status, outcome.Active!.Manifest.BuiltUtc));
        Assert.Equal([items[0]], SentIds());
        Assert.Equal(new SyncReport("2 items not sent — Field Sales didn't respond. They'll send next Sync.", null), SyncMessages.Explain(outcome));
    }

    [Theory]
    [InlineData("no connection", "Your day didn't download — no connection.")]
    [InlineData("server failure", "Your day didn't download. Sync again to try.")]
    [InlineData("no space", "There isn't enough free space on this tablet to download your day.")]
    [InlineData("cancelled", "Sync was cancelled before your day downloaded.")]
    public async Task Should_SayWhatWasSentAndWhyTheDayDidNotDownload_When_EveryItemWentButTheDownloadFailed(string failure, string day)
    {
        await ThreeItemsAsync();
        using CancellationTokenSource cancel = new();
        FailTheDownload(failure, cancel);

        SyncOutcome outcome = await SyncFailingAsync(cancel);

        Assert.Equal(new UploadSummary(3, 3), outcome.Upload);
        Assert.Equal(new SyncReport("3 items sent.", day), SyncMessages.Explain(outcome));
    }

    [Theory]
    [InlineData("no connection", SyncMessages.NoConnection)]
    [InlineData("server failure", SyncMessages.Failed)]
    [InlineData("no space", SyncMessages.InsufficientSpace)]
    [InlineData("cancelled", SyncMessages.Cancelled)]
    public async Task Should_KeepTodaysMessages_When_NothingWasSentAndTheDayDidNotDownload(string failure, string day)
    {
        await tablet.SyncAsync(morning);
        using CancellationTokenSource cancel = new();
        FailTheDownload(failure, cancel);

        SyncOutcome outcome = await SyncFailingAsync(cancel);

        Assert.Equal(new SyncReport(null, day), SyncMessages.Explain(outcome));
    }

    private void FailTheDownload(string failure, CancellationTokenSource cancel)
    {
        switch (failure)
        {
            case "no connection": tablet.Server.Respond = _ => throw Faults.NoSignal(); break;
            case "server failure": tablet.Server.Respond = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); break;
            case "no space": tablet.Space.Available = 0; break;
            case "cancelled": tablet.Server.Respond = _ => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); }; break;
        }
    }

    private Task<SyncOutcome> SyncFailingAsync(CancellationTokenSource cancel)
    {
        tablet.Clock.Advance(TimeSpan.FromMinutes(1));
        return tablet.Sync().SyncAsync(ct: cancel.Token);
    }
}
