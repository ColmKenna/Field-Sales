using System.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

internal static class GeographyTransactions
{
    public static Task<T> RunAsync<T>(DirectoryDbContext db, Func<Task<T>> operation, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            T result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });
}

