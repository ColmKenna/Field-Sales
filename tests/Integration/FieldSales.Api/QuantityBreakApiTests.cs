using System.Net;
using System.Net.Http.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Pricing;
using FieldSales.Quantities;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceResolver = FieldSales.Pricing.PriceResolver;

namespace FieldSales.Api.Tests;

/// <summary>WI-062 agreed scenarios 3 and 6–11: a product's quantity breaks through the API, with their rules and never-applies
/// warnings. The API's clock reads 1 October in Dublin, the business day for "today's" base price.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class QuantityBreakApiTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
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

    private async Task<Guid> LozengesAsync() =>
        await _app.ProductAsync("LOZ-36", "Throat Lozenges 36s", (await _app.CategoryAsync("Pharmacy")).Id, price: 2.20m);

    private async Task<Guid> TeaAsync() => await _app.ProductAsync("TEA-LOOSE", "Loose Herbal Tea", (await _app.CategoryAsync("Tea")).Id,
        UnitOfMeasure.Kilogram, step: 0.5m, minimum: 1.0m, price: 4.80m);

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid product, params (decimal? From, decimal? Price)[] breaks) =>
        client.PutAsJsonAsync($"/catalogue/products/{product}/quantity-breaks",
            new SetProductQuantityBreaksRequest([.. breaks.Select(entry => new QuantityBreakRequestItem(entry.From, entry.Price))]));

    private static async Task SavedAsync(HttpClient client, Guid product, params (decimal? From, decimal? Price)[] breaks)
    {
        using HttpResponseMessage response = await PutAsync(client, product, breaks);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FieldSales.Catalogue.Contracts.ProductValidationErrors>())!.Errors;
    }

    private static async Task<IReadOnlyList<ProductQuantityBreakItem>> BreaksAsync(HttpClient client, Guid product) =>
        (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{product}"))!.QuantityBreaks!;

    [Fact]
    public async Task Should_SaveAndWarn_When_ABreakFrom30At230IsAboveTheFrom10At200()
    {
        Guid lozenges = await LozengesAsync();
        using HttpClient client = HeadOffice();

        await SavedAsync(client, lozenges, (30m, 2.30m), (10m, 2.00m));

        // €2.30 is above the €2.00 break below it, and above the €2.20 base price as well.
        Assert.Equal([new ProductQuantityBreakItem(10m, 2.00m, false, false), new ProductQuantityBreakItem(30m, 2.30m, true, true)],
            await BreaksAsync(client, lozenges));
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Product product = await db.Products.AsNoTracking().Include(row => row.BasePrices).Include(row => row.QuantityBreaks)
            .SingleAsync(row => row.Id == lozenges);
        var thirty = PriceResolver.Resolve(product.ToPricedProduct(), [], 30m, Today)!.Winner;
        Assert.Equal(("Break: 10+", 2.00m), (thirty.Label, thirty.Amount)); // The rising break never applies.
    }

    [Fact]
    public async Task Should_RefuseAThreshold_When_ItIsNotAQuantityAboveTheSmallestOrder()
    {
        Guid lozenges = await LozengesAsync();
        Guid tea = await TeaAsync();
        using HttpClient client = HeadOffice();

        foreach ((Guid product, decimal from, string message) in new (Guid, decimal, string)[]
                 {
                     (lozenges, 10.5m, "Enter a whole number."),
                     (lozenges, 1m, "A break starts above the smallest quantity you can order (1)."),
                     (lozenges, 0m, "A break starts above the smallest quantity you can order (1)."),
                     (tea, 12.3m, "Enter multiples of 0.5 kg"),
                     (tea, 1m, "A break starts above the smallest quantity you can order (1 kg)."),
                     (tea, 0.5m, "A break starts above the smallest quantity you can order (1 kg).")
                 })
            Assert.Equal([message], (await ErrorsAsync(await PutAsync(client, product, (from, 2.00m))))["Breaks[0].FromQuantity"]);

        Assert.Equal([QuantityBreakRules.QuantityMessage],
            (await ErrorsAsync(await PutAsync(client, lozenges, (null, 2.00m))))["Breaks[0].FromQuantity"]);
        Assert.Empty(await BreaksAsync(client, lozenges));
        await SavedAsync(client, tea, (1.5m, 4.50m), (12.5m, 4.00m));
        Assert.Equal([1.5m, 12.5m], (await BreaksAsync(client, tea)).Select(entry => entry.FromQuantity));
    }

    [Fact]
    public async Task Should_RefuseTheBreaks_When_TwoHaveTheSameQuantity()
    {
        Guid lozenges = await LozengesAsync();
        using HttpClient client = HeadOffice();

        Dictionary<string, string[]> errors = await ErrorsAsync(await PutAsync(client, lozenges, (10m, 2.00m), (10m, 1.90m)));

        Assert.Equal(["Each break needs its own quantity."], errors["Breaks[1].FromQuantity"]);
        Assert.False(errors.ContainsKey("Breaks[0].FromQuantity"));
        Assert.Empty(await BreaksAsync(client, lozenges));
    }

    [Fact]
    public async Task Should_RefuseAPrice_When_ItIsOutsideTheLimits()
    {
        Guid lozenges = await LozengesAsync();
        using HttpClient client = HeadOffice();

        foreach (decimal? refused in new decimal?[] { 0m, -1m, 10.505m, null })
            Assert.Equal([QuantityBreakRules.PriceMessage], (await ErrorsAsync(await PutAsync(client, lozenges, (10m, refused))))["Breaks[0].Price"]);

        Assert.Empty(await BreaksAsync(client, lozenges));
        await SavedAsync(client, lozenges, (10m, 0.01m));
    }

    [Fact]
    public async Task Should_SaveAndWarn_When_ABreakIsAboveTheBasePrice()
    {
        Guid lozenges = await LozengesAsync();
        using HttpClient client = HeadOffice();

        await SavedAsync(client, lozenges, (10m, 2.50m), (20m, 2.20m));

        Assert.Equal([new ProductQuantityBreakItem(10m, 2.50m, false, true), new ProductQuantityBreakItem(20m, 2.20m, false, false)],
            await BreaksAsync(client, lozenges));
    }

    [Fact]
    public async Task Should_RefuseAUnitChange_When_ABreakWouldNoLongerFit()
    {
        Guid tea = await TeaAsync();
        using HttpClient client = HeadOffice();
        await SavedAsync(client, tea, (12.5m, 4.00m));

        foreach (object unit in new object[] { new { Unit = "kg", QuantityStep = 1m, MinimumQuantity = 1m }, new { Unit = "Each" } })
        {
            using HttpResponseMessage refused = await client.PutAsJsonAsync($"/catalogue/products/{tea}/unit", unit);
            Assert.Equal(["Change or remove the quantity breaks first: from 12.5 kg doesn't fit."], (await ErrorsAsync(refused))["Unit"]);
        }
        ProductDetails details = (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{tea}"))!;
        Assert.Equal(("kg", (decimal?)0.5m), (details.Product.Unit, details.Product.QuantityStep));

        using HttpResponseMessage finer = await client.PutAsJsonAsync($"/catalogue/products/{tea}/unit",
            new { Unit = "kg", QuantityStep = 0.25m, MinimumQuantity = 1m });
        Assert.Equal(HttpStatusCode.OK, finer.StatusCode); // 12.5 kg is still a multiple of 0.25 kg.
    }

    [Fact]
    public async Task Should_RefuseQuantityBreaks_When_TheCallerIsNotAHeadOfficeUser()
    {
        Guid lozenges = await LozengesAsync();

        foreach (string role in new[] { BusinessRoles.FieldSalesperson, BusinessRoles.SalesManager })
        {
            using HttpClient other = _app.ClientAs("someone", [role]);
            Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(other, lozenges, (10m, 2.00m))).StatusCode);
        }
        using HttpClient headOffice = HeadOffice();
        Assert.Empty(await BreaksAsync(headOffice, lozenges));
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(headOffice, Guid.NewGuid(), (10m, 2.00m))).StatusCode);
    }
}
