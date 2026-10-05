using FieldSales.Identity.Services.Persistence;
using System.Data;
using FieldSales.Identity.Services.SecretReveals;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FieldSales.Identity.Data.Adapters;

/// <summary>
///     EF-backed <see cref=\"ISecretRevealStore" /> adapter against <see cref=\"ApplicationDbContext\" />.
///     Owns every SQL-Server-specific concern moved out of the (now host-agnostic) library service:
///     digest-collision detection on insert, and locked, serializable-isolation match-and-consume.
/// </summary>
public sealed class EfSecretRevealStore(ApplicationDbContext dbContext) : ISecretRevealStore
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<SecretRevealInsertStatus> TryInsertAsync(
        byte[] handleDigest,
        SecretSecurityContext securityContext,
        string protectedPayload,
        DateTimeOffset createdUtc,
        DateTimeOffset expiresUtc,
        CancellationToken cancellationToken = default)
    {
        string actorSubjectIdStr = securityContext.ActorSubjectId.Value ?? string.Empty;
        _dbContext.SecretRevealRecords.Add(new SecretRevealRecord
        {
            HandleDigest = handleDigest,
            ActorSubjectId = actorSubjectIdStr,
            Purpose = securityContext.Purpose.ToString(),
            TargetId = securityContext.TargetId,
            ProtectedPayload = protectedPayload,
            CreatedUtc = createdUtc,
            ExpiresUtc = expiresUtc
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return SecretRevealInsertStatus.Inserted;
        }
        catch (DbUpdateException ex) when (IsDigestCollision(ex))
        {
            _dbContext.ChangeTracker.Clear();
            return SecretRevealInsertStatus.DigestCollision;
        }
    }

    public async Task<SecretRevealLookup> ConsumeAsync(
        byte[] handleDigest,
        SecretSecurityContext securityContext,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        string actorSubjectIdStr = securityContext.ActorSubjectId.Value ?? string.Empty;
        string purposeStr = securityContext.Purpose.ToString();
        string targetId = securityContext.TargetId;
        return await IdentityTransactions.RunAsync(_dbContext, IsolationLevel.Serializable, async transaction =>
        {
            SecretRevealRecord? record = await LoadForConsumeAsync(handleDigest, cancellationToken);
            if (record is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return SecretRevealLookup.NotFound();
            }

            if (!string.Equals(record.ActorSubjectId, actorSubjectIdStr, StringComparison.Ordinal)
                || !string.Equals(record.Purpose, purposeStr, StringComparison.Ordinal)
                || !string.Equals(record.TargetId, targetId, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return SecretRevealLookup.WrongContext();
            }

            if (record.ExpiresUtc <= now)
            {
                _dbContext.SecretRevealRecords.Remove(record);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return SecretRevealLookup.Expired();
            }

            var revealed = SecretRevealLookup.Revealed(record.ProtectedPayload);
            _dbContext.SecretRevealRecords.Remove(record);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return revealed;
        }, cancellationToken);
    }

    public async Task CleanupExpiredAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

        // Deleting in bounded chunks limits log-flush and locking pressure on SQL Server.
        List<long> expiredIds = await _dbContext.SecretRevealRecords
            .Where(r => r.ExpiresUtc <= now)
            .OrderBy(r => r.ExpiresUtc)
            .Select(r => r.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (expiredIds.Count == 0) return;

        await _dbContext.SecretRevealRecords
            .Where(r => expiredIds.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<SecretRevealRecord?> LoadForConsumeAsync(byte[] handleDigest,
        CancellationToken cancellationToken)
    {
        // On SQL Server, take an exclusive row-level lock so concurrent consumers serialize behind
        // the first transaction. On SQLite/in-memory test providers, fall back to standard LINQ.
        if (_dbContext.Database.IsSqlServer())
            return await _dbContext.SecretRevealRecords
                .FromSqlInterpolated(
                    $"SELECT * FROM dbo.SecretRevealRecords WITH (UPDLOCK, ROWLOCK) WHERE HandleDigest = {handleDigest}")
                .SingleOrDefaultAsync(cancellationToken);

        return await _dbContext.SecretRevealRecords
            .SingleOrDefaultAsync(r => r.HandleDigest == handleDigest, cancellationToken);
    }

    private static bool IsDigestCollision(DbUpdateException ex)
    {
        if (FieldSales.Identity.Services.Validation.UniqueConstraintViolationDetector.IsUniqueConstraintViolation(ex)) return true;
        // Retain the supported fake-store collision signal used by provider-independent tests.
        string message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("DuplicateKeyException", StringComparison.OrdinalIgnoreCase);
    }
}