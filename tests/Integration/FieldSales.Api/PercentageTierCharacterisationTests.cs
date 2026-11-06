using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Pricing;
using FieldSales.StaffAccess;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PriceResolver = FieldSales.Pricing.PriceResolver;

namespace FieldSales.Api.Tests;

/// <summary>WI-060's percentage tiers as they stand before WI-061 adds price lists: agreed characterisation scenarios C1–C4, which pass
/// before and after the change. C1 and C2 write tiers in WI-060's schema, then bring the database to the latest migration, so a later
/// migration must keep them pricing exactly as before. C3 and C4 read and write the API's JSON by property name, so properties a later
/// change adds don't disturb them.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class PercentageTierCharacterisationTests(SqlServerFixture sql, TabletSnapshotFixture fixture)
    : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private const string WithPercentageTiers = "20261106124628_AddPercentagePriceTiers";
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

    private sealed record Shelf(Guid Spf30, Guid HandCream, Guid Sudocrem);

    // A catalogue at WI-060's schema with three products, and a way to write tier rows exactly as WI-060 stored them.
    private async Task<(DbContextOptions<CatalogueDbContext> Options, Shelf Shelf)> WI060CatalogueAsync(string name)
    {
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(sql.CreateConnectionString(name)).Options;
        await using CatalogueDbContext db = new(options);
        await db.GetService<IMigrator>().MigrateAsync(WithPercentageTiers);
        Category suncare = new CategoryTree([]).Add("Suncare");
        db.Categories.Add(suncare);
        Product Add(string code, string product, decimal price)
        {
            Product created = Product.Create(code, product, suncare.Id, price, new DateOnly(2026, 9, 1));
            db.Products.Add(created);
            return created;
        }
        Shelf shelf = new(Add("SUN-030", "SPF30 Sun Lotion 200ml", 12.50m).Id, Add("HAND-75", "Hand Cream 75ml", 4.00m).Id,
            Add("SUDO-125", "Sudocrem 125g", 4.85m).Id);
        await db.SaveChangesAsync();
        return (options, shelf);
    }

    private static async Task<Guid> WI060TierAsync(DbContextOptions<CatalogueDbContext> options, string name, decimal percentOff,
        params (Guid Product, decimal? PercentOff, decimal? FixedPrice)[] overrides)
    {
        Guid id = Guid.NewGuid();
        await using CatalogueDbContext db = new(options);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO PriceTiers (Id, Name, PercentOff) VALUES (@id, @name, @percent)",
            new SqlParameter("@id", id), new SqlParameter("@name", name), new SqlParameter("@percent", percentOff));
        foreach ((Guid product, decimal? percent, decimal? price) in overrides)
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO PriceTierOverrides (PriceTierId, ProductId, PercentOff, FixedPrice, SetOn) VALUES (@id, @product, @percent, @price, @setOn)",
                new SqlParameter("@id", id), new SqlParameter("@product", product),
                new SqlParameter("@percent", (object?)percent ?? DBNull.Value), new SqlParameter("@price", (object?)price ?? DBNull.Value),
                new SqlParameter("@setOn", Today.ToDateTime(TimeOnly.MinValue)));
        return id;
    }

    // After any later migration has run: what a customer holding only this tier pays for one of each product today.
    private static async Task<Dictionary<Guid, decimal>> PricesAfterUpgradeAsync(DbContextOptions<CatalogueDbContext> options, Guid tierId)
    {
        await using CatalogueDbContext db = new(options);
        await db.Database.MigrateAsync();
        PriceTier tier = await db.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Id == tierId);
        List<Product> products = await db.Products.AsNoTracking().Include(row => row.BasePrices).ToListAsync();
        return products.ToDictionary(product => product.Id,
            product => PriceResolver.Resolve(product.ToPricedProduct(), [tier.ToEngine()], 1m, Today)!.Winner.Amount);
    }

    [Fact]
    public async Task Should_PriceEveryProductAtTheDefaultPercentage_When_AStoredPercentageTierHasNoOverrides()
    {
        (DbContextOptions<CatalogueDbContext> options, Shelf shelf) = await WI060CatalogueAsync("CharacteriseTierDefault");
        Guid tierB = await WI060TierAsync(options, "Tier B", 8m);

        Dictionary<Guid, decimal> prices = await PricesAfterUpgradeAsync(options, tierB);

        Assert.Equal((11.50m, 3.68m, 4.46m), (prices[shelf.Spf30], prices[shelf.HandCream], prices[shelf.Sudocrem]));
    }

    [Fact]
    public async Task Should_UseEachOverrideAndTheDefaultElsewhere_When_AStoredPercentageTierHasOverrides()
    {
        (DbContextOptions<CatalogueDbContext> options, Shelf shelf) = await WI060CatalogueAsync("CharacteriseTierOverrides");
        Guid tierB = await WI060TierAsync(options, "Tier B", 8m, (shelf.HandCream, 15m, null), (shelf.Spf30, null, 10.50m));

        Dictionary<Guid, decimal> prices = await PricesAfterUpgradeAsync(options, tierB);

        Assert.Equal((10.50m, 3.40m, 4.46m), (prices[shelf.Spf30], prices[shelf.HandCream], prices[shelf.Sudocrem]));
        await using CatalogueDbContext stored = new(options);
        PriceTier tier = await stored.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync();
        Assert.Equal(("Tier B", (decimal?)8m), (tier.Name, (decimal?)tier.PercentOff));
        Assert.Equal([(shelf.HandCream, (decimal?)15m, (decimal?)null, Today), (shelf.Spf30, null, 10.50m, Today)],
            tier.Overrides.OrderBy(entry => entry.ProductId == shelf.Spf30)
                .Select(entry => (entry.ProductId, entry.PercentOff, entry.FixedPrice, entry.SetOn)));
    }

    [Fact]
    public async Task Should_ReturnTheSameListAndDetail_When_HeadOfficeReadsAPercentageTier()
    {
        Category suncare = await _app.CategoryAsync("Suncare");
        Guid spf30 = await _app.ProductAsync("SUN-030", "SPF30 Sun Lotion 200ml", suncare.Id, price: 12.50m);
        Guid handCream = await _app.ProductAsync("HAND-75", "Hand Cream 75ml", suncare.Id, price: 4.00m);
        using HttpClient client = _app.ClientAs("orla", [BusinessRoles.HeadOfficeUser]);
        Guid tierB = await CreateAsync(client, new { name = "Tier B", percentOff = 8m });
        foreach ((Guid product, object term) in new (Guid, object)[] { (handCream, new { percentOff = 15m }), (spf30, new { fixedPrice = 10.50m }) })
        {
            using HttpResponseMessage set = await client.PutAsJsonAsync($"{Path}/{tierB}/overrides/{product}", term);
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        }

        using JsonDocument list = JsonDocument.Parse(await client.GetStringAsync(Path));
        using JsonDocument detail = JsonDocument.Parse(await client.GetStringAsync($"{Path}/{tierB}"));

        JsonElement row = Assert.Single(list.RootElement.EnumerateArray());
        Assert.Equal((tierB, "Tier B", 8m, 2),
            (row.GetProperty("id").GetGuid(), row.GetProperty("name").GetString(), row.GetProperty("percentOff").GetDecimal(),
                row.GetProperty("overrideCount").GetInt32()));
        JsonElement tier = detail.RootElement;
        Assert.Equal((tierB, "Tier B", 8m),
            (tier.GetProperty("id").GetGuid(), tier.GetProperty("name").GetString(), tier.GetProperty("percentOff").GetDecimal()));
        Assert.Equal(
            [
                (handCream, "HAND-75", "Hand Cream 75ml", "Each", (decimal?)15m, (decimal?)null, (decimal?)4.00m, (decimal?)3.40m, "2026-10-01"),
                (spf30, "SUN-030", "SPF30 Sun Lotion 200ml", "Each", null, 10.50m, 12.50m, 10.50m, "2026-10-01")
            ],
            tier.GetProperty("overrides").EnumerateArray().Select(entry => (
                entry.GetProperty("productId").GetGuid(), entry.GetProperty("code").GetString(), entry.GetProperty("name").GetString(),
                entry.GetProperty("unit").GetString(), Decimal(entry, "percentOff"), Decimal(entry, "fixedPrice"),
                Decimal(entry, "listPrice"), Decimal(entry, "tierPrice"), entry.GetProperty("setOn").GetString())));
    }

    [Fact]
    public async Task Should_CreateAPercentageTier_When_TheRequestHasOnlyANameAndPercentage()
    {
        Category suncare = await _app.CategoryAsync("Suncare");
        Guid spf30 = await _app.ProductAsync("SUN-030", "SPF30 Sun Lotion 200ml", suncare.Id, price: 12.50m);
        using HttpClient client = _app.ClientAs("orla", [BusinessRoles.HeadOfficeUser]);

        Guid tierB = await CreateAsync(client, new { name = "Tier B", percentOff = 8m });

        using JsonDocument detail = JsonDocument.Parse(await client.GetStringAsync($"{Path}/{tierB}"));
        Assert.Equal(("Tier B", 8m), (detail.RootElement.GetProperty("name").GetString(), detail.RootElement.GetProperty("percentOff").GetDecimal()));
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        PriceTier tier = await db.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Id == tierB);
        Product product = await db.Products.AsNoTracking().Include(row => row.BasePrices).SingleAsync(row => row.Id == spf30);
        Assert.Equal(11.50m, PriceResolver.Resolve(product.ToPricedProduct(), [tier.ToEngine()], 1m, Today)!.Winner.Amount);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, object request)
    {
        using HttpResponseMessage created = await client.PostAsJsonAsync(Path, request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static decimal? Decimal(JsonElement entry, string property) =>
        entry.GetProperty(property) is { ValueKind: JsonValueKind.Number } value ? value.GetDecimal() : null;
}
