using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

/// <summary>WI-051 (T-5.5.1, REP-US-008): a Low item added to the order from a Call, from the tablet to the server. Agreed
/// scenario 15 end to end: the order's link to its Call (WI-051 D1 as revised, the Order's <c>callId</c>) is received and kept.</summary>
public sealed partial class SuggestedListTests
{
    [Fact]
    public async Task Should_KeepTheCallLink_When_AnOrderAddedFromACallIsUploaded()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 2);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex morning = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));
        ProductSearchResult low = morning.Find(product[0])!;

        // In person: product 0 counted 3 and marked Low, then 24 added to the order from the Call; the Call is saved.
        Guid call = Guid.NewGuid();
        OrderResult added = new OrderStore(Workspace, _clock).AddFromCall(call, murphys, low.Id, 24m);
        Assert.True(added.Succeeded, added.Error);
        CallStockEntry entry = new(low.Id, low.Code, low.Name, low.Unit, 3m, Low: true);
        Assert.True(new CallStore(Workspace, _clock).Save(call, murphys, new(CallChannel.InPerson, null, [entry])).Succeeded);
        Assert.True(new OrderStore(Workspace, _clock).MarkReadyToSend(added.Order!.Id).Succeeded);

        Assert.Equal(new UploadSummary(2, 2), (await SyncAsync()).Upload);

        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        OrderingDbContext ordering = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        ReceivedOrder order = await ordering.Orders.AsNoTracking().SingleAsync(received => received.Id == added.Order.Id);
        Assert.Equal((murphys, (Guid?)call), (order.LocationId, order.CallId));
        ReceivedCall received = await ordering.Calls.AsNoTracking().Include(each => each.StockEntries).SingleAsync(each => each.Id == call);
        Assert.Equal((low.Id, (decimal?)3m, true), received.StockEntries.Select(each => (each.ProductId, each.Count, each.Low)).Single());
    }
}
