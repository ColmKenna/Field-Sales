using System.Text;
using System.Text.Json;
using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

/// <summary>WI-056 (T-5.7.1, REP-US-016): a follow-up Call, linked to the Call it follows up. Agreed scenarios 12 (the server stores
/// the link), 13 (the Suggested List joins a follow-up's products to its original's Stock Check) and 14 (end to end).</summary>
public sealed partial class SuggestedListTests
{
    // Scenario 12: the link is stored as sent, whether or not its original has arrived; an empty or self link is refused.
    [Fact]
    public async Task Should_StoreTheFollowUpLink_When_TheServerReceivesAFollowUpCall()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 1);
        Guid notYetArrived = Guid.NewGuid();

        Guid followUp = await ReceiveLinkedCallAsync(murphys, new DateTimeOffset(2026, 9, 22, 16, 0, 0, TimeSpan.FromHours(1)),
            CallChannel.Phone, notYetArrived, (product[0], 2m));

        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
        {
            ReceivedCall received = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Calls.AsNoTracking()
                .SingleAsync(call => call.Id == followUp);
            Assert.Equal(notYetArrived, received.FollowUpOf);
        }
        foreach (Func<Guid, Guid> link in new Func<Guid, Guid>[] { _ => Guid.Empty, self => self })
        {
            Guid id = Guid.NewGuid();
            TabletCall call = new(CallContract.Schema, CallContract.CurrentVersion, id, murphys, "colm", new DateOnly(2026, 9, 22),
                new DateTimeOffset(2026, 9, 22, 16, 0, 0, TimeSpan.FromHours(1)), CallChannel.Phone, CallMorning,
                StockEntries: [new(product[0], "CODE", "Name", "Each", 2m)], FollowUpOf: link(id));

            await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
            ReceiveResult refused = await scope.ServiceProvider.GetRequiredService<TabletWorkReceiver>().ReceiveCallAsync(id, "colm",
                JsonSerializer.SerializeToUtf8Bytes(call, CallJsonContext.Default.TabletCall), CancellationToken.None);
            Assert.Equal((ReceiveOutcome.Refused, "The call's follow-up link must name another call."), (refused.Outcome, refused.Detail));
            Assert.False(await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Calls.AnyAsync(each => each.Id == id));
        }
    }

    // Scenario 13 (D5): products a follow-up counts join the original's Stock Check, and its follow-ups' too; a follow-up never
    // becomes the last check itself, however late it was saved.
    [Fact]
    public async Task Should_AddTheFollowUpsProductsToTheLastStockCheck_When_AnInPersonFollowUpCountsAForgottenProduct()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 5);
        // Last week's check (product 4) is replaced by this morning's (products 0 and 1).
        await ReceiveCallAsync(murphys, new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(1)), CallChannel.InPerson, (product[4], 6m));
        Guid morning = await ReceiveLinkedCallAsync(murphys, new DateTimeOffset(2026, 9, 22, 9, 40, 0, TimeSpan.FromHours(1)),
            CallChannel.InPerson, null, (product[0], 3m), (product[1], null));
        // Back in at 16:00 for the forgotten product 2 (and product 0 again), then a phone follow-up of that for product 3.
        Guid afternoon = await ReceiveLinkedCallAsync(murphys, new DateTimeOffset(2026, 9, 22, 16, 0, 0, TimeSpan.FromHours(1)),
            CallChannel.InPerson, morning, (product[2], 4m), (product[0], 13m));
        await ReceiveLinkedCallAsync(murphys, new DateTimeOffset(2026, 9, 22, 17, 30, 0, TimeSpan.FromHours(1)),
            CallChannel.Phone, afternoon, (product[3], 1m));

        TabletSnapshot next = await _app.SyncAsync("colm");

        Assert.Equal(new[] { product[0], product[1], product[2], product[3] }.Order(),
            next.LastStockCheckLines!.Select(line => line.ProductId).Order());
    }

    // Scenario 14: a follow-up saved on the tablet, and its original, upload in one Sync; the link survives and the next day's
    // list carries both Calls' products.
    [Fact]
    public async Task Should_KeepTheFollowUpLinkedToItsOriginal_When_ASyncUploadsBoth()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 3);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex index = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));
        CallStockEntry Entry(int at, decimal? count)
        {
            ProductSearchResult item = index.Find(product[at])!;
            return new(item.Id, item.Code, item.Name, item.Unit, count);
        }
        CallStore calls = new(Workspace, _clock);
        Guid original = Guid.NewGuid(), followUp = Guid.NewGuid();
        Assert.True(calls.Save(original, murphys, new(CallChannel.InPerson, null, [Entry(0, 3m), Entry(1, null)])).Succeeded);
        _clock.Advance(TimeSpan.FromHours(6));
        CallResult saved = calls.Save(followUp, murphys, new(CallChannel.InPerson, null, [Entry(2, 2m)]), followUpOf: original);
        Assert.True(saved.Succeeded, saved.Error);

        Assert.Equal(new UploadSummary(2, 2), (await SyncAsync()).Upload);

        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
        {
            ReceivedCall received = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Calls.AsNoTracking()
                .SingleAsync(call => call.Id == followUp);
            Assert.Equal(original, received.FollowUpOf);
        }
        Assert.Equal([("Product 00", "Last visit"), ("Product 01", "Last visit"), ("Product 02", "Last visit")],
            SuggestedFor(murphys).Select(row => (row.Product.Name, row.Label)).ToArray());
    }

    private async Task<Guid> ReceiveLinkedCallAsync(Guid location, DateTimeOffset savedAt, CallChannel channel, Guid? followUpOf,
        params (Guid Product, decimal? Count)[] entries)
    {
        Guid id = Guid.NewGuid();
        TabletCall call = new(CallContract.Schema, CallContract.CurrentVersion, id, location, "colm", DateOnly.FromDateTime(savedAt.DateTime),
            savedAt, channel, CallMorning, StockEntries: [.. entries.Select(entry => new CallStockEntry(entry.Product, "CODE", "Name", "Each", entry.Count))],
            FollowUpOf: followUpOf);
        await ReceiveAsync(receiver => receiver.ReceiveCallAsync(id, "colm",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(call, CallJsonContext.Default.TabletCall)), CancellationToken.None));
        return id;
    }
}
