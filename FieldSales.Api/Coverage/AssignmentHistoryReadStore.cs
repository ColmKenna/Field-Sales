using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class AssignmentHistoryReadStore(DirectoryDbContext db, CoverageReadStore coverage)
{
    public Task<IReadOnlyList<AssignmentHistoryDetails>?> ForLocationAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct) =>
        GeographyTransactions.RunAsync<IReadOnlyList<AssignmentHistoryDetails>?>(db, async () =>
        {
            if (await coverage.FindLocationInTransactionAsync(actor, id, ct) is null) return null;
            var rows = await db.AssignmentHistory.AsNoTracking().Where(row => row.LocationId == id)
                .OrderByDescending(row => row.Sequence).ToArrayAsync(ct);
            return rows.Select(AssignmentHistoryFormatter.Details).ToArray();
        }, ct);

    public Task<IReadOnlyList<AssignmentHistoryDetails>> ForRepAsync(ClaimsPrincipal actor, string subject, CancellationToken ct) =>
        GeographyTransactions.RunAsync<IReadOnlyList<AssignmentHistoryDetails>>(db, async () =>
        {
            await coverage.RequireRepAccessAsync(actor, subject, ct);
            var rows = await db.AssignmentHistory.AsNoTracking().Where(row => row.PreviousRepSubject == subject || row.NewRepSubject == subject)
                .OrderByDescending(row => row.Sequence).ToArrayAsync(ct);
            return rows.Select(AssignmentHistoryFormatter.Details).ToArray();
        }, ct);
}
