using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-051 (T-5.5.1, REP-US-008): adding a product to the order from a Call. The Call's order is found or started under
/// the order gate, and the order carries the Call's id (<see cref="TabletOrder.CallId"/>). Agreed scenarios 2, 3, 10 and 11
/// at storage level; the screen side is in the tablet component tests.</summary>
[Trait("Category", "Integration")]
public sealed class OrderFromCallTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private OrderStore Store() => new(tablet.Workspace(), tablet.Clock);

    private TabletOrder[] Orders() => [.. new UnsentStore(tablet.Workspace()).Read()
        .Select(OrderStore.Parse).OfType<TabletOrder>()];

    // Murphy's Pharmacy and three Each products with prices.
    private async Task<(Guid Shop, Guid[] Products)> MurphysAsync()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 3);
        await tablet.SyncAsync(snapshot with { Locations = [snapshot.Locations[0] with { Name = "Murphy's Pharmacy" }] });
        return (snapshot.Locations[0].Id, [.. snapshot.Products.Select(product => product.Id)]);
    }

    // Scenario 2 (S1), storage side.
    [Fact]
    public async Task Should_CreateAnInProgressOrderForMurphysLinkedToTheCall_When_24IsAddedFromACall()
    {
        (Guid shop, Guid[] products) = await MurphysAsync();
        Guid call = Guid.NewGuid();
        Assert.Null(Store().FindForCall(call, shop));

        OrderResult added = Store().AddFromCall(call, shop, products[0], 24m);

        Assert.True(added.Succeeded, added.Error);
        TabletOrder order = Assert.Single(Orders());
        Assert.Equal((OrderState.InProgress, shop, (Guid?)call), (order.State, order.LocationId, order.CallId));
        Assert.Equal((products[0], 24m), (Assert.Single(order.Lines).ProductId, order.Lines[0].Quantity));
        Assert.Equal(order.Id, Store().FindForCall(call, shop)?.Id);
    }

    // Scenario 3 (S2).
    [Fact]
    public async Task Should_AddA3rdLineAndCreateNoSecondOrder_When_MurphysAlreadyHasAnInProgressOrderWith2Lines()
    {
        (Guid shop, Guid[] products) = await MurphysAsync();
        Guid existing = Guid.NewGuid(), call = Guid.NewGuid();
        Assert.True(Store().AddLine(existing, shop, products[0], 2m).Succeeded);
        Assert.True(Store().AddLine(existing, shop, products[1], 3m).Succeeded);
        Assert.Equal(existing, Store().FindForCall(call, shop)?.Id); // The shop's In Progress order is the Call's order.

        OrderResult added = Store().AddFromCall(call, shop, products[2], 24m);

        Assert.True(added.Succeeded, added.Error);
        TabletOrder order = Assert.Single(Orders());
        Assert.Equal(existing, order.Id);
        Assert.Equal([products[0], products[1], products[2]], order.Lines.Select(line => line.ProductId));
        Assert.Equal(call, order.CallId); // An order with no Call yet becomes this Call's.
    }

    // Scenario 10: the checkpoint question. Low products from one Call, one after the other and released together, make one order.
    [Fact]
    public async Task Should_UseOneOrderForTheCall_When_TwoLowProductsAreAdded()
    {
        (Guid shop, Guid[] products) = await MurphysAsync();
        Guid call = Guid.NewGuid();
        Assert.True(Store().AddFromCall(call, Guid.Empty, products[0], 1m) is { Succeeded: false }); // Warms the store; adds nothing.

        using ManualResetEventSlim go = new();
        Task<OrderResult>[] adds = [.. Enumerable.Range(0, 8).Select(index => Task.Run(() =>
        {
            go.Wait();
            return Store().AddFromCall(call, shop, products[index % 2], 1m);
        }))];
        go.Set();
        OrderResult[] added = await Task.WhenAll(adds);
        Assert.True(Store().AddFromCall(call, shop, products[2], 1m).Succeeded);

        Assert.All(added, result => Assert.True(result.Succeeded, result.Error));
        TabletOrder order = Assert.Single(Orders());
        // Whichever of the two products landed first is the first line; each has one line with all four adds.
        Assert.Equal(new Dictionary<Guid, decimal> { [products[0]] = 4m, [products[1]] = 4m, [products[2]] = 1m },
            order.Lines.ToDictionary(line => line.ProductId, line => line.Quantity));
        Assert.Equal(call, order.CallId);
    }

    // Scenario 11 (D2): the linked order left In Progress, so the Call starts another and the new one is linked to it.
    [Fact]
    public async Task Should_FindOrCreateAgainAndMoveTheLink_When_TheLinkedOrderWasMarkedReadyToSendElsewhere()
    {
        (Guid shop, Guid[] products) = await MurphysAsync();
        Guid call = Guid.NewGuid();
        Guid first = Store().AddFromCall(call, shop, products[0], 2m).Order!.Id;
        Assert.True(Store().MarkReadyToSend(first).Succeeded);
        Assert.Null(Store().FindForCall(call, shop));

        OrderResult added = Store().AddFromCall(call, shop, products[1], 5m);

        Assert.True(added.Succeeded, added.Error);
        Assert.NotEqual(first, added.Order!.Id);
        Assert.Equal((OrderState.InProgress, (Guid?)call), (added.Order.State, added.Order.CallId));
        Assert.Equal(added.Order.Id, Store().FindForCall(call, shop)?.Id);
        Assert.Equal([products[0]], Store().Find(first)!.Lines.Select(line => line.ProductId)); // The Ready order is untouched.
    }

    [Fact]
    public async Task Should_PreferTheCallsOwnOrderAndKeepAnotherCallsLink_When_TheShopHasTwoInProgressOrders()
    {
        (Guid shop, Guid[] products) = await MurphysAsync();
        Guid otherCall = Guid.NewGuid(), call = Guid.NewGuid();
        Guid mine = Store().AddFromCall(call, shop, products[0], 1m).Order!.Id;
        tablet.Clock.Advance(TimeSpan.FromMinutes(5));
        Guid newer = Guid.NewGuid();
        Assert.True(Store().AddLine(newer, shop, products[1], 1m).Succeeded); // Saved later, from order entry.

        Assert.Equal(mine, Store().FindForCall(call, shop)?.Id);
        // Another Call at the shop adds to the most recently saved In Progress order.
        Assert.Equal(newer, Store().AddFromCall(otherCall, shop, products[2], 1m).Order!.Id);
        Assert.Equal(call, Store().Find(mine)!.CallId);
    }
}
