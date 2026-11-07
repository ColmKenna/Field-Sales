using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.Api.Ordering;
using FieldSales.Api.Pricing;
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

/// <summary>WI-064 agreed scenarios 1, 2 and 9: snapshot 1.6 carries what the tablet prices with — the tiers the customers of the
/// rep's Locations hold, their terms for included products, and quantity breaks — and the real tablet prices and captures a line
/// with them, offline, and uploads it as captured.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "TabletSync")]
public sealed class TabletCustomerPricingTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 10, 1);
    private readonly TabletSnapshotApplication _app = fixture.Application;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fieldsales-customer-prices", Guid.NewGuid().ToString("N"));
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

    private sealed record World(Guid Hickeys, Guid HickeysCustomer, Guid Murphys, Guid Spf30, Guid HandCream, Guid Restricted,
        Guid TierB, Guid Deal);

    // Hickey's (Arklow, served by colm) holds Tier B; Murphy's (Gorey, not colm's) holds the deal. Tier B has terms for SPF30 and a
    // product colm may not see; SPF30 and that product have breaks.
    private async Task<World> WorldAsync()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid hickeys = await _app.CustomerAsync("Hickey's Arklow", places.Arklow.Id, customerName: "Hickey's Pharmacies");
        Guid murphys = await _app.CustomerAsync("Murphy's Gorey", places.Gorey.Id, customerName: "Murphy's Pharmacy");
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category suncare = await _app.CategoryAsync("Suncare");
        RestrictionGroup prescription = await _app.RestrictionGroupAsync("Prescription only");
        Guid spf30 = await _app.ProductAsync("SUN-030", "SPF30 Sun Lotion 200ml", suncare.Id, price: 12.50m);
        Guid handCream = await _app.ProductAsync("HAND-75", "Hand Cream 75ml", suncare.Id, price: 4.00m);
        Guid restricted = await _app.ProductAsync("RX-BALM", "Pharmacy Rx Balm", suncare.Id, group: prescription, price: 9.00m);
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        DirectoryDbContext directory = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Guid Customer(Guid location) => directory.Locations.Where(row => row.Id == location).Select(row => row.CustomerId).Single();
        CatalogueDbContext catalogue = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        PriceTier tierB = PriceTier.CreatePercentage("Tier B", 8m);
        tierB.SetOverride(spf30, TierTerm.Fixed(10.50m), Today);
        tierB.SetOverride(restricted, TierTerm.Percent(20m), Today);
        PriceTier deal = PriceTier.CreatePriceList("Suncare Deal 2027");
        deal.SetOverride(handCream, TierTerm.Fixed(3.00m), Today);
        catalogue.PriceTiers.AddRange(tierB, deal);
        Guid hickeysCustomer = Customer(hickeys);
        catalogue.CustomerPriceTiers.AddRange(new CustomerPriceTier(hickeysCustomer, tierB.Id), new CustomerPriceTier(Customer(murphys), deal.Id));
        foreach ((Guid product, decimal from, decimal price) in new[] { (handCream, 10m, 3.50m), (restricted, 5m, 8.00m) })
        {
            Product item = await catalogue.Products.Include(row => row.QuantityBreaks).SingleAsync(row => row.Id == product);
            item.SetQuantityBreaks([new QuantityBreakInput(from, price)]);
        }
        await catalogue.SaveChangesAsync();
        return new(hickeys, hickeysCustomer, murphys, spf30, handCream, restricted, tierB.Id, deal.Id);
    }

    [Fact]
    public async Task Should_CarryTheCustomersTiersAndTheProductsBreaks_When_TheRepSyncs()
    {
        World world = await WorldAsync();

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        Assert.Equal("1.6", snapshot.Manifest.Version);
        Assert.Equal([new SnapshotPriceTier(world.TierB, "Tier B", SnapshotTierShapes.Percentage, 8m)], snapshot.PriceTiers!);
        Assert.Equal([new SnapshotCustomerTier(world.HickeysCustomer, world.TierB)], snapshot.CustomerTiers!);
        Assert.Equal([new SnapshotPriceTierTerm(world.TierB, world.Spf30, null, 10.50m)], snapshot.PriceTierTerms!);
        Assert.Equal([new SnapshotQuantityBreak(world.HandCream, 10m, 3.50m)], snapshot.QuantityBreaks!);
        Assert.Equal((1, 1, 1, 1), (snapshot.Manifest.Counts.PriceTiers, snapshot.Manifest.Counts.PriceTierTerms,
            snapshot.Manifest.Counts.CustomerTiers, snapshot.Manifest.Counts.QuantityBreaks));
    }

    [Fact]
    public async Task Should_LeaveOutTiersAndTerms_When_TheRepDoesNotServeTheCustomerOrTheProductIsRestricted()
    {
        World world = await WorldAsync();

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        Assert.DoesNotContain(snapshot.PriceTiers!, tier => tier.Id == world.Deal); // Only Murphy's holds it; colm doesn't serve Murphy's.
        Assert.DoesNotContain(snapshot.PriceTierTerms!, term => term.TierId == world.Deal);
        Assert.DoesNotContain(snapshot.PriceTierTerms!, term => term.ProductId == world.Restricted); // Not in colm's products.
        Assert.DoesNotContain(snapshot.QuantityBreaks!, entry => entry.ProductId == world.Restricted);
        Assert.DoesNotContain(snapshot.Locations, location => location.Id == world.Murphys);
    }

    [Fact]
    public async Task Should_PriceAndCaptureTheTierPriceOfflineAndUploadIt_When_TheRepSyncsAndOrders()
    {
        World world = await WorldAsync();
        Assert.Equal(SyncStatus.Completed, (await SyncAsync()).Status);
        SnapshotStore store = new(Workspace, _clock);

        // Offline from here: everything comes from the snapshot.
        LocationPrices prices = store.ReadLocationPrices(world.Hickeys, Today);
        ProductSearchIndex pad = prices.Apply(store.ReadProductIndex(Today));
        Assert.Equal((10.50m, 3.68m), (pad.Find(world.Spf30)!.Price, pad.Find(world.HandCream)!.Price));
        OrderStore orders = new(Workspace, _clock);
        Guid orderId = Guid.NewGuid();
        Assert.True(orders.AddLine(orderId, world.Hickeys, world.Spf30, 2m).Succeeded);
        Assert.True(orders.AddLine(orderId, world.Hickeys, world.HandCream, 12m).Succeeded);
        Assert.True(orders.MarkReadyToSend(orderId).Succeeded);

        SyncOutcome outcome = await SyncAsync();

        Assert.Equal((SyncStatus.Completed, new UploadSummary(1, 1)), (outcome.Status, outcome.Upload));
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceivedWork work = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().ReceivedWork.AsNoTracking()
            .SingleAsync(item => item.Id == orderId);
        TabletOrder received = JsonSerializer.Deserialize(work.Body, OrderJsonContext.Default.TabletOrder)!;
        Assert.Equal([(10.50m, "Tier", "Tier B"), (3.50m, "Break", "Break: 10+")],
            received.Lines.Select(line => (line.Price.Amount, line.Price.Kind, line.Price.Label)));
    }

    private RepWorkspace Workspace => new(_root, "colm");

    private async Task<SyncOutcome> SyncAsync()
    {
        using HttpClient api = _app.Anonymous();
        TabletSyncClient sync = new(Workspace, api, new FixedToken(_app.TokenFor("colm", [BusinessRoles.FieldSalesperson])), new PlentyOfSpace(), _clock);
        return await sync.SyncAsync();
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
