using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class CoverageReadStore(DirectoryDbContext db, CoverageOwnershipReader ownership)
{
    public Task<IReadOnlyList<AssignedTerritoryDetails>> ListAssignmentsAsync(
        ClaimsPrincipal actor, string repSubject, CancellationToken ct = default) =>
        GeographyTransactions.RunAsync<IReadOnlyList<AssignedTerritoryDetails>>(db, async () =>
        {
            await RequireRepAccessAsync(actor, repSubject, ct);
            var rows = await (from assignment in db.TerritoryAssignments.AsNoTracking()
                where assignment.RepSubject == repSubject
                join region in db.Regions on assignment.RegionId equals region.Id into regions
                from region in regions.DefaultIfEmpty()
                join county in db.Counties on assignment.CountyId equals county.Id into counties
                from county in counties.DefaultIfEmpty()
                join town in db.Towns on assignment.TownId equals town.Id into towns
                from town in towns.DefaultIfEmpty()
                join location in db.Locations on assignment.LocationId equals location.Id into locations
                from location in locations.DefaultIfEmpty()
                select new AssignmentName(assignment, assignment.RegionId != null ? region.Name
                    : assignment.CountyId != null ? county.Name
                    : assignment.TownId != null ? town.Name : location.Name)).ToArrayAsync(ct);
            return rows.Select(row => new AssignedTerritoryDetails(new(row.Assignment.Id,
                    row.Assignment.RepSubject, row.Assignment.Target, Convert.ToBase64String(row.Assignment.Version)), row.Name))
                .OrderBy(row => row.Assignment.Target.Level)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Assignment.Target.UnitId).ToArray();
        }, ct);

    public Task<IReadOnlyList<LocationCoverageDetails>> ListLocationsAsync(
        ClaimsPrincipal actor, string repSubject, CancellationToken ct = default) =>
        GeographyTransactions.RunAsync<IReadOnlyList<LocationCoverageDetails>>(db, async () =>
        {
            await RequireRepAccessAsync(actor, repSubject, ct);
            // Include every rep's assignments: a narrower assignment belonging to
            // another rep must still carve its Locations out of this rep's list.
            var paths = await ownership.Paths().ToArrayAsync(ct);
            var assignments = await db.TerritoryAssignments.AsNoTracking().ToArrayAsync(ct);
            var resolver = new EffectiveOwnerResolver(assignments);
            return paths.Select(path => Details(path, resolver.Resolve(path)))
                .Where(location => string.Equals(location.Owner?.RepSubject, repSubject, StringComparison.Ordinal))
                .OrderBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.LocationId).ToArray();
        }, ct);

    public Task<LocationCoverageDetails?> FindLocationAsync(
        ClaimsPrincipal actor, Guid id, CancellationToken ct = default) =>
        GeographyTransactions.RunAsync(db, () => FindLocationInTransactionAsync(actor, id, ct), ct);

    internal async Task<LocationCoverageDetails?> FindLocationInTransactionAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct)
    {
        var path = await ownership.Paths(id).SingleOrDefaultAsync(ct);
        if (path is null) return null;
        var assignments = await db.TerritoryAssignments.AsNoTracking().Where(assignment =>
            assignment.LocationId == path.LocationId || assignment.TownId == path.TownId
            || assignment.CountyId == path.CountyId || assignment.RegionId == path.RegionId).ToArrayAsync(ct);
        var owner = new EffectiveOwnerResolver(assignments).Resolve(path);
        if (owner is null)
        {
            if (!IsHeadOffice(actor)) throw new CoverageReadForbiddenException();
        }
        else await RequireRepAccessAsync(actor, owner.RepSubject, ct);
        return Details(path, owner);
    }

    // Both the team check and the coverage snapshot run in the same serializable
    // transaction. Reporting-line changes take effect on the next request.
    internal async Task RequireRepAccessAsync(ClaimsPrincipal actor, string repSubject, CancellationToken ct)
    {
        if (IsHeadOffice(actor)) return;
        string? subject = actor.GetStaffSubject();
        if (!actor.IsInRole(BusinessRoles.SalesManager) || string.IsNullOrWhiteSpace(subject)
            || !await db.RepReportingLines.AsNoTracking().AnyAsync(line =>
                line.RepSubject == repSubject && line.ManagerSubject == subject, ct))
            throw new CoverageReadForbiddenException();
    }

    private static bool IsHeadOffice(ClaimsPrincipal actor) =>
        !string.IsNullOrWhiteSpace(actor.GetStaffSubject()) && actor.IsInRole(BusinessRoles.HeadOfficeUser);

    private static LocationCoverageDetails Details(LocationOwnershipPath path, EffectiveOwner? owner) =>
        new(path.LocationId, path.LocationName, owner is null ? null
            : new(owner.RepSubject, new(owner.Source.AssignmentId, owner.Source.Target, owner.Source.Name)));

    private sealed record AssignmentName(TerritoryAssignment Assignment, string Name);
}

public sealed class CoverageReadForbiddenException : Exception;
