using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

public sealed class TabletSignOutScenarios
{
    [Fact]
    public async Task Should_RestoreTheExactWorkOnlyToItsOwner_When_ManualSignOutIsConfirmed()
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(SyncStatus.Completed, (await tablet.SyncAsync("colm")).Status);
        UnsentItem[] saved = SaveWork(tablet.Device);
        long version = tablet.Device.SessionVersion;
        int requests = tablet.Server.Requests.Count;

        Assert.True(await tablet.Device.EndSessionAsync(version));
        Assert.True(tablet.Device.SessionVersion > version);
        AssertCredentialsGone(tablet.Secure);
        Assert.Equal((EntryScreen.ConnectToSignInAgain, null),
            ((await tablet.Device.EnterAsync(false)) is var entry ? (entry.Screen, entry.RepSubject) : default));
        Assert.Equal(saved, new UnsentStore(tablet.Device.Workspace("colm")).Read());
        Assert.Equal(requests, tablet.Server.Requests.Count);

        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("aoife"));
        TabletEntry other = await tablet.Device.EnterAsync(false);
        Assert.Equal((EntryScreen.Sync, "aoife"), (other.Screen, other.RepSubject));
        Assert.Empty(new UnsentStore(tablet.Device.Workspace(other.RepSubject!)).Read());
        Assert.Empty(new SnapshotStore(tablet.Device.Workspace(other.RepSubject!)).ReadLocations());

        await tablet.Device.EndSessionAsync();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        TabletEntry restored = await tablet.Device.EnterAsync(false);
        Assert.Equal((EntryScreen.Home, "colm"), (restored.Screen, restored.RepSubject));
        Assert.Equal(saved, new UnsentStore(tablet.Device.Workspace(restored.RepSubject!)).Read());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_ClearCredentialsAndKeepWork_When_AutomaticLogoutIsDetectedOnReturn(bool clockRollback)
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(SyncStatus.Completed, (await tablet.SyncAsync("colm")).Status);
        UnsentItem[] saved = SaveWork(tablet.Device);
        tablet.Clock.UtcNow = clockRollback ? SessionTablet.Morning.AddTicks(-1) : SessionTablet.Morning.AddHours(24);
        TabletEntry locked = await tablet.Device.EnterAsync(false);
        Assert.Null(locked.RepSubject);
        Assert.Equal(EntryScreen.ConnectToSignInAgain, locked.Screen);
        AssertCredentialsGone(tablet.Secure);
        Assert.Equal(saved, new UnsentStore(tablet.Device.Workspace("colm")).Read());
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(EntryScreen.Home, (await tablet.Device.EnterAsync(false)).Screen);
        Assert.Equal(saved, new UnsentStore(tablet.Device.Workspace("colm")).Read());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_EndForegroundAccessWithoutDeletingWork_When_TheMonitorDetectsExpiryOrRollback(bool rollback)
    {
        using TestTablet files = new();
        AdjustableTimerClock clock = new();
        MemorySecureStore secure = new();
        TabletDevice device = new(files.Root, secure, clock);
        await device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        UnsentItem[] saved = SaveWork(device);
        List<Exception> errors = [];
        using TabletSessionMonitor monitor = new(device, clock, errors.Add);
        monitor.Start();
        clock.Timers.Advance(TimeSpan.FromHours(24).Subtract(TimeSpan.FromMilliseconds(1)));
        Assert.NotNull(await device.LastConnectedSignInAsync("colm"));
        if (rollback) clock.Offset = -TimeSpan.FromDays(2);
        clock.Timers.Advance(TimeSpan.FromMilliseconds(1));
        await Until(() => device.LastConnectedSignInAsync("colm"), value => value is null);
        AssertCredentialsGone(secure);
        Assert.Equal(saved, new UnsentStore(device.Workspace("colm")).Read());
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Should_CheckBeforeRestoringAccess_When_TheAppReturnsAfterBackgroundExpiry()
    {
        using TestTablet files = new();
        FakeTimeProvider clock = new(SessionTablet.Morning);
        MemorySecureStore secure = new();
        TabletDevice device = new(files.Root, secure, clock);
        await device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        UnsentItem[] saved = SaveWork(device);
        using TabletSessionMonitor monitor = new(device, clock, error => Assert.Fail(error.ToString()));
        monitor.Start();
        monitor.Stop();
        clock.Advance(TimeSpan.FromDays(1));
        Assert.NotNull(await device.LastConnectedSignInAsync("colm"));
        monitor.Start();
        await Until(() => device.LastConnectedSignInAsync("colm"), value => value is null);
        Assert.Null((await device.EnterAsync(false)).RepSubject);
        Assert.Equal(saved, new UnsentStore(device.Workspace("colm")).Read());
    }

    [Theory]
    [InlineData("colm", HttpStatusCode.OK)]
    [InlineData("aoife", HttpStatusCode.OK)]
    [InlineData("colm", HttpStatusCode.BadRequest)]
    [InlineData("aoife", HttpStatusCode.BadRequest)]
    public async Task Should_IgnoreTheOldRefreshResult_When_SignOutAndANewSignInFinishFirst(string nextRep, HttpStatusCode response)
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(2);
        using PausedRefresh handler = new();
        using HttpClient identity = new(handler) { BaseAddress = new("https://identity.test/") };
        IAccessTokenSource oldSource = tablet.Device.Tokens("colm", identity);
        Task<string?> refresh = oldSource.GetAccessTokenAsync(CancellationToken.None);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await tablet.Device.EndSessionAsync();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn(nextRep) with
        {
            AccessToken = "new-session-access", AccessTokenExpiresUtc = tablet.Clock.UtcNow.AddHours(1), RefreshToken = "new-session-refresh"
        });
        handler.Result.SetResult(new(response) { Content = new StringContent(response == HttpStatusCode.OK
            ? """{"access_token":"late-access","expires_in":3600,"refresh_token":"late-refresh"}"""
            : """{"error":"invalid_grant"}""") });

        Assert.Null(await refresh.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Null(await oldSource.GetAccessTokenAsync(CancellationToken.None));
        Assert.Equal(nextRep, (await tablet.Device.EnterAsync(false)).RepSubject);
        Assert.Equal("new-session-access", await tablet.Device.Tokens(nextRep, identity).GetAccessTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Should_NotRecreateTokens_When_AnOldRefreshFinishesAfterSignOut()
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(2);
        using PausedRefresh handler = new();
        using HttpClient identity = new(handler) { BaseAddress = new("https://identity.test/") };
        Task<string?> pending = tablet.Device.Tokens("colm", identity).GetAccessTokenAsync(CancellationToken.None);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await tablet.Device.EndSessionAsync();
        handler.Result.SetResult(new(HttpStatusCode.OK)
        { Content = new StringContent("""{"access_token":"late","expires_in":3600,"refresh_token":"late-refresh"}""") });
        Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        AssertCredentialsGone(tablet.Secure);
        Assert.Null((await tablet.Device.EnterAsync(true)).RepSubject);
    }

    [Fact]
    public async Task Should_RefuseStaleSignOutAndSignIn_When_AnotherSessionHasStarted()
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        long observed = tablet.Device.SessionVersion;
        await tablet.Device.EndSessionAsync(observed);
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("aoife"));
        Assert.False(await tablet.Device.EndSessionAsync(observed));
        Assert.Equal(SignInStatus.Superseded,
            (await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"), observed)).Status);
        Assert.Equal("aoife", (await tablet.Device.EnterAsync(false)).RepSubject);
    }

    [Fact]
    public async Task Should_DenyAccessAndReportFailure_When_PlatformCredentialRemovalFails()
    {
        using TestTablet files = new();
        RefusingRemoval secure = new();
        TabletDevice device = new(files.Root, secure, new MovableClock(SessionTablet.Morning));
        await device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        UnsentItem[] saved = SaveWork(device);
        secure.Refuse = true;
        await Assert.ThrowsAsync<IOException>(() => device.EndSessionAsync());
        Assert.Null((await device.EnterAsync(false)).RepSubject);
        using HttpClient identity = new();
        Assert.Null(await device.Tokens("colm", identity).GetAccessTokenAsync(CancellationToken.None));
        Assert.Equal(saved, new UnsentStore(device.Workspace("colm")).Read());
    }

    internal static UnsentItem[] SaveWork(TabletDevice device)
    {
        UnsentItem[] items = [
            new(new Guid("6a1f2b30-0000-0000-0000-000000000001"), UnsentKind.Call, SessionTablet.Morning.AddMinutes(5), """{ "state": "Saved", "note": "First call" }"""),
            new(new Guid("6a1f2b30-0000-0000-0000-000000000002"), UnsentKind.Call, SessionTablet.Morning.AddMinutes(10), """{"state":"Saved","note":"Second call"}"""),
            new(new Guid("6a1f2b30-0000-0000-0000-000000000003"), UnsentKind.Order, SessionTablet.Morning.AddMinutes(15), """{"state":"Ready to Send","lines":[{"product":"A-00001","quantity":6}]}""")];
        UnsentStore store = new(device.Workspace("colm"));
        foreach (UnsentItem item in items) store.Save(item);
        return items;
    }

    private static void AssertCredentialsGone(MemorySecureStore secure) => Assert.DoesNotContain(secure.Values.Keys,
        key => key == "fieldsales.current-rep" || key.StartsWith("fieldsales.tokens.") || key.StartsWith("fieldsales.session."));

    private static async Task Until<T>(Func<Task<T>> read, Func<T, bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        while (!condition(await read())) await Task.Delay(1, timeout.Token);
    }

    private sealed class PausedRefresh : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Started.SetResult(); return Result.Task; } // Deliberately ignores cancellation to exercise a late result.
    }

    private sealed class AdjustableTimerClock : TimeProvider
    {
        public FakeTimeProvider Timers { get; } = new(SessionTablet.Morning);
        public TimeSpan Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => Timers.GetUtcNow() + Offset;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => Timers.CreateTimer(callback, state, dueTime, period);
    }

    private sealed class RefusingRemoval : ISecureStore
    {
        private readonly MemorySecureStore inner = new();
        public bool Refuse { get; set; }
        public Task<string?> GetAsync(string key) => inner.GetAsync(key);
        public Task SetAsync(string key, string value) => inner.SetAsync(key, value);
        public bool Remove(string key) => !Refuse && inner.Remove(key);
    }
}
