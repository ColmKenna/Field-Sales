using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-049 (T-5.4.1, REP-US-007): a Stock Check saved on a Call, with uncounted rows as Not checked. Agreed
/// scenarios 10 and 15 at store level.</summary>
[Trait("Category", "Integration")]
public sealed class StockCheckRecordingTests : IDisposable
{
    private readonly TestTablet tablet = new();

    public void Dispose() => tablet.Dispose();

    private static CallStockEntry Entry(SnapshotProduct product, decimal? count) => new(product.Id, product.Code, product.Name, product.Unit, count);

    [Fact]
    public async Task Should_SaveThe3AsNotChecked_When_TheCallIsSaved()
    {
        TabletSnapshot day = Snapshots.Build("colm", locations: 1, products: 13);
        await tablet.SyncAsync(day);
        Guid id = Guid.NewGuid();
        CallStockEntry[] check = [.. day.Products.Select((product, index) => Entry(product, index < 10 ? index : null))];

        CallResult saved = new CallStore(tablet.Workspace(), tablet.Clock).Save(id, day.Locations[0].Id, new(CallChannel.InPerson, null, check));

        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal("In person · 10 counted, 3 not checked", saved.Call!.Content.Summary);
        string body = new UnsentStore(tablet.Workspace()).Find(id)!.Json;
        Assert.Equal(check, JsonSerializer.Deserialize(body, CallJsonContext.Default.TabletCall)!.StockEntries);
        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement[] entries = [.. json.RootElement.GetProperty("stockEntries").EnumerateArray()];
        Assert.Equal(13, entries.Length);
        // Not checked is the absence of a count; zero is a count (the first product was counted 0).
        Assert.Equal(3, entries.Count(entry => !entry.TryGetProperty("count", out _)));
        Assert.Equal(0m, entries[0].GetProperty("count").GetDecimal());
        Assert.Equal((day.Products[12].Code, day.Products[12].Name, "Each"),
            (entries[12].GetProperty("productCode").GetString(), entries[12].GetProperty("productName").GetString(), entries[12].GetProperty("unit").GetString()));
    }

    [Fact]
    public async Task Should_ShowAddPitchNotesAStockCheckOrACompetitorNote_When_EveryLineIsNotChecked()
    {
        TabletSnapshot day = Snapshots.Build("colm", locations: 1, products: 3);
        await tablet.SyncAsync(day);

        CallResult refused = new CallStore(tablet.Workspace(), tablet.Clock).Save(Guid.NewGuid(), day.Locations[0].Id,
            new(CallChannel.InPerson, null, [.. day.Products.Select(product => Entry(product, null))]));

        Assert.Equal(CallContract.RecordSomething, refused.Error);
        Assert.Empty(new UnsentStore(tablet.Workspace()).Read());
    }
}
