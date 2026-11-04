using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-049 agreed scenarios 17 and 18: snapshot 1.3's stock history on the tablet — reading older snapshots,
/// refusing history that doesn't match its snapshot, and stepping the local database up to format 3 safely.</summary>
[Trait("Category", "Integration")]
public sealed class SnapshotStockHistoryTests : IDisposable
{
    private readonly TestTablet tablet = new();

    public void Dispose() => tablet.Dispose();

    // Murphy's (shop 0) last counted products 0-2 and recently ordered 2-3; Doyle's (shop 1) has no history.
    private static TabletSnapshot WithHistory()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", locations: 2, products: 5, version: "1.3");
        Guid murphys = snapshot.Locations[0].Id;
        SnapshotStockCheckLine[] checks = [.. snapshot.Products.Take(3).Select(product => new SnapshotStockCheckLine(murphys, product.Id))];
        SnapshotRecentOrderLine[] orders = [.. snapshot.Products.Skip(2).Take(2).Select(product => new SnapshotRecentOrderLine(murphys, product.Id))];
        return snapshot with
        {
            LastStockCheckLines = checks, RecentOrderLines = orders,
            Manifest = snapshot.Manifest with { Counts = snapshot.Manifest.Counts with { LastStockCheckLines = 3, RecentOrderLines = 2 } }
        };
    }

    private IReadOnlyList<SuggestedProduct> Suggested(Guid location)
    {
        SnapshotStore store = new(tablet.Workspace());
        ProductSearchIndex index = store.ReadProductIndex(new DateOnly(2026, 10, 1));
        return SuggestedList.Build(store.ReadStockHistory(index.Generation, location), index);
    }

    [Fact]
    public async Task Should_ImportWithAnEmptyHistory_When_TheSnapshotIs12()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", locations: 2, products: 5, version: "1.2");

        await tablet.SyncAsync(snapshot);

        Assert.Empty(Suggested(snapshot.Locations[0].Id));
        Assert.Equal(new SnapshotCounts(2, 2, 5, 10, 0, 0, 0), new SnapshotStore(tablet.Workspace()).ReadActive()!.Manifest.Counts);
    }

    [Fact]
    public async Task Should_ReadTheShopsHistory_When_A13SnapshotIsImported()
    {
        TabletSnapshot snapshot = WithHistory();

        await tablet.SyncAsync(snapshot);

        Assert.Equal(["Last visit", "Last visit", "Last visit · Ordered recently", "Ordered recently"],
            Suggested(snapshot.Locations[0].Id).Select(row => row.Label));
        Assert.Empty(Suggested(snapshot.Locations[1].Id));
    }

    [Theory]
    [InlineData("unknown product")]
    [InlineData("unknown shop")]
    [InlineData("repeated row")]
    [InlineData("family missing")]
    [InlineData("count wrong")]
    [InlineData("history on 1.2")]
    public async Task Should_RefuseTheSnapshot_When_AHistoryRowNamesAProductOrShopNotInIt(string fault)
    {
        ActiveSnapshot before = await tablet.SyncAsync(Snapshots.Build("colm", locations: 2, products: 5, version: "1.2"));
        TabletSnapshot snapshot = WithHistory();
        JsonObject json = JsonSerializer.SerializeToNode(snapshot, SnapshotContract.JsonOptions)!.AsObject();
        JsonArray checks = json["lastStockCheckLines"]!.AsArray();
        string version = "1.3";
        switch (fault)
        {
            case "unknown product": checks[0]!["productId"] = Guid.NewGuid().ToString(); break;
            case "unknown shop": checks[0]!["locationId"] = Guid.NewGuid().ToString(); break;
            case "repeated row":
                checks.Add(checks[0]!.DeepClone());
                json["manifest"]!["counts"]!["lastStockCheckLines"] = 4;
                break;
            case "family missing": json.Remove("recentOrderLines"); break;
            case "count wrong": json["manifest"]!["counts"]!["recentOrderLines"] = 3; break;
            case "history on 1.2": version = "1.2"; json["manifest"]!["version"] = version; break;
        }
        tablet.Server.Serve(EncodedSnapshot.FromJson(Encoding.UTF8.GetBytes(json.ToJsonString()), version));

        SyncOutcome outcome = await tablet.Sync().SyncAsync();

        Assert.Equal(SyncStatus.Failed, outcome.Status);
        // The day already downloaded stays the rep's day.
        Assert.Equal(before.Generation, new SnapshotStore(tablet.Workspace()).ReadActive()!.Generation);
    }

    [Fact]
    public async Task Should_KeepEverySnapshotAndUnsentItem_When_TheTabletDatabaseUpgradesToFormat3()
    {
        TabletSnapshot old = Snapshots.Build("colm", locations: 3, products: 5, version: "1.2");
        ActiveSnapshot active = await tablet.SyncAsync(old);
        string unsent = tablet.SaveWork().Fingerprint();
        // Exact predecessor: format 2 had neither history table.
        using (SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DROP TABLE last_stock_check_lines; DROP TABLE recent_order_lines; PRAGMA user_version=2;";
            command.ExecuteNonQuery();
        }

        SnapshotStore upgraded = new(tablet.Workspace());

        Assert.Equal(active.Generation, upgraded.ReadActive()!.Generation);
        Assert.Equal(3, upgraded.ReadLocations().Count);
        Assert.Empty(Suggested(old.Locations[0].Id));
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
        using (SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            Assert.Equal(3L, command.ExecuteScalar());
        }
        TabletSnapshot next = WithHistory();
        await tablet.SyncAsync(next);
        Assert.Equal(4, Suggested(next.Locations[0].Id).Count);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
    }
}
