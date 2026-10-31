using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-038 scenarios 20-25: Mark Ready to Send, the one allowed transition, and the Unsent summary.</summary>
public sealed class OrderReadyTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private OrderStore Store(string rep = "colm") => new(tablet.Workspace(rep), tablet.Clock);
    private UnsentStore Unsent(string rep = "colm") => new(tablet.Workspace(rep));

    // One shop and three Each products, priced 2.50 from 1 Oct.
    private static TabletSnapshot Byrnes()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 3);
        return snapshot with { Locations = [snapshot.Locations[0] with { Name = "Byrne's Chemist" }] };
    }

    private static string Json(TabletOrder order) => JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder);

    private Guid ThreeLineOrder(TabletSnapshot snapshot)
    {
        Guid orderId = Guid.NewGuid();
        foreach ((SnapshotProduct product, decimal quantity) in snapshot.Products.Zip(new[] { 12m, 6m, 1m }))
            Assert.True(Store().AddLine(orderId, snapshot.Locations[0].Id, product.Id, quantity).Succeeded);
        return orderId;
    }

    [Fact]
    public async Task Should_BecomeReadyToSendAndKeepItsLines_When_AnInProgressOrderWithThreeLinesIsMarkedReady()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = ThreeLineOrder(snapshot);
        TabletOrder before = Store().Find(orderId)!;
        Assert.Equal(OrderState.InProgress, before.State);
        int requests = tablet.Server.Requests.Count;

        OrderResult result = Store().MarkReadyToSend(orderId);

        Assert.True(result.Succeeded, result.Error);
        TabletOrder ready = result.Order!;
        Assert.Equal(OrderState.ReadyToSend, ready.State);
        Assert.Equal(Json(before with { State = OrderState.ReadyToSend }), Json(ready));
        Assert.Equal(Json(ready), Json(Store().Find(orderId)!));
        Assert.Equal(3, ready.Lines.Count);
        UnsentItem saved = Assert.Single(Unsent().Read());
        using JsonDocument body = JsonDocument.Parse(saved.Json);
        Assert.Equal("ReadyToSend", body.RootElement.GetProperty("state").GetString());
        Assert.Equal(requests, tablet.Server.Requests.Count); // Nothing is sent until the rep syncs.
    }

    [Fact]
    public async Task Should_RefuseWithAddAtLeastOneProductAndStayInProgress_When_TheOrderHasNoLines()
    {
        await tablet.SyncAsync(Byrnes());
        Assert.Equal("Add at least one product", Store().MarkReadyToSend(Guid.NewGuid()).Error); // Nothing was ever added.
        Assert.Empty(Unsent().Read());

        TabletSnapshot snapshot = Byrnes();
        TabletOrder noLines = Store().AddLine(Guid.NewGuid(), snapshot.Locations[0].Id, snapshot.Products[0].Id, 1).Order! with { Lines = [] };
        Unsent().Save(new(noLines.Id, UnsentKind.Order, tablet.Clock.GetUtcNow(), Json(noLines)));

        OrderResult result = Store().MarkReadyToSend(noLines.Id);

        Assert.Equal("Add at least one product", result.Error);
        Assert.Equal(OrderState.InProgress, Store().Find(noLines.Id)!.State);
    }

    [Fact]
    public async Task Should_RefuseAndChangeNothing_When_TheOrderIsAlreadyReadyToSend()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = ThreeLineOrder(snapshot);
        Store().MarkReadyToSend(orderId);
        UnsentItem saved = Assert.Single(Unsent().Read());
        tablet.Clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(OrderStore.AlreadyReady, Store().MarkReadyToSend(orderId).Error);
        Assert.Equal(OrderStore.AlreadyReady, Store().AddLine(orderId, snapshot.Locations[0].Id, snapshot.Products[0].Id, 1).Error);

        Assert.Equal(saved, Assert.Single(Unsent().Read())); // The body and the saved time are untouched.
    }

    [Fact]
    public async Task Should_RefuseAndChangeNothing_When_TheSavedOrderIsNotAnOrderRecordTheTabletWrote()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid legacy = Guid.NewGuid();
        Unsent().Save(new(legacy, UnsentKind.Order, tablet.Clock.GetUtcNow(), """{"state":"In Progress","lines":[{"product":"A-00001","quantity":6}]}"""));
        UnsentItem before = Assert.Single(Unsent().Read());

        Assert.Equal(OrderStore.NotChangeable, Store().MarkReadyToSend(legacy).Error);
        Assert.Equal(OrderStore.NotChangeable, Store().AddLine(legacy, snapshot.Locations[0].Id, snapshot.Products[0].Id, 1).Error);
        Assert.Equal(before, Assert.Single(Unsent().Read()));
    }

    [Fact]
    public async Task Should_StillMarkReady_When_TheShopWasReassignedSinceTheOrderWasStarted()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = ThreeLineOrder(snapshot);
        await tablet.SyncAsync(Snapshots.Build("colm", 0, 3)); // Valid when captured: a later reassignment does not block it.

        Assert.True(Store().MarkReadyToSend(orderId).Succeeded);
        Assert.Equal(OrderState.ReadyToSend, Store().Find(orderId)!.State);
    }

    [Fact]
    public async Task Should_CountReadyAndInProgressOrdersAndEveryItem_When_UnsentWorkIsSummarised()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Assert.Equal(new UnsentSummary(0, 0, 0), Unsent().Summarise());
        tablet.SaveWork(); // Two Calls and one Order saved by an earlier build, whose body has no recognised state.
        Guid ready = ThreeLineOrder(snapshot);
        ThreeLineOrder(snapshot);
        Store().MarkReadyToSend(ready);

        UnsentSummary summary = Unsent().Summarise();

        Assert.Equal(new UnsentSummary(Total: 5, InProgress: 1, ReadyToSend: 3), summary); // Saved Calls are Ready to Send on T-01.
        Assert.Equal(2, Unsent().Read().Count(item => item.Kind == UnsentKind.Call));
        Assert.Equal(3, Unsent().Read().Count(item => item.Kind == UnsentKind.Order));
    }

    [Fact]
    public async Task Should_CountOnlyTheCurrentRepsWorkAndCreateNothing_When_TwoRepsShareTheTablet()
    {
        TabletSnapshot snapshot = Byrnes();
        await tablet.SyncAsync(snapshot);
        Guid orderId = ThreeLineOrder(snapshot);
        Store().MarkReadyToSend(orderId);

        Assert.Equal(new UnsentSummary(1, 0, 1), Unsent().Summarise());
        Assert.Equal(new UnsentSummary(0, 0, 0), Unsent("aoife").Summarise());
        Assert.False(File.Exists(tablet.Workspace("aoife").UnsentPath)); // Asking never creates a database.
        Assert.Null(Store("aoife").Find(orderId));
    }
}
