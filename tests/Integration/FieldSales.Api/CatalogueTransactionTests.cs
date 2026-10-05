using System.Data;
using FieldSales.Api.Catalogue;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

[Collection(SqlServerCollection.Name)]
public class CatalogueTransactionTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task SqlRetryClearsFailedStateAndUncommittedWorkRollsBack()
    {
        await using var db = new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(fixture.CreateConnectionString("Tx"), options => options.EnableRetryOnFailure(2, TimeSpan.Zero, null)).Options);
        await db.Database.MigrateAsync();
        int attempts = 0;
        Guid id = await CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
        {
            Assert.Empty(db.ChangeTracker.Entries());
            var brand = Brand.Create("retried brand");
            db.Brands.Add(brand);
            await db.SaveChangesAsync();
            if (++attempts == 1) throw new TimeoutException("transient timeout after save");
            await transaction.CommitAsync();
            return brand.Id;
        }, default);
        Assert.Equal(2, attempts);
        Assert.Equal(id, (await db.Brands.AsNoTracking().SingleAsync()).Id);
        var failure = new InvalidOperationException("fail after save");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogueTransactions.RunAsync(db,
            IsolationLevel.Serializable, async transaction =>
            {
                db.Brands.Add(Brand.Create("rolled back brand"));
                await db.SaveChangesAsync();
                throw failure;
            }, default));
        Assert.Same(failure, actual);
        Assert.Equal(1, await db.Brands.AsNoTracking().CountAsync());
    }
}
