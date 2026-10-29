using FieldSales.Tablet;
using Microsoft.Extensions.Logging.Abstractions;

namespace FieldSales.Tablet.Core.Tests;

public sealed class TabletAppSessionScenarios
{
    [Fact]
    public async Task Should_CountOnlyTheActiveRepsWork_When_PreparingSignOut()
    {
        using SessionTablet tablet = new();
        using TabletApp app = Create(tablet);
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        TabletSignOutScenarios.SaveWork(tablet.Device);
        long colmSession = app.SessionVersion;
        Assert.Equal([UnsentKind.Call, UnsentKind.Call, UnsentKind.Order], await app.ReadUnsentKindsAsync(colmSession));
        Assert.True(await app.SignOutAsync(colmSession));
        await Assert.ThrowsAsync<OperationCanceledException>(() => app.ReadUnsentKindsAsync(colmSession));

        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("aoife"));
        Assert.Empty(await app.ReadUnsentKindsAsync(app.SessionVersion));
        Assert.False(await app.SignOutAsync(colmSession));
        Assert.Equal("aoife", (await app.EnterAsync()).RepSubject);
    }

    [Fact]
    public async Task Should_RejectOldHandoffsAndDirectEntry_When_ManualSignOutEndsTheSession()
    {
        using SessionTablet tablet = new();
        using TabletApp app = Create(tablet);
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await tablet.SyncAsync("colm");
        TabletEntry old = await app.EnterAsync();
        app.HandOff(old);
        Assert.True(await app.SignOutAsync(old.SessionVersion));
        Assert.Null((await app.EnterOnArrivalAsync()).RepSubject);
        app.HandOff(old);
        Assert.Null((await app.EnterOnArrivalAsync()).RepSubject);
        Assert.Null((await app.EnterAsync()).RepSubject);
    }

    [Fact]
    public async Task Should_CheckTheWindowAgain_When_ACachedHomeCrossesExpiry()
    {
        using SessionTablet tablet = new();
        using TabletApp app = Create(tablet);
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await tablet.SyncAsync("colm");
        tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(24).AddTicks(-1);
        app.HandOff(await app.EnterAsync());
        tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(24);
        Assert.Null((await app.EnterOnArrivalAsync()).RepSubject);
    }

    [Fact]
    public async Task Should_CancelTheOldSyncResult_When_SignOutHappensDuringDownload()
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await tablet.SyncAsync("colm");
        UnsentItem[] captured = TabletSignOutScenarios.SaveWork(tablet.Device);
        string generation = new SnapshotStore(tablet.Device.Workspace("colm")).ReadActive()!.Generation;
        using PausedApi api = new();
        using HttpClient client = new(api) { BaseAddress = new("https://api.test/") };
        using TabletApp app = new(tablet.Device, new(client, tablet.Identity.Client()), new TestSpace(), tablet.Clock, NullLogger<TabletApp>.Instance);
        Task<SyncOutcome> sync = app.SyncAsync("colm");
        await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await app.SignOutAsync(app.SessionVersion);
        tablet.Server.Serve(EncodedSnapshot.From(Snapshots.Build("colm", 1, 2, label: "B")));
        api.Result.SetResult(tablet.Server.Respond(new HttpRequestMessage()));
        SyncOutcome late = await sync.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(SyncStatus.Cancelled, late.Status);
        Assert.Null(late.Active);
        Assert.Null((await app.EnterAsync()).RepSubject);
        Assert.Equal(generation, new SnapshotStore(tablet.Device.Workspace("colm")).ReadActive()!.Generation);
        Assert.Equal(captured, new UnsentStore(tablet.Device.Workspace("colm")).Read());
    }

    private static TabletApp Create(SessionTablet tablet) => new(tablet.Device,
        new(tablet.Server.Client(), tablet.Identity.Client()), new TestSpace(), tablet.Clock, NullLogger<TabletApp>.Instance);

    private sealed class PausedApi : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Started.SetResult(); return Result.Task; }
    }
}
