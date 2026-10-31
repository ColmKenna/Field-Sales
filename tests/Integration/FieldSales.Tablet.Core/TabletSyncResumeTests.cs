using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-041 (T-4.6.1, Sign-in US-004): a Sync stopped because the online sign-in expired leaves every item as it was, the
/// rep keeps capturing inside the local window, and once they sign in again each item is sent once. The token handling is real
/// (the identity host is faked); uploads go to the fake receiver.</summary>
public sealed class TabletSyncResumeTests : IDisposable
{
    private readonly SessionTablet session = new();
    private readonly FakeReceiver receiver;
    private readonly TabletSnapshot day = Snapshots.Build("colm", 3, products: 20, label: "C"); // The day SessionTablet serves.

    public TabletSyncResumeTests()
    {
        receiver = new(session.Server);
        session.Server.RespondToUpload = receiver.Respond;
    }

    public void Dispose() => session.Dispose();

    private RepWorkspace Workspace => session.Device.Workspace("colm");
    private UnsentStore Unsent() => new(Workspace);
    private int SnapshotRequests() => session.Server.Requests.Count(request => request.Path == SnapshotContract.Path);

    // Signed in at 07:00 with the day downloaded, then three finished items saved.
    private async Task<Guid[]> SignedInWithThreeItemsAsync()
    {
        await session.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        Assert.Equal(SyncStatus.Completed, (await session.SyncAsync("colm")).Status);
        return FinishedWork.SaveThree(Workspace, day, session.Clock);
    }

    // Two hours on, the access token needs a refresh and the identity host refuses it.
    private void ExpireTheOnlineSignIn()
    {
        session.Clock.UtcNow = SessionTablet.Morning.AddHours(2);
        session.Identity.Override = Faults.ExpiredSignIn;
    }

    private Task<SignInOutcome> SignInAgainAsync(string rep = "colm", params string[] roles)
    {
        session.Identity.Override = null;
        DateTimeOffset now = session.Clock.GetUtcNow();
        return session.Device.CompleteSignInAsync(new StaffSignIn(rep,
            roles.Length == 0 ? [TabletClientContract.RequiredRole] : roles, "access-again", now.AddHours(1), "refresh-again"));
    }

    private Task<SyncOutcome> SyncAsync() => session.Sync("colm").SyncAsync();

    // I1: today's behaviour, locked before the resume is added.

    [Fact]
    public async Task Should_SendEachItemOnce_When_TheSyncResumesAfterSignIn()
    {
        Guid[] items = await SignedInWithThreeItemsAsync();
        ExpireTheOnlineSignIn();
        SyncOutcome stopped = await SyncAsync();
        Assert.Equal((SyncStatus.SignInRequired, new UploadSummary(3, 0, UploadStop.SignInRequired)), (stopped.Status, stopped.Upload));
        Assert.Empty(receiver.Arrivals);
        int snapshotsBefore = SnapshotRequests();

        Assert.Equal(SignInStatus.Started, (await SignInAgainAsync()).Status);
        session.Server.Serve(EncodedSnapshot.From(day));
        SyncOutcome resumed = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(3, 3)), (resumed.Status, resumed.Upload));
        Assert.Equal(items, receiver.Arrivals); // Each item once, in the order they go.
        Assert.Equal(3, receiver.Stored.Count);
        Assert.Equivalent(items, Unsent().ReadSent().Select(sent => sent.Item.Id));
        Assert.Equal(snapshotsBefore + 1, SnapshotRequests()); // Then the day downloads.
    }

    [Fact]
    public async Task Should_ConfirmTheLostItemOnceAsAlreadyReceived_When_ItsConfirmationWasLostBeforeTheSignInExpired()
    {
        Guid[] items = await SignedInWithThreeItemsAsync();
        // The server stores the first item, but its confirmation never reaches the tablet, and the signal goes.
        receiver.Interfere = (id, arrival) =>
        {
            if (arrival != 1) return null;
            receiver.Stored[id] = session.Server.Uploads[^1].Body;
            throw Faults.NoSignal();
        };
        session.Server.Respond = _ => throw Faults.NoSignal();
        Assert.Equal(new UploadSummary(3, 0, UploadStop.ConnectionLost), (await SyncAsync()).Upload);
        receiver.Interfere = null;
        ExpireTheOnlineSignIn();
        Assert.Equal(SyncStatus.SignInRequired, (await SyncAsync()).Status);

        Assert.Equal(SignInStatus.Started, (await SignInAgainAsync()).Status);
        session.Server.Serve(EncodedSnapshot.From(day));
        SyncOutcome resumed = await SyncAsync();

        Assert.Equal(new UploadSummary(3, 3), resumed.Upload);
        Assert.Equal([items[0], items[0], items[1], items[2]], receiver.Arrivals); // The lost one again, then the rest.
        Assert.Equal(3, receiver.Stored.Count); // Stored once: the re-send was answered "already received".
        Assert.Equivalent(items, Unsent().ReadSent().Select(sent => sent.Item.Id));
        Assert.Empty(Unsent().Read());
    }

    [Theory]
    [InlineData("not a Field Salesperson")]
    [InlineData("overtaken by another sign-in")]
    public async Task Should_KeepTheQueueAndCount_When_SignInFails(string failure)
    {
        await SignedInWithThreeItemsAsync();
        ExpireTheOnlineSignIn();
        Assert.Equal(SyncStatus.SignInRequired, (await SyncAsync()).Status);
        string fingerprint = Unsent().Fingerprint();
        UnsentSummary summary = Unsent().Summarise();

        SignInOutcome outcome = failure == "not a Field Salesperson"
            ? await SignInAgainAsync("colm", "HeadOffice")
            : await session.Device.CompleteSignInAsync(new StaffSignIn("colm", [TabletClientContract.RequiredRole], "access-late",
                session.Clock.GetUtcNow().AddHours(1), "refresh-late"), expectedVersion: session.Device.SessionVersion - 1);

        Assert.NotEqual(SignInStatus.Started, outcome.Status);
        Assert.Equal((fingerprint, summary), (Unsent().Fingerprint(), Unsent().Summarise()));
        SyncOutcome again = await SyncAsync();
        Assert.Equal((SyncStatus.SignInRequired, new UploadSummary(3, 0, UploadStop.SignInRequired)), (again.Status, again.Upload));
        Assert.Empty(receiver.Arrivals);
    }

    [Fact]
    public async Task Should_SaveACallAsUnsent_When_TheOnlineSignInExpiredInsideTheLocalWindow()
    {
        Guid[] items = await SignedInWithThreeItemsAsync();
        ExpireTheOnlineSignIn();
        Assert.Equal(SyncStatus.SignInRequired, (await SyncAsync()).Status);
        Assert.Equal(EntryScreen.Home, (await session.Device.EnterAsync(connected: false)).Screen); // The day still opens offline.

        Guid call = Guid.NewGuid();
        Unsent().Save(new(call, UnsentKind.Call, session.Clock.GetUtcNow(),
            $$"""{"id":"{{call}}","locationId":"{{day.Locations[2].Id}}","channel":"InPerson"}"""));

        Assert.Equal(4, Unsent().Summarise().Total);
        Assert.Equal(SignInStatus.Started, (await SignInAgainAsync()).Status);
        session.Server.Serve(EncodedSnapshot.From(day));
        Assert.Equal(new UploadSummary(4, 4), (await SyncAsync()).Upload);
        Assert.Equal([items[0], items[1], call, items[2]], receiver.Arrivals); // Calls first, oldest first, then the order.
    }
}
