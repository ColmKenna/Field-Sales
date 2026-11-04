using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Directory.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TabletSyncClient = FieldSales.Tablet.Core.TabletSync;

namespace FieldSales.Api.Tests;

/// <summary>WI-049 (T-5.4.1, REP-US-007): the Suggested List a Stock Check starts from, built from snapshot 1.3's history.
/// Agreed scenarios 1, 3, 4 and 5 (server and end to end).</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "TabletSync")]
public sealed class SuggestedListTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private static readonly CallSnapshotRef CallMorning = new("1.3", new DateTimeOffset(2026, 9, 1, 6, 30, 0, TimeSpan.Zero));
    private static readonly OrderSnapshotRef OrderMorning = new("1.3", new DateTimeOffset(2026, 9, 1, 6, 30, 0, TimeSpan.Zero));
    private readonly TabletSnapshotApplication _app = fixture.Application;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fieldsales-suggested", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 22, 7, 42, 0, TimeSpan.Zero));
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

    [Fact]
    public async Task Should_Show14ProductsEachOnceLabelledLastVisitOrderedRecentlyOrBoth_When_MurphysLastCheckHad10CountedAnd2NotCheckedAndItsLast3AcceptedOrdersHad5Products()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid murphys = await _app.CustomerAsync("Murphy's Pharmacy", places.Arklow.Id);
        Guid doyles = await _app.CustomerAsync("Doyle's Shop", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category remedies = await _app.CategoryAsync("Remedies");
        Guid[] product = new Guid[16]; // product[1]..product[15]
        for (int number = 1; number <= 15; number++)
            product[number] = await _app.ProductAsync($"P-{number:D2}", $"Product {number:D2}", remedies.Id);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex morning = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));

        // The last visit: 10 counted (products 1-10) and 2 Not checked (11, 12), recorded on the tablet as T-06 saves it.
        CallStockEntry Entry(int number, decimal? count)
        {
            ProductSearchResult item = morning.Find(product[number])!;
            return new(item.Id, item.Code, item.Name, item.Unit, count);
        }
        CallStockEntry[] check = [.. Enumerable.Range(1, 10).Select(number => Entry(number, number)), Entry(11, null), Entry(12, null)];
        Assert.True(new CallStore(Workspace, _clock).Save(Guid.NewGuid(), murphys, new(CallChannel.InPerson, null, check)).Succeeded);
        // Three orders with 5 products between them, 3 of which (10, 11, 12) were on that Stock Check.
        OrderStore orders = new(Workspace, _clock);
        Guid[] orderIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        foreach ((Guid order, int[] lines) in orderIds.Zip<Guid, int[]>([[10, 13], [11, 14], [12, 13]]))
        {
            foreach (int number in lines) Assert.True(orders.AddLine(order, murphys, product[number], 2m).Succeeded);
            Assert.True(orders.MarkReadyToSend(order).Succeeded);
        }
        Assert.Equal(new UploadSummary(4, 4), (await SyncAsync()).Upload);
        await AcceptAsync(orderIds);

        Assert.True((await SyncAsync()).Completed);

        IReadOnlyList<SuggestedProduct> list = SuggestedFor(murphys);
        Assert.Equal(14, list.Count);
        Assert.Equal(14, list.Select(row => row.Product.Id).Distinct().Count());
        Assert.Equal(
            [.. Enumerable.Range(1, 9).Select(number => ($"Product {number:D2}", "Last visit")),
             ("Product 10", "Last visit · Ordered recently"), ("Product 11", "Last visit · Ordered recently"),
             ("Product 12", "Last visit · Ordered recently"), ("Product 13", "Ordered recently"), ("Product 14", "Ordered recently")],
            list.Select(row => (row.Product.Name, row.Label)).ToArray());
        Assert.Empty(SuggestedFor(doyles));
    }

    [Fact]
    public async Task Should_LeaveTheOrderOut_When_TheMostRecentOrderIsNotYetAccepted()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 3);
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 1), accepted: true, product[0]);
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 20), accepted: false, product[1]);

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        Assert.Equal([new SnapshotRecentOrderLine(murphys, product[0])], snapshot.RecentOrderLines);
    }

    [Fact]
    public async Task Should_UseOnlyTheLast3AcceptedOrders_When_TheShopHas4()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 4);
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 8, 1), accepted: true, product[0]); // The oldest: no longer recent.
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 8, 15), accepted: true, product[1]);
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 1), accepted: true, product[2]);
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 15), accepted: true, product[3], product[2]);

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        Assert.Equal(new HashSet<Guid>([product[1], product[2], product[3]]),
            snapshot.RecentOrderLines!.Select(line => line.ProductId).ToHashSet());
        Assert.Equal(3, snapshot.Manifest.Counts.RecentOrderLines); // Product 3 is on two orders and listed once.
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_NeverListTheProduct_When_ARestrictedProductIsOnAnOldOrderOrTheLastCheck(bool permitted)
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 1);
        Category medicines = await _app.CategoryAsync("Medicines");
        RestrictionGroup pharmacyOnly = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid restricted = await _app.ProductAsync("CPR-30", "Controlled Pain Relief 30s", medicines.Id, group: pharmacyOnly);
        if (permitted) await _app.GrantAsync("colm", pharmacyOnly.Id);
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 1), accepted: true, product[0], restricted);
        await ReceiveCallAsync(murphys, new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.FromHours(1)), CallChannel.InPerson,
            (product[0], 4m), (restricted, null));

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        // A rep who may see the product gets it like any other; one who may not never sees it in either family.
        Guid[] expected = permitted ? [product[0], restricted] : [product[0]];
        Assert.Equal(expected.Order(), snapshot.LastStockCheckLines!.Select(line => line.ProductId).Order());
        Assert.Equal(expected.Order(), snapshot.RecentOrderLines!.Select(line => line.ProductId).Order());
    }

    [Fact]
    public async Task Should_PutTheNotCheckedProductsOnTheNextVisitsList_When_TheCallSyncsAndTheDayDownloadsAgain()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 5);
        // An earlier visit counted product 3; it was ordered too, so it is "Ordered recently" whatever this visit does.
        await ReceiveCallAsync(murphys, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(1)), CallChannel.InPerson, (product[3], 6m));
        await ReceiveOrderAsync(murphys, new DateOnly(2026, 9, 1), accepted: true, product[4]);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex morning = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));
        CallStockEntry Entry(int index, decimal? count)
        {
            ProductSearchResult item = morning.Find(product[index])!;
            return new(item.Id, item.Code, item.Name, item.Unit, count);
        }
        // Today: product 0 counted, 1 and 2 left Not checked; product 3 was removed from the list, so it isn't saved.
        Assert.True(new CallStore(Workspace, _clock).Save(Guid.NewGuid(), murphys,
            new(CallChannel.InPerson, null, [Entry(0, 4m), Entry(1, null), Entry(2, null)])).Succeeded);

        SyncOutcome evening = await SyncAsync();

        Assert.Equal(new UploadSummary(1, 1), evening.Upload);
        Assert.Equal([("Product 00", "Last visit"), ("Product 01", "Last visit"), ("Product 02", "Last visit"), ("Product 04", "Ordered recently")],
            SuggestedFor(murphys).Select(row => (row.Product.Name, row.Label)).ToArray());
    }

    [Fact]
    public async Task Should_KeepTheProductOnTheList_When_ItIsNotCheckedAgain()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 4);
        await ReceiveCallAsync(murphys, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(1)), CallChannel.InPerson,
            (product[0], 4m), (product[1], null));
        Assert.Equal(new[] { product[0], product[1] }.Order(), (await _app.SyncAsync("colm")).LastStockCheckLines!.Select(line => line.ProductId).Order());

        await ReceiveCallAsync(murphys, new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(1)), CallChannel.InPerson,
            (product[1], null), (product[2], 7m));
        // A later Phone call doesn't replace the last Stock Check (Stock mentioned, T-5.3.2, isn't a count of the shelf).
        await ReceiveCallAsync(murphys, new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.FromHours(1)), CallChannel.Phone, (product[3], 0m));

        TabletSnapshot next = await _app.SyncAsync("colm");

        Assert.Equal(new[] { product[1], product[2] }.Order(), next.LastStockCheckLines!.Select(line => line.ProductId).Order());
    }

    private async Task<(Guid Murphys, Guid[] Products)> MurphysAsync(int products)
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid murphys = await _app.CustomerAsync("Murphy's Pharmacy", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category remedies = await _app.CategoryAsync("Remedies");
        Guid[] ids = new Guid[products];
        for (int index = 0; index < products; index++)
            ids[index] = await _app.ProductAsync($"P-{index:D2}", $"Product {index:D2}", remedies.Id);
        return (murphys, ids);
    }

    private IReadOnlyList<SuggestedProduct> SuggestedFor(Guid location)
    {
        SnapshotStore store = new(Workspace);
        ProductSearchIndex index = store.ReadProductIndex(new DateOnly(2026, 9, 22));
        return SuggestedList.Build(store.ReadStockHistory(index.Generation, location), index);
    }

    private async Task<SyncOutcome> SyncAsync()
    {
        using HttpClient api = _app.Anonymous();
        TabletSyncClient sync = new(Workspace, api, new FixedToken(_app.TokenFor("colm", [BusinessRoles.FieldSalesperson])),
            new PlentyOfSpace(), _clock);
        return await sync.SyncAsync();
    }

    // Until WI-067 accepts orders at the cut-off, tests stand in for it (WI-049 D1).
    private async Task AcceptAsync(params Guid[] orderIds)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        Assert.Equal(orderIds.Length, await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders
            .Where(order => orderIds.Contains(order.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(order => order.Status, OrderLifecycleStatus.Accepted)));
    }

    // Received through the same receiver the upload endpoint uses.
    private async Task ReceiveOrderAsync(Guid location, DateOnly takenOn, bool accepted, params Guid[] products)
    {
        Guid id = Guid.NewGuid();
        OrderLine[] lines = [.. products.Select(product => new OrderLine(Guid.NewGuid(), product, "CODE", "Name", 2m, "Each",
            new OrderLinePrice(2.50m, OrderContract.BasePriceKind, OrderContract.BasePriceLabel,
                [new OrderPriceCandidate(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, 2.50m, true)]), OrderMorning))];
        TabletOrder order = new(OrderContract.Schema, OrderContract.CurrentVersion, id, OrderState.ReadyToSend, location, location,
            "colm", takenOn, null, OrderMorning, lines);
        await ReceiveAsync(receiver => receiver.ReceiveOrderAsync(id, "colm",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder)), CancellationToken.None));
        if (accepted) await AcceptAsync(id);
    }

    private async Task ReceiveCallAsync(Guid location, DateTimeOffset savedAt, CallChannel channel, params (Guid Product, decimal? Count)[] entries)
    {
        Guid id = Guid.NewGuid();
        TabletCall call = new(CallContract.Schema, CallContract.CurrentVersion, id, location, "colm", DateOnly.FromDateTime(savedAt.DateTime),
            savedAt, channel, CallMorning, StockEntries: [.. entries.Select(entry => new CallStockEntry(entry.Product, "CODE", "Name", "Each", entry.Count))]);
        await ReceiveAsync(receiver => receiver.ReceiveCallAsync(id, "colm",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(call, CallJsonContext.Default.TabletCall)), CancellationToken.None));
    }

    private async Task ReceiveAsync(Func<TabletWorkReceiver, Task<ReceiveResult>> receive)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceiveResult result = await receive(scope.ServiceProvider.GetRequiredService<TabletWorkReceiver>());
        Assert.Equal(ReceiveOutcome.Received, result.Outcome);
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
