using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

// WI-043 approved T1-T4: active-generation visibility and captured-work preservation, not historical erasure.
public sealed class RestrictedTabletTests : IDisposable
{
    private readonly TestTablet tablet = new();
    private readonly TabletSnapshot day = Snapshots.Build("colm", 1, 2);
    public void Dispose() => tablet.Dispose();
    private Guid Hidden => day.Products[0].Id;
    private Guid Visible => day.Products[1].Id;
    private UnsentStore Work() => new(tablet.Workspace());
    private OrderStore Orders() => new(tablet.Workspace(), tablet.Clock);
    private DateOnly Today => DateOnly.FromDateTime(tablet.Clock.GetUtcNow().UtcDateTime);

    private TabletSnapshot Filtered()
    {
        SnapshotProduct[] products = day.Products.Where(product => product.Id != Hidden).ToArray();
        SnapshotBasePrice[] prices = day.BasePrices.Where(price => price.ProductId != Hidden).ToArray();
        return day with
        {
            Products = products, BasePrices = prices,
            Manifest = day.Manifest with { Counts = day.Manifest.Counts with { Products = products.Length, BasePrices = prices.Length } }
        };
    }

    private async Task<Guid> CaptureAsync()
    {
        await tablet.SyncAsync(day);
        Guid id = Guid.NewGuid();
        Assert.True(Orders().AddLine(id, day.Locations[0].Id, Hidden, 6).Succeeded);
        Assert.True(Orders().AddLine(id, day.Locations[0].Id, Visible, 2).Succeeded);
        return id;
    }

    [Fact]
    public async Task Should_RemoveDeniedProductsFromActiveReads_When_AFilteredSnapshotReplacesTheDay()
    {
        await tablet.SyncAsync(day);
        await tablet.SyncAsync(Filtered());
        SnapshotStore snapshot = new(tablet.Workspace(), tablet.Clock);

        Assert.Equal(Visible, Assert.Single(snapshot.ReadProducts(0).Products).Id);
        Assert.Null(snapshot.ReadProduct(Hidden));
        Assert.Null(snapshot.ReadOrderableProduct(Hidden, Today));
        Assert.NotNull(snapshot.ReadProduct(Visible));
        Assert.Equal(0, snapshot.ReadProductIndex(Today).Search(day.Products[0].Code).Total);
        Assert.Equal(Visible, Assert.Single(snapshot.ReadProductIndex(Today).Search(day.Products[1].Code).Results).Id);
        // Policy A: the old permitted row still exists, so these assertions catch reads ignoring the active pointer.
        Assert.Equal(2, snapshot.Generations().Count(row => row.State == "complete"));
        using SqliteConnection connection = SqliteEngine.Open(tablet.Workspace().SnapshotPath);
        using SqliteCommand check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM products WHERE id=$id";
        check.Parameters.AddWithValue("$id", Hidden.ToString());
        Assert.Equal(1L, (long)check.ExecuteScalar()!);
    }

    [Fact]
    public async Task Should_KeepThePreviousDayAndCapturedWork_When_TheReplacementSnapshotFails()
    {
        await CaptureAsync();
        string captured = Work().Fingerprint();
        ActiveSnapshot before = tablet.Sync().Store.ReadActive()!;
        tablet.Server.Serve(EncodedSnapshot.From(Filtered()), gzipHash: new string('0', 64));

        SyncOutcome failed = await tablet.Sync().SyncAsync();

        Assert.Equal(SyncStatus.Failed, failed.Status);
        Assert.Equal(before.Generation, failed.Active!.Generation);
        Assert.Equal(captured, Work().Fingerprint());
        Assert.NotNull(new SnapshotStore(tablet.Workspace()).ReadProduct(Hidden));
        Assert.False(File.Exists(tablet.Workspace().TransferPath));
    }

    [Fact]
    public async Task Should_PreserveCapturedLineValues_When_PermissionIsRemovedAfterCapture()
    {
        Guid id = await CaptureAsync();
        string captured = Work().Find(id)!.Json;
        OrderLine original = Orders().Find(id)!.Lines.Single(line => line.ProductId == Hidden);

        await tablet.SyncAsync(Filtered());

        Assert.Equal(captured, Work().Find(id)!.Json);
        TabletOrder order = Orders().Find(id)!;
        OrderLine kept = order.Lines.Single(line => line.ProductId == Hidden);
        Assert.Equal(OrderState.InProgress, order.State);
        Assert.Equal((original.Id, original.ProductName, original.Quantity, original.Price.Amount, original.Snapshot),
            (kept.Id, kept.ProductName, kept.Quantity, kept.Price.Amount, kept.Snapshot));
        Assert.Empty(tablet.Server.Uploads);
    }

    [Fact]
    public async Task Should_RefuseAddingTheProductAgain_When_ItIsAbsentFromTheActiveSnapshot()
    {
        Guid id = await CaptureAsync();
        await tablet.SyncAsync(Filtered());
        string captured = Work().Fingerprint();
        Guid newOrder = Guid.NewGuid();

        Assert.Equal(OrderStore.NotDownloaded, Orders().AddLine(id, day.Locations[0].Id, Hidden, 1).Error);
        Assert.Equal(OrderStore.NotDownloaded, Orders().AddLine(newOrder, day.Locations[0].Id, Hidden, 1).Error);

        Assert.Equal(captured, Work().Fingerprint());
        Assert.Null(Work().Find(newOrder));
        Assert.Single(Work().Read());
    }

    [Fact]
    public async Task Should_MarkOnlyMissingLinesUnavailable_When_AStoredOrderIsReviewed()
    {
        Guid id = await CaptureAsync();
        Assert.Empty(Assert.Single(Work().ReadViews()).MissingProductIds);

        await tablet.SyncAsync(Filtered());

        UnsentView review = Assert.Single(Work().ReadViews());
        Assert.Equal(id, review.Item.Id);
        Assert.Equal(new[] { Hidden }, review.MissingProductIds.Order());
        Assert.True(review.CanMarkReady);
        Assert.Equal(2, review.Order!.Lines.Count);
    }
}
