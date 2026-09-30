using FieldSales.Api.Catalogue;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

public sealed class ProductPersistenceTests
{
    [Fact]
    public async Task Should_SaveOnlyOneProduct_When_DuplicateCodesAreSubmittedConcurrently()
    {
        await using MsSqlContainer sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(sql.GetConnectionString()).Options;
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
        await using MsSqlContainer sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        DbContextOptions<CatalogueDbContext> options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(sql.GetConnectionString()).Options;
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
