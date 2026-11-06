using FieldSales.Api.Catalogue;
using FieldSales.Api.Pricing;
using PriceResolver = FieldSales.Pricing.PriceResolver;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Tests;

/// <summary>WI-060 agreed scenarios 1–3 (Pricing US-001 S1, S2): a percentage tier stores its terms only, and what it prices comes
/// from the engine reading the stored tier and the products' base price history, so a base price change flows through.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class PriceTierPricingTests(SqlServerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private sealed record Shelf(Guid Spf30, Guid HandCream, Guid Sudocrem);

    private static async Task<(DbContextOptions<CatalogueDbContext> Options, Shelf Shelf)> CatalogueAsync(SqlServerFixture sql, string name)
    {
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(sql.CreateConnectionString(name)).Options;
        await using CatalogueDbContext db = new(options);
        await db.Database.MigrateAsync();
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

    private static async Task<Guid> SaveAsync(DbContextOptions<CatalogueDbContext> options, PriceTier tier)
    {
        await using CatalogueDbContext db = new(options);
        db.PriceTiers.Add(tier);
        await db.SaveChangesAsync();
        return tier.Id;
    }

    // What a customer holding only this tier pays for one of each product on the day, read back from storage into the engine.
    private static async Task<Dictionary<Guid, decimal>> PricesAsync(DbContextOptions<CatalogueDbContext> options, Guid tierId, DateOnly day)
    {
        await using CatalogueDbContext db = new(options);
        PriceTier tier = await db.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Id == tierId);
        List<Product> products = await db.Products.AsNoTracking().Include(row => row.BasePrices).ToListAsync();
        return products.ToDictionary(product => product.Id,
            product => PriceResolver.Resolve(product.ToPricedProduct(), [tier.ToEngine()], 1m, day)!.Winner.Amount);
    }

    [Fact]
    public async Task Should_PriceEveryProduct8PercentBelowItsCurrentBase_When_TierBIsCreatedAt8PercentOff()
    {
        (DbContextOptions<CatalogueDbContext> options, Shelf shelf) = await CatalogueAsync(fixture, "TierPercentage");

        Guid tierB = await SaveAsync(options, PriceTier.CreatePercentage("Tier B", 8m));

        Dictionary<Guid, decimal> prices = await PricesAsync(options, tierB, Today);
        Assert.Equal((11.50m, 3.68m, 4.46m), (prices[shelf.Spf30], prices[shelf.HandCream], prices[shelf.Sudocrem]));
    }

    [Fact]
    public async Task Should_FollowTheNewBase_When_ABasePriceRisesAfterTheTierIsSet()
    {
        (DbContextOptions<CatalogueDbContext> options, Shelf shelf) = await CatalogueAsync(fixture, "TierFollowsBase");
        Guid tierB = await SaveAsync(options, PriceTier.CreatePercentage("Tier B", 8m));

        await using (CatalogueDbContext db = new(options))
        {
            Product spf30 = await db.Products.Include(row => row.BasePrices).SingleAsync(row => row.Id == shelf.Spf30);
            spf30.AddBasePrice(13.20m, new DateOnly(2026, 11, 1));
            await db.SaveChangesAsync();
        }

        Assert.Equal(11.50m, (await PricesAsync(options, tierB, new DateOnly(2026, 10, 31)))[shelf.Spf30]);
        Assert.Equal(12.14m, (await PricesAsync(options, tierB, new DateOnly(2026, 11, 1)))[shelf.Spf30]);
        await using CatalogueDbContext stored = new(options);
        PriceTier tier = await stored.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync();
        Assert.Equal(("Tier B", 8m), (tier.Name, tier.PercentOff)); // Nothing in the tier changed for the rise to apply.
        Assert.Empty(tier.Overrides);
    }

    [Fact]
    public async Task Should_UseEachOverrideAnd8PercentForEverythingElse_When_TierBOverridesSpf30At1050FixedAndHandCreamAt15Percent()
    {
        (DbContextOptions<CatalogueDbContext> options, Shelf shelf) = await CatalogueAsync(fixture, "TierOverrides");
        PriceTier tier = PriceTier.CreatePercentage("Tier B", 8m);
        tier.SetOverride(shelf.Spf30, TierTerm.Fixed(10.50m), Today);
        tier.SetOverride(shelf.HandCream, TierTerm.Percent(15m), Today);

        Guid tierB = await SaveAsync(options, tier);

        Dictionary<Guid, decimal> prices = await PricesAsync(options, tierB, Today);
        Assert.Equal((10.50m, 3.40m, 4.46m), (prices[shelf.Spf30], prices[shelf.HandCream], prices[shelf.Sudocrem]));
        await using CatalogueDbContext stored = new(options);
        Assert.Equal([(shelf.HandCream, (decimal?)15m, (decimal?)null, Today), (shelf.Spf30, null, 10.50m, Today)],
            (await stored.PriceTierOverrides.AsNoTracking().ToListAsync())
                .OrderBy(entry => entry.ProductId == shelf.Spf30)
                .Select(entry => (entry.ProductId, entry.PercentOff, entry.FixedPrice, entry.SetOn)));
    }
}
