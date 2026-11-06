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

/// <summary>WI-063 agreed scenarios 1 and 4–10 (Pricing US-003 S1, S3, S4; H-21): every Location of a customer prices against the
/// tiers the customer holds, through the price engine; removing a tier changes prices from then on only; with no tier, a Location
/// pays the base price or a break; and the API that holds tiers and samples their prices. The API's clock reads 1 October in
/// Dublin.</summary>
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

    private sealed record Shelf(Guid Spf30, Guid HandCream, Guid Sudocrem, Guid Aloe, Guid LipBalm);

    private async Task<Shelf> ShelfAsync()
    {
        Category suncare = await _app.CategoryAsync("Suncare");
        return new(await _app.ProductAsync("SUN-030", "SPF30 Sun Lotion 200ml", suncare.Id, price: 12.50m),
            await _app.ProductAsync("HAND-75", "Hand Cream 75ml", suncare.Id, price: 4.00m),
            await _app.ProductAsync("SUDO-125", "Sudocrem 125g", suncare.Id, price: 4.85m),
            await _app.ProductAsync("ALOE-200", "Aloe Gel 200ml", suncare.Id, price: 6.00m),
            await _app.ProductAsync("LIP-15", "Lip Balm SPF15", suncare.Id, price: 3.00m));
    }

    private HttpClient HeadOffice() => _app.ClientAs("orla", [BusinessRoles.HeadOfficeUser]);

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
    private async Task<(Guid TierB, Guid Deal)> TiersAsync(Guid customer, Shelf shelf, bool held = true)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        PriceTier tierB = PriceTier.CreatePercentage("Tier B", 8m);
        PriceTier deal = PriceTier.CreatePriceList("Suncare Deal 2027");
        deal.SetOverride(shelf.Spf30, TierTerm.Fixed(10.00m), Today);
        db.PriceTiers.AddRange(tierB, deal);
        if (held) db.CustomerPriceTiers.AddRange(new CustomerPriceTier(customer, tierB.Id), new CustomerPriceTier(customer, deal.Id));
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
        Guid orderId = await OrderAsync(locations[0], (shelf.Spf30, 10.00m, "Suncare Deal 2027"));

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

    [Fact]
    public async Task Should_SampleTheMostOrderedProducts_When_TheCustomerHasAcceptedOrders()
    {
        Shelf shelf = await ShelfAsync();
        (Guid customer, Guid[] locations) = await HickeysAsync(2);
        (Guid tierB, Guid deal) = await TiersAsync(customer, shelf, held: false);
        await OrderAsync(locations[0], Line(shelf.Spf30), Line(shelf.HandCream));
        await OrderAsync(locations[1], Line(shelf.Spf30), Line(shelf.Sudocrem));
        await OrderAsync(locations[1], Line(shelf.Spf30), Line(shelf.HandCream), Line(shelf.HandCream));
        await OrderAsync(locations[0], accepted: false, Line(shelf.Aloe), Line(shelf.Aloe)); // Pending: doesn't count.
        Guid murphys = await _app.CustomerAsync("Murphy's Pharmacy", await TownAsync());
        await OrderAsync(murphys, Line(shelf.LipBalm)); // Another customer's: doesn't count.
        using HttpClient client = HeadOffice();

        CustomerPriceSample plain = (await client.GetFromJsonAsync<CustomerPriceSample>($"/pricing/customers/{customer}/sample"))!;
        CustomerPriceSample ticked = (await client.GetFromJsonAsync<CustomerPriceSample>(
            $"/pricing/customers/{customer}/sample?tierId={tierB}&tierId={deal}"))!;

        // SPF30 is on 3 orders, Hand Cream on 2 (twice on one counts once), Sudocrem on 1.
        Assert.Equal([shelf.Spf30, shelf.HandCream, shelf.Sudocrem], plain.MostOrdered.Select(row => row.ProductId));
        Assert.Equal(new CustomerPriceSampleRow(shelf.Spf30, "SUN-030", "SPF30 Sun Lotion 200ml", "Each", 1m, 12.50m, 12.50m, "List price"),
            plain.MostOrdered[0]);
        Assert.Equal([(12.50m, 10.00m, "Suncare Deal 2027"), (4.00m, 3.68m, "Tier B"), (4.85m, 4.46m, "Tier B")],
            ticked.MostOrdered.Select(row => (row.BasePrice!.Value, row.CustomerPrice!.Value, row.From!)));
        Assert.Equal((0, 0), (ticked.Found.Count, ticked.FoundCount));
        // Nothing was saved: the sample only priced the ticked tiers.
        Assert.Empty((await client.GetFromJsonAsync<CustomerTiersDetails>($"/pricing/customers/{customer}/tiers"))!.HeldTierIds);
    }

    [Fact]
    public async Task Should_PriceASearchedProduct_When_HeadOfficeChecksAnother()
    {
        Shelf shelf = await ShelfAsync();
        (Guid customer, _) = await HickeysAsync(1);
        (Guid tierB, _) = await TiersAsync(customer, shelf, held: false);
        using HttpClient client = HeadOffice();

        CustomerPriceSample sample = (await client.GetFromJsonAsync<CustomerPriceSample>(
            $"/pricing/customers/{customer}/sample?tierId={tierB}&find=sudo"))!;

        Assert.Empty(sample.MostOrdered); // No accepted orders yet.
        Assert.Equal(1, sample.FoundCount);
        Assert.Equal(new CustomerPriceSampleRow(shelf.Sudocrem, "SUDO-125", "Sudocrem 125g", "Each", 1m, 4.85m, 4.46m, "Tier B"),
            Assert.Single(sample.Found));
    }

    [Fact]
    public async Task Should_HoldEachTierOnce_When_TheSameTierIsSentTwice()
    {
        Shelf shelf = await ShelfAsync();
        (Guid customer, _) = await HickeysAsync(1);
        (Guid tierB, Guid deal) = await TiersAsync(customer, shelf, held: false);
        using HttpClient client = HeadOffice();

        using HttpResponseMessage saved = await client.PutAsJsonAsync($"/pricing/customers/{customer}/tiers",
            new SetCustomerTiersRequest([tierB, tierB, deal]));

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        CustomerTiersDetails details = (await client.GetFromJsonAsync<CustomerTiersDetails>($"/pricing/customers/{customer}/tiers"))!;
        Assert.Equal(new[] { tierB, deal }.Order(), details.HeldTierIds.Order());
        Assert.Equal(["Suncare Deal 2027", "Tier B"], details.Tiers.Select(tier => tier.Name));
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().CustomerPriceTiers
            .CountAsync(link => link.CustomerId == customer));
    }

    [Fact]
    public async Task Should_RefuseTheTiers_When_TheCustomerOrATierIsUnknown()
    {
        Shelf shelf = await ShelfAsync();
        (Guid customer, _) = await HickeysAsync(1);
        (Guid tierB, _) = await TiersAsync(customer, shelf, held: false);
        using HttpClient client = HeadOffice();
        Guid nobody = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/pricing/customers/{nobody}/tiers")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/pricing/customers/{nobody}/sample")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/pricing/customers/{nobody}/tiers", new SetCustomerTiersRequest([tierB]))).StatusCode);
        using HttpResponseMessage unknownTier = await client.PutAsJsonAsync($"/pricing/customers/{customer}/tiers",
            new SetCustomerTiersRequest([tierB, Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.BadRequest, unknownTier.StatusCode);
        Assert.Equal([CustomerTierEndpoints.UnknownTierMessage],
            (await unknownTier.Content.ReadFromJsonAsync<FieldSales.Catalogue.Contracts.ProductValidationErrors>())!.Errors["TierIds"]);
        Assert.Empty((await client.GetFromJsonAsync<CustomerTiersDetails>($"/pricing/customers/{customer}/tiers"))!.HeldTierIds);
    }

    [Fact]
    public async Task Should_RefuseTierAssignment_When_TheCallerIsNotAHeadOfficeUser()
    {
        Shelf shelf = await ShelfAsync();
        (Guid customer, _) = await HickeysAsync(1);
        (Guid tierB, _) = await TiersAsync(customer, shelf, held: false);

        foreach (string role in new[] { BusinessRoles.FieldSalesperson, BusinessRoles.SalesManager })
        {
            using HttpClient other = _app.ClientAs("someone", [role]);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/pricing/customers/{customer}/tiers")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/pricing/customers/{customer}/sample")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await other.PutAsJsonAsync($"/pricing/customers/{customer}/tiers", new SetCustomerTiersRequest([tierB]))).StatusCode);
        }
    }

    private async Task<Guid> TownAsync()
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Towns.Select(town => town.Id).FirstAsync();
    }

    private static (Guid Product, decimal Price, string Label) Line(Guid product) => (product, 2.00m, OrderContract.BasePriceLabel);

    private Task<Guid> OrderAsync(Guid location, params (Guid Product, decimal Price, string Label)[] lines) =>
        OrderAsync(location, accepted: true, lines);

    // An order captured at the Location with these line prices, received the way the upload endpoint receives it, then accepted.
    private async Task<Guid> OrderAsync(Guid location, bool accepted, params (Guid Product, decimal Price, string Label)[] lines)
    {
        Guid id = Guid.NewGuid();
        OrderLine[] orderLines = [.. lines.Select(line => new OrderLine(Guid.NewGuid(), line.Product, "CODE", "Name", 1m, "Each",
            new OrderLinePrice(line.Price, PriceKinds.Tier, line.Label, [new OrderPriceCandidate(PriceKinds.Tier, line.Label, line.Price, true)]),
            Morning))];
        TabletOrder order = new(OrderContract.Schema, OrderContract.CurrentVersion, id, OrderState.ReadyToSend, location, location,
            "colm", Today, null, Morning, orderLines);
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceiveResult received = await scope.ServiceProvider.GetRequiredService<TabletWorkReceiver>().ReceiveOrderAsync(id, "colm",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder)), CancellationToken.None);
        Assert.Equal(ReceiveOutcome.Received, received.Outcome);
        if (accepted)
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.Where(row => row.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.Status, OrderLifecycleStatus.Accepted)));
        return id;
    }
}
