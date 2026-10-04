using System.Security.Claims;
using System.Text.Json;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class AssignmentTransferStore(DirectoryDbContext db, CoverageReadStore access, CoverageOwnershipReader ownership,
    CoverageStaffProvider staff, AssignmentHistoryWriter history, AssignmentPreviewProofs proofs, TimeProvider clock)
{
    public async Task<TransferReview> ReviewAsync(ClaimsPrincipal actor, string source, CancellationToken ct)
    {
        source = CoverageSubjects.Validate(source, "SourceRepSubject");
        await access.RequireRepAccessAsync(actor, source, ct);
        var identities = await staff.ListAsync(ct);
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            await access.RequireRepAccessAsync(actor, source, ct);
            StaffChoice Person(string subject) => new(subject, CoverageStaffProvider.Find(identities, subject).DisplayName);
            var lines = await db.RepReportingLines.AsNoTracking().ToArrayAsync(ct);
            var team = lines.Where(row => row.ManagerSubject == actor.GetStaffSubject()).Select(row => row.RepSubject).ToArray();
            var recipients = identities.Values.Where(row => row.Subject != source && row.Available && row.Roles.Contains(BusinessRoles.FieldSalesperson)
                && lines.Any(line => line.RepSubject == row.Subject)
                && (actor.IsInRole(BusinessRoles.HeadOfficeUser) || team.Contains(row.Subject)))
                .Select(row => Person(row.Subject)).OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var assignments = await db.TerritoryAssignments.AsNoTracking().ToArrayAsync(ct);
            var geography = await GeographyAsync(ct); var paths = await ownership.Paths().ToArrayAsync(ct);
            int Count(TerritoryTarget target) => paths.Count(path => target.Level switch
            {
                TerritoryLevel.Region => path.RegionId == target.UnitId, TerritoryLevel.County => path.CountyId == target.UnitId,
                TerritoryLevel.Town => path.TownId == target.UnitId, _ => path.LocationId == target.UnitId
            });
            TransferScope Scope(TerritoryAssignment assignment, TransferUnit unit, bool root)
            {
                var held = root ? null : assignments.SingleOrDefault(row => row.Target == unit.Target);
                var children = root && unit.Target.Level is TerritoryLevel.Region or TerritoryLevel.County
                    ? geography.Where(row => row.Parent == unit.Target).OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(row => Scope(assignment, row, false)).ToArray() : [];
                var names = new List<string>(); var ancestor = unit.Parent;
                while (ancestor is not null) { var next = geography.Single(row => row.Target == ancestor); names.Add(next.Name); ancestor = next.Parent; }
                return new(new(assignment.Id, unit.Target), unit.Name, unit.Archived, Count(unit.Target), held is null ? null : Person(held.RepSubject), children, string.Join(" · ", names));
            }
            return new TransferReview(Person(source), recipients, assignments.Where(row => row.RepSubject == source)
                .Select(row => Scope(row, geography.Single(unit => unit.Target == row.Target), true))
                .OrderBy(row => row.Selection.Target.Level).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }, ct);
    }

    public Task<AssignmentImpactDetails> PreviewAsync(ClaimsPrincipal actor, TransferAssignmentsRequest request, CancellationToken ct) =>
        ExecuteAsync(actor, request, false, ct);
    public Task<AssignmentImpactDetails> SaveAsync(ClaimsPrincipal actor, TransferAssignmentsRequest request, CancellationToken ct) =>
        ExecuteAsync(actor, request, true, ct);

    private async Task<AssignmentImpactDetails> ExecuteAsync(ClaimsPrincipal actor, TransferAssignmentsRequest request, bool save, CancellationToken ct)
    {
        string source = CoverageSubjects.Validate(request.SourceRepSubject, "SourceRepSubject");
        string recipient = CoverageSubjects.Validate(request.ReceivingRepSubject, "ReceivingRepSubject");
        if (request.Selections is not { Count: > 0 and <= 10000 } || request.Selections.Any(row => row is null || row.Target is null))
            throw new CoverageValidationException("Selections", "Select at least one assignment or area.");
        if (request.Reason?.Trim().Length > 1000 || request.Reason?.Any(char.IsControl) == true)
            throw new CoverageValidationException("Reason", "Enter a reason of up to 1000 characters.");
        await access.RequireRepAccessAsync(actor, source, ct); await access.RequireRepAccessAsync(actor, recipient, ct);
        var identities = await staff.ListAsync(ct);
        CoverageStaffProvider.Find(identities, source);
        CoverageStaffProvider.RequireEligible(identities, recipient, BusinessRoles.FieldSalesperson, "ReceivingRepSubject");
        var selections = request.Selections.Distinct().OrderBy(row => row.AssignmentId).ThenBy(row => row.Target.Level).ThenBy(row => row.Target.UnitId).ToArray();
        string command = JsonSerializer.Serialize(new { Action = "Transfer", Source = source, Recipient = recipient, Selections = selections, Reason = request.Reason?.Trim() });
        Guid operation = Guid.NewGuid();
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            await access.RequireRepAccessAsync(actor, source, ct); await access.RequireRepAccessAsync(actor, recipient, ct);
            if (!await db.RepReportingLines.AnyAsync(row => row.RepSubject == recipient, ct))
                throw new CoverageValidationException("ReceivingRepSubject", "Set this rep's reporting line before transferring assignments.");
            var inputs = await AssignmentImpactInputs.ReadAsync(db, ownership, ct);
            var geography = await GeographyAsync(ct);
            var plan = AssignmentTransferPlanner.Calculate(geography, inputs.Assignments, source, recipient, selections,
                CoverageStaffProvider.Find(identities, recipient).DisplayName);
            var changes = AssignmentImpactPreview.Calculate(inputs.Locations, inputs.Assignments, plan.Assignments);
            string fingerprint = inputs.Fingerprint(identities, actor.IsInRole(BusinessRoles.HeadOfficeUser) ? BusinessRoles.HeadOfficeUser : BusinessRoles.SalesManager);
            var preview = new AssignmentImpactDetails(proofs.Issue(actor.GetStaffSubject()!, command, fingerprint), "Transfer",
                "Selected assignments", CoverageStaffProvider.Find(identities, recipient).DisplayName, changes.Count,
                Describe(changes, identities), TransferNotices: plan.Notices,
                TransferredAssignments: plan.Assignments.Where(row => !inputs.Assignments.Any(old => old.Id == row.Id && old.RepSubject == row.RepSubject))
                    .Select(row => $"{geography.Single(unit => unit.Target == row.Target).Name} ({row.Target.Level})").Order(StringComparer.Ordinal).ToArray());
            if (!save) return preview;
            if (!proofs.Matches(request.PreviewProof, request.Confirmed, actor.GetStaffSubject()!, command, fingerprint))
                throw new CoveragePreviewChangedException(preview);
            var tracked = await db.TerritoryAssignments.ToDictionaryAsync(row => row.Id, ct);
            foreach (var row in plan.Assignments)
                if (tracked.TryGetValue(row.Id, out var existing))
                { if (existing.RepSubject != row.RepSubject) existing.TransferTo(row.RepSubject); }
                else db.TerritoryAssignments.Add(row);
            await db.SaveChangesAsync(ct);
            var after = await ownership.ReadAllAsync(ct);
            var before = inputs.Owners(); var identitySnapshot = new HistoryStaffSnapshot(actor.GetStaffSubject()!, identities.Values);
            var changedAt = clock.GetUtcNow();
            foreach (var change in changes)
                history.Append(change.Location.LocationId, change.Location.LocationName, before.Owners[change.Location.LocationId],
                    after.Owners[change.Location.LocationId], identitySnapshot, operation,
                    change.Next!.Source.Target.Level == TerritoryLevel.Location ? OwnershipChangeCause.DirectLocationAssignment : OwnershipChangeCause.TerritoryAssignment,
                    changedAt, request.Reason);
            await db.SaveChangesAsync(ct);
            return preview;
        }, ct);
    }

    private static AssignmentImpactGroup[] Describe(IReadOnlyList<AssignmentOwnershipChange> changes, IReadOnlyDictionary<string, StaffDirectoryEntry> identities)
    {
        ImpactOwnerDetails? Owner(EffectiveOwner? owner) => owner is null ? null : new(new(owner.RepSubject,
            CoverageStaffProvider.Find(identities, owner.RepSubject).DisplayName), owner.Source.Target, owner.Source.Name);
        return changes.GroupBy(row => (Previous: row.Previous!.RepSubject, Next: row.Next!.RepSubject)).Select(group =>
        {
            var locations = group.Select(row => new ImpactLocationDetails(row.Location.LocationId, row.Location.LocationName, Owner(row.Previous), Owner(row.Next))).ToArray();
            string count = locations.Length == 1 ? "1 Location moves" : $"{locations.Length} Locations move";
            return new AssignmentImpactGroup(group.Key.Previous, group.Key.Next,
                $"{count} from {locations[0].PreviousOwner!.Rep.Name} to {locations[0].NewOwner!.Rep.Name}", locations);
        }).OrderByDescending(row => row.Locations.Count).ThenBy(row => row.Sentence, StringComparer.Ordinal).ToArray();
    }

    private async Task<TransferUnit[]> GeographyAsync(CancellationToken ct)
    {
        var regions = await db.Regions.AsNoTracking().ToArrayAsync(ct);
        var counties = await db.Counties.AsNoTracking().ToArrayAsync(ct);
        var towns = await db.Towns.AsNoTracking().ToArrayAsync(ct);
        var locations = await db.Locations.AsNoTracking().ToArrayAsync(ct);
        var regionArchived = regions.ToDictionary(row => row.Id, row => row.IsArchived);
        var countyArchived = counties.ToDictionary(row => row.Id, row => row.IsArchived || regionArchived[row.RegionId]);
        var townArchived = towns.ToDictionary(row => row.Id, row => row.IsArchived || countyArchived[row.CountyId]);
        return regions.Select(row => new TransferUnit(new(TerritoryLevel.Region, row.Id), null, row.Name, row.IsArchived))
            .Concat(counties.Select(row => new TransferUnit(new(TerritoryLevel.County, row.Id), new(TerritoryLevel.Region, row.RegionId), row.Name, countyArchived[row.Id])))
            .Concat(towns.Select(row => new TransferUnit(new(TerritoryLevel.Town, row.Id), new(TerritoryLevel.County, row.CountyId), row.Name, townArchived[row.Id])))
            .Concat(locations.Select(row => new TransferUnit(new(TerritoryLevel.Location, row.Id), new(TerritoryLevel.Town, row.TownId), row.Name, townArchived[row.TownId]))).ToArray();
    }
}
