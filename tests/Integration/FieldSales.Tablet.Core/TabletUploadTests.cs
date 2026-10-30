using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>Stands in for the server's receive (WI-039 D1/D2): each id is stored once; a re-send is confirmed as already received.</summary>
public sealed class FakeReceiver(FakeSnapshotServer server)
{
    public Dictionary<Guid, string> Stored { get; } = [];
    public List<Guid> Arrivals { get; } = [];
    /// <summary>Lets a test answer one arrival differently: return a response, throw, or null for the normal receipt.</summary>
    public Func<Guid, int, HttpResponseMessage?>? Interfere { get; set; }

    public HttpResponseMessage Respond(HttpRequestMessage request)
    {
        Guid id = Guid.Parse(request.RequestUri!.Segments[^1]);
        Arrivals.Add(id);
        if (Interfere?.Invoke(id, Arrivals.Count) is { } answer) return answer;
        bool already = !Stored.TryAdd(id, server.Uploads[^1].Body);
        return Receipt(id, already);
    }

    public static HttpResponseMessage Receipt(Guid id, bool already) =>
        new(already ? HttpStatusCode.OK : HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new UploadReceipt(id, already ? UploadReceiptStatus.AlreadyReceived : UploadReceiptStatus.Received),
                UploadJsonContext.Default.UploadReceipt)
        };
}

/// <summary>WI-039 (T-4.1.2) T1–T12: the tablet sends finished work first, one item at a time, marks each Sent only when the
/// server confirms it, never sends anything twice as new work, and then downloads the snapshot.</summary>
public sealed class TabletUploadTests : IDisposable
{
    private readonly TestTablet tablet = new();
    private readonly FakeReceiver receiver;
    private readonly TabletSnapshot morning = Snapshots.Build("colm", 3, 3);

    public TabletUploadTests()
    {
        receiver = new(tablet.Server);
        tablet.Server.RespondToUpload = receiver.Respond;
    }

    public void Dispose() => tablet.Dispose();

    private OrderStore Orders() => new(tablet.Workspace(), tablet.Clock);
    private UnsentStore Unsent() => new(tablet.Workspace());
    private Guid Shop(int index) => morning.Locations[index].Id;

    private Guid Call(int shop, int minutesAgo = 0)
    {
        Guid id = Guid.NewGuid();
        Unsent().Save(new(id, UnsentKind.Call, tablet.Clock.GetUtcNow().AddMinutes(-minutesAgo),
            $$"""{"id":"{{id}}","locationId":"{{Shop(shop)}}","channel":"InPerson"}"""));
        return id;
    }

    private Guid Order(int shop, bool ready)
    {
        Guid id = Guid.NewGuid();
        Assert.True(Orders().AddLine(id, Shop(shop), morning.Products[0].Id, 6m).Succeeded);
        if (ready) Assert.True(Orders().MarkReadyToSend(id).Succeeded);
        return id;
    }

    private Task<SyncOutcome> SyncAsync(IProgress<UploadProgress>? progress = null)
    {
        tablet.Clock.Advance(TimeSpan.FromMinutes(1));
        return tablet.Sync().SyncAsync(progress: progress);
    }

    private IReadOnlyList<Guid> SentIds() => Unsent().ReadSent().Select(sent => sent.Item.Id).ToArray();
    private IReadOnlyList<Guid> UnsentIds() => Unsent().Read().Select(item => item.Id).ToArray();
    private int SnapshotRequests() => tablet.Server.Requests.Count(request => request.Path == SnapshotContract.Path);

