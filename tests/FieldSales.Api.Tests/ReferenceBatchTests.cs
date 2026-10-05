using System.Data.Common;
using FieldSales.Api.Catalogue;
using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

public sealed class ReferenceBatchTests
{
    [Fact]
    public void Registry_RejectsMissingAndDuplicateSourcesAndResolvesUnknownKeys()
    {
        using var db = new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer("Server=localhost;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        IReferenceListStore[] stores = [new BrandListStore(db)];
        ReferenceCatalogueRegistry missing = new(stores, []);
        Assert.Null(missing.Find("unknown"));
        Assert.Throws<InvalidOperationException>(missing.ValidateRegistrations);
        ReferenceCatalogueRegistry duplicate = new(stores, [new ProductBrandUsageSource(db), new ProductBrandUsageSource(db)]);
        Assert.Throws<InvalidOperationException>(duplicate.ValidateRegistrations);
        new ReferenceCatalogueRegistry(stores, [new ProductBrandUsageSource(db)]).ValidateRegistrations();
        Assert.False(SqlServerErrors.IsUniqueViolation(new InvalidOperationException("UNIQUE constraint failed")));
    }

    [Fact]
    public async Task SourceFailure_RemainsUnavailableWithItsOriginalCause()
    {
        using var db = new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer("Server=localhost;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        InvalidOperationException cause = new("source unavailable");
        var reader = new ReferenceUsageReader(new([new BrandListStore(db)], [new FailedSource(cause)]));
        var error = await Assert.ThrowsAsync<ReferenceUsageUnavailableException>(() =>
            reader.ReadManyAsync(ReferenceListKeys.Brands, [Guid.NewGuid()], CancellationToken.None));
        Assert.Same(cause, error.InnerException);
    }

    [Fact]
    public async Task BatchUsage_CountsDistinctProductsWithOneSqlReadPerSourceRegardlessOfListSize()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        CommandCounter counter = new();
        await using var db = new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(sql.GetConnectionString()).AddInterceptors(counter).Options);
        await db.Database.MigrateAsync();
        Category category = new CategoryTree([]).Add("category");
        Brand primary = Brand.Create("primary"), alternative = Brand.Create("alternative");
        Brand[] unused = Enumerable.Range(0, 20).Select(index => Brand.Create($"unused {index}")).ToArray();
        ProductProfile profile = ProductProfile.Create("profile");
        Supplier supplier = Supplier.Create("supplier");
        AttributeName attribute = AttributeName.Create("attribute");
        Product product = Product.Create("code", "name", category.Id, 1, new(2026, 1, 1));
        product.SetBrands(primary, [primary, alternative]);
        product.SetClassification(profile, supplier);
        product.AddAttribute(attribute, "one");
        product.AddAttribute(attribute, "two");
        alternative.Archive();
        db.AddRange(category, primary, alternative, profile, supplier, attribute, product);
        db.Brands.AddRange(unused);
        await db.SaveChangesAsync();
        var registry = new ReferenceCatalogueRegistry(
            [new BrandListStore(db), new ProductProfileListStore(db), new AttributeNameListStore(db), new SupplierListStore(db)],
            [new ProductBrandUsageSource(db), new ProductReferenceUsageSource(db)]);
        registry.ValidateRegistrations();
        ReferenceUsageReader reader = new(registry);
        counter.Reads = 0;
        var brands = await reader.ReadManyAsync(ReferenceListKeys.Brands,
            [primary.Id, alternative.Id, .. unused.Select(brand => brand.Id)], CancellationToken.None);
        Assert.Equal(1, counter.Reads);
        Assert.Equal(1, Assert.Single(brands[primary.Id].Counts).Count);
        Assert.Equal(1, Assert.Single(brands[alternative.Id].Counts).Count);
        Assert.All(unused, brand => Assert.False(brands[brand.Id].IsUsed));
        foreach (var entry in new[] { (ReferenceListKeys.Profiles, profile.Id), (ReferenceListKeys.Suppliers, supplier.Id), (ReferenceListKeys.AttributeNames, attribute.Id) })
        {
            counter.Reads = 0;
            var usage = await reader.ReadManyAsync(entry.Item1, [entry.Item2, Guid.NewGuid()], CancellationToken.None);
            Assert.Equal(1, counter.Reads);
            Assert.Equal(1, Assert.Single(usage[entry.Item2].Counts).Count);
        }
        Assert.Equal(1, Assert.Single((await reader.ReadAsync(new(ReferenceListKeys.Brands, primary.Id), default)).Counts).Count);
    }

    private sealed class FailedSource(Exception cause) : IReferenceUsageSource
    {
        public string SourceKey => "products";
        public bool Supports(string listKey) => listKey == ReferenceListKeys.Brands;
        public Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken) => throw cause;
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Reads { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }
}
