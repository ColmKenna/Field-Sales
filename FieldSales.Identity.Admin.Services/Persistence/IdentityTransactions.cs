using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FieldSales.Identity.Services.Persistence;

/// <summary>Retries own fresh tracked state. Callers retain save, commit and rollback ownership.</summary>
public static class IdentityTransactions
{
    public static Task<T> RetryAsync<T>(DbContext db, Func<Task<T>> operation) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
        {
            db.ChangeTracker.Clear();
            return operation();
        });

    public static Task<T> RunAsync<T>(DbContext db, IsolationLevel? isolationLevel,
        Func<IDbContextTransaction, Task<T>> operation, CancellationToken cancellationToken) =>
        RetryAsync(db, async () =>
        {
            await using var transaction = isolationLevel.HasValue
                ? await db.Database.BeginTransactionAsync(isolationLevel.Value, cancellationToken)
                : await db.Database.BeginTransactionAsync(cancellationToken);
            return await operation(transaction);
        });
}
