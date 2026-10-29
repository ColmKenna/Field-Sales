using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Snapshot.Contracts;
using Legacy = FieldSales.Snapshot.V10.Contracts;
using LegacyReader = FieldSales.Tablet.V10.Core.SnapshotDocumentReader;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

public sealed class LocationContactStatusTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private static TabletSnapshot Rathdrum()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 0, version: "1.2");
        return snapshot with { Locations = [snapshot.Locations[0] with { Name = "Rathdrum" }] };
    }

    private static TabletSnapshot WithGap(TabletSnapshot snapshot, bool inactive = true) => snapshot with
    {
        Locations = [snapshot.Locations[0] with
        {
            MainContact = null,
            MainContactReplacement = new(snapshot.Locations[0].MainContact!.Id, "Mary Walsh", inactive)
        }]
    };

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.1")]
    public async Task Should_NotInferReplacementGaps_When_ReadingOlderSnapshotFields(string version)
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 2, 0, version: version);
        await tablet.SyncAsync(snapshot);
        tablet.Server.Respond = _ => throw new InvalidOperationException("No online contact reads");
        var reopened = new SnapshotStore(tablet.Workspace());
        foreach (SnapshotLocation shop in snapshot.Locations)
            Assert.Equal(shop, Assert.IsType<OfflineLocationDetail>(reopened.ReadLocation(shop.Id)).Location);
        using SqliteConnection db = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False");
        db.Open();
        using SqliteCommand schema = db.CreateCommand();
        schema.CommandText = "PRAGMA user_version";
        Assert.Equal(2L, schema.ExecuteScalar()); // Existing SQLite format, no migration.
    }

    [Fact]
    public async Task Should_ReadContactStatusOffline_When_DownloadIsReopenedAndRefreshed()
    {
        TabletSnapshot original = Rathdrum();
        await tablet.SyncAsync(original);
        UnsentStore work = tablet.SaveWork();
        string fingerprint = work.Fingerprint();
        string bytes = tablet.UnsentFileHash();
        foreach (bool inactive in new[] { true, false })
        {
            TabletSnapshot gap = WithGap(original, inactive);
            await tablet.SyncAsync(gap);
            tablet.Server.Respond = _ => throw new InvalidOperationException("No online contact reads");
            var detail = new SnapshotStore(tablet.Workspace()).ReadLocation(original.Locations[0].Id)!;
            Assert.Equal(gap.Locations[0], detail.Location);
            Assert.Null(detail.Location.MainContact);
            Assert.True(detail.IsAssigned);
        }
        await tablet.SyncAsync(original); // A replacement clears only the new generation's gap.
        Assert.Equal(original.Locations[0], new SnapshotStore(tablet.Workspace()).ReadLocation(original.Locations[0].Id)!.Location);
        Assert.Equal(bytes, tablet.UnsentFileHash());
        Assert.Equal(fingerprint, work.Fingerprint());
    }

    [Fact]
    public async Task Should_KeepEveryLocationsStatus_When_TheTargetSnapshotHas500ReplacementGaps()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 500, 0, version: "1.2");
        snapshot = snapshot with { Locations = snapshot.Locations.Select((shop, index) => shop with
        {
            MainContact = null,
            MainContactReplacement = new(Guid.NewGuid(), $"Outgoing contact {index}", index % 2 == 0)
        }).ToArray() };
        await tablet.SyncAsync(snapshot);
        tablet.Server.Respond = _ => throw new InvalidOperationException("No online contact reads");
        var reopened = new SnapshotStore(tablet.Workspace());
        Assert.Equal(snapshot.Manifest, reopened.ReadActive()!.Manifest);
        foreach (SnapshotLocation shop in snapshot.Locations)
            Assert.Equal(shop, reopened.ReadLocation(shop.Id)!.Location);
    }

    [Fact]
    public async Task Should_RetainMainContactReplacement_When_ShopLeavesAssignmentWithUnsentWork()
    {
        TabletSnapshot gap = WithGap(Rathdrum());
        SnapshotLocation shop = gap.Locations[0];
        await tablet.SyncAsync(gap);
        UnsentStore work = new(tablet.Workspace());
        work.Save(new(Guid.NewGuid(), UnsentKind.Order, tablet.Clock.GetUtcNow(), JsonSerializer.Serialize(new { locationId = shop.Id })));
        string fingerprint = work.Fingerprint();
        for (int refresh = 0; refresh < 4; refresh++)
        {
            await tablet.SyncAsync(Snapshots.Build("colm", 0, 0, version: "1.2"));
            await tablet.Sync().CleanupAsync();
        }
        var reopened = new SnapshotStore(tablet.Workspace());
        OfflineLocationDetail detail = Assert.IsType<OfflineLocationDetail>(reopened.ReadLocation(shop.Id));
        Assert.Equal(shop, detail.Location);
        Assert.False(detail.IsAssigned);
        Assert.True(detail.HasUnsentOrder);
        Assert.Empty(reopened.ReadLocationIndex().Search("Rathdrum"));
        Assert.Null(new SnapshotStore(tablet.Workspace("aoife")).ReadLocation(shop.Id));
        Assert.Equal(fingerprint, work.Fingerprint());
    }

    [Theory]
    [InlineData("invalid-status")]
    [InlineData("invalid-property-case")]
    [InlineData("bad-hash")]
    [InlineData("wrong-version")]
    [InlineData("conflicting-active-main")]
    [InlineData("cancel-before-activation")]
    public async Task Should_KeepPreviousContactAndWork_When_ContactStatusDownloadFails(string fault)
    {
        TabletSnapshot original = Rathdrum();
        await tablet.SyncAsync(original);
        UnsentStore work = tablet.SaveWork();
        string fingerprint = work.Fingerprint();
        string bytes = tablet.UnsentFileHash();
        string generation = new SnapshotStore(tablet.Workspace()).ReadActive()!.Generation;
        TabletSnapshot next = WithGap(original);
        if (fault == "wrong-version") next = next with { Manifest = next.Manifest with { Version = "1.1" } };
        if (fault == "conflicting-active-main")
            next = next with { Locations = [next.Locations[0] with { MainContact = original.Locations[0].MainContact }] };
        EncodedSnapshot encoded;
        if (fault is "invalid-status" or "invalid-property-case")
        {
            JsonObject json = JsonSerializer.SerializeToNode(next, SnapshotContract.JsonOptions)!.AsObject();
            if (fault == "invalid-status")
                json["locations"]![0]!["mainContactReplacement"]!.AsObject().Remove("isInactive");
            else
            {
                JsonObject location = json["locations"]![0]!.AsObject();
                JsonNode? status = location["mainContactReplacement"];
                location.Remove("mainContactReplacement");
                location["MainContactReplacement"] = status;
            }
            encoded = EncodedSnapshot.FromJson(JsonSerializer.SerializeToUtf8Bytes(json), "1.2");
        }
        else encoded = EncodedSnapshot.From(next);
        tablet.Server.Serve(encoded, plainHash: fault == "bad-hash" ? new string('0', 64) : null);
        using var cancel = new CancellationTokenSource();
        SyncOutcome outcome = await tablet.Sync().SyncAsync(new() { AtBoundary = point =>
        {
            if (point == "before-activation" && fault == "cancel-before-activation") cancel.Cancel();
        } }, cancel.Token);
        Assert.Equal(fault == "cancel-before-activation" ? SyncStatus.Cancelled : SyncStatus.Failed, outcome.Status);
        var reopened = new SnapshotStore(tablet.Workspace());
        Assert.Equal(generation, reopened.ReadActive()!.Generation);
        Assert.Equal(original.Locations[0], reopened.ReadLocation(original.Locations[0].Id)!.Location);
        Assert.Equal(bytes, tablet.UnsentFileHash());
        Assert.Equal(fingerprint, work.Fingerprint());
    }

    [Fact]
    public void Should_KeepOriginalLocationFields_When_OldReaderReceivesVersion12Replacement()
    {
        Assert.Equal("1.2", SnapshotContract.CurrentVersion);
        TabletSnapshot snapshot = WithGap(Rathdrum());
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotContract.JsonOptions);
        using JsonDocument json = JsonDocument.Parse(bytes);
        Assert.Equal(["contactId", "name", "isInactive"], json.RootElement.GetProperty("locations")[0]
            .GetProperty("mainContactReplacement").EnumerateObject().Select(property => property.Name));
        using var stream = new MemoryStream(bytes);
        List<Legacy.SnapshotLocation> rows = [];
        new LegacyReader(stream).Read(_ => { }, (family, row) =>
        {
            if (family == "locations") rows.Add(row.Deserialize<Legacy.SnapshotLocation>(Legacy.SnapshotContract.JsonOptions)!);
        }, CancellationToken.None);
        SnapshotLocation shop = snapshot.Locations[0];
        Legacy.SnapshotLocation expected = new(shop.Id, shop.Name, shop.CustomerId, shop.CustomerName,
            new(shop.Town.Id, shop.Town.Name, shop.Town.CountyName), shop.Eircode, null);
        Assert.Equal(expected, Assert.Single(rows));
        Assert.Null(Assert.Single(rows).MainContact); // Outgoing Mary is never represented as active to old apps.
    }
}