    [Fact]
    public async Task Should_UploadTheTwoCallsAndTheReadyOrderMarkEachSentThenDownload_When_ISyncWithSignal()
    {
        ActiveSnapshot before = await tablet.SyncAsync(morning);
        Guid first = Call(0, minutesAgo: 20), second = Call(1, minutesAgo: 10);
        Guid ready = Order(1, ready: true);
        Guid building = Order(2, ready: false);
        string inProgressBefore = Unsent().Find(building)!.Json;
        int snapshotsBefore = SnapshotRequests();
        TabletSnapshot evening = Snapshots.Build("colm", 3, 3, label: "N");
        tablet.Server.Serve(EncodedSnapshot.From(evening));
        tablet.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 7, 41, 0, TimeSpan.Zero));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal([first, second, ready], receiver.Arrivals);
        Assert.Equal((SyncStatus.Completed, new UploadSummary(3, 3)), (outcome.Status, outcome.Upload));
        Assert.Equivalent(new[] { first, second, ready }, SentIds());
        Assert.Equal([building], UnsentIds());
        Assert.Equal(inProgressBefore, Unsent().Find(building)!.Json);
        Assert.Equal(snapshotsBefore + 1, SnapshotRequests());
        // Uploads come first: the download is the last request of the Sync.
        Assert.Equal(SnapshotContract.Path, tablet.Server.Requests[^1].Path);
        Assert.NotEqual(before.Generation, outcome.Active!.Generation);
        Assert.Equal(evening.Manifest.BuiltUtc, outcome.Active.Manifest.BuiltUtc);
        Assert.Equal("Last synced 07:42", outcome.Message);
        Assert.Equal("Last synced 07:42", SyncMessages.LastSynced(outcome.Active.CompletedUtc, tablet.Clock)); // What Home shows.
        // The server holds exactly what the tablet keeps as Sent.
        Assert.Equal(receiver.Stored[ready], Unsent().ReadSent().Single(sent => sent.Item.Id == ready).Item.Json);
    }

    [Fact]
    public async Task Should_MarkOnlyConfirmedItemsSentAndStillTryTheDownload_When_TheConnectionDropsAfterTheFirst()
    {
        await tablet.SyncAsync(morning);
        Guid first = Call(0, 30), second = Call(1, 20), third = Call(2, 10);
        string remainingBefore = Unsent().Find(second)!.Json + Unsent().Find(third)!.Json;
        receiver.Interfere = (_, arrival) => arrival > 1 ? throw new HttpRequestException("Signal lost") : null;
        tablet.Server.Respond = _ => throw new HttpRequestException("Signal lost");
        int snapshotsBefore = SnapshotRequests();

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.NoConnection, new UploadSummary(3, 1)), (outcome.Status, outcome.Upload));
        Assert.Equal([first], SentIds());
        Assert.Equivalent(new[] { second, third }, UnsentIds());
        Assert.Equal(remainingBefore, Unsent().Find(second)!.Json + Unsent().Find(third)!.Json);
        Assert.Equal(2, receiver.Arrivals.Count); // Nothing more is sent once the connection is gone.
        Assert.Equal(snapshotsBefore + 1, SnapshotRequests());
    }

    [Fact]
    public async Task Should_MarkSentWithoutADuplicate_When_ALostConfirmationIsResentAtTheNextSync()
    {
        await tablet.SyncAsync(morning);
        Guid order = Order(0, ready: true);
        // The server stores the order, but its confirmation never reaches the tablet.
        receiver.Interfere = (id, arrival) =>
        {
            if (arrival != 1) return null;
            receiver.Stored[id] = tablet.Server.Uploads[^1].Body;
            throw new HttpRequestException("Confirmation lost");
        };
        tablet.Server.Serve(EncodedSnapshot.From(morning));

        SyncOutcome lost = await SyncAsync();
        Assert.Equal(new UploadSummary(1, 0), lost.Upload);
        Assert.Equal([order], UnsentIds());

        SyncOutcome next = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(1, 1)), (next.Status, next.Upload));
        Assert.Equal([order], SentIds());
        Assert.Empty(UnsentIds());
        Assert.Single(receiver.Stored);
        Assert.Equal(tablet.Server.Uploads[0].Body, tablet.Server.Uploads[1].Body); // The re-send is the same item, byte for byte.
    }

    [Fact]
    public async Task Should_ResendAndBeConfirmedOnce_When_TheAppIsKilledAfterTheServerConfirms()
    {
        await tablet.SyncAsync(morning);
        Guid order = Order(0, ready: true);
        string body = Unsent().Find(order)!.Json;

        // The crash host's server confirms the order; the app is killed before it records that.
        await tablet.KillAtAsync("upload-confirmed", "colm", "upload");
        Assert.Equal([order], UnsentIds());
        Assert.Empty(SentIds());

        receiver.Stored[order] = body; // What the server kept from the killed Sync.
        tablet.Server.Serve(EncodedSnapshot.From(morning));
        SyncOutcome next = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(1, 1)), (next.Status, next.Upload));
        Assert.Equal([order], SentIds());
        Assert.Equal([order], receiver.Arrivals); // Confirmed as already received, not stored again.
        Assert.Equal(body, Assert.Single(receiver.Stored).Value);
    }

    [Fact]
    public async Task Should_UploadCallsBeforeOrdersOldestFirst_When_SeveralItemsWait()
    {
        await tablet.SyncAsync(morning);
        Guid newerOrder = Order(0, ready: true);
        tablet.Clock.Advance(TimeSpan.FromMinutes(5));
        Guid newestOrder = Order(1, ready: true);
        Guid newCall = Call(2, minutesAgo: 1), oldCall = Call(0, minutesAgo: 90);
        tablet.Server.Serve(EncodedSnapshot.From(morning));

        await SyncAsync();

        Assert.Equal([oldCall, newCall, newerOrder, newestOrder], receiver.Arrivals);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, UploadFaults.Corrupt)]
    [InlineData(HttpStatusCode.Conflict, UploadFaults.IdInUse)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, UploadFaults.TooLarge)]
    public async Task Should_LeaveTheFaultedItemUnsentAndSendTheRest_When_TheServerRefusesOneItem(HttpStatusCode status, string fault)
    {
        await tablet.SyncAsync(morning);
        Guid first = Call(0, 30), refused = Call(1, 20);
        Guid ready = Order(2, ready: true);
        receiver.Interfere = (id, _) => id == refused
            ? new HttpResponseMessage(status) { Content = JsonContent.Create(new Dictionary<string, string> { [UploadFaults.ProblemKey] = fault }) }
            : null;
        tablet.Server.Serve(EncodedSnapshot.From(morning));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(3, 2)), (outcome.Status, outcome.Upload));
        Assert.Equivalent(new[] { first, ready }, SentIds());
        Assert.Equal([refused], UnsentIds());
    }

    [Fact]
    public async Task Should_UploadNothing_When_NoSignInTokenIsAvailable()
    {
        await tablet.SyncAsync(morning);
        Call(0);
        Order(1, ready: true);
        string fingerprint = Unsent().Fingerprint();
        tablet.Tokens.Token = null;

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.SignInRequired, UploadSummary.None), (outcome.Status, outcome.Upload));
        Assert.Empty(tablet.Server.Uploads);
        Assert.Equal(fingerprint, Unsent().Fingerprint());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SyncStatus.SignInRequired)]
    [InlineData(HttpStatusCode.Forbidden, SyncStatus.NotPermitted)]
    public async Task Should_StopEverything_When_TheServerRefusesTheSignInOrPermission(HttpStatusCode refusal, SyncStatus expected)
    {
        await tablet.SyncAsync(morning);
        Call(0, 20);
        Call(1, 10);
        string fingerprint = Unsent().Fingerprint();
        int snapshotsBefore = SnapshotRequests();
        receiver.Interfere = (_, _) => new HttpResponseMessage(refusal);

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((expected, new UploadSummary(2, 0)), (outcome.Status, outcome.Upload));
        Assert.Single(receiver.Arrivals);
        Assert.Equal(snapshotsBefore, SnapshotRequests());
        Assert.Equal(fingerprint, Unsent().Fingerprint());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Should_StopUploadingAndStillDownload_When_TheServerIsUnavailable(HttpStatusCode status)
    {
        await tablet.SyncAsync(morning);
        Call(0, 30);
        Call(1, 20);
        Order(2, ready: true);
        string fingerprint = Unsent().Fingerprint();
        receiver.Interfere = (_, _) => new HttpResponseMessage(status);
        tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 3, 3, label: "N")));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(3, 0)), (outcome.Status, outcome.Upload));
        Assert.Single(receiver.Arrivals);
        Assert.Equal(fingerprint, Unsent().Fingerprint());
        Assert.Equal(3, outcome.Upload.NotSent);
    }

    [Fact]
    public async Task Should_StillUpload_When_ThereIsNotEnoughSpaceToDownload()
    {
        await tablet.SyncAsync(morning);
        Guid call = Call(0);
        Guid ready = Order(1, ready: true);
        int snapshotsBefore = SnapshotRequests();
        tablet.Space.Available = 0;

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.InsufficientSpace, new UploadSummary(2, 2)), (outcome.Status, outcome.Upload));
        Assert.Equivalent(new[] { call, ready }, SentIds());
        Assert.Equal(snapshotsBefore, SnapshotRequests());
    }

    [Fact]
    public async Task Should_NeverUploadInProgressOrdersOrScheduleChanges()
    {
        await tablet.SyncAsync(morning);
        Guid building = Order(0, ready: false);
        Guid schedule = Guid.NewGuid();
        Unsent().Save(new(schedule, UnsentKind.ScheduleChange, tablet.Clock.GetUtcNow(), $$"""{"locationId":"{{Shop(1)}}","day":"2026-10-02"}"""));
        Guid legacy = Guid.NewGuid();
        Unsent().Save(new(legacy, UnsentKind.Order, tablet.Clock.GetUtcNow(), """{"location":"Quinn's Centra","state":"In Progress"}"""));
        string fingerprint = Unsent().Fingerprint();
        tablet.Server.Serve(EncodedSnapshot.From(morning));

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, UploadSummary.None), (outcome.Status, outcome.Upload));
        Assert.Empty(tablet.Server.Uploads);
        Assert.Equal(fingerprint, Unsent().Fingerprint());
        Assert.Equivalent(new[] { building, schedule, legacy }, UnsentIds());
    }

    [Fact]
    public async Task Should_LeaveSentItemsOutOfUnsentCountsSignOutAndOrderChanges()
    {
        await tablet.SyncAsync(morning);
        Guid call = Call(0);
        Guid sent = Order(1, ready: true);
        Guid building = Order(2, ready: false);
        tablet.Server.Serve(EncodedSnapshot.From(morning));

        await SyncAsync();

        Assert.Equal(new UnsentSummary(1, 1, 0), Unsent().Summarise());
        Assert.Equal([building], UnsentIds()); // What the sign-out warning and shop retention read.
        Assert.Null(Orders().Find(sent));
        Assert.Equal(OrderStore.NotChangeable, Orders().AddLine(sent, Shop(1), morning.Products[1].Id, 1m).Error);
        Assert.Equal(OrderStore.NotChangeable, Orders().MarkReadyToSend(sent).Error);
        Assert.Equal([building], UnsentIds());
        SentItem record = Unsent().ReadSent().Single(item => item.Item.Id == sent);
        Assert.Equal((UnsentKind.Order, tablet.Clock.GetUtcNow()), (record.Item.Kind, record.SentUtc));
        Assert.True(Unsent().IsSent(call));
        Assert.False(Unsent().IsSent(building));
    }

    [Fact]
    public async Task Should_KeepEveryItem_When_AFormat1UnsentDatabaseIsUpgraded()
    {
        RepWorkspace workspace = tablet.Workspace();
        Directory.CreateDirectory(workspace.DirectoryPath);
        Guid id = new("6a1f2b30-0000-0000-0000-0000000000aa");
        using (SqliteConnection connection = SqliteEngine.Open(workspace.UnsentPath))
            SqliteEngine.Execute(connection, $"""
                CREATE TABLE work(id TEXT PRIMARY KEY, kind TEXT NOT NULL CHECK(kind IN ('Call','Order','ScheduleChange')),
                    saved_utc TEXT NOT NULL, json TEXT NOT NULL);
                INSERT INTO work VALUES('{id}', 'Call', '2026-09-30T16:00:00.0000000+00:00', '{"{"}"outcome":"Visited"{"}"}');
                PRAGMA user_version=1;
                """);

        UnsentItem item = Assert.Single(Unsent().Read());

        Assert.Equal((id, UnsentKind.Call, """{"outcome":"Visited"}"""), (item.Id, item.Kind, item.Json));
        Assert.True(Unsent().MarkSent(id, tablet.Clock.GetUtcNow()));
        Assert.Empty(Unsent().Read());
        Assert.Equal(id, Assert.Single(Unsent().ReadSent()).Item.Id);
        using SqliteConnection check = SqliteEngine.Open(workspace.UnsentPath);
        using SqliteCommand version = check.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        Assert.Equal(2L, (long)version.ExecuteScalar()!);
    }

    [Fact]
    public async Task Should_ReportSendingNOfTotal_When_ItemsUpload()
    {
        await tablet.SyncAsync(morning);
        Call(0, 30);
        Call(1, 20);
        Order(2, ready: true);
        tablet.Server.Serve(EncodedSnapshot.From(morning));
        List<UploadProgress> reported = [];

        await SyncAsync(new SynchronousProgress(reported.Add));

        Assert.Equal([new(1, 3), new(2, 3), new(3, 3), UploadProgress.Finished], reported);
    }

    // The Sync screen shows "Sending 1 of N…" from the tap, before the session check and the token (found on the Samsung).
    [Fact]
    public async Task Should_CountWhatTheNextSyncWouldSend_When_TheScreenStartsASync()
    {
        Assert.Equal(0, WorkUploader.CountWaiting(tablet.Workspace("colm")));
        await tablet.SyncAsync(morning);
        Call(0, 30);
        Call(1, 20);
        Order(2, ready: true);
        Order(0, ready: false);
        Unsent().Save(new(Guid.NewGuid(), UnsentKind.ScheduleChange, tablet.Clock.GetUtcNow(), "{}"));

        Assert.Equal(3, WorkUploader.CountWaiting(tablet.Workspace("colm")));
    }

    [Fact]
    public async Task Should_ReportNothing_When_NothingIsWaitingToSend()
    {
        await tablet.SyncAsync(morning);
        Order(0, ready: false);
        tablet.Server.Serve(EncodedSnapshot.From(morning));
        List<UploadProgress> reported = [];

        SyncOutcome outcome = await SyncAsync(new SynchronousProgress(reported.Add));

        Assert.Equal((SyncStatus.Completed, UploadSummary.None), (outcome.Status, outcome.Upload));
        Assert.Empty(reported);
    }

    private sealed class SynchronousProgress(Action<UploadProgress> report) : IProgress<UploadProgress>
    {
        public void Report(UploadProgress value) => report(value);
    }
}
