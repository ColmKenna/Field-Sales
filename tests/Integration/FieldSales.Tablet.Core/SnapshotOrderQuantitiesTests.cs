using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-054 agreed scenarios T1 and T2: snapshot 1.5's recent order quantities on the tablet — importing them, still reading
/// 1.4, refusing quantities that don't match the version, stepping the local database up to format 5, and working out the Review
/// marker offline from what was imported (US-012 S18).</summary>
[Trait("Category", "Integration")]
public sealed class SnapshotOrderQuantitiesTests : IDisposable
{
    private readonly TestTablet tablet = new();

    public void Dispose() => tablet.Dispose();

    // Murphy's (shop 0) last 3 accepted orders had product 1 at 48, 50 and 44 (newest first) and product 3 on two of them.
    private static TabletSnapshot WithQuantities()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", locations: 2, products: 5, version: "1.5");
        Guid murphys = snapshot.Locations[0].Id;
        SnapshotRecentOrderLine[] lines =
        [
            new(murphys, snapshot.Products[1].Id, [new(48m, "Each"), new(50m, "Each"), new(44m, "Each")]),
            new(murphys, snapshot.Products[3].Id, [new(6m, "Each"), new(12m, "Each")])
        ];
        return snapshot with
        {
            RecentOrderLines = lines,
            Manifest = snapshot.Manifest with { Counts = snapshot.Manifest.Counts with { RecentOrderLines = lines.Length } }
        };
    }

    private IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>> Quantities(Guid location)
    {
        SnapshotStore store = new(tablet.Workspace());
        return store.ReadOrderedQuantities(store.ReadActive()?.Generation, location);
    }

    // T2.
    [Fact]
    public async Task Should_ImportTheRecentOrderQuantities_When_A15SnapshotIsSynced()
    {
        TabletSnapshot snapshot = WithQuantities();

        await tablet.SyncAsync(snapshot);

        IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>> murphys = Quantities(snapshot.Locations[0].Id);
        Assert.Equal([new(48m, "Each"), new(50m, "Each"), new(44m, "Each")], murphys[snapshot.Products[1].Id]);
        Assert.Equal([new(6m, "Each"), new(12m, "Each")], murphys[snapshot.Products[3].Id]);
        Assert.Empty(Quantities(snapshot.Locations[1].Id));
    }

    // T2: a 1.4 snapshot still imports, with recent order products but no quantities, so nothing is marked.
    [Fact]
    public async Task Should_ImportWithNoQuantities_When_TheSnapshotIs14()
    {
        TabletSnapshot built = Snapshots.Build("colm", locations: 2, products: 5, version: "1.4");
        TabletSnapshot snapshot = built with
        {
            RecentOrderLines = [new(built.Locations[0].Id, built.Products[1].Id)],
            Manifest = built.Manifest with { Counts = built.Manifest.Counts with { RecentOrderLines = 1 } }
        };

        await tablet.SyncAsync(snapshot);

        Assert.Empty(Quantities(snapshot.Locations[0].Id));
    }

    // T2.
    [Theory]
    [InlineData("quantities on 1.4")]
    [InlineData("quantities missing")]
    [InlineData("no quantities")]
    [InlineData("zero quantity")]
    [InlineData("empty unit")]
    public async Task Should_RefuseTheSnapshot_When_ARecentOrderRowsQuantitiesDoNotMatchItsVersion(string fault)
    {
        ActiveSnapshot before = await tablet.SyncAsync(Snapshots.Build("colm", locations: 2, products: 5, version: "1.4"));
        JsonObject json = JsonSerializer.SerializeToNode(WithQuantities(), SnapshotContract.JsonOptions)!.AsObject();
        JsonObject row = json["recentOrderLines"]![0]!.AsObject();
        string version = "1.5";
        switch (fault)
        {
            case "quantities on 1.4": version = "1.4"; json["manifest"]!["version"] = version; break;
            case "quantities missing": row.Remove("quantities"); break;
            case "no quantities": row["quantities"] = new JsonArray(); break;
            case "zero quantity": row["quantities"]![0]!["quantity"] = 0; break;
            case "empty unit": row["quantities"]![0]!["unit"] = " "; break;
        }
        tablet.Server.Serve(EncodedSnapshot.FromJson(Encoding.UTF8.GetBytes(json.ToJsonString()), version));

        SyncOutcome outcome = await tablet.Sync().SyncAsync();

        Assert.Equal(SyncStatus.Failed, outcome.Status);
        // The day already downloaded stays the rep's day.
        Assert.Equal(before.Generation, new SnapshotStore(tablet.Workspace()).ReadActive()!.Generation);
    }

    // T2.
    [Fact]
    public async Task Should_KeepEverySnapshotAndUnsentItem_When_TheTabletDatabaseUpgradesToFormat5()
    {
        TabletSnapshot old = Snapshots.Build("colm", locations: 3, products: 5, version: "1.4");
        ActiveSnapshot active = await tablet.SyncAsync(old);
        string unsent = tablet.SaveWork().Fingerprint();
        using (SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            // Exact predecessor: format 4 had no order quantities table, nor format 6's pricing tables.
            command.CommandText = "DROP TABLE recent_order_quantities; DROP TABLE price_tier_terms; DROP TABLE price_tiers; DROP TABLE customer_tiers; DROP TABLE quantity_breaks; PRAGMA user_version=4;";
            command.ExecuteNonQuery();
        }

        SnapshotStore upgraded = new(tablet.Workspace());

        Assert.Equal(active.Generation, upgraded.ReadActive()!.Generation);
        Assert.Equal(3, upgraded.ReadLocations().Count);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
        using (SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            Assert.Equal(6L, command.ExecuteScalar()); // Through format 5 to the current format 6.
        }
        TabletSnapshot next = WithQuantities();
        await tablet.SyncAsync(next);
        Assert.Equal(3, Quantities(next.Locations[0].Id)[next.Products[1].Id].Count);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
    }

    // T1 (S18): with no connection, the marker comes from the order history already imported.
    [Fact]
    public async Task Should_CalculateTheMarkerFromTheSnapshot_When_TheTabletIsOffline()
    {
        TabletSnapshot snapshot = WithQuantities();
        await tablet.SyncAsync(snapshot);
        tablet.Server.Respond = _ => throw new HttpRequestException("No connection.");
        int requests = tablet.Server.Requests.Count;
        Guid murphys = snapshot.Locations[0].Id;
        Guid order = Guid.NewGuid();
        OrderStore orders = new(tablet.Workspace(), tablet.Clock);
        Assert.True(orders.AddLine(order, murphys, snapshot.Products[1].Id, 480m).Succeeded); // 48 typed as 480.
        TabletOrder saved = orders.AddLine(order, murphys, snapshot.Products[3].Id, 12m).Order!;

        SnapshotStore store = new(tablet.Workspace(), tablet.Clock);
        ProductSearchIndex index = store.ReadProductIndex(new DateOnly(2026, 10, 1));
        IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>> history = store.ReadOrderedQuantities(index.Generation, murphys);
        string?[] markers = [.. saved.Lines.Select(line =>
            QuantityMarker.For(line, history.GetValueOrDefault(line.ProductId) ?? [], index.Find(line.ProductId)?.QuantityStep))];

        Assert.Equal(("Usually about 48 here", (string?)null), (markers[0], markers[1]));
        Assert.Equal(requests, tablet.Server.Requests.Count);
    }
}
