namespace FieldSales.Tablet.Core.Tests;

// WI-037 increment 1: characterize the persisted work and existing local-access rules
// before introducing logout. New devices/stores reopen the same on-disk SQLite files;
// the platform secure store is represented by the existing MemorySecureStore fixture.
public sealed class TabletSignOutCharacterizationTests : IDisposable
{
    private readonly SessionTablet tablet = new();

    public void Dispose() => tablet.Dispose();

    [Fact]
    public async Task Should_RestoreExactSavedWork_When_TheAppReopensWithinTheLocalWindow()
    {
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(SyncStatus.Completed, (await tablet.SyncAsync("colm")).Status);
        UnsentItem[] captured = SaveWork();
        tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(24).AddTicks(-1);
        int requests = tablet.Server.Requests.Count;

        TabletDevice reopened = tablet.Device;
        TabletEntry entry = await reopened.EnterAsync(connected: false);

        Assert.Equal((EntryScreen.Home, "colm"), (entry.Screen, entry.RepSubject));
        Assert.Equal(SessionTablet.Morning, await reopened.LastConnectedSignInAsync("colm"));
        Assert.Equal(captured, new UnsentStore(reopened.Workspace(entry.RepSubject!)).Read());
        Assert.Equal(3, new SnapshotStore(reopened.Workspace(entry.RepSubject!)).ReadLocations().Count);
        Assert.Equal(requests, tablet.Server.Requests.Count);
        Assert.Empty(tablet.Identity.RefreshTokensUsed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_KeepExactSavedWorkForReauthentication_When_ExistingLocalAccessRulesDenyEntry(bool clockMovedBack)
    {
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(SyncStatus.Completed, (await tablet.SyncAsync("colm")).Status);
        UnsentItem[] captured = SaveWork();
        tablet.Clock.UtcNow = clockMovedBack
            ? SessionTablet.Morning.AddTicks(-1)
            : SessionTablet.Morning.AddHours(24);

        TabletDevice reopened = tablet.Device;
        TabletEntry offline = await reopened.EnterAsync(connected: false);
        TabletEntry online = await reopened.EnterAsync(connected: true);

        Assert.Equal((EntryScreen.ConnectToSignInAgain, null), (offline.Screen, offline.RepSubject));
        Assert.Equal((EntryScreen.SignIn, null), (online.Screen, online.RepSubject));
        Assert.Equal(EntryMessages.ConnectToSignInAgain, offline.Message);
        Assert.Equal(EntryMessages.SignInToStart, online.Message);
        Assert.Equal(captured, new UnsentStore(reopened.Workspace("colm")).Read());

        // A real connected sign-in, rather than reopening or refreshing tokens, restores access.
        tablet.Clock.UtcNow = SessionTablet.Morning.AddDays(1).AddHours(1);
        Assert.Equal(SignInStatus.Started,
            (await reopened.CompleteSignInAsync(SessionTablet.SignIn("colm"))).Status);
        TabletEntry restored = await tablet.Device.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.Home, "colm"), (restored.Screen, restored.RepSubject));
        Assert.Equal(captured, new UnsentStore(tablet.Device.Workspace(restored.RepSubject!)).Read());
    }

    [Fact]
    public async Task Should_ReopenOnlyTheCurrentRepsWorkspace_When_ASecondRepUsesTheTablet()
    {
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(SyncStatus.Completed, (await tablet.SyncAsync("colm")).Status);
        UnsentItem[] colmWork = SaveWork();

        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("aoife"));
        Assert.Equal(SyncStatus.Completed, (await tablet.SyncAsync("aoife", locations: 1)).Status);
        // Even identical item IDs belong to separate workspaces; no transfer or collision is permitted.
        UnsentItem aoifeWork = colmWork[0] with { Json = """{"state":"Saved","note":"Aoife's call"}""" };
        new UnsentStore(tablet.Device.Workspace("aoife")).Save(aoifeWork);

        TabletDevice reopened = tablet.Device;
        TabletEntry entry = await reopened.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.Home, "aoife"), (entry.Screen, entry.RepSubject));
        Assert.Equal([aoifeWork], new UnsentStore(reopened.Workspace(entry.RepSubject!)).Read());
        Assert.Single(new SnapshotStore(reopened.Workspace(entry.RepSubject!)).ReadLocations());

        await reopened.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        TabletEntry restored = await tablet.Device.EnterAsync(connected: false);
        Assert.Equal((EntryScreen.Home, "colm"), (restored.Screen, restored.RepSubject));
        Assert.Equal(colmWork, new UnsentStore(tablet.Device.Workspace(restored.RepSubject!)).Read());
        Assert.Equal([aoifeWork], new UnsentStore(tablet.Device.Workspace("aoife")).Read());
    }

    private UnsentItem[] SaveWork()
    {
        // State is opaque capture data in the predecessor contract, not a new typed state model.
        UnsentItem[] items =
        [
            new(new Guid("6a1f2b30-0000-0000-0000-000000000001"), UnsentKind.Call,
                SessionTablet.Morning.AddMinutes(5), """{ "state": "Saved", "outcome": "Visited", "note": "Call one" }"""),
            new(new Guid("6a1f2b30-0000-0000-0000-000000000002"), UnsentKind.Call,
                SessionTablet.Morning.AddMinutes(10), """{"state":"Saved","outcome":"No order","note":"Call two"}"""),
            new(new Guid("6a1f2b30-0000-0000-0000-000000000003"), UnsentKind.Order,
                SessionTablet.Morning.AddMinutes(15), """{"state":"Ready to Send","lines":[{"product":"A-00001","quantity":6}]}""")
        ];
        UnsentStore work = new(tablet.Device.Workspace("colm"));
        foreach (UnsentItem item in items) work.Save(item);
        return items;
    }
}
