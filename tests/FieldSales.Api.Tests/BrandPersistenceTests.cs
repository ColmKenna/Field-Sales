using FieldSales.Api.Catalogue;
using FieldSales.ReferenceData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

public sealed class BrandPersistenceTests
{
    [Fact]
    public async Task Should_SaveBrand_When_NameIsValid()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        var options = new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(sql.GetConnectionString()).Options;
        Guid id;
        await using (var predecessor = new CatalogueDbContext(options))
        {
            await predecessor.GetService<IMigrator>().MigrateAsync("20260930202155_AddProductQuantityRules");
            var category = new CategoryTree([]).Add("Existing category");
            predecessor.Categories.Add(category);
            await predecessor.SaveChangesAsync();
            id = Guid.NewGuid();
            await predecessor.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Products (Id, Code, Name, CategoryId, Unit, Attributes) VALUES ({id}, 'LEGACY', 'Existing product', {category.Id}, 'Each', '[]')");
            await predecessor.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ProductBasePrices (ProductId, EffectiveFrom, Amount) VALUES ({id}, '2026-09-30', 4.80)");
            await predecessor.Database.MigrateAsync();
        }
        await using var upgraded = new CatalogueDbContext(options);
        var existing = await upgraded.Products.Include(product => product.BasePrices).Include(product => product.AlternativeBrands).SingleAsync();
        Assert.Equal(id, existing.Id);
        Assert.Null(existing.PrimaryBrandId);
        Assert.Empty(existing.AlternativeBrands);
        Assert.Equal(4.80m, Assert.Single(existing.BasePrices).Amount);
        Assert.Equal("Each", existing.Unit);
        Assert.Equal("LEGACY", existing.Code);
        Assert.Empty(await upgraded.Brands.ToArrayAsync());
        var brand = Brand.Create(" SunCo ");
        upgraded.Brands.Add(brand);
        await upgraded.SaveChangesAsync();
        brand.Archive();
        await upgraded.SaveChangesAsync();
        await using var duplicate = new CatalogueDbContext(options);
        duplicate.Brands.Add(Brand.Create("SUNCO"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        Assert.True(error.InnerException is SqlException { Number: 2601 or 2627 });
        await using var restarted = new CatalogueDbContext(options);
        var stored = await restarted.Brands.SingleAsync();
        Assert.Equal("SunCo", stored.Name);
        Assert.True(stored.IsArchived);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RefuseDelete_When_ReferenceAppearsAfterPageLoad(bool alternative)
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        var options = new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(sql.GetConnectionString()).Options;
        await using var db = new CatalogueDbContext(options);
        await db.Database.MigrateAsync();
        var category = new CategoryTree([]).Add("Lotion");
        var brand = Brand.Create("SunCo");
        var product = Product.Create("SUN", "Lotion", category.Id, 4.80m, new DateOnly(2026, 10, 1));
        db.Categories.Add(category);
        db.Brands.Add(brand);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        if (alternative)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ProductAlternativeBrands (ProductId, BrandId) VALUES ({product.Id}, {brand.Id})");
        else
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Products SET PrimaryBrandId = {brand.Id} WHERE Id = {product.Id}");
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Brands WHERE Id = {brand.Id}"));
        Assert.Equal(547, error.Number);
        Assert.Equal(brand.Id, (await db.Brands.AsNoTracking().SingleAsync()).Id);
        var count = await new ProductBrandUsageSource(db).CountAsync(new("brands", brand.Id), default);
        Assert.Equal("Used by 1 product", new ReferenceUsage([count]).Description);
    }
}
