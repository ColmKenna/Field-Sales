using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.ReferenceData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

public sealed class ProductReferencePersistenceTests
{
    private const string Predecessor = "20261001085456_AddBrandsAndProductReferences";

    [Fact]
    public async Task Should_PreserveLegacyAttributesAndProductData_When_ReferenceMigrationUpgradesAndRollsBack()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        var options = new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(sql.GetConnectionString()).Options;
        Guid id = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        string longValue = "Quoted \"value\" ·\n" + new string('界', 6000);
        string payload = JsonSerializer.Serialize(new[] { new { Name = "Weight", Value = "200 g" },
            new { Name = "Weight", Value = longValue }, new { Name = "Shelf life", Value = "" } });
        await using var db = new CatalogueDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(Predecessor);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Categories (Id, Name) VALUES ({categoryId}, 'Legacy category')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Products (Id, Code, Name, CategoryId, Unit, QuantityStep, MinimumQuantity, Attributes) VALUES ({id}, 'LEGACY', 'Existing product', {categoryId}, 'kg', 0.5, 1.0, {payload})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ProductBasePrices (ProductId, EffectiveFrom, Amount) VALUES ({id}, '2026-09-30', 4.80)");
        await db.Database.MigrateAsync();
        var product = await db.Products.AsNoTracking().Include(product => product.BasePrices)
            .Include(product => product.AttributeValues).ThenInclude(value => value.AttributeName).AsSplitQuery().SingleAsync();
        Assert.Equal(id, product.Id);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal("LEGACY", product.Code);
        Assert.Null(product.ProductProfileId);
        Assert.Null(product.SupplierId);
        Assert.Equal("kg", product.Unit);
        Assert.Equal(0.5m, product.QuantityStep);
        Assert.Equal(1m, product.MinimumQuantity);
        Assert.Equal(4.80m, Assert.Single(product.BasePrices).Amount);
        Assert.Equal(new[] { "Weight", "Weight", "Shelf life" }, product.Attributes.Select(value => value.Name));
        Assert.Equal(new[] { "200 g", longValue, "" }, product.Attributes.Select(value => value.Value));
        Assert.Equal(2, await db.AttributeNames.CountAsync());
        var weight = await db.AttributeNames.SingleAsync(name => name.Name == "Weight");
        Assert.Equal(1, (await new ProductReferenceUsageSource(db).CountAsync(new("attribute-names", weight.Id), default)).Count);
        weight.Rename("Mass");
        await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync(Predecessor);
        string restored = await db.Database.SqlQuery<string>($"SELECT Attributes AS Value FROM Products WHERE Id = {id}").SingleAsync();
        var restoredRows = JsonSerializer.Deserialize<ProductAttribute[]>(restored)!;
        Assert.Equal(new[] { "Mass", "Mass", "Shelf life" }, restoredRows.Select(value => value.Name));
        Assert.Equal(new[] { "200 g", longValue, "" }, restoredRows.Select(value => value.Value));
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        Assert.Equal(3, await db.ProductAttributeValues.CountAsync());
    }

    [Fact]
    public async Task Should_KeepLegacyData_When_MigrationFindsInvalidAttributeInput()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        var options = new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(sql.GetConnectionString()).Options;
        await using var db = new CatalogueDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(Predecessor);
        Guid category = Guid.NewGuid();
        Guid product = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Categories (Id, Name) VALUES ({category}, 'Legacy')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Products (Id, Code, Name, CategoryId, Unit, Attributes) VALUES ({product}, 'LEGACY', 'Existing product', {category}, 'Each', '[]')");
        foreach (string invalid in new[] { "not-json", "{}", "[42]", "[{\"Name\":\"\",\"Value\":\"a\"}]",
                     "[{\"Name\":\"Weight\",\"Value\":null}]", JsonSerializer.Serialize(new[] { new { Name = new string('N', 201), Value = "kept" } }) })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Products SET Attributes = {invalid} WHERE Id = {product}");
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.MigrateAsync());
            Assert.Equal(51000, error.Number);
            Assert.Equal(invalid, await db.Database.SqlQuery<string>($"SELECT Attributes AS Value FROM Products WHERE Id = {product}").SingleAsync());
            Assert.Equal(Predecessor, (await db.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'AttributeNames'").SingleAsync());
        }
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    [InlineData("restriction-groups")]
    public async Task Should_RefuseDeletion_When_ProductReferencesExist(string key)
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        var options = new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(sql.GetConnectionString()).Options;
        await using var db = new CatalogueDbContext(options);
        await db.Database.MigrateAsync();
        var category = new CategoryTree([]).Add("Category");
        var product = Product.Create("ONE", "Lotion", category.Id, 4.80m, new DateOnly(2026, 10, 1));
        NamedReferenceItem item = key switch
        {
            "profiles" => ProductProfile.Create("Chilled"),
            "suppliers" => Supplier.Create("Supplier"),
            "restriction-groups" => RestrictionGroup.Create("Pharmacy-only medicines"),
            _ => AttributeName.Create("Weight")
        };
        db.Add(item);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var assignments = new ProductReferenceAssignments(db);
        if (key == "attribute-names") await assignments.AddAttributeAsync(product.Id, item.Id, "200 g");
        else if (key == "restriction-groups") await assignments.SetRestrictionGroupAsync(product.Id, item.Id);
        else await assignments.SetClassificationAsync(product.Id, key == "profiles" ? item.Id : null, key == "suppliers" ? item.Id : null);
        string command = key switch
        {
            "profiles" => "DELETE FROM ProductProfiles WHERE Id = @id",
            "suppliers" => "DELETE FROM Suppliers WHERE Id = @id",
            "restriction-groups" => "DELETE FROM RestrictionGroups WHERE Id = @id",
            _ => "DELETE FROM AttributeNames WHERE Id = @id"
        };
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(command, new SqlParameter("@id", item.Id)));
        Assert.Equal(547, error.Number);
        Assert.Equal(1, (await new ProductReferenceUsageSource(db).CountAsync(new(key, item.Id), default)).Count);
    }
}
