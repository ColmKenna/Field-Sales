using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-053 agreed scenario 11: snapshot 1.4's last Call gaps on the tablet — importing them, still reading 1.3,
/// refusing gaps that don't match their snapshot, and stepping the local database up to format 4 safely.</summary>
[Trait("Category", "Integration")]
public sealed class SnapshotLastCallGapsTests : IDisposable
{
    private static readonly Guid CallAtMurphys = new("5e1f0000-0000-0000-0000-000000000015");
    private static readonly DateOnly Sep15 = new(2026, 9, 15);
    private readonly TestTablet tablet = new();

    public void Dispose() => tablet.Dispose();

    // Murphy's (shop 0) last Call, on 15 Sep, left products 1 and 3 unordered; Doyle's (shop 1) has no gaps.
    private static TabletSnapshot WithGaps()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", locations: 2, products: 5, version: "1.4");
        Guid murphys = snapshot.Locations[0].Id;
        SnapshotLastCallLowLine[] gaps = [.. new[] { 1, 3 }.Select(index =>
            new SnapshotLastCallLowLine(murphys, CallAtMurphys, Sep15, snapshot.Products[index].Id))];
        return snapshot with
        {
            LastCallLowLines = gaps,
            Manifest = snapshot.Manifest with { Counts = snapshot.Manifest.Counts with { LastCallLowLines = 2 } }
        };
    }

    private LastCallGaps? Gaps(Guid location)
    {
        SnapshotStore store = new(tablet.Workspace());
        return store.ReadLastCallGaps(store.ReadActive()?.Generation, location);
    }

    [Fact]
    public async Task Should_ImportTheLastCallLowLines_When_A14SnapshotIsSynced()
    {
        TabletSnapshot snapshot = WithGaps();

        await tablet.SyncAsync(snapshot);

        LastCallGaps murphys = Gaps(snapshot.Locations[0].Id)!;
        Assert.Equal(CallAtMurphys, murphys.CallId);
        Assert.Equal(Sep15, murphys.CalledOn);
        Assert.Equal(new[] { snapshot.Products[1].Id, snapshot.Products[3].Id }.Order(), murphys.ProductIds.Order());
        Assert.Null(Gaps(snapshot.Locations[1].Id));
    }

    [Fact]
    public async Task Should_ImportWithNoGaps_When_TheSnapshotIs13()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", locations: 2, products: 5, version: "1.3");

        await tablet.SyncAsync(snapshot);

        Assert.Null(Gaps(snapshot.Locations[0].Id));
        Assert.Equal(new SnapshotCounts(2, 2, 5, 10, 0, 0, 0, 0), new SnapshotStore(tablet.Workspace()).ReadActive()!.Manifest.Counts);
    }

    [Theory]
    [InlineData("unknown product")]
    [InlineData("unknown shop")]
    [InlineData("repeated row")]
    [InlineData("two calls at one shop")]
    [InlineData("family missing")]
    [InlineData("count wrong")]
    [InlineData("gaps on 1.3")]
    public async Task Should_RefuseTheSnapshot_When_ALastCallGapDoesNotMatchIt(string fault)
    {
        ActiveSnapshot before = await tablet.SyncAsync(Snapshots.Build("colm", locations: 2, products: 5, version: "1.3"));
        TabletSnapshot snapshot = WithGaps();
        JsonObject json = JsonSerializer.SerializeToNode(snapshot, SnapshotContract.JsonOptions)!.AsObject();
        JsonArray gaps = json["lastCallLowLines"]!.AsArray();
        string version = "1.4";
        switch (fault)
        {
            case "unknown product": gaps[0]!["productId"] = Guid.NewGuid().ToString(); break;
            case "unknown shop": gaps[0]!["locationId"] = Guid.NewGuid().ToString(); break;
            case "repeated row":
                gaps.Add(gaps[0]!.DeepClone());
                json["manifest"]!["counts"]!["lastCallLowLines"] = 3;
                break;
            case "two calls at one shop": gaps[1]!["callId"] = Guid.NewGuid().ToString(); break;
            case "family missing": json.Remove("lastCallLowLines"); json["manifest"]!["counts"]!["lastCallLowLines"] = 0; break;
            case "count wrong": json["manifest"]!["counts"]!["lastCallLowLines"] = 1; break;
            case "gaps on 1.3": version = "1.3"; json["manifest"]!["version"] = version; break;
        }
        tablet.Server.Serve(EncodedSnapshot.FromJson(Encoding.UTF8.GetBytes(json.ToJsonString()), version));

        SyncOutcome outcome = await tablet.Sync().SyncAsync();

        Assert.Equal(SyncStatus.Failed, outcome.Status);
        // The day already downloaded stays the rep's day.
        Assert.Equal(before.Generation, new SnapshotStore(tablet.Workspace()).ReadActive()!.Generation);
    }

    [Fact]
    public async Task Should_KeepEverySnapshotAndUnsentItem_When_TheTabletDatabaseUpgradesToFormat4()
    {
        TabletSnapshot old = Snapshots.Build("colm", locations: 3, products: 5, version: "1.3");
        ActiveSnapshot active = await tablet.SyncAsync(old);
        string unsent = tablet.SaveWork().Fingerprint();
        long Format()
        {
            using SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            return (long)command.ExecuteScalar()!;
        }
        // Exact predecessor: format 3 had no last Call gaps table (nor format 5's order quantities, WI-054).
        using (SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DROP TABLE last_call_low_lines; DROP TABLE recent_order_quantities; DROP TABLE price_tier_terms; DROP TABLE price_tiers; DROP TABLE customer_tiers; DROP TABLE quantity_breaks; PRAGMA user_version=3;";
            command.ExecuteNonQuery();
        }

        SnapshotStore upgraded = new(tablet.Workspace());

        Assert.Equal(active.Generation, upgraded.ReadActive()!.Generation);
        Assert.Equal(3, upgraded.ReadLocations().Count);
        Assert.Null(Gaps(old.Locations[0].Id));
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
        Assert.Equal(6L, Format()); // Through formats 4 and 5 to the current format 6.
        TabletSnapshot next = WithGaps();
        await tablet.SyncAsync(next);
        Assert.Equal(2, Gaps(next.Locations[0].Id)!.ProductIds.Count);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
    }
}
