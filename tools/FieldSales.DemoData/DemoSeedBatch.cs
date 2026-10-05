using Microsoft.EntityFrameworkCore;

namespace FieldSales.DemoData;

public static class DemoSeedBatch
{
    public const string SeedName = "sample-v1";

    // Each database commits its sample records and receipt together. A failed run can
    // be retried without duplicating completed batches or overwriting testing changes.
    public static async Task<bool> RunAsync(DbContext db, Func<Task> seed)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = 'FieldSalesDemoSeed',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
            IF @result < 0 THROW 51000, 'Another demo seed is running. Try again shortly.', 1;
            IF OBJECT_ID(N'dbo.__FieldSalesDemoSeeds', N'U') IS NULL
                CREATE TABLE dbo.__FieldSalesDemoSeeds (
                    Name nvarchar(100) NOT NULL PRIMARY KEY,
                    CreatedAt datetimeoffset NOT NULL);
            """);
        if (await IsSeededAsync(db))
        {
            await transaction.CommitAsync();
            return false;
        }
        await seed();
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.__FieldSalesDemoSeeds (Name, CreatedAt) VALUES ({SeedName}, {DateTimeOffset.UtcNow})");
        await transaction.CommitAsync();
        return true;
    }

    public static async Task<bool> IsSeededAsync(DbContext db)
    {
        int exists = await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.__FieldSalesDemoSeeds', N'U') IS NULL
                THEN 0 ELSE 1 END AS [Value]
            """).SingleAsync();
        return exists == 1 && await db.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM dbo.__FieldSalesDemoSeeds WHERE Name = {SeedName}").SingleAsync() == 1;
    }
}
