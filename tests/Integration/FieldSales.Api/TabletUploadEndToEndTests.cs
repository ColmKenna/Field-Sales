using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Directory.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TabletSyncClient = FieldSales.Tablet.Core.TabletSync;

namespace FieldSales.Api.Tests;

// WI-039 E1–E2: the real tablet store syncs against the real API, uploads first and then downloads.
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "TabletSync")]
public sealed class TabletUploadEndToEndTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private readonly TabletSnapshotApplication _app = fixture.Application;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fieldsales-upload-e2e", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 1, 7, 42, 0, TimeSpan.Zero));
    private bool _initialized;

    public async Task InitializeAsync()
    {
        await _app.ResetAsync();
        _initialized = true;
    }

    public async Task DisposeAsync()
    {
        if (_initialized) await _app.ResetAsync();
        try { System.IO.Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private RepWorkspace Workspace => new(_root, "colm");

    private async Task<SyncOutcome> SyncAsync()
    {
        using HttpClient api = _app.Anonymous();
        TabletSyncClient sync = new(Workspace, api, new FixedToken(_app.TokenFor("colm", [BusinessRoles.FieldSalesperson])), new PlentyOfSpace(), _clock);
        return await sync.SyncAsync();
    }

    [Fact]
    public async Task Should_AcceptTheOrderAsPendingAndMarkItSent_When_ItsProductBecameUnavailableAfterTheMorningSync()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid hickeys = await _app.CustomerAsync("Hickey's Pharmacy", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category remedies = await _app.CategoryAsync("Remedies");
        Guid syrup = await _app.ProductAsync("ACS-100", "Autumn Cough Syrup 100ml", remedies.Id, price: 6.40m);
        Assert.Equal(SyncStatus.Completed, (await SyncAsync()).Status); // 07:42

        // 10:00: the product stops being available (stand-in for an archived range, see A5).
        RestrictionGroup seasonal = await _app.RestrictionGroupAsync("Seasonal remedies");
        await _app.RestrictAsync(syrup, seasonal.Id);
        await _app.ArchiveGroupAsync(seasonal.Id);

        // 14:00: the rep adds it from the morning's data and marks the order Ready to Send.
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero));
        OrderStore orders = new(Workspace, _clock);
        Guid orderId = Guid.NewGuid();
        Assert.True(orders.AddLine(orderId, hickeys, syrup, 12m).Succeeded);
        Assert.True(orders.MarkReadyToSend(orderId).Succeeded);

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 17, 0, 0, TimeSpan.Zero));
        SyncOutcome evening = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(1, 1)), (evening.Status, evening.Upload));
        UnsentStore unsent = new(Workspace);
        Assert.Empty(unsent.Read());
        Assert.Equal(orderId, Assert.Single(unsent.ReadSent()).Item.Id);
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceivedOrder pending = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.AsNoTracking().SingleAsync();
        Assert.Equal((orderId, OrderLifecycleStatus.Pending, hickeys, "colm"), (pending.Id, pending.Status, pending.LocationId, pending.CapturingRep));
        // The new snapshot no longer offers the product, so new orders follow it; the sent order was judged as captured.
        Assert.Equal(OrderStore.NotDownloaded, orders.AddLine(Guid.NewGuid(), hickeys, syrup, 1m).Error);
    }

    [Fact]
    public async Task Should_SendTheStoredOrderBodyByteForByte_When_UploadingToTheRealApi()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid quinns = await _app.CustomerAsync("Quinn's Centra", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category remedies = await _app.CategoryAsync("Remedies");
        Guid vitamin = await _app.ProductAsync("VD-1000", "Vitamin D 1000IU 90s", remedies.Id, price: 8.95m);
        await SyncAsync();
        OrderStore orders = new(Workspace, _clock);
        Guid orderId = Guid.NewGuid();
        Assert.True(orders.AddLine(orderId, quinns, vitamin, 12m).Succeeded);
        Assert.True(orders.MarkReadyToSend(orderId).Succeeded);
        // A Call recorded the way T-06 records it (WI-047), so the server checks the real body.
        Assert.True(new CallStore(Workspace, _clock).Save(Guid.NewGuid(), quinns, new(CallChannel.Phone, "Café — rang back")).Succeeded);

        SyncOutcome first = await SyncAsync();
        SyncOutcome second = await SyncAsync();

        Assert.Equal((new UploadSummary(2, 2), UploadSummary.None), (first.Upload, second.Upload));
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceivedWork[] received = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().ReceivedWork.AsNoTracking().ToArrayAsync();
        foreach (SentItem sent in new UnsentStore(Workspace).ReadSent())
        {
            ReceivedWork work = Assert.Single(received, item => item.Id == sent.Item.Id);
            Assert.Equal(sent.Item.Json, work.Body);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sent.Item.Json))), work.ContentSha256);
            Assert.Equal(0, work.ResentWithChangesCount);
        }
        Assert.Equal(2, received.Length);
    }

    // WI-043 T6: both removal and archival reach the same captured-work exception, through the real API.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_SendCapturedWork_When_AnInProgressOrderIsReviewedAndMarkedReadyAfterRemoval(bool archiveGroup)
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid quinns = await _app.CustomerAsync("Quinn's Centra", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category category = await _app.CategoryAsync("Medicines");
        RestrictionGroup group = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid controlled = await _app.ProductAsync("CPR-30", "Controlled Pain Relief 30s", category.Id, group: group, price: 8.95m);
        await _app.GrantAsync("colm", group.Id);
        Assert.True((await SyncAsync()).Completed);
        OrderStore orders = new(Workspace, _clock);
        UnsentStore work = new(Workspace);
        Guid id = Guid.NewGuid();
        Assert.True(orders.AddLine(id, quinns, controlled, 6).Succeeded);
        string captured = work.Find(id)!.Json;
        if (archiveGroup) await _app.ArchiveGroupAsync(group.Id);
        else
        {
            await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().RestrictionPermissions
                .Where(grant => grant.RepSubject == "colm" && grant.RestrictionGroupId == group.Id).ExecuteDeleteAsync());
        }

        SyncOutcome filtered = await SyncAsync();

        Assert.True(filtered.Completed);
        Assert.Equal(UploadSummary.None, filtered.Upload); // In Progress is deliberately not uploaded.
        Assert.Equal(captured, work.Find(id)!.Json);
        Assert.Null(new SnapshotStore(Workspace).ReadProduct(controlled));
        UnsentView review = Assert.Single(work.ReadViews());
        Assert.Equal(new[] { controlled }, review.MissingProductIds);
        Assert.True(review.CanMarkReady);
        Assert.Equal(("Controlled Pain Relief 30s", 6m, 8.95m),
            (review.Order!.Lines[0].ProductName, review.Order.Lines[0].Quantity, review.Order.Lines[0].Price.Amount));
        Assert.Null(review.Item.Rejection);
        // The reviewed action uses the same existing order operation; component/session tests cover its app boundary.
        Assert.True(orders.MarkReadyToSend(id).Succeeded);
        string ready = work.Find(id)!.Json;

        SyncOutcome sent = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(1, 1)), (sent.Status, sent.Upload));
        Assert.Empty(work.Read());
        Assert.Equal(ready, Assert.Single(work.ReadSent()).Item.Json);
        await using AsyncServiceScope verify = _app.Api.Services.CreateAsyncScope();
        OrderingDbContext received = verify.ServiceProvider.GetRequiredService<OrderingDbContext>();
        ReceivedOrder row = await received.Orders.AsNoTracking().SingleAsync();
        Assert.Equal((id, OrderLifecycleStatus.Pending), (row.Id, row.Status));
        Assert.Equal(ready, (await received.ReceivedWork.AsNoTracking().SingleAsync()).Body);
    }

    // WI-059 agreed scenario 7 (S2, D5): a line keeps the price it was captured at; the server stores it as received, never re-priced.
    [Fact]
    public async Task Should_KeepThe1250LineOnTheServer_When_AnOrderCapturedOn28OctSyncsOn2Nov()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid byrnes = await _app.CustomerAsync("Byrne's Chemist", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category vitamins = await _app.CategoryAsync("Vitamins");
        Guid vitaminD = await _app.ProductAsync("VITD-1000-90", "Vitamin D 1000IU 90s", vitamins.Id, price: 12.50m);
        await _app.AddPriceAsync(vitaminD, 13.20m, new(2026, 11, 1));
        DateTimeOffset capturedAt = new(2026, 10, 28, 11, 0, 0, TimeSpan.Zero);
        _app.Clock.UtcNow = capturedAt.AddHours(-4);
        _clock.SetUtcNow(capturedAt.AddHours(-4));
        Assert.Equal(SyncStatus.Completed, (await SyncAsync()).Status); // The morning of 28 Oct.

        _clock.SetUtcNow(capturedAt);
        OrderStore orders = new(Workspace, _clock);
        Guid orderId = Guid.NewGuid();
        Assert.True(orders.AddLine(orderId, byrnes, vitaminD, 12m).Succeeded);
        Assert.True(orders.MarkReadyToSend(orderId).Succeeded);

        // Next Sync on 2 Nov, when €13.20 is the price in effect on the server too.
        DateTimeOffset synced = new(2026, 11, 2, 17, 0, 0, TimeSpan.Zero);
        _app.Clock.UtcNow = synced;
        _clock.SetUtcNow(synced);
        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(1, 1)), (outcome.Status, outcome.Upload));
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        OrderingDbContext ordering = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        ReceivedWork work = await ordering.ReceivedWork.AsNoTracking().SingleAsync(item => item.Id == orderId);
        TabletOrder received = JsonSerializer.Deserialize(work.Body, OrderJsonContext.Default.TabletOrder)!;
        Assert.Equal((new DateOnly(2026, 10, 28), 12m, 12.50m),
            (received.TakenOn, Assert.Single(received.Lines).Quantity, received.Lines[0].Price.Amount));
        Assert.Equal(new DateOnly(2026, 10, 28), (await ordering.Orders.AsNoTracking().SingleAsync()).TakenOn);
        // The server has €13.20 in effect: the snapshot downloaded at the same Sync prices a new line at it.
        Assert.Equal(13.20m, Assert.Single(orders.AddLine(Guid.NewGuid(), byrnes, vitaminD, 1m).Order!.Lines).Price.Amount);
    }

    private sealed class FixedToken(string token) : IAccessTokenSource
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult<string?>(token);
    }

    private sealed class PlentyOfSpace : IStorageSpace
    {
        public long AvailableBytes(string directory) => long.MaxValue;
    }
}
