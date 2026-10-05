using FieldSales.Api.Catalogue;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using FieldSales.Quantities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FieldSales.Api.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductPersistenceTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Should_PreserveEachAndPriceHistory_When_QuantityMigrationUpgradesExistingProduct()
    {
        string connectionString = fixture.CreateConnectionString("ProductQuantityMigration");
        var options = new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(connectionString).Options;
        Guid id = Guid.NewGuid();
        Guid categoryId;
        await using (CatalogueDbContext predecessor = new(options))
        {
            await predecessor.GetService<IMigrator>().MigrateAsync("20260930173447_AddMinimumProducts");
            Category category = new CategoryTree([]).Add("Suncare");
            predecessor.Categories.Add(category);
            await predecessor.SaveChangesAsync();
            categoryId = category.Id;
            await predecessor.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Products (Id, Code, Name, CategoryId, Unit, Attributes) VALUES ({id}, 'LEGACY', 'Existing product', {categoryId}, 'Each', '[]')");
            await predecessor.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ProductBasePrices (ProductId, EffectiveFrom, Amount) VALUES ({id}, '2026-09-30', 4.80)");
            await predecessor.Database.MigrateAsync();
        }
        await using CatalogueDbContext upgraded = new(options);
        Product product = await upgraded.Products.Include(product => product.BasePrices).SingleAsync();
        Assert.Equal(id, product.Id);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal("Each", product.Unit);
        Assert.Null(product.QuantityStep);
        Assert.Null(product.MinimumQuantity);
        Assert.True(product.GetQuantityRules().ValidateOrder(1m).IsValid);
        Assert.False(product.GetQuantityRules().ValidateOrder(0m).IsValid);
        Assert.Equal(4.80m, Assert.Single(product.BasePrices).Amount);
        Assert.Empty(product.Attributes);
    }

    [Fact]
    public async Task Should_PersistExactRulesAndRejectInvalidMinimum_When_ProductIsReloaded()
    {
        string connectionString = fixture.CreateConnectionString("ProductRules");
        var options = new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(connectionString).Options;
        Guid id;
        await using (CatalogueDbContext setup = new(options))
        {
            await setup.Database.MigrateAsync();
            Category category = new CategoryTree([]).Add("Tea");
            setup.Categories.Add(category);
            Assert.True(QuantityRules.TryCreate("kg", 0.000001m, 0.000003m, out var rules, out _));
            Product product = Product.Create("TEA", "Loose tea", category.Id, 4.80m, new DateOnly(2026, 10, 1), rules);
            setup.Products.Add(product);
            await setup.SaveChangesAsync();
            id = product.Id;
            SqlException error = await Assert.ThrowsAsync<SqlException>(() => setup.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET QuantityStep = 0.5, MinimumQuantity = 0.7 WHERE Id = {id}"));
            Assert.Equal(547, error.Number);
        }
        await using CatalogueDbContext reloaded = new(options);
        Product saved = await reloaded.Products.Include(product => product.BasePrices).SingleAsync(product => product.Id == id);
        Assert.Equal(0.000001m, saved.QuantityStep);
        Assert.Equal(0.000003m, saved.MinimumQuantity);
        Assert.True(saved.GetQuantityRules().ValidateOrder(0.000004m).IsValid);
        Assert.Equal(4.80m, Assert.Single(saved.BasePrices).Amount);
    }

    [Fact]
    public async Task Should_SaveOnlyOneProduct_When_DuplicateCodesAreSubmittedConcurrently()
    {
        string connectionString = fixture.CreateConnectionString("ProductConcurrent");
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(connectionString).Options;
        Guid categoryId;
        await using (CatalogueDbContext setup = new(options))
        {
            await setup.Database.MigrateAsync();
            Category category = new CategoryTree([]).Add("Suncare");
            setup.Categories.Add(category);
            await setup.SaveChangesAsync();
            categoryId = category.Id;
        }

        DateOnly today = new(2026, 9, 30);
        await using CatalogueDbContext first = new(options);
        await using CatalogueDbContext second = new(options);
        first.Products.Add(Product.Create(" SUN-0342 ", "First Lotion", categoryId, 12.50m, today));
        second.Products.Add(Product.Create("sun-0342", "Second Lotion", categoryId, 13m, today));
        bool[] saved = await Task.WhenAll(SaveAsync(first), SaveAsync(second));
        Assert.Single(saved, success => success);

        await using CatalogueDbContext restarted = new(options);
        Product winner = Assert.Single(await restarted.Products.Include(product => product.BasePrices).ToListAsync());
        Assert.Equal("SUN-0342", winner.Code, ignoreCase: true);
        Assert.Equal(categoryId, winner.CategoryId);
        Assert.Equal("Each", winner.Unit);
        Assert.Null(winner.ParentProductId);
        Assert.Empty(winner.Attributes);
        ProductBasePrice price = Assert.Single(await restarted.ProductBasePrices.ToListAsync());
        Assert.Equal(winner.Id, price.ProductId);
        Assert.Equal(today, price.EffectiveFrom);
        Assert.Equal(winner.Name == "First Lotion" ? 12.50m : 13m, price.Amount);
    }

    [Fact]
    public async Task Should_ReturnApplicablePrice_When_EffectiveDateIsReached()
    {
        string connectionString = fixture.CreateConnectionString("ProductEffectiveDate");
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(connectionString).Options;
        Guid productId;
        DateOnly initial = new(2026, 9, 30);
        DateOnly next = new(2026, 11, 1);
        await using (CatalogueDbContext setup = new(options))
        {
            await setup.Database.MigrateAsync();
            Category category = new CategoryTree([]).Add("Suncare");
            setup.Categories.Add(category);
            Product product = Product.Create("SUN-0342", "Lotion", category.Id, 12.50m, initial);
            setup.Products.Add(product);
            // Seed a later entry to verify the agreed read rule; no price-edit API is introduced.
            setup.ProductBasePrices.Add(ProductBasePrice.Create(product.Id, 13.20m, next));
            await setup.SaveChangesAsync();
            productId = product.Id;
        }

        await using CatalogueDbContext restarted = new(options);
        Product loaded = await restarted.Products.Include(product => product.BasePrices)
            .SingleAsync(product => product.Id == productId);
        Assert.Null(loaded.BasePriceOn(initial.AddDays(-1)));
        Assert.Equal(12.50m, loaded.BasePriceOn(initial)!.Amount);
        Assert.Equal(12.50m, loaded.BasePriceOn(next.AddDays(-1))!.Amount);
        Assert.Equal(13.20m, loaded.BasePriceOn(next)!.Amount);
        Assert.Equal(13.20m, loaded.BasePriceOn(next.AddDays(1))!.Amount);
        Assert.Equal(2, loaded.BasePrices.Count);
    }

    private static async Task<bool> SaveAsync(CatalogueDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return false;
        }
    }
}
