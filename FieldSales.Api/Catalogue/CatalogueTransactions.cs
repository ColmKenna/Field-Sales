using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FieldSales.Api.Catalogue;

public static class CatalogueTransactions
{
    public static Task<T> RunAsync<T>(CatalogueDbContext db, IsolationLevel isolationLevel,
        Func<IDbContextTransaction, Task<T>> operation, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            return await operation(transaction);
        });

    public static Task RunAsync(CatalogueDbContext db, IsolationLevel isolationLevel,
        Func<IDbContextTransaction, Task> operation, CancellationToken cancellationToken) =>
        RunAsync(db, isolationLevel, async transaction => { await operation(transaction); return true; }, cancellationToken);
}
