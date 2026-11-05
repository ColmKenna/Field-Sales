using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Calls.Contracts;
using FieldSales.Directory.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Api.Tests;

/// <summary>WI-053 (T-5.1.5, REP-US-012 S19–S22): the gaps a shop's most recent Call left, sent in snapshot 1.4. Agreed
/// scenario 10 (server). "Ordered since" is any order at the shop dated on or after the Call's date, whatever its status.</summary>
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
