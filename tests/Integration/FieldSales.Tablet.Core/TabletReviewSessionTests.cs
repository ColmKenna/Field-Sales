using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core.Tests;

public sealed partial class TabletAppSessionScenarios
{
    [Theory]
    [InlineData("expired")]
    [InlineData("signed-out")]
    [InlineData("another-rep")]
    [InlineData("new-session-same-rep")]
    public async Task Should_LeaveTheOrderUnchanged_When_AReviewActionUsesAnExpiredOrDifferentSession(string change)
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await tablet.SyncAsync("colm");
        using var app = Create(tablet);
        RepWorkspace workspace = tablet.Device.Workspace("colm");
        SnapshotStore snapshot = new(workspace);
        OrderStore orders = new(workspace, tablet.Clock);
        Guid id = Guid.NewGuid();
        Assert.True(orders.AddLine(id, snapshot.ReadLocations()[0].Id, snapshot.ReadProducts(0).Products[0].Id, 1).Succeeded);
        UnsentStore work = new(workspace);
        string before = work.Fingerprint();
        long version = app.SessionVersion;
        if (change == "expired") tablet.Clock.UtcNow = SessionTablet.Morning.AddHours(24);
        else
        {
            await app.SignOutAsync(version);
            if (change != "signed-out")
                await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn(change == "another-rep" ? "aoife" : "colm"));
        }

        Assert.False(await app.MarkReviewedOrderReadyAsync("colm", id, version));

        Assert.Equal(before, work.Fingerprint());
        Assert.Equal(OrderState.InProgress, orders.Find(id)!.State);
    }

    [Fact]
    public async Task Should_MarkTheOrderReady_When_TheCurrentRepReviewsIt()
    {
        using SessionTablet tablet = new();
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await tablet.SyncAsync("colm");
        using var app = Create(tablet);
        RepWorkspace workspace = tablet.Device.Workspace("colm");
        SnapshotStore snapshot = new(workspace);
        OrderStore orders = new(workspace, tablet.Clock);
        Guid id = Guid.NewGuid();
        Assert.True(orders.AddLine(id, snapshot.ReadLocations()[0].Id, snapshot.ReadProducts(0).Products[0].Id, 1).Succeeded);

        Assert.True(await app.MarkReviewedOrderReadyAsync("colm", id, app.SessionVersion));

        Assert.Equal(OrderState.ReadyToSend, orders.Find(id)!.State);
        Assert.False(await app.MarkReviewedOrderReadyAsync("colm", id, app.SessionVersion));
        Assert.Empty(new UnsentStore(workspace).ReadSent()); // Review never initiates Sync.
    }
}
