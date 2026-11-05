using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Calls.Contracts;
using FieldSales.Directory.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Core;

namespace FieldSales.Api.Tests;

/// <summary>WI-053 (T-5.1.5, REP-US-012 S19–S22): the gaps a shop's most recent Call left, sent in snapshot 1.4. Agreed
/// scenarios 10 (server) and 12 (end to end). "Ordered since" is any order at the shop dated on or after the Call's date, whatever
/// its status.</summary>
public sealed partial class SuggestedListTests
{
    [Fact]
    public async Task Should_SendTheMostRecentCallsLowItemsMinusProductsOrderedSince_When_TheSnapshotIsBuilt()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid murphys = await _app.CustomerAsync("Murphy's Pharmacy", places.Arklow.Id);
        Guid doyles = await _app.CustomerAsync("Doyle's Shop", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category remedies = await _app.CategoryAsync("Remedies");
        Guid[] product = new Guid[5];
        for (int index = 0; index < product.Length; index++)
            product[index] = await _app.ProductAsync($"P-{index:D2}", $"Product {index:D2}", remedies.Id);
        DateTimeOffset At(int day) => new(2026, 9, day, 10, 0, 0, TimeSpan.FromHours(1));

        // Murphy's: an older Call's gap (product 0) is superseded by the 15 Sep phone Call, which marked 1, 2 and 3 Low and
        // counted 4. Product 1 was ordered the day before that Call, product 2 on the day (still Pending), 3 not at all.
        await ReceiveLowCallAsync(murphys, At(1), CallChannel.InPerson, (product[0], 1m, true));
        Guid lastCall = await ReceiveLowCallAsync(murphys, At(15), CallChannel.Phone,
            (product[1], null, true), (product[2], 0m, true), (product[3], null, true), (product[4], 6m, false));
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 14), accepted: true, product[1]);
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 15), accepted: false, product[2]);
        // Doyle's: its most recent Call marked nothing Low, so the earlier Call's gap isn't carried.
        await ReceiveLowCallAsync(doyles, At(1), CallChannel.InPerson, (product[0], 1m, true));
        await ReceiveLowCallAsync(doyles, At(10), CallChannel.InPerson, (product[0], 9m, false));

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        DateOnly sep15 = new(2026, 9, 15);
        Assert.Equal(new[] { product[1], product[3] }.Order().Select(id => new SnapshotLastCallLowLine(murphys, lastCall, sep15, id)),
            snapshot.LastCallLowLines!);
        Assert.Equal(2, snapshot.Manifest.Counts.LastCallLowLines);
        Assert.Equal("1.4", snapshot.Manifest.Version);
    }

    // Scenario 12, end to end: a Call marks two products Low and one goes on the Call's order; both are synced, and the next
    // snapshot carries the other to a later order at the shop with no Call.
    [Fact]
    public async Task Should_CarryAGapToALaterOrderWithNoCall_When_ACallWithLowItemsIsSyncedAndTheSnapshotRebuilt()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 3);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex morning = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));
        CallStockEntry Low(int index)
        {
            ProductSearchResult item = morning.Find(product[index])!;
            return new(item.Id, item.Code, item.Name, item.Unit, 1m, Low: true);
        }
        Guid call = Guid.NewGuid();
        OrderResult added = new OrderStore(Workspace, _clock).AddFromCall(call, murphys, product[0], 12m);
        Assert.True(added.Succeeded, added.Error);
        Assert.True(new CallStore(Workspace, _clock).Save(call, murphys, new(CallChannel.InPerson, null, [Low(0), Low(1)])).Succeeded);
        Assert.True(new OrderStore(Workspace, _clock).MarkReadyToSend(added.Order!.Id).Succeeded);

        Assert.Equal(new UploadSummary(2, 2), (await SyncAsync()).Upload);

        SnapshotStore store = new(Workspace);
        ProductSearchIndex next = store.ReadProductIndex(new DateOnly(2026, 9, 22));
        // The server took product 0 off: it is on an order since the Call.
        LastCallGaps gaps = store.ReadLastCallGaps(next.Generation, murphys)!;
        Assert.Equal((call, new DateOnly(2026, 9, 22)), (gaps.CallId, gaps.CalledOn));
        Assert.Equal([product[1]], gaps.ProductIds);
        CarriedLow carried = CarriedGaps.Read(Workspace, murphys, Guid.NewGuid(), next)!;
        Assert.Equal((call, new DateOnly(2026, 9, 22)), (carried.CallId, carried.CalledOn));
        Assert.Equal(["Product 01"], carried.Entries.Select(entry => entry.ProductName));
    }

    private async Task<Guid> ReceiveLowCallAsync(Guid location, DateTimeOffset savedAt, CallChannel channel,
        params (Guid Product, decimal? Count, bool Low)[] entries)
    {
        Guid id = Guid.NewGuid();
        TabletCall call = new(CallContract.Schema, CallContract.CurrentVersion, id, location, "colm", DateOnly.FromDateTime(savedAt.DateTime),
            savedAt, channel, CallMorning,
            StockEntries: [.. entries.Select(entry => new CallStockEntry(entry.Product, "CODE", "Name", "Each", entry.Count, entry.Low))]);
        await ReceiveAsync(receiver => receiver.ReceiveCallAsync(id, "colm",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(call, CallJsonContext.Default.TabletCall)), CancellationToken.None));
        return id;
    }
}
