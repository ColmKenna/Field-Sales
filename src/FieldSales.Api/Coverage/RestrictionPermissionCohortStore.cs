using System.Data;
using System.Security.Claims;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

// The other M-14 view. A cohort uses the existing permission rule and writer; the transaction,
// not a sequence of calls to the single-rep endpoint, makes the action all-or-nothing.
public sealed class RestrictionPermissionCohortStore(CatalogueDbContext catalogue, DirectoryDbContext directory,
    CoverageStaffProvider staff, RestrictionPermissionWriter writer, TimeProvider clock)
{
    public async Task<IReadOnlyList<PermissionGroupChoice>> GroupsAsync(bool includeArchived, CancellationToken ct) =>
        (await catalogue.RestrictionGroups.AsNoTracking().Where(group => !group.IsArchived || includeArchived
            && catalogue.RestrictionPermissions.Any(permission => permission.RestrictionGroupId == group.Id)).ToArrayAsync(ct))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ThenBy(group => group.Id)
            .Select(group => new PermissionGroupChoice(group.Id, group.Name, group.IsArchived)).ToArray();

    public async Task<GroupPermissionsPage?> ReadAsync(ClaimsPrincipal actor, Guid groupId, CancellationToken ct)
    {
        var people = await staff.ListAsync(ct);
        var managers = await directory.RepReportingLines.AsNoTracking()
            .ToDictionaryAsync(row => row.RepSubject, row => row.ManagerSubject, StringComparer.Ordinal, ct);
        var (subject, roles) = Actor(actor);
        return await CatalogueTransactions.RunAsync<GroupPermissionsPage?>(catalogue, IsolationLevel.Serializable, async () =>
        {
            var group = await catalogue.RestrictionGroups.AsNoTracking().SingleOrDefaultAsync(row => row.Id == groupId, ct);
            if (group is null) return null;
            var held = (await catalogue.RestrictionPermissions.AsNoTracking().Where(row => row.RestrictionGroupId == groupId)
                .Select(row => row.RepSubject).ToArrayAsync(ct)).ToHashSet(StringComparer.Ordinal);
            var changes = await catalogue.RestrictionPermissionChanges.AsNoTracking().Where(row => row.RestrictionGroupId == groupId)
                .OrderByDescending(row => row.Sequence).ToArrayAsync(ct);
            var lastChanges = changes.GroupBy(row => row.RepSubject, StringComparer.Ordinal)
                .ToDictionary(rows => rows.Key, rows => rows.First(), StringComparer.Ordinal);
            var rows = people.Values.Where(person => IsActiveRep(person) || held.Contains(person.Subject))
                .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(person => person.Subject, StringComparer.Ordinal)
                .Select(person =>
                {
                    bool granted = held.Contains(person.Subject);
                    var last = lastChanges.TryGetValue(person.Subject, out var change) ? RestrictionPermissionFormatter.Details(change) : null;
                    bool canChange = RestrictionPermissionRule.CanChange(subject, roles, person.Subject, managers.GetValueOrDefault(person.Subject));
                    return new GroupPermissionRep(new(person.Subject, person.DisplayName), IsActiveRep(person), granted, last,
                        CanGrant: canChange && !group.IsArchived && IsActiveRep(person) && !granted,
                        CanRemove: canChange && granted);
                }).ToArray();
            return new GroupPermissionsPage(new(group.Id, group.Name, group.IsArchived), rows);
        }, ct);
    }

    public async Task<CohortPermissionGrantResult?> GrantAsync(ClaimsPrincipal actor, Guid groupId,
        GrantCohortPermissionRequest request, CancellationToken ct) =>
        await ChangeAsync(actor, groupId, request?.Reps, request?.Reason, RestrictionPermissionChangeKind.Granted, ct) is { } changes
            ? new(groupId, changes) : null;

    public async Task<CohortPermissionRemovalResult?> RemoveAsync(ClaimsPrincipal actor, Guid groupId,
        RemoveCohortPermissionRequest request, CancellationToken ct) =>
        await ChangeAsync(actor, groupId, request?.Reps, request?.Reason, RestrictionPermissionChangeKind.Removed, ct) is { } changes
            ? new(groupId, changes) : null;

    private async Task<IReadOnlyList<RestrictionPermissionChangeDetails>?> ChangeAsync(ClaimsPrincipal actor, Guid groupId,
        IReadOnlyList<CohortPermissionSelection>? selected, string? reason, RestrictionPermissionChangeKind change, CancellationToken ct)
    {
        if (selected is not { Count: > 0 } || selected.Any(row => row is null))
            throw new CoverageValidationException("Reps", change == RestrictionPermissionChangeKind.Granted
                ? "Select at least one rep to grant the permission to." : "Select at least one rep to remove the permission from.");
        var selections = selected.OrderBy(row => row.RepSubject, StringComparer.Ordinal).ToArray();
        foreach (var row in selections) CoverageSubjects.Validate(row.RepSubject, "Reps");
        string[] reps = selections.Select(row => row.RepSubject).ToArray();
        if (reps.Distinct(StringComparer.Ordinal).Count() != reps.Length)
            throw new CoverageValidationException("Reps", "Select each rep only once.");

        // All network reads precede both transactions. These labels are captured by the existing writer.
        var people = await staff.LookupAsync(reps, ct);
        // Inactive/former reps may still hold permissions; only grants require an active salesperson.
        if (change == RestrictionPermissionChangeKind.Granted)
            foreach (string rep in reps) CoverageStaffProvider.RequireEligible(people, rep, BusinessRoles.FieldSalesperson, "Reps");
        var (subject, roles) = Actor(actor);
        var snapshot = new HistoryStaffSnapshot(subject, people.Values);
        return await GeographyTransactions.RunAsync<IReadOnlyList<RestrictionPermissionChangeDetails>?>(directory, async () =>
        {
            // Hold every selected reporting line until the catalogue commit; a team change must wait.
            var managers = await directory.RepReportingLines.AsNoTracking().Where(row => reps.Contains(row.RepSubject))
                .OrderBy(row => row.RepSubject)
                .ToDictionaryAsync(row => row.RepSubject, row => row.ManagerSubject, StringComparer.Ordinal, ct);
            foreach (string rep in reps)
                if (!RestrictionPermissionRule.CanChange(subject, roles, rep, managers.GetValueOrDefault(rep)))
                    throw new CoverageReadForbiddenException();

            return await CatalogueTransactions.RunAsync<IReadOnlyList<RestrictionPermissionChangeDetails>?>(catalogue, IsolationLevel.Serializable, async transaction =>
            {
                var changes = new List<RestrictionPermissionChangeDetails>();
                var changedAt = clock.GetUtcNow();
                foreach (var row in selections)
                {
                    var entry = await writer.ApplyAsync(change, row.RepSubject, groupId,
                        row.ExpectedLastChangeId, snapshot, changedAt, reason, ct);
                    if (entry is null) return null;
                    changes.Add(RestrictionPermissionFormatter.Details(entry));
                }
                await transaction.CommitAsync(ct);
                return changes;
            }, ct);
        }, ct);
    }

    private static bool IsActiveRep(StaffDirectoryEntry person) =>
        person.Available && person.Roles.Contains(BusinessRoles.FieldSalesperson, StringComparer.Ordinal);
    private static (string Subject, string[] Roles) Actor(ClaimsPrincipal actor) =>
        (actor.GetStaffSubject() ?? throw new CoverageReadForbiddenException(), BusinessRoles.All.Where(actor.IsInRole).ToArray());
}
