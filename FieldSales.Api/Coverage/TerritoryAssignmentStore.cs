using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class TerritoryAssignmentStore(DirectoryDbContext db, CoverageReadStore access,
    CoverageOwnershipReader ownership, CoverageStaffProvider staff, AssignmentHistoryWriter history, TimeProvider clock)
{
    public async Task<TerritoryAssignmentDetails> AddAsync(ClaimsPrincipal actor, AddTerritoryAssignmentRequest request, CancellationToken ct)
    {
        if (request.Target is null) throw new CoverageValidationException("Target", "Choose a territory or location.");
        var candidate = TerritoryAssignment.Create(request.RepSubject, request.Target);
        ValidateReason(request.Reason);
        var identities = await PrepareAsync(candidate.Target, candidate.RepSubject, actor, ct);
        CoverageStaffProvider.RequireEligible(identities, candidate.RepSubject, BusinessRoles.FieldSalesperson, "RepSubject");
        Guid operation = Guid.NewGuid();
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            await access.RequireRepAccessAsync(actor, candidate.RepSubject, ct);
            await RequireActiveTargetAsync(candidate.Target, ct);
            var existing = await ForTarget(candidate.Target).SingleOrDefaultAsync(ct);
            if (existing is not null)
            {
                await access.RequireRepAccessAsync(actor, existing.RepSubject, ct);
                throw new CoverageConflictException("This unit already has an assignment. Remove it before assigning another rep.");
            }
            var before = await ownership.ReadAllAsync(ct);
            var assignment = TerritoryAssignment.Create(candidate.RepSubject, candidate.Target);
            db.TerritoryAssignments.Add(assignment);
            await db.SaveChangesAsync(ct);
            var after = await ownership.ReadAllAsync(ct);
            history.AppendChanges(before, after, new(actor.GetStaffSubject()!, identities.Values), operation,
                Cause(candidate.Target), clock.GetUtcNow(), request.Reason);
            await db.SaveChangesAsync(ct);
            return new TerritoryAssignmentDetails(assignment.Id, assignment.RepSubject, assignment.Target, Convert.ToBase64String(assignment.Version));
        }, ct);
    }

    public async Task<CoverageMutationResult?> RemoveAsync(ClaimsPrincipal actor, Guid id, RemoveTerritoryAssignmentRequest request, CancellationToken ct)
    {
        var candidate = await db.TerritoryAssignments.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct);
        if (candidate is null) return null;
        ValidateReason(request.Reason);
        var version = Version(request.Version);
        var identities = await PrepareAsync(candidate.Target, candidate.RepSubject, actor, ct);
        CoverageStaffProvider.RequireEligible(identities, candidate.RepSubject, BusinessRoles.FieldSalesperson, "RepSubject");
        Guid operation = Guid.NewGuid();
        return await GeographyTransactions.RunAsync<CoverageMutationResult?>(db, async () =>
        {
            var assignment = await db.TerritoryAssignments.SingleOrDefaultAsync(row => row.Id == id, ct);
            if (assignment is null) return null;
            await access.RequireRepAccessAsync(actor, assignment.RepSubject, ct);
            if (!assignment.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
            var before = await ownership.ReadAllAsync(ct);
            db.TerritoryAssignments.Remove(assignment);
            await db.SaveChangesAsync(ct);
            var after = await ownership.ReadAllAsync(ct);
            int changed = history.AppendChanges(before, after, new(actor.GetStaffSubject()!, identities.Values), operation,
                Cause(assignment.Target), clock.GetUtcNow(), request.Reason);
            await db.SaveChangesAsync(ct);
            return new(true, changed);
        }, ct);
    }

    private async Task<IReadOnlyDictionary<string, StaffDirectoryEntry>> PrepareAsync(TerritoryTarget target, string rep,
        ClaimsPrincipal actor, CancellationToken ct)
    {
        // Deny a foreign team's subject before looking up its labels. Recheck under
        // the write transaction, so a reporting-line change cannot grant stale access.
        await access.RequireRepAccessAsync(actor, rep, ct);
        var snapshot = await ownership.ReadAllAsync(ct);
        var keys = snapshot.Locations.Where(path => Applies(target, path)).SelectMany(path => new[]
        {
            new TerritoryTarget(TerritoryLevel.Region, path.RegionId), new(TerritoryLevel.County, path.CountyId),
            new(TerritoryLevel.Town, path.TownId), new(TerritoryLevel.Location, path.LocationId)
        }).ToHashSet();
        // Include suppressed parent assignments: removal may expose their reps.
        var assignments = await db.TerritoryAssignments.AsNoTracking().ToArrayAsync(ct);
        var subjects = assignments.Where(row => keys.Contains(row.Target)).Select(row => row.RepSubject)
            .Append(rep).Distinct(StringComparer.Ordinal).ToArray();
        return await staff.LookupAsync(subjects, ct);
    }

    private static bool Applies(TerritoryTarget target, LocationOwnershipPath path) => target.Level switch
    {
        TerritoryLevel.Region => path.RegionId == target.UnitId,
        TerritoryLevel.County => path.CountyId == target.UnitId,
        TerritoryLevel.Town => path.TownId == target.UnitId,
        TerritoryLevel.Location => path.LocationId == target.UnitId,
        _ => false
    };
    private IQueryable<TerritoryAssignment> ForTarget(TerritoryTarget target) => target.Level switch
    {
        TerritoryLevel.Region => db.TerritoryAssignments.Where(row => row.RegionId == target.UnitId),
        TerritoryLevel.County => db.TerritoryAssignments.Where(row => row.CountyId == target.UnitId),
        TerritoryLevel.Town => db.TerritoryAssignments.Where(row => row.TownId == target.UnitId),
        TerritoryLevel.Location => db.TerritoryAssignments.Where(row => row.LocationId == target.UnitId),
        _ => throw new CoverageValidationException("Target.Level", "Choose a territory level.")
    };
    private async Task RequireActiveTargetAsync(TerritoryTarget target, CancellationToken ct)
    {
        bool active = target.Level switch
        {
            TerritoryLevel.Region => await db.Regions.AnyAsync(row => row.Id == target.UnitId && !row.IsArchived, ct),
            TerritoryLevel.County => await (from county in db.Counties join region in db.Regions on county.RegionId equals region.Id
                where county.Id == target.UnitId && !county.IsArchived && !region.IsArchived select county.Id).AnyAsync(ct),
            TerritoryLevel.Town => await ActiveTowns().AnyAsync(row => row.Id == target.UnitId, ct),
            TerritoryLevel.Location => await (from location in db.Locations join town in ActiveTowns() on location.TownId equals town.Id
                where location.Id == target.UnitId select location.Id).AnyAsync(ct),
            _ => false
        };
        if (!active) throw new CoverageValidationException("Target.UnitId", "Choose an active territory or location.");
    }
    private IQueryable<Town> ActiveTowns() => from town in db.Towns join county in db.Counties on town.CountyId equals county.Id
        join region in db.Regions on county.RegionId equals region.Id
        where !town.IsArchived && !county.IsArchived && !region.IsArchived select town;
    private static OwnershipChangeCause Cause(TerritoryTarget target) => target.Level == TerritoryLevel.Location
        ? OwnershipChangeCause.DirectLocationAssignment : OwnershipChangeCause.TerritoryAssignment;
    internal static byte[] Version(string? value)
    {
        try { var bytes = Convert.FromBase64String(value ?? ""); if (bytes.Length == 8) return bytes; }
        catch (FormatException) { }
        throw new CoverageValidationException("Version", "Reload this page before saving.");
    }
    private static void ValidateReason(string? reason)
    {
        if (reason?.Trim().Length > 1000 || reason?.Any(char.IsControl) == true)
            throw new CoverageValidationException("Reason", "Enter a reason of up to 1000 characters.");
    }
}

public sealed class CoverageConflictException(string message) : Exception(message);
