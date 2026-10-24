using System.Net;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

// WI-032 increment 3: tablet entry, the 24-hour local window and sign-in (approved scenarios 15-22, tablet side).
// The interactive system-browser sign-in is verified on the Samsung in increment 4; these tests start from its result.
public sealed class TabletSessionScenarios : IDisposable
{
    private readonly SessionTablet _tablet = new();

    public void Dispose() => _tablet.Dispose();

    [Fact]
    public async Task Should_OpenHomeAfterConnectedSignInAndSync_When_FirstUse()
    {
        TabletEntry first = await _tablet.Device.EnterAsync(connected: true);
        Assert.Equal((EntryScreen.SignIn, null, EntryMessages.SignInToStart), (first.Screen, first.RepSubject, first.Message));

        Assert.Equal(SignInStatus.Started, (await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"))).Status);
        // Signing in alone does not start a Sync; first use has no snapshot, so Sync is the next step.
        TabletEntry signedIn = await _tablet.Device.EnterAsync(connected: true);
        Assert.Equal((EntryScreen.Sync, "colm", EntryMessages.SyncFirst), (signedIn.Screen, signedIn.RepSubject, signedIn.Message));
        Assert.Empty(_tablet.Server.Requests);

        _tablet.Clock.UtcNow = SessionTablet.Morning.AddMinutes(42);
        SyncOutcome synced = await _tablet.SyncAsync("colm");
        Assert.Equal(("Last synced 07:42", "Bearer access-1"), (synced.Message, _tablet.Server.Requests[^1].Authorization));
        TabletEntry home = await _tablet.Device.EnterAsync(connected: true);
        Assert.Equal((EntryScreen.Home, "colm"), (home.Screen, home.RepSubject));
        Assert.Equal(["C shop 0000", "C shop 0001", "C shop 0002"],
            new SnapshotStore(_tablet.Device.Workspace("colm")).ReadLocations().Select(location => location.Name));
    }

    [Fact]
    public async Task Should_ReopenAndCaptureOffline_When_ConnectedSignInWasTwoHoursAgo()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm");
        int requests = _tablet.Server.Requests.Count;

        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(2);
        TabletEntry reopened = await _tablet.Device.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.Home, "colm"), (reopened.Screen, reopened.RepSubject));
        // Capture works offline and the work waits as Unsent; nothing contacts the server or identity host.
        UnsentStore work = new(_tablet.Device.Workspace("colm"));
        work.Save(new(Guid.NewGuid(), UnsentKind.Call, _tablet.Clock.UtcNow, """{"location":"Hickey's Pharmacy"}"""));
        Assert.Single(work.Read());
        Assert.Equal(requests, _tablet.Server.Requests.Count);
        Assert.Empty(_tablet.Identity.RefreshTokensUsed);
    }

    [Fact]
    public async Task Should_ExplainConnectionNeeded_When_NoSnapshotAndNoConnection()
    {
        TabletEntry fresh = await _tablet.Device.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.ConnectionNeeded, null, EntryMessages.FirstUseNeedsConnection),
            (fresh.Screen, fresh.RepSubject, fresh.Message));

        // Another rep's downloaded day on this tablet is never shown on the locked screen.
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("aoife"));
        await _tablet.SyncAsync("aoife");
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddDays(2);
        TabletEntry locked = await _tablet.Device.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.ConnectToSignInAgain, null), (locked.Screen, locked.RepSubject));
        Assert.DoesNotContain("aoife", locked.Message, StringComparison.OrdinalIgnoreCase);

        // A signed-in rep with no download yet, offline: the Sync screen says there is no connection.
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        TabletEntry noDay = await _tablet.Device.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.Sync, "colm", SyncMessages.NoConnection), (noDay.Screen, noDay.RepSubject, noDay.Message));
    }

    [Fact]
    public async Task Should_KeepUnsentWorkAndRequireSignIn_When_TwentyFourHoursHaveElapsed()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm");
        UnsentStore work = new(_tablet.Device.Workspace("colm"));
        work.Save(new(new Guid("6a1f2b30-0000-0000-0000-000000000001"), UnsentKind.Call, SessionTablet.Morning, """{"n":1}"""));
        work.Save(new(new Guid("6a1f2b30-0000-0000-0000-000000000002"), UnsentKind.Call, SessionTablet.Morning, """{"n":2}"""));
        string fingerprint = work.Fingerprint();

        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(24).AddSeconds(-1);
        Assert.Equal(EntryScreen.Home, (await _tablet.Device.EnterAsync(connected: false)).Screen);
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(24);
        TabletEntry offline = await _tablet.Device.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.ConnectToSignInAgain, EntryMessages.ConnectToSignInAgain), (offline.Screen, offline.Message));
        Assert.Equal(EntryScreen.SignIn, (await _tablet.Device.EnterAsync(connected: true)).Screen);
        // The two Calls and the downloaded day stay on the tablet for Colm's next sign-in.
        Assert.Equal(fingerprint, work.Fingerprint());
        Assert.Equal(2, work.Read().Count(item => item.Kind == UnsentKind.Call));

        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(EntryScreen.Home, (await _tablet.Device.EnterAsync(connected: false)).Screen);
        Assert.Equal(fingerprint, work.Fingerprint());
    }

    [Fact]
    public async Task Should_NotExtendTheWindow_When_TheAppIsReopenedOffline()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm");
        foreach (int hours in new[] { 1, 12, 23 })
        {
            _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(hours);
            Assert.Equal(EntryScreen.Home, (await _tablet.Device.EnterAsync(connected: false)).Screen);
        }
        // A Sync that refreshes tokens is not a connected sign-in, so the window still ends at 24 hours.
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(23).AddMinutes(30);
        Assert.Equal(SyncStatus.Completed, (await _tablet.SyncAsync("colm")).Status);
        Assert.Equal(["refresh-1"], _tablet.Identity.RefreshTokensUsed);
        Assert.Equal(SessionTablet.Morning, await _tablet.Device.LastConnectedSignInAsync("colm"));
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(24);
        Assert.Equal(EntryScreen.ConnectToSignInAgain, (await _tablet.Device.EnterAsync(connected: false)).Screen);
    }

    [Fact]
    public async Task Should_LockHome_When_TheDeviceClockReadsBeforeTheLastSignIn()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm");
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddMinutes(-1);
        Assert.Equal(EntryScreen.ConnectToSignInAgain, (await _tablet.Device.EnterAsync(connected: false)).Screen);
        Assert.Equal(EntryScreen.SignIn, (await _tablet.Device.EnterAsync(connected: true)).Screen);
        Assert.Equal(LocalAccess.ClockMovedBack, LocalAccessRules.Evaluate(SessionTablet.Morning, SessionTablet.Morning.AddTicks(-1)));
        Assert.Equal(LocalAccess.NoSignIn, LocalAccessRules.Evaluate(null, SessionTablet.Morning));
    }

    [Fact]
    public async Task Should_KeepEachRepsDataSeparate_When_AnotherRepSignsInOnTheTablet()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm", locations: 3);
        UnsentStore colmWork = new(_tablet.Device.Workspace("colm"));
        colmWork.Save(new(Guid.NewGuid(), UnsentKind.Order, SessionTablet.Morning, """{"lines":2}"""));
        string colmFingerprint = colmWork.Fingerprint();

        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("aoife"));
        TabletEntry aoife = await _tablet.Device.EnterAsync(connected: true);
        Assert.Equal((EntryScreen.Sync, "aoife"), (aoife.Screen, aoife.RepSubject));
        Assert.Empty(new SnapshotStore(_tablet.Device.Workspace("aoife")).ReadLocations());
        Assert.Empty(new UnsentStore(_tablet.Device.Workspace("aoife")).Read());

        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal((EntryScreen.Home, "colm"), ((await _tablet.Device.EnterAsync(connected: false)) is var colm ? (colm.Screen, colm.RepSubject) : default));
        Assert.Equal(3, new SnapshotStore(_tablet.Device.Workspace("colm")).ReadLocations().Count);
        Assert.Equal(colmFingerprint, colmWork.Fingerprint());
    }

    [Theory]
    [InlineData("Sales Manager")]
    [InlineData("Head Office User")]
    [InlineData("SysAdmin")]
    public async Task Should_RefuseTheTabletDay_When_TheAccountIsNotAFieldSalesperson(string role)
    {
        SignInOutcome refused = await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("niamh", role));
        Assert.Equal((SignInStatus.NotPermitted, "This app is for Field Salespeople."), (refused.Status, refused.Message));
        Assert.Empty(_tablet.Secure.Values);
        Assert.Equal(EntryScreen.SignIn, (await _tablet.Device.EnterAsync(connected: true)).Screen);

        // A refused attempt leaves the signed-in rep's day as it was.
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("niamh", role));
        Assert.Equal("colm", (await _tablet.Device.EnterAsync(connected: true)).RepSubject);
        // A Sales Manager who also sells may start a tablet day.
        Assert.Equal(SignInStatus.Started,
            (await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("sorcha", role, TabletClientContract.RequiredRole))).Status);
    }

    [Fact]
    public async Task Should_RefreshWithTheOneTimeToken_When_TheAccessTokenHasExpired()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm");
        Assert.Empty(_tablet.Identity.RefreshTokensUsed);

        _tablet.Clock.UtcNow = SessionTablet.Morning.AddMinutes(61);
        await _tablet.SyncAsync("colm");
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(3);
        await _tablet.SyncAsync("colm");
        // Each refresh token is used once; the API receives the newly issued access token.
        Assert.Equal(["refresh-1", "refresh-2"], _tablet.Identity.RefreshTokensUsed);
        Assert.Equal("Bearer access-3", _tablet.Server.Requests[^1].Authorization);
    }

    [Fact]
    public async Task Should_AskForSignIn_When_TheRefreshIsRefused()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm");
        _tablet.Identity.Override = () => new(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"invalid_grant"}""") };
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(2);
        int requests = _tablet.Server.Requests.Count;
        Assert.Equal(SyncStatus.SignInRequired, (await _tablet.SyncAsync("colm")).Status);
        Assert.Equal(SyncStatus.SignInRequired, (await _tablet.SyncAsync("colm")).Status);
        Assert.Single(_tablet.Identity.RefreshTokensUsed);
        Assert.Equal(requests, _tablet.Server.Requests.Count);
        // The local day is still open offline until its 24 hours end.
        Assert.Equal(EntryScreen.Home, (await _tablet.Device.EnterAsync(connected: false)).Screen);
    }

    [Fact]
    public async Task Should_ReportNoConnection_When_TheIdentityHostCannotBeReached()
    {
        await _tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await _tablet.SyncAsync("colm");
        _tablet.Identity.Override = () => throw new HttpRequestException(HttpRequestError.ConnectionError, "No route to host.");
        _tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(2);
        SyncOutcome outcome = await _tablet.SyncAsync("colm");
        Assert.Equal((SyncStatus.NoConnection, SyncMessages.NoConnection), (outcome.Status, outcome.Message));
        _tablet.Identity.Override = null;
        Assert.Equal(SyncStatus.Completed, (await _tablet.SyncAsync("colm")).Status);
        Assert.Equal(["refresh-1", "refresh-1"], _tablet.Identity.RefreshTokensUsed);
    }
}
