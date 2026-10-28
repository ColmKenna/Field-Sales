using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

public sealed class ProductAttributeStorageTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    [Fact]
    public async Task Should_KeepTheDownloadedDayAndUnsent_When_VersionOneDatabaseIsUpgraded()
    {
        TabletSnapshot old = Snapshots.Build("colm", 3, 51);
        ActiveSnapshot active = await tablet.SyncAsync(old);
        string unsent = tablet.SaveWork().Fingerprint();
        // Exact predecessor schema: v2 adds only this table. Remove the additive metadata too.
        Execute("DROP TABLE product_attributes; UPDATE generations SET manifest=json_remove(manifest, '$.counts.productAttributes'); PRAGMA user_version=1;");
        var upgraded = new SnapshotStore(tablet.Workspace());
        Assert.Equal(active.Generation, upgraded.ReadActive()!.Generation);
        Assert.Equal(3, upgraded.ReadLocations().Count);
        Assert.Equal(50, upgraded.ReadProducts(0).Products.Count);
        Assert.Empty(upgraded.ReadProduct(old.Products[0].Id)!.Attributes);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
        Assert.Equal(2L, Scalar("PRAGMA user_version"));
        await tablet.SyncAsync(WithAttributes(products: 51));
        Assert.Equal("200 g", upgraded.ReadProduct(old.Products[0].Id)!.Attributes[0].Value);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
    }

    [Fact]
    public async Task Should_ReadPagedProductsAndExactOrderedRowsOffline_When_SnapshotIsReopened()
    {
        TabletSnapshot snapshot = WithAttributes(products: 51);
        var rows = snapshot.ProductAttributes!.ToList();
        string exact = "  <value>\n" + new string('界', 6000);
        rows.Add(new(snapshot.Products[0].Id, 3, "Weight", exact, true));
        rows.Add(new(snapshot.Products[0].Id, 4, "Weight", "", false));
        snapshot = snapshot with { ProductAttributes = rows, Manifest = snapshot.Manifest with { Counts = snapshot.Manifest.Counts with { ProductAttributes = rows.Count } } };
        await tablet.SyncAsync(snapshot);
        tablet.Server.Respond = _ => throw new InvalidOperationException("Offline reads must not make a request");
        var reopened = new SnapshotStore(tablet.Workspace());
        var first = reopened.ReadProducts(0);
        var second = reopened.ReadProducts(1);
        Assert.True(first.HasNext); Assert.False(second.HasNext);
        Assert.Equal(50, first.Products.Count); Assert.Single(second.Products);
        Assert.Equal(51, first.Products.Concat(second.Products).Select(row => row.Id).Distinct().Count());
        var detail = reopened.ReadProduct(snapshot.Products[0].Id)!;
        Assert.Equal(["Health", "Remedies A"], detail.Breadcrumb);
        Assert.Equal(rows.Where(row => row.ProductId == snapshot.Products[0].Id), detail.Attributes);
        Assert.Equal(exact, detail.Attributes[3].Value);
        Assert.True(detail.Attributes[3].IsArchived);
        Assert.Equal("", detail.Attributes[4].Value);
        Assert.Null(reopened.ReadProduct(Guid.NewGuid()));
        Assert.Empty(new SnapshotStore(tablet.Workspace("aoife")).ReadProducts(0).Products);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("negative-count")]
    [InlineData("orphan")]
    [InlineData("duplicate-position")]
    [InlineData("negative-position")]
    [InlineData("missing-value")]
    [InlineData("missing-family")]
    [InlineData("invalid-minor")]
    [InlineData("oversized-entry")]
    public async Task Should_KeepAllOldFactsAndUnsent_When_AnAttributePayloadIsInvalid(string fault)
    {
        TabletSnapshot old = WithAttributes();
        ActiveSnapshot active = await tablet.SyncAsync(old);
        string unsent = tablet.SaveWork().Fingerprint();
        JsonObject payload = JsonSerializer.SerializeToNode(WithAttributes(value: "300 g"), SnapshotContract.JsonOptions)!.AsObject();
        var attributes = payload["productAttributes"]!.AsArray();
        switch (fault)
        {
            case "count": payload["manifest"]!["counts"]!["productAttributes"] = 99; break;
            case "negative-count": payload["manifest"]!["counts"]!["productAttributes"] = -1; break;
            case "orphan": attributes[0]!["productId"] = Guid.NewGuid().ToString(); break;
            case "duplicate-position": attributes[1]!["position"] = 0; break;
            case "negative-position": attributes[0]!["position"] = -1; break;
            case "missing-value": attributes[0]!.AsObject().Remove("value"); break;
            case "missing-family": payload.Remove("productAttributes"); break;
            case "invalid-minor": payload["manifest"]!["version"] = "1.invalid"; break;
            case "oversized-entry": attributes[0]!["value"] = new string('x', 1024 * 1024); break;
        }
        tablet.Server.Serve(EncodedSnapshot.FromJson(JsonSerializer.SerializeToUtf8Bytes(payload), fault == "invalid-minor" ? "1.invalid" : "1.1"));
        Assert.Equal(SyncStatus.Failed, (await tablet.Sync().SyncAsync()).Status);
        var store = new SnapshotStore(tablet.Workspace());
        Assert.Equal(active.Generation, store.ReadActive()!.Generation);
        Assert.Equal(old.ProductAttributes!.Take(3), store.ReadProduct(old.Products[0].Id)!.Attributes);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
    }

    [Theory]
    [InlineData("before-activation")]
    [InlineData("after-activation")]
    public async Task Should_RecoverWholeAttributeGenerations_When_TheProcessStopsAtTheSwitch(string boundary)
    {
        TabletSnapshot old = WithAttributes(products: 100);
        await tablet.SyncAsync(old);
        string unsent = tablet.SaveWork().Fingerprint();
        TabletSnapshot next = WithAttributes(products: 100, value: "300 g");
        EncodedSnapshot encoded = EncodedSnapshot.From(next);
        string gzip = Path.Combine(tablet.Root, "attributes.gz");
        await File.WriteAllBytesAsync(gzip, encoded.Gzip);
        await tablet.KillAtAsync(boundary, "colm", "apply", gzip, encoded.GzipSha256, encoded.PlainSha256);
        var reopened = new SnapshotStore(tablet.Workspace());
        Assert.Equal(boundary == "after-activation" ? "300 g" : "200 g", reopened.ReadProduct(old.Products[0].Id)!.Attributes[0].Value);
        await tablet.SyncAsync(WithAttributes(products: 100, value: "400 g"));
        await tablet.SyncAsync(WithAttributes(products: 100, value: "500 g"));
        await tablet.Sync().CleanupAsync();
        Assert.Equal(2, reopened.Generations().Count);
        Assert.Equal(600L, Scalar("SELECT COUNT(*) FROM product_attributes"));
        Assert.Equal("500 g", reopened.ReadProduct(old.Products[0].Id)!.Attributes[0].Value);
        Assert.Equal(unsent, new UnsentStore(tablet.Workspace()).Fingerprint());
    }

    [Fact]
    public async Task Should_ValidateReferencesAfterAllFamilies_When_AttributeRowsPrecedeProducts()
    {
        TabletSnapshot snapshot = WithAttributes(products: 100);
        JsonObject original = JsonSerializer.SerializeToNode(snapshot, SnapshotContract.JsonOptions)!.AsObject();
        JsonObject reordered = new() { ["manifest"] = original["manifest"]!.DeepClone(), ["productAttributes"] = original["productAttributes"]!.DeepClone() };
        foreach (string family in new[] { "locations", "categories", "products", "basePrices" }) reordered[family] = original[family]!.DeepClone();
        tablet.Server.Serve(EncodedSnapshot.FromJson(JsonSerializer.SerializeToUtf8Bytes(reordered), "1.1"));
        Assert.Equal(SyncStatus.Completed, (await tablet.Sync().SyncAsync()).Status);
        Assert.Equal("200 g", new SnapshotStore(tablet.Workspace()).ReadProduct(snapshot.Products[0].Id)!.Attributes[0].Value);
    }

    public static TabletSnapshot WithAttributes(int products = 1, string value = "200 g")
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 3, products, version: "1.1");
        SnapshotProductAttribute[] rows = snapshot.Products.SelectMany(product => new[] {
            new SnapshotProductAttribute(product.Id, 0, "Weight", value, false),
            new SnapshotProductAttribute(product.Id, 1, "Shelf life", "24 months", false),
            new SnapshotProductAttribute(product.Id, 2, "Barcode", "5391234567890", false) }).ToArray();
        return snapshot with { ProductAttributes = rows, Manifest = snapshot.Manifest with { Counts = snapshot.Manifest.Counts with { ProductAttributes = rows.Length } } };
    }
    private void Execute(string sql)
    {
        using SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False");
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    private long Scalar(string sql)
    {
        using SqliteConnection connection = new($"Data Source={tablet.Workspace().SnapshotPath};Pooling=False");
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; return (long)command.ExecuteScalar()!;
    }
}
