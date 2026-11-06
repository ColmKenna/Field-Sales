using System.Net;
using System.Net.Http.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Pricing;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceResolver = FieldSales.Pricing.PriceResolver;

namespace FieldSales.Api.Tests;

/// <summary>WI-060 agreed scenarios 4–9: head office's percentage tiers through the API. The API's clock reads 1 October in
/// Dublin, the business day for "today's" prices.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class PriceTierApiTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private const string Path = "/pricing/tiers";
    private static readonly DateOnly Today = new(2026, 10, 1);
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

    private HttpClient HeadOffice() => _app.ClientAs("orla", [BusinessRoles.HeadOfficeUser]);

    private async Task<(Guid Spf30, Guid HandCream)> ShelfAsync()
    {
        Category suncare = await _app.CategoryAsync("Suncare");
        return (await _app.ProductAsync("SUN-030", "SPF30 Sun Lotion 200ml", suncare.Id, price: 12.50m),
            await _app.ProductAsync("HAND-75", "Hand Cream 75ml", suncare.Id, price: 4.00m));
    }

    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, string? name, decimal? percentOff) =>
        await client.PostAsJsonAsync(Path, new SavePriceTierRequest(name, percentOff));

    private static async Task<Guid> CreatedAsync(HttpClient client, string name, decimal percentOff)
    {
        using HttpResponseMessage response = await CreateAsync(client, name, percentOff);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PriceTierListItem>())!.Id;
    }

    private static Task<HttpResponseMessage> OverrideAsync(HttpClient client, Guid tier, Guid product, decimal? percentOff, decimal? fixedPrice) =>
        client.PutAsJsonAsync($"{Path}/{tier}/overrides/{product}", new SetPriceTierOverrideRequest(percentOff, fixedPrice));

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<FieldSales.Catalogue.Contracts.ProductValidationErrors>())!.Errors;

    // The price a customer holding only this tier pays for one of the product, from the stored tier through the engine.
    private async Task<decimal> PriceAsync(Guid tierId, Guid productId)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        PriceTier tier = await db.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Id == tierId);
        Product product = await db.Products.AsNoTracking().Include(row => row.BasePrices).SingleAsync(row => row.Id == productId);
        return PriceResolver.Resolve(product.ToPricedProduct(), [tier.ToEngine()], 1m, Today)!.Winner.Amount;
    }

    [Fact]
    public async Task Should_RefuseTheTerm_When_APercentageOrPriceIsOutsideTheLimits()
    {
        (Guid spf30, _) = await ShelfAsync();
        using HttpClient client = HeadOffice();

        foreach (decimal refused in new[] { 0m, 100m, -5m, 8.125m })
        {
            using HttpResponseMessage response = await CreateAsync(client, $"Tier {refused}", refused);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal([TierTerms.PercentMessage], (await ErrorsAsync(response))["PercentOff"]);
        }
        foreach (decimal accepted in new[] { 0.01m, 99.99m, 12.5m }) await CreatedAsync(client, $"Tier {accepted}", accepted);
        Assert.Equal([0.01m, 12.5m, 99.99m],
            (await client.GetFromJsonAsync<PriceTierListItem[]>(Path))!.Select(tier => tier.PercentOff).Order());

        Guid tier = await CreatedAsync(client, "Tier B", 8m);
        foreach ((decimal? percentOff, decimal? fixedPrice, string field, string message) in new (decimal?, decimal?, string, string)[]
                 {
                     (null, 0.00m, "FixedPrice", TierTerms.FixedPriceMessage), (null, 10.505m, "FixedPrice", TierTerms.FixedPriceMessage),
                     (100m, null, "PercentOff", TierTerms.PercentMessage), (15m, 10.50m, "PercentOff", "Enter either a percentage off or a fixed price."),
                     (null, null, "PercentOff", "Enter either a percentage off or a fixed price.")
                 })
        {
            using HttpResponseMessage response = await OverrideAsync(client, tier, spf30, percentOff, fixedPrice);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal([message], (await ErrorsAsync(response))[field]);
        }
        Assert.Empty((await client.GetFromJsonAsync<PriceTierDetails>($"{Path}/{tier}"))!.Overrides);
    }

    [Fact]
    public async Task Should_RefuseTheName_When_AnotherTierHasItIgnoringCase()
    {
        using HttpClient client = HeadOffice();
        await CreatedAsync(client, "Tier B", 8m);
        Guid other = await CreatedAsync(client, "Tier C", 5m);

        using HttpResponseMessage again = await CreateAsync(client, "  tier b ", 10m);
        using HttpResponseMessage renamed = await client.PutAsJsonAsync($"{Path}/{other}", new SavePriceTierRequest("TIER B", 5m));
        using HttpResponseMessage blank = await CreateAsync(client, "   ", 10m);

        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.Conflict), (again.StatusCode, renamed.StatusCode));
        Assert.Equal(new ProductSaveError("Name", "A tier called Tier B already exists."), await again.Content.ReadFromJsonAsync<ProductSaveError>());
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal([PriceTier.NameMessage], (await ErrorsAsync(blank))["Name"]);
        Assert.Equal(["Tier B", "Tier C"], (await client.GetFromJsonAsync<PriceTierListItem[]>(Path))!.Select(tier => tier.Name));
    }

    [Fact]
    public async Task Should_KeepOneOverridePerProduct_When_AnOverrideIsSetAgain()
    {
        (Guid spf30, Guid handCream) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid tier = await CreatedAsync(client, "Tier B", 8m);

        Assert.Equal(HttpStatusCode.NoContent, (await OverrideAsync(client, tier, spf30, null, 10.50m)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await OverrideAsync(client, tier, spf30, 15m, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await OverrideAsync(client, tier, handCream, null, 3.50m)).StatusCode);

        PriceTierDetails details = (await client.GetFromJsonAsync<PriceTierDetails>($"{Path}/{tier}"))!;
        Assert.Equal(("Tier B", 8m), (details.Name, details.PercentOff));
        // By product name; today's list price and what the tier gives today are worked out on reading (D6).
        Assert.Equal([new PriceTierOverrideItem(handCream, "HAND-75", "Hand Cream 75ml", "Each", null, 3.50m, 4.00m, 3.50m, Today),
                new PriceTierOverrideItem(spf30, "SUN-030", "SPF30 Sun Lotion 200ml", "Each", 15m, null, 12.50m, 10.63m, Today)],
            details.Overrides);
        Assert.Equal(2, (await client.GetFromJsonAsync<PriceTierListItem[]>(Path))!.Single().OverrideCount);
        Assert.Equal(10.63m, await PriceAsync(tier, spf30));
    }

    [Fact]
    public async Task Should_FallBackToTheDefault_When_AnOverrideIsRemoved()
    {
        (Guid spf30, _) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid tier = await CreatedAsync(client, "Tier B", 8m);
        await OverrideAsync(client, tier, spf30, null, 10.50m);
        Assert.Equal(10.50m, await PriceAsync(tier, spf30));

        using HttpResponseMessage removed = await client.DeleteAsync($"{Path}/{tier}/overrides/{spf30}");
        using HttpResponseMessage again = await client.DeleteAsync($"{Path}/{tier}/overrides/{spf30}");

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NotFound), (removed.StatusCode, again.StatusCode));
        Assert.Empty((await client.GetFromJsonAsync<PriceTierDetails>($"{Path}/{tier}"))!.Overrides);
        Assert.Equal(11.50m, await PriceAsync(tier, spf30));
    }

    [Fact]
    public async Task Should_PriceAtTheNewPercentage_When_TheTierIsRenamedAndChangedTo10Percent()
    {
        (Guid spf30, Guid handCream) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid tier = await CreatedAsync(client, "Tier B", 8m);
        await OverrideAsync(client, tier, handCream, 15m, null);

        using HttpResponseMessage changed = await client.PutAsJsonAsync($"{Path}/{tier}", new SavePriceTierRequest(" Tier B Wholesale ", 10m));

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(new PriceTierListItem(tier, "Tier B Wholesale", 10m, 1), (await client.GetFromJsonAsync<PriceTierListItem[]>(Path))!.Single());
        Assert.Equal((11.25m, 3.40m), (await PriceAsync(tier, spf30), await PriceAsync(tier, handCream))); // The override is kept.
    }

    [Fact]
    public async Task Should_RefuseTiers_When_TheCallerIsNotAHeadOfficeUser()
    {
        (Guid spf30, _) = await ShelfAsync();
        using HttpClient headOffice = HeadOffice();
        Guid tier = await CreatedAsync(headOffice, "Tier B", 8m);

        foreach (string role in new[] { BusinessRoles.FieldSalesperson, BusinessRoles.SalesManager })
        {
            using HttpClient other = _app.ClientAs("someone", [role]);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(Path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(other, "Tier X", 5m)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await OverrideAsync(other, tier, spf30, 50m, null)).StatusCode);
        }
        using HttpClient anonymous = _app.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await headOffice.GetAsync($"{Path}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await headOffice.PutAsJsonAsync($"{Path}/{Guid.NewGuid()}", new SavePriceTierRequest("Tier Z", 5m))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await OverrideAsync(headOffice, tier, Guid.NewGuid(), 15m, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await OverrideAsync(headOffice, Guid.NewGuid(), spf30, 15m, null)).StatusCode);
        Assert.Equal(new PriceTierListItem(tier, "Tier B", 8m, 0), (await headOffice.GetFromJsonAsync<PriceTierListItem[]>(Path))!.Single());
    }
}
