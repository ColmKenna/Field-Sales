using System.Security.Claims;
using System.Text.Json;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class TerritoryAssignmentStore(DirectoryDbContext db, CoverageReadStore access,
    CoverageOwnershipReader ownership, CoverageStaffProvider staff, AssignmentHistoryWriter history, TimeProvider clock, AssignmentPreviewProofs proofs)
{
    public async Task<AssignmentImpactDetails> PreviewAddAsync(ClaimsPrincipal actor, AddTerritoryAssignmentRequest request, CancellationToken ct)
    {
        var (candidate, identities) = await PrepareAddAsync(actor, request, ct);
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            await ValidateAddAsync(actor, candidate, ct);
            var inputs = await AssignmentImpactInputs.ReadAsync(db, ownership, ct);
            return await PreviewAsync(actor, inputs, identities, candidate, true, AddCommand(request), ct);
        }, ct);
    }

    public async Task<AssignmentImpactDetails?> PreviewRemoveAsync(ClaimsPrincipal actor, Guid id, RemoveTerritoryAssignmentRequest request, CancellationToken ct)
    {
        var prepared = await PrepareRemoveAsync(actor, id, request, ct);
        if (prepared is null) return null;
        return await GeographyTransactions.RunAsync<AssignmentImpactDetails?>(db, async () =>
        {
            var assignment = await db.TerritoryAssignments.SingleOrDefaultAsync(row => row.Id == id, ct);
            if (assignment is null) return null;
            await access.RequireRepAccessAsync(actor, assignment.RepSubject, ct);
            if (!assignment.Version.SequenceEqual(Version(request.Version))) throw new DbUpdateConcurrencyException();
            var inputs = await AssignmentImpactInputs.ReadAsync(db, ownership, ct);
            return await PreviewAsync(actor, inputs, prepared, assignment, false, RemoveCommand(id, request.Version, request.Reason), ct);
        }, ct);
    }

    public async Task<TerritoryAssignmentDetails> AddAsync(ClaimsPrincipal actor, AddTerritoryAssignmentRequest request, CancellationToken ct)
    {
        var (candidate, identities) = await PrepareAddAsync(actor, request, ct);
        Guid operation = Guid.NewGuid();
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            await ValidateAddAsync(actor, candidate, ct);
            var inputs = await AssignmentImpactInputs.ReadAsync(db, ownership, ct);
            string command = AddCommand(request);
            if (!proofs.Matches(request.PreviewProof, request.Confirmed, actor.GetStaffSubject()!, command, Fingerprint(inputs, identities, actor)))
                throw new CoveragePreviewChangedException(await PreviewAsync(actor, inputs, identities, candidate, true, command, ct));
            var before = inputs.Owners();
            db.TerritoryAssignments.Add(candidate);
            await db.SaveChangesAsync(ct);
            var after = await ownership.ReadAllAsync(ct);
            history.AppendChanges(before, after, new(actor.GetStaffSubject()!, identities.Values), operation,
                Cause(candidate.Target), clock.GetUtcNow(), request.Reason);
            await db.SaveChangesAsync(ct);
            return new TerritoryAssignmentDetails(candidate.Id, candidate.RepSubject, candidate.Target, Convert.ToBase64String(candidate.Version));
        }, ct);
    }

    public async Task<CoverageMutationResult?> RemoveAsync(ClaimsPrincipal actor, Guid id, RemoveTerritoryAssignmentRequest request, CancellationToken ct)
    {
        var identities = await PrepareRemoveAsync(actor, id, request, ct);
        if (identities is null) return null;
        var version = Version(request.Version);
        Guid operation = Guid.NewGuid();
        return await GeographyTransactions.RunAsync<CoverageMutationResult?>(db, async () =>
        {
            var assignment = await db.TerritoryAssignments.SingleOrDefaultAsync(row => row.Id == id, ct);
            if (assignment is null) return null;
            await access.RequireRepAccessAsync(actor, assignment.RepSubject, ct);
            // Preserve malformed/stale rowversion denial; a valid reviewed stale
            // removal can instead return its refreshed version and impact.
            if (!assignment.Version.SequenceEqual(version) && request.PreviewProof is null) throw new DbUpdateConcurrencyException();
            var inputs = await AssignmentImpactInputs.ReadAsync(db, ownership, ct);
            if (!proofs.Matches(request.PreviewProof, request.Confirmed, actor.GetStaffSubject()!, RemoveCommand(id, request.Version, request.Reason),
                Fingerprint(inputs, identities, actor)))
                throw new CoveragePreviewChangedException(await PreviewAsync(actor, inputs, identities, assignment, false,
                    RemoveCommand(id, Convert.ToBase64String(assignment.Version), request.Reason), ct));
            if (!assignment.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
            var before = inputs.Owners();
            db.TerritoryAssignments.Remove(assignment);
            await db.SaveChangesAsync(ct);
            var after = await ownership.ReadAllAsync(ct);
            int changed = history.AppendChanges(before, after, new(actor.GetStaffSubject()!, identities.Values), operation,
                Cause(assignment.Target), clock.GetUtcNow(), request.Reason);
            await db.SaveChangesAsync(ct);
            return new(true, changed);
        }, ct);
    }

    private async Task<(TerritoryAssignment Candidate, IReadOnlyDictionary<string, StaffDirectoryEntry> Identities)> PrepareAddAsync(
        ClaimsPrincipal actor, AddTerritoryAssignmentRequest request, CancellationToken ct)
    {
        if (request.Target is null) throw new CoverageValidationException("Target", "Choose a territory or location.");
        var candidate = TerritoryAssignment.Create(request.RepSubject, request.Target);
        ValidateReason(request.Reason);
        var identities = await PrepareAsync(candidate.Target, candidate.RepSubject, actor, ct);
        CoverageStaffProvider.RequireEligible(identities, candidate.RepSubject, BusinessRoles.FieldSalesperson, "RepSubject");
        return (candidate, identities);
    }

    private async Task<IReadOnlyDictionary<string, StaffDirectoryEntry>?> PrepareRemoveAsync(ClaimsPrincipal actor,
        Guid id, RemoveTerritoryAssignmentRequest request, CancellationToken ct)
    {
        var candidate = await db.TerritoryAssignments.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct);
        if (candidate is null) return null;
        ValidateReason(request.Reason); Version(request.Version);
        var identities = await PrepareAsync(candidate.Target, candidate.RepSubject, actor, ct);
        CoverageStaffProvider.RequireEligible(identities, candidate.RepSubject, BusinessRoles.FieldSalesperson, "RepSubject");
        return identities;
    }

    private async Task ValidateAddAsync(ClaimsPrincipal actor, TerritoryAssignment candidate, CancellationToken ct)
    {
        await access.RequireRepAccessAsync(actor, candidate.RepSubject, ct);
        await RequireActiveTargetAsync(candidate.Target, ct);
        var existing = await ForTarget(candidate.Target).SingleOrDefaultAsync(ct);
        if (existing is not null)
        {
            await access.RequireRepAccessAsync(actor, existing.RepSubject, ct);
            throw new CoverageConflictException(existing.RepSubject == candidate.RepSubject
                ? "This rep already holds this assignment."
                : "This area is already assigned. Choose it in Add assignment to review a transfer.");
        }
    }

    private async Task<AssignmentImpactDetails> PreviewAsync(ClaimsPrincipal actor, AssignmentImpactInputs inputs,
        IReadOnlyDictionary<string, StaffDirectoryEntry> identities, TerritoryAssignment assignment, bool adding, string command, CancellationToken ct)
    {
        var proposed = adding ? inputs.Assignments.Append(assignment) : inputs.Assignments.Where(row => row.Id != assignment.Id);
        var changes = AssignmentImpactPreview.Calculate(inputs.Locations, inputs.Assignments, proposed);
        ImpactOwnerDetails? Owner(EffectiveOwner? value) => value is null ? null
            : new(new(value.RepSubject, CoverageStaffProvider.Find(identities, value.RepSubject).DisplayName), value.Source.Target, value.Source.Name);
        var groups = changes.GroupBy(change => (Previous: change.Previous?.RepSubject, Next: change.Next?.RepSubject))
            .Select(group =>
            {
                var locations = group.Select(change => new ImpactLocationDetails(change.Location.LocationId,
                    change.Location.LocationName, Owner(change.Previous), Owner(change.Next))).ToArray();
                string count = locations.Length == 1 ? "1 Location" : $"{locations.Length} Locations";
                string become = locations.Length == 1 ? "becomes" : "become";
                string move = locations.Length == 1 ? "moves" : "move";
                string sentence = group.Key.Next is null ? $"{count} {become} Unassigned"
                    : group.Key.Previous is null ? $"{count} {become} {locations[0].NewOwner!.Rep.Name}'s"
                    : $"{count} {move} from {locations[0].PreviousOwner!.Rep.Name} to {locations[0].NewOwner!.Rep.Name}";
                return new AssignmentImpactGroup(group.Key.Previous, group.Key.Next, sentence, locations);
            }).OrderByDescending(group => group.Locations.Count).ThenBy(group => group.Sentence, StringComparer.Ordinal).ToArray();
        string name = assignment.Target.Level switch
        {
            TerritoryLevel.Region => await db.Regions.Where(row => row.Id == assignment.Target.UnitId).Select(row => row.Name).SingleAsync(ct),
            TerritoryLevel.County => await db.Counties.Where(row => row.Id == assignment.Target.UnitId).Select(row => row.Name).SingleAsync(ct),
            TerritoryLevel.Town => await db.Towns.Where(row => row.Id == assignment.Target.UnitId).Select(row => row.Name).SingleAsync(ct),
            _ => await db.Locations.Where(row => row.Id == assignment.Target.UnitId).Select(row => row.Name).SingleAsync(ct)
        };
        return new(proofs.Issue(actor.GetStaffSubject()!, command, Fingerprint(inputs, identities, actor)), adding ? "Add" : "Remove", name,
            CoverageStaffProvider.Find(identities, assignment.RepSubject).DisplayName, changes.Count, groups,
            adding ? null : Convert.ToBase64String(assignment.Version));
    }

    private static string Fingerprint(AssignmentImpactInputs inputs, IReadOnlyDictionary<string, StaffDirectoryEntry> identities, ClaimsPrincipal actor) =>
        inputs.Fingerprint(identities, actor.IsInRole(BusinessRoles.HeadOfficeUser) ? BusinessRoles.HeadOfficeUser : BusinessRoles.SalesManager);
    private static string AddCommand(AddTerritoryAssignmentRequest request) => JsonSerializer.Serialize(new
        { Action = "Add", request.RepSubject, request.Target, Reason = request.Reason?.Trim() });
    private static string RemoveCommand(Guid id, string? version, string? reason) => JsonSerializer.Serialize(new
        { Action = "Remove", Id = id, Version = version, Reason = reason?.Trim() });

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
