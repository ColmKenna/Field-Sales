using System.Security.Cryptography;
using System.Text;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Ordering;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TabletSyncClient = FieldSales.Tablet.Core.TabletSync;

namespace FieldSales.Api.Tests;

// WI-039 E1–E2: the real tablet store syncs against the real API, uploads first and then downloads.
[Collection(SqlServerCollection.Name)]
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
        Guid callId = Guid.NewGuid();
        new UnsentStore(Workspace).Save(new(callId, UnsentKind.Call, _clock.GetUtcNow(),
            $$"""{"id":"{{callId}}","locationId":"{{quinns}}","channel":"Phone","note":"Café — rang back"}"""));

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

    private sealed class FixedToken(string token) : IAccessTokenSource
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult<string?>(token);
    }

    private sealed class PlentyOfSpace : IStorageSpace
    {
        public long AvailableBytes(string directory) => long.MaxValue;
    }
}
