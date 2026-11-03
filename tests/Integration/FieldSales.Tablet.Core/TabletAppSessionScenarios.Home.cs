using FieldSales.Tablet;

namespace FieldSales.Tablet.Core.Tests;

public sealed partial class TabletAppSessionScenarios
{
    [Fact]
    public async Task Should_RestoreTheDayOnlyToItsOwner_When_TheRepSignsOutAndReturns()
    {
        using SessionTablet tablet = new();
        using TabletApp app = Create(tablet);
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        await tablet.SyncAsync("colm");
        var snapshot = new SnapshotStore(tablet.Device.Workspace("colm")).ReadLocations();
        var call = new CallStore(tablet.Device.Workspace("colm"), tablet.Clock).Save(Guid.NewGuid(), snapshot[0].Id,
            new(FieldSales.Calls.Contracts.CallChannel.Phone, "Phone pitch"));
        Assert.True(call.Succeeded, call.Error);
        var before = app.ReadHomeRecord("colm");
        Assert.True(Assert.Single(before.Sections[1].Locations).Done);
        Assert.True(await app.SignOutAsync(app.SessionVersion));
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("aoife"));
        var other = app.ReadHomeRecord((await app.EnterAsync()).RepSubject!);
        Assert.All(other.Sections, section => Assert.Empty(section.Locations));
        Assert.Equal(0, Assert.Single(other.Counters).Count);
        await app.SignOutAsync(app.SessionVersion);
        await tablet.Device.CompleteSignInAsync(SessionTablet.SignIn("colm"));
        var restored = app.ReadHomeRecord((await app.EnterAsync()).RepSubject!);
        Assert.Equal(1, Assert.Single(restored.Counters).Count);
        Assert.Equal(Assert.Single(before.Sections[1].Locations).Calls,
            Assert.Single(restored.Sections[1].Locations).Calls);
    }
}
