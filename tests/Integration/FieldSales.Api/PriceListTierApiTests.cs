using System.Net;
using System.Net.Http.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Pricing;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceKinds = FieldSales.Pricing.PriceKinds;
using PriceResolver = FieldSales.Pricing.PriceResolver;
using ResolvedPrice = FieldSales.Pricing.ResolvedPrice;

namespace FieldSales.Api.Tests;

/// <summary>WI-061 agreed scenarios 1–7, 9 and 10 (Pricing US-001 S3–S5): price-list tiers, the above-list flag and changing a
/// tier's shape, through storage, the engine and the API. The API's clock reads 1 October in Dublin, the business day for "today".</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class PriceListTierApiTests(SqlServerFixture sql, TabletSnapshotFixture fixture)
    : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
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

    private static async Task<Guid> CreatedAsync(HttpClient client, string name, decimal? percentOff, string? shape)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(Path, new SavePriceTierRequest(name, percentOff, shape));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PriceTierListItem>())!.Id;
    }

    private static Task<HttpResponseMessage> OverrideAsync(HttpClient client, Guid tier, Guid product, decimal? percentOff, decimal? fixedPrice) =>
        client.PutAsJsonAsync($"{Path}/{tier}/overrides/{product}", new SetPriceTierOverrideRequest(percentOff, fixedPrice));

    private static async Task SetAsync(HttpClient client, Guid tier, Guid product, decimal? percentOff, decimal? fixedPrice)
    {
        using HttpResponseMessage response = await OverrideAsync(client, tier, product, percentOff, fixedPrice);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task<HttpResponseMessage> ShapeAsync(HttpClient client, Guid tier, string? shape, decimal? percentOff) =>
        client.PutAsJsonAsync($"{Path}/{tier}/shape", new ChangePriceTierShapeRequest(shape, percentOff));

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FieldSales.Catalogue.Contracts.ProductValidationErrors>())!.Errors;
    }

    private static async Task<PriceTierOverrideItem> RowAsync(HttpClient client, Guid tier, Guid product) =>
        (await client.GetFromJsonAsync<PriceTierDetails>($"{Path}/{tier}"))!.Overrides.Single(row => row.ProductId == product);

    // What a customer holding only this tier pays for one of the product on the day, from the stored tier through the engine.
    private async Task<ResolvedPrice> ResolveAsync(Guid tierId, Guid productId, DateOnly? day = null)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        PriceTier tier = await db.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Id == tierId);
        Product product = await db.Products.AsNoTracking().Include(row => row.BasePrices).SingleAsync(row => row.Id == productId);
        return PriceResolver.Resolve(product.ToPricedProduct(), [tier.ToEngine()], 1m, day ?? Today)!;
    }

    private async Task<PriceTier> StoredAsync(Guid tierId)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        return await db.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Id == tierId);
    }

    [Fact]
    public async Task Should_UseTheListedPricesAndBaseElsewhere_When_SuncareDeal2027IsAPriceListOf20Products()
    {
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(sql.CreateConnectionString("PriceListTier")).Options;
        Dictionary<Guid, decimal> listed = [];
        Guid sudocrem;
        Guid dealId;
        await using (CatalogueDbContext db = new(options))
        {
            await db.Database.MigrateAsync();
            Category suncare = new CategoryTree([]).Add("Suncare");
            db.Categories.Add(suncare);
            PriceTier deal = PriceTier.CreatePriceList("Suncare Deal 2027");
            for (int index = 1; index <= 20; index++)
            {
                Product product = Product.Create($"SUN-{index:000}", $"Suncare product {index}", suncare.Id, 10m + index, new DateOnly(2026, 9, 1));
                db.Products.Add(product);
                listed[product.Id] = 9m + index * 0.75m; // Each below its base, so each listed price applies.
                deal.SetOverride(product.Id, TierTerm.Fixed(listed[product.Id]), Today);
            }
            Product unlisted = Product.Create("SUDO-125", "Sudocrem 125g", suncare.Id, 4.85m, new DateOnly(2026, 9, 1));
            db.Products.Add(unlisted);
            db.PriceTiers.Add(deal);
            await db.SaveChangesAsync();
            sudocrem = unlisted.Id;
            dealId = deal.Id;
        }

        await using CatalogueDbContext stored = new(options);
        PriceTier tier = await stored.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Id == dealId);
        Dictionary<Guid, ResolvedPrice> prices = (await stored.Products.AsNoTracking().Include(row => row.BasePrices).ToListAsync())
            .ToDictionary(product => product.Id, product => PriceResolver.Resolve(product.ToPricedProduct(), [tier.ToEngine()], 1m, Today)!);

        Assert.Equal((PriceTierShape.PriceList, (decimal?)null, 20), (tier.Shape, tier.PercentOff, tier.Overrides.Count));
        Assert.All(listed, entry => Assert.Equal((PriceKinds.Tier, "Suncare Deal 2027", entry.Value),
            (prices[entry.Key].Winner.Kind, prices[entry.Key].Winner.Label, prices[entry.Key].Winner.Amount)));
        Assert.Equal((PriceKinds.List, 4.85m), (prices[sudocrem].Winner.Kind, prices[sudocrem].Winner.Amount));
        Assert.Single(prices[sudocrem].Candidates); // The price list offers nothing for a product it doesn't list.
    }

    [Fact]
    public async Task Should_CreateAPriceListTierWithNoPercentage_When_HeadOfficeChoosesPriceList()
    {
        using HttpClient client = HeadOffice();

        using HttpResponseMessage created = await client.PostAsJsonAsync(Path,
            new SavePriceTierRequest("Suncare Deal 2027", null, PriceTierShapes.PriceList));
        using HttpResponseMessage withPercent = await client.PostAsJsonAsync(Path, new SavePriceTierRequest("Deal X", 8m, PriceTierShapes.PriceList));
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(Path, new SavePriceTierRequest("Deal Y", null, "Bundle"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        PriceTierListItem tier = (await created.Content.ReadFromJsonAsync<PriceTierListItem>())!;
        Assert.Equal(new PriceTierListItem(tier.Id, "Suncare Deal 2027", null, 0, PriceTierShapes.PriceList), tier);
        Assert.Equal([PriceTier.PriceListPercentMessage], (await ErrorsAsync(withPercent))["PercentOff"]);
        Assert.Equal(["Choose a percentage tier or a price list."], (await ErrorsAsync(unknown))["Shape"]);
        Assert.Equal([tier], (await client.GetFromJsonAsync<PriceTierListItem[]>(Path))!);

        using HttpResponseMessage renamed = await client.PutAsJsonAsync($"{Path}/{tier.Id}", new SavePriceTierRequest("Suncare Deal 2027/28", null));
        using HttpResponseMessage givenPercent = await client.PutAsJsonAsync($"{Path}/{tier.Id}", new SavePriceTierRequest("Suncare Deal", 8m));
        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        Assert.Equal([PriceTier.PriceListPercentMessage], (await ErrorsAsync(givenPercent))["PercentOff"]);
        PriceTierDetails details = (await client.GetFromJsonAsync<PriceTierDetails>($"{Path}/{tier.Id}"))!;
        Assert.Equal(("Suncare Deal 2027/28", (decimal?)null, PriceTierShapes.PriceList), (details.Name, details.PercentOff, details.Shape));
    }

    [Fact]
    public async Task Should_RefuseAPercentage_When_AnEntryIsAddedToAPriceListTier()
    {
        (Guid spf30, _) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid deal = await CreatedAsync(client, "Suncare Deal 2027", null, PriceTierShapes.PriceList);

        using HttpResponseMessage percent = await OverrideAsync(client, deal, spf30, 15m, null);
        Assert.Equal([PriceTier.PriceListTermMessage], (await ErrorsAsync(percent))["PercentOff"]);
        Assert.Empty((await client.GetFromJsonAsync<PriceTierDetails>($"{Path}/{deal}"))!.Overrides);

        await SetAsync(client, deal, spf30, null, 10.00m);
        Assert.Equal((null, 10.00m, 10.00m), ((await RowAsync(client, deal, spf30)) is var row ? (row.PercentOff, row.FixedPrice, row.TierPrice) : default));
        Assert.Equal(10.00m, (await ResolveAsync(deal, spf30)).Winner.Amount);
    }

    [Fact]
    public async Task Should_SaveAndFlagTheRow_When_AFixedPriceOf13IsEnteredWhereBaseIs1250()
    {
        (Guid spf30, Guid handCream) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid deal = await CreatedAsync(client, "Suncare Deal 2027", null, PriceTierShapes.PriceList);
        Guid tierB = await CreatedAsync(client, "Tier B", 8m, null);

        foreach (Guid tier in new[] { deal, tierB })
        {
            await SetAsync(client, tier, spf30, null, 13.00m); // Saved, not refused.
            await SetAsync(client, tier, handCream, null, 3.90m);

            PriceTierOverrideItem above = await RowAsync(client, tier, spf30);
            Assert.Equal((13.00m, 12.50m, true), (above.FixedPrice, above.ListPrice, above.AboveList));
            Assert.False((await RowAsync(client, tier, handCream)).AboveList);
            // It never applies: the list price beats it.
            Assert.Equal((PriceKinds.List, 12.50m), ((await ResolveAsync(tier, spf30)).Winner is var winner ? (winner.Kind, winner.Amount) : default));
        }
    }

    [Fact]
    public async Task Should_ClearTheWarning_When_ThePriceIsCorrectedTo1050()
    {
        (Guid spf30, _) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid deal = await CreatedAsync(client, "Suncare Deal 2027", null, PriceTierShapes.PriceList);
        await SetAsync(client, deal, spf30, null, 13.00m);
        Assert.True((await RowAsync(client, deal, spf30)).AboveList);

        await SetAsync(client, deal, spf30, null, 10.50m);

        PriceTierOverrideItem row = await RowAsync(client, deal, spf30);
        Assert.Equal((10.50m, false), (row.FixedPrice, row.AboveList));
        Assert.Equal(10.50m, (await ResolveAsync(deal, spf30)).Winner.Amount);
    }

    [Fact]
    public async Task Should_JudgeTheWarningOnTodaysBase_When_TheBasePriceRisesLater()
    {
        (Guid spf30, _) = await ShelfAsync();
        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            Product product = await db.Products.Include(row => row.BasePrices).SingleAsync(row => row.Id == spf30);
            product.AddBasePrice(13.50m, Today.AddDays(1));
            await db.SaveChangesAsync();
        }
        using HttpClient client = HeadOffice();
        Guid deal = await CreatedAsync(client, "Suncare Deal 2027", null, PriceTierShapes.PriceList);
        await SetAsync(client, deal, spf30, null, 13.00m);

        PriceTierOverrideItem today = await RowAsync(client, deal, spf30);
        _app.Clock.UtcNow = TabletSnapshotApplication.SnapshotClock.Start.AddDays(1);
        PriceTierOverrideItem tomorrow = await RowAsync(client, deal, spf30);

        Assert.Equal((12.50m, true), (today.ListPrice, today.AboveList));
        Assert.Equal((13.50m, false, 13.00m), (tomorrow.ListPrice, tomorrow.AboveList, tomorrow.TierPrice));
        Assert.Equal(13.00m, (await ResolveAsync(deal, spf30, Today.AddDays(1))).Winner.Amount);
    }

    [Fact]
    public async Task Should_NotWarn_When_AFixedPriceEqualsBase()
    {
        (Guid spf30, _) = await ShelfAsync();
        Category later = await _app.CategoryAsync("Autumn launches");
        Guid notYetPriced = await _app.ProductAsync("AUT-001", "Autumn Balm 50ml", later.Id, price: 6.00m, from: new DateOnly(2026, 12, 1));
        using HttpClient client = HeadOffice();
        Guid deal = await CreatedAsync(client, "Suncare Deal 2027", null, PriceTierShapes.PriceList);

        await SetAsync(client, deal, spf30, null, 12.50m);
        await SetAsync(client, deal, notYetPriced, null, 7.00m);

        PriceTierOverrideItem equal = await RowAsync(client, deal, spf30);
        Assert.Equal((12.50m, 12.50m, false), (equal.FixedPrice, equal.ListPrice, equal.AboveList));
        Assert.Equal(PriceKinds.Tier, (await ResolveAsync(deal, spf30)).Winner.Kind); // On a tie the rep sees the tier.
        PriceTierOverrideItem unpriced = await RowAsync(client, deal, notYetPriced);
        Assert.Equal(((decimal?)null, false), (unpriced.ListPrice, unpriced.AboveList)); // No list price today to be above (D3).
    }

    [Fact]
    public async Task Should_KeepFixedOverridesAsListedPricesAndRemovePercentOverrides_When_TheChangeIsConfirmed()
    {
        (Guid spf30, Guid handCream) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid tierB = await CreatedAsync(client, "Tier B", 8m, null);
        await SetAsync(client, tierB, spf30, null, 10.50m);
        await SetAsync(client, tierB, handCream, 15m, null);

        using HttpResponseMessage withPercent = await ShapeAsync(client, tierB, PriceTierShapes.PriceList, 8m);
        Assert.Equal([PriceTier.PriceListPercentMessage], (await ErrorsAsync(withPercent))["PercentOff"]);
        using HttpResponseMessage changed = await ShapeAsync(client, tierB, PriceTierShapes.PriceList, null);
        using HttpResponseMessage again = await ShapeAsync(client, tierB, PriceTierShapes.PriceList, null);

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(["Tier B is already a price list."], (await ErrorsAsync(again))["Shape"]);
        PriceTier stored = await StoredAsync(tierB);
        Assert.Equal((PriceTierShape.PriceList, (decimal?)null), (stored.Shape, stored.PercentOff));
        Assert.Equal([(spf30, (decimal?)null, (decimal?)10.50m, Today)],
            stored.Overrides.Select(entry => (entry.ProductId, entry.PercentOff, entry.FixedPrice, entry.SetOn)));
        Assert.Equal((10.50m, 4.00m), ((await ResolveAsync(tierB, spf30)).Winner.Amount, (await ResolveAsync(tierB, handCream)).Winner.Amount));
        Assert.Equal(new PriceTierListItem(tierB, "Tier B", null, 1, PriceTierShapes.PriceList),
            (await client.GetFromJsonAsync<PriceTierListItem[]>(Path))!.Single());
    }

    [Fact]
    public async Task Should_KeepListedPricesAsFixedOverrides_When_APriceListChangesBackToPercentage()
    {
        (Guid spf30, Guid handCream) = await ShelfAsync();
        using HttpClient client = HeadOffice();
        Guid deal = await CreatedAsync(client, "Suncare Deal 2027", null, PriceTierShapes.PriceList);
        await SetAsync(client, deal, spf30, null, 10.00m);

        foreach (decimal? refused in new decimal?[] { null, 0m, 100m, 8.125m })
            Assert.Equal([TierTerms.PercentMessage], (await ErrorsAsync(await ShapeAsync(client, deal, PriceTierShapes.Percentage, refused)))["PercentOff"]);
        Assert.Equal(PriceTierShape.PriceList, (await StoredAsync(deal)).Shape);
        using HttpResponseMessage changed = await ShapeAsync(client, deal, PriceTierShapes.Percentage, 8m);

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        PriceTier stored = await StoredAsync(deal);
        Assert.Equal((PriceTierShape.Percentage, (decimal?)8m), (stored.Shape, stored.PercentOff));
        Assert.Equal([(spf30, (decimal?)null, (decimal?)10.00m)], stored.Overrides.Select(entry => (entry.ProductId, entry.PercentOff, entry.FixedPrice)));
        Assert.Equal((10.00m, 3.68m), ((await ResolveAsync(deal, spf30)).Winner.Amount, (await ResolveAsync(deal, handCream)).Winner.Amount));
        Assert.Equal(["Suncare Deal 2027 is already a percentage tier."],
            (await ErrorsAsync(await ShapeAsync(client, deal, PriceTierShapes.Percentage, 10m)))["Shape"]);
    }
}
