using FieldSales.Api.Catalogue;
using FieldSales.Api.Pricing;
using FieldSales.Quantities;
using Microsoft.EntityFrameworkCore;
using PriceKinds = FieldSales.Pricing.PriceKinds;
using PriceResolver = FieldSales.Pricing.PriceResolver;
using ResolvedPrice = FieldSales.Pricing.ResolvedPrice;

namespace FieldSales.Api.Tests;

/// <summary>WI-062 agreed scenarios 1, 2 and 4 (Pricing US-004 S1, S2, S4): a product's quantity breaks are stored with it and reach
/// the price engine, which prices a line at the break its quantity reaches, for Each and measured products alike.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class QuantityBreakPricingTests(SqlServerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static async Task<DbContextOptions<CatalogueDbContext>> CatalogueAsync(SqlServerFixture sql, string name)
    {
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(sql.CreateConnectionString(name)).Options;
        await using CatalogueDbContext db = new(options);
        await db.Database.MigrateAsync();
        return options;
    }

    // Creates the product with its breaks and saves it.
    private static async Task<Guid> ProductAsync(DbContextOptions<CatalogueDbContext> options, string code, string name, decimal price,
        QuantityRules? rules, params (decimal From, decimal Price)[] breaks)
    {
        await using CatalogueDbContext db = new(options);
        Category category = new CategoryTree([]).Add("Pharmacy " + code);
        db.Categories.Add(category);
        Product product = Product.Create(code, name, category.Id, price, new DateOnly(2026, 9, 1), rules);
        product.SetQuantityBreaks([.. breaks.Select(entry => new QuantityBreakInput(entry.From, entry.Price))]);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    // The product read back from storage and priced by the engine at each quantity, for a customer holding these tiers.
    private static async Task<ResolvedPrice[]> PricesAsync(DbContextOptions<CatalogueDbContext> options, Guid productId,
        IReadOnlyList<FieldSales.Pricing.PriceTier> tiers, params decimal[] quantities)
    {
        await using CatalogueDbContext db = new(options);
        Product product = await db.Products.AsNoTracking().Include(row => row.BasePrices).Include(row => row.QuantityBreaks)
            .SingleAsync(row => row.Id == productId);
        return [.. quantities.Select(quantity => PriceResolver.Resolve(product.ToPricedProduct(), tiers, quantity, Today)!)];
    }

    private static (string Kind, string Label, decimal Amount) Winner(ResolvedPrice price) =>
        (price.Winner.Kind, price.Winner.Label, price.Winner.Amount);

    [Fact]
    public async Task Should_PriceTwelveAt200AndThirtyAt175_When_ThroatLozengesHasBreaksFrom10And30()
    {
        DbContextOptions<CatalogueDbContext> options = await CatalogueAsync(fixture, "BreaksEach");
        Guid lozenges = await ProductAsync(options, "LOZ-36", "Throat Lozenges 36s", 2.20m, null, (10m, 2.00m), (30m, 1.75m));

        ResolvedPrice[] prices = await PricesAsync(options, lozenges, [], 9m, 12m, 30m);

        Assert.Equal((PriceKinds.List, "List price", 2.20m), Winner(prices[0]));
        Assert.Equal((PriceKinds.Break, "Break: 10+", 2.00m), Winner(prices[1]));
        Assert.Equal((PriceKinds.Break, "Break: 30+", 1.75m), Winner(prices[2]));
    }

    [Fact]
    public async Task Should_PriceTwelveKgAt400PerKg_When_LooseHerbalTeaHasBreaksFrom10KgAnd20Kg()
    {
        DbContextOptions<CatalogueDbContext> options = await CatalogueAsync(fixture, "BreaksMeasure");
        Assert.True(QuantityRules.TryCreate("kg", 0.5m, 1.0m, out QuantityRules? perKg, out _));
        Guid tea = await ProductAsync(options, "TEA-LOOSE", "Loose Herbal Tea", 4.80m, perKg, (10m, 4.00m), (20m, 3.60m));

        ResolvedPrice[] prices = await PricesAsync(options, tea, [], 9.5m, 12m, 20m);

        Assert.Equal((PriceKinds.List, 4.80m), (prices[0].Winner.Kind, prices[0].Winner.Amount));
        Assert.Equal((PriceKinds.Break, "Break: 10 kg+", 4.00m), Winner(prices[1]));
        Assert.Equal("€4.00 per kg — Break: 10 kg+ (list €4.80 per kg)", prices[1].Summary);
        Assert.Equal((PriceKinds.Break, "Break: 20 kg+", 3.60m), Winner(prices[2]));
    }

    [Fact]
    public async Task Should_LetTheTierPriceWin_When_ATierPricesTheProductBelowTheBreak()
    {
        DbContextOptions<CatalogueDbContext> options = await CatalogueAsync(fixture, "BreaksTier");
        Guid lozenges = await ProductAsync(options, "LOZ-36", "Throat Lozenges 36s", 2.20m, null, (10m, 2.00m), (30m, 1.75m));
        PriceTier tier = PriceTier.CreatePercentage("Tier B", 8m);
        tier.SetOverride(lozenges, TierTerm.Fixed(1.90m), Today);

        ResolvedPrice twelve = (await PricesAsync(options, lozenges, [tier.ToEngine()], 12m))[0];

        Assert.Equal((PriceKinds.Tier, "Tier B", 1.90m), Winner(twelve));
        Assert.Contains(twelve.Candidates, candidate => (candidate.Kind, candidate.Amount) == (PriceKinds.Break, 2.00m));
    }
}
