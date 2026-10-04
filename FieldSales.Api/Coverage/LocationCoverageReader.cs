using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

// This shop-level read deliberately does not grant access to rep books or writes.
// Existing CoverageReadStore/AssignmentHistoryReadStore team checks stay intact.
public sealed class LocationCoverageReader(DirectoryDbContext db, CoverageOwnershipReader ownership,
    CoverageStaffProvider staff)
{
    public async Task<LocationCoveragePage?> ReadAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct)
    {
        RequireManager(actor);
        // Fetch trusted labels before SQL locks. Resolve against the authoritative
        // transaction snapshot, so a concurrent assignment cannot use a stale owner.
        var people = await staff.ListAsync(ct);
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            var path = await ownership.Paths(id).SingleOrDefaultAsync(ct);
            if (path is null) return null;
            var paths = await ownership.Paths(townId: path.TownId).ToArrayAsync(ct);
            var assignments = await db.TerritoryAssignments.AsNoTracking().Where(row =>
                row.RegionId == path.RegionId || row.CountyId == path.CountyId || row.TownId == path.TownId
                || row.LocationId != null && db.Locations.Any(location => location.Id == row.LocationId && location.TownId == path.TownId)).ToArrayAsync(ct);
            var resolver = new EffectiveOwnerResolver(assignments);
            var owner = resolver.Resolve(path);
            ImpactOwnerDetails? details = null;
            if (owner is not null)
            {
                var person = CoverageStaffProvider.Find(people, owner.RepSubject);
                details = new(new(person.Subject, person.DisplayName), owner.Source.Target, owner.Source.Name);
            }
            return new LocationCoveragePage(path.LocationId, path.LocationName, path.TownId, path.TownName,
                details, paths.Count(row => row.LocationId != id && resolver.Resolve(row) is null));
        }, ct);
    }

    public Task<LocationCoverageHistoryPage?> HistoryAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct)
    {
        RequireManager(actor);
        // History is entirely captured data: current staff labels/availability
        // must not erase the record or require another identity directory lookup.
        return GeographyTransactions.RunAsync(db, async () =>
        {
            var path = await ownership.Paths(id).SingleOrDefaultAsync(ct);
            if (path is null) return null;
            var rows = await db.AssignmentHistory.AsNoTracking().Where(row => row.LocationId == id)
                .OrderByDescending(row => row.Sequence).ToArrayAsync(ct);
            return new LocationCoverageHistoryPage(id, path.LocationName, path.TownName,
                rows.Select(AssignmentHistoryFormatter.Details).ToArray());
        }, ct);
    }

    private static void RequireManager(ClaimsPrincipal actor)
    {
        if (string.IsNullOrWhiteSpace(actor.GetStaffSubject())
            || !actor.IsInRole(BusinessRoles.SalesManager) && !actor.IsInRole(BusinessRoles.HeadOfficeUser))
            throw new CoverageReadForbiddenException();
    }
}
