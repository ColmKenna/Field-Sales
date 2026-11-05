using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

/// <summary>WI-055 (T-5.6.1, REP-US-015): a Call corrected on the tablet before Sync reaches the server as corrected. Agreed scenario 17
/// (end to end).</summary>
public sealed partial class SuggestedListTests
{
    [Fact]
    public async Task Should_SendTheCorrectedCall_When_AnEditedCallIsSynced()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 2);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex morning = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));
        CallStockEntry Entry(int index, decimal count)
        {
            ProductSearchResult item = morning.Find(product[index])!;
            return new(item.Id, item.Code, item.Name, item.Unit, count);
        }
        // Counted 3 of product 0, and product 1 by mistake; corrected to 13 with product 1 removed before Sync.
        Guid call = Guid.NewGuid();
        CallStore calls = new(Workspace, _clock);
        Assert.True(calls.Save(call, murphys, new(CallChannel.InPerson, null, [Entry(0, 3m), Entry(1, 2m)])).Succeeded);
        CallResult corrected = calls.Correct(call, murphys, new(CallChannel.InPerson, null, [Entry(0, 13m)]));
        Assert.True(corrected.Succeeded, corrected.Error);

        Assert.Equal(new UploadSummary(1, 1), (await SyncAsync()).Upload);

        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceivedCall received = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Calls.AsNoTracking()
            .Include(each => each.StockEntries).SingleAsync(each => each.Id == call);
        Assert.Equal([(product[0], (decimal?)13m)], received.StockEntries.Select(entry => (entry.ProductId, entry.Count)));
        Assert.Equal(CallStanding.Sent, calls.ReadView(call)!.Standing);
    }
}
