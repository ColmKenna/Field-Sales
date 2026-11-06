using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.Api.Ordering;
using FieldSales.Api.Pricing;
using FieldSales.Directory.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceKinds = FieldSales.Pricing.PriceKinds;
using ResolvedPrice = FieldSales.Pricing.ResolvedPrice;

namespace FieldSales.Api.Tests;

/// <summary>WI-063 agreed scenarios 1, 4 and 5 (Pricing US-003 S1, S3, S4): every Location of a customer prices against the tiers
/// the customer holds, through the price engine; removing a tier changes prices from then on only; with no tier, a Location pays the
/// base price or a break. The API's clock reads 1 October in Dublin.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class CustomerTierPricingTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 10, 1);
    private static readonly OrderSnapshotRef Morning = new("1.3", new DateTimeOffset(2026, 10, 1, 6, 30, 0, TimeSpan.Zero));
    private readonly TabletSnapshotApplication _app = fixture.Application;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        await _app.ResetAsync();
        _initialized = true;
    }

    public async Task DisposeAsync()
    {
        if (_initialized) await _app.ResetAsync();
    }

    private sealed record Shelf(Guid Spf30, Guid HandCream);

    private async Task<Shelf> ShelfAsync()
    {
        Category suncare = await _app.CategoryAsync("Suncare");
        return new(await _app.ProductAsync("SUN-030", "SPF30 Sun Lotion 200ml", suncare.Id, price: 12.50m),
            await _app.ProductAsync("HAND-75", "Hand Cream 75ml", suncare.Id, price: 4.00m));
    }

    // Hickey's Pharmacies with the given number of Locations; returns the customer and its Locations.
    private async Task<(Guid Customer, Guid[] Locations)> HickeysAsync(int locations)
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid first = await _app.CustomerAsync("Hickey's Arklow", places.Arklow.Id, customerName: "Hickey's Pharmacies");
        Guid customer;
        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
            customer = await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Locations
                .Where(location => location.Id == first).Select(location => location.CustomerId).SingleAsync();
        List<Guid> all = [first];
        using HttpClient office = _app.ClientAs("orla", [BusinessRoles.HeadOfficeUser]);
        for (int index = 2; index <= locations; index++)
        {
            using HttpResponseMessage added = await office.PostAsJsonAsync($"/directory/customers/{customer}/locations",
                new CreateLocationRequest($"Hickey's {index}", places.Gorey.Id));
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            all.Add((await added.Content.ReadFromJsonAsync<LocationSummary>())!.Id);
        }
        return (customer, [.. all]);
    }

    // Tier B (8% off) and the "Suncare Deal 2027" price list with SPF30 at €10.00, held by the customer.
    private async Task<(Guid TierB, Guid Deal)> TiersAsync(Guid customer, Shelf shelf)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        PriceTier tierB = PriceTier.CreatePercentage("Tier B", 8m);
        PriceTier deal = PriceTier.CreatePriceList("Suncare Deal 2027");
        deal.SetOverride(shelf.Spf30, TierTerm.Fixed(10.00m), Today);
        db.PriceTiers.AddRange(tierB, deal);
        db.CustomerPriceTiers.AddRange(new CustomerPriceTier(customer, tierB.Id), new CustomerPriceTier(customer, deal.Id));
        await db.SaveChangesAsync();
        return (tierB.Id, deal.Id);
    }

    private async Task<ResolvedPrice> ResolveAsync(Guid location, Guid product, decimal quantity = 1m)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<CustomerTierPricing>()
            .ResolveForLocationAsync(location, product, quantity, Today, CancellationToken.None))!;
    }

    private static (string Kind, string Label, decimal Amount) Winner(ResolvedPrice price) =>
        (price.Winner.Kind, price.Winner.Label, price.Winner.Amount);

    [Fact]
    public async Task Should_PriceEveryLocationAgainstBothTiers_When_HickeysHoldsTierBAndSuncareDeal2027()
    {
        Shelf shelf = await ShelfAsync();
        (Guid customer, Guid[] locations) = await HickeysAsync(13);
        await TiersAsync(customer, shelf);

        Assert.Equal(13, locations.Length);
        foreach (Guid location in locations)
        {
            ResolvedPrice spf30 = await ResolveAsync(location, shelf.Spf30);
            Assert.Equal((PriceKinds.Tier, "Suncare Deal 2027", 10.00m), Winner(spf30)); // Better than Tier B's €11.50.
            Assert.Contains(spf30.Candidates, candidate => (candidate.Label, candidate.Amount) == ("Tier B", 11.50m));
            Assert.Equal((PriceKinds.Tier, "Tier B", 3.68m), Winner(await ResolveAsync(location, shelf.HandCream)));
        }
    }

    [Fact]
    public async Task Should_PriceNewOrdersWithoutTheTierAndKeepCapturedPrices_When_SuncareDeal2027IsRemoved()
    {
        Shelf shelf = await ShelfAsync();
        (Guid customer, Guid[] locations) = await HickeysAsync(2);
        (_, Guid deal) = await TiersAsync(customer, shelf);
        Guid orderId = await AcceptedOrderAsync(locations[0], shelf.Spf30, 10.00m, "Suncare Deal 2027");

        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            Assert.Equal(1, await db.CustomerPriceTiers.Where(link => link.CustomerId == customer && link.PriceTierId == deal).ExecuteDeleteAsync());
        }

        Assert.Equal((PriceKinds.Tier, "Tier B", 11.50m), Winner(await ResolveAsync(locations[0], shelf.Spf30)));
        await using AsyncServiceScope stored = _app.Api.Services.CreateAsyncScope();
        string body = (await stored.ServiceProvider.GetRequiredService<OrderingDbContext>().ReceivedWork.AsNoTracking()
            .SingleAsync(work => work.Id == orderId)).Body;
        OrderLine line = Assert.Single(JsonSerializer.Deserialize(body, OrderJsonContext.Default.TabletOrder)!.Lines);
        Assert.Equal((10.00m, "Suncare Deal 2027"), (line.Price.Amount, line.Price.Label)); // Captured, so kept.
    }

    [Fact]
    public async Task Should_PriceAtBasePlusAnyBreak_When_TheCustomerHoldsNoTier()
    {
        Shelf shelf = await ShelfAsync();
        (_, Guid[] locations) = await HickeysAsync(1);
        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            Product spf30 = await db.Products.Include(row => row.QuantityBreaks).SingleAsync(row => row.Id == shelf.Spf30);
            spf30.SetQuantityBreaks([new QuantityBreakInput(10m, 11.00m)]);
            await db.SaveChangesAsync();
        }

        Assert.Equal((PriceKinds.List, "List price", 12.50m), Winner(await ResolveAsync(locations[0], shelf.Spf30)));
        Assert.Equal((PriceKinds.Break, "Break: 10+", 11.00m), Winner(await ResolveAsync(locations[0], shelf.Spf30, 12m)));
    }

    // An order captured at the Location with this line price, received the way the upload endpoint receives it, then accepted.
    private async Task<Guid> AcceptedOrderAsync(Guid location, Guid product, decimal price, string label)
    {
        Guid id = Guid.NewGuid();
        OrderLine line = new(Guid.NewGuid(), product, "SUN-030", "SPF30 Sun Lotion 200ml", 1m, "Each",
            new OrderLinePrice(price, PriceKinds.Tier, label, [new OrderPriceCandidate(PriceKinds.Tier, label, price, true)]), Morning);
        TabletOrder order = new(OrderContract.Schema, OrderContract.CurrentVersion, id, OrderState.ReadyToSend, location, location,
            "colm", Today, null, Morning, [line]);
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceiveResult received = await scope.ServiceProvider.GetRequiredService<TabletWorkReceiver>().ReceiveOrderAsync(id, "colm",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder)), CancellationToken.None);
        Assert.Equal(ReceiveOutcome.Received, received.Outcome);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.Where(row => row.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.Status, OrderLifecycleStatus.Accepted)));
        return id;
    }
}
