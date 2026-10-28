using System.Data;
using System.Security.Claims;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

// Rep permissions (M-14). Any Sales Manager or Head Office User may view a rep; RestrictionPermissionRule
// decides who may change. Identity requests run before any SQL transaction is opened.
public sealed class RestrictionPermissionStore(CatalogueDbContext catalogue, DirectoryDbContext directory,
    CoverageStaffProvider staff, RestrictionPermissionWriter writer, TimeProvider clock)
{
    public async Task<IReadOnlyList<PermissionRepChoice>> RepsAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var people = await staff.ListAsync(ct);
        var holders = (await catalogue.RestrictionPermissions.AsNoTracking().Select(row => row.RepSubject).Distinct()
            .ToArrayAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var managers = await directory.RepReportingLines.AsNoTracking()
            .ToDictionaryAsync(row => row.RepSubject, row => row.ManagerSubject, StringComparer.Ordinal, ct);
        var (subject, roles) = Actor(actor);
        // Reps who still hold a permission stay listed after they stop being active, so it can be removed.
        return people.Values.Where(entry => IsActiveRep(entry) || holders.Contains(entry.Subject))
            .Select(entry => new PermissionRepChoice(new(entry.Subject, entry.DisplayName), IsActiveRep(entry),
                RestrictionPermissionRule.CanChange(subject, roles, entry.Subject, managers.GetValueOrDefault(entry.Subject))))
            .OrderBy(choice => choice.Rep.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.Rep.Subject, StringComparer.Ordinal).ToArray();
    }

    public async Task<RepPermissionsPage?> ReadAsync(ClaimsPrincipal actor, string rep, CancellationToken ct)
    {
        rep = CoverageSubjects.Validate(rep, "RepSubject");
        var person = CoverageStaffProvider.Find(await staff.LookupAsync([rep], ct), rep);
        string? manager = await GeographyTransactions.RunAsync(directory, () => ManagerAsync(rep, ct), ct);
        var (subject, roles) = Actor(actor);
        bool canChange = RestrictionPermissionRule.CanChange(subject, roles, rep, manager);
        bool active = IsActiveRep(person);
        return await CatalogueTransactions.RunAsync<RepPermissionsPage?>(catalogue, IsolationLevel.Serializable, async () =>
        {
            var held = (await catalogue.RestrictionPermissions.AsNoTracking().Where(row => row.RepSubject == rep)
                .Select(row => row.RestrictionGroupId).ToArrayAsync(ct)).ToHashSet();
            var changes = await catalogue.RestrictionPermissionChanges.AsNoTracking().Where(row => row.RepSubject == rep)
                .OrderByDescending(row => row.Sequence).ToArrayAsync(ct);
            // Only reps, or people with a permission record, have a permissions page.
            if (!person.Roles.Contains(BusinessRoles.FieldSalesperson, StringComparer.Ordinal) && changes.Length == 0) return null;
            var lastChanges = changes.GroupBy(row => row.RestrictionGroupId).ToDictionary(group => group.Key, group => group.First());
            var groups = await catalogue.RestrictionGroups.AsNoTracking().ToArrayAsync(ct);
            // Archived groups grant nothing, so only those the rep still holds are listed (with Remove only).
            var rows = groups.Where(group => !group.IsArchived || held.Contains(group.Id))
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ThenBy(group => group.Id)
                .Select(group =>
                {
                    bool granted = held.Contains(group.Id);
                    var last = lastChanges.TryGetValue(group.Id, out var change) ? RestrictionPermissionFormatter.Details(change) : null;
                    return new RepPermissionGroup(group.Id, group.Name, group.IsArchived, granted, last,
                        CanGrant: canChange && active && !granted && !group.IsArchived, CanRemove: canChange && granted);
                }).ToArray();
            return new RepPermissionsPage(new(person.Subject, person.DisplayName), active, canChange, rows);
        }, ct);
    }

    /// <returns>The recorded change, or null when the Restriction Group does not exist.</returns>
    public async Task<RestrictionPermissionChangeDetails?> ChangeAsync(ClaimsPrincipal actor, string rep, Guid groupId,
        ChangeRestrictionPermissionRequest request, CancellationToken ct)
    {
        rep = CoverageSubjects.Validate(rep, "RepSubject");
        if (request is null || !Enum.IsDefined(request.Change))
            throw new CoverageValidationException("Change", "Choose whether to grant or remove the permission.");
        var people = await staff.LookupAsync([rep], ct);
        var (subject, roles) = Actor(actor);
        var snapshot = new HistoryStaffSnapshot(subject, people.Values);
        // Only an active Field Salesperson can be granted; a permission can be removed from anyone who holds it.
        if (request.Change == RestrictionPermissionChangeKind.Granted)
            CoverageStaffProvider.RequireEligible(people, rep, BusinessRoles.FieldSalesperson, "RepSubject");
        // The reporting line stays locked until the catalogue write commits, so a team change waits for it.
        return await GeographyTransactions.RunAsync<RestrictionPermissionChangeDetails?>(directory, async () =>
        {
            if (!RestrictionPermissionRule.CanChange(subject, roles, rep, await ManagerAsync(rep, ct)))
                throw new CoverageReadForbiddenException();
            var entry = await CatalogueTransactions.RunAsync(catalogue, IsolationLevel.Serializable, async transaction =>
            {
                var saved = await writer.ApplyAsync(request.Change, rep, groupId, request.ExpectedLastChangeId,
                    snapshot, clock.GetUtcNow(), request.Reason, ct);
                if (saved is not null) await transaction.CommitAsync(ct);
                return saved;
            }, ct);
            return entry is null ? null : RestrictionPermissionFormatter.Details(entry);
        }, ct);
    }

    private Task<string?> ManagerAsync(string rep, CancellationToken ct) =>
        directory.RepReportingLines.AsNoTracking().Where(row => row.RepSubject == rep)
            .Select(row => row.ManagerSubject).SingleOrDefaultAsync(ct);

    private static bool IsActiveRep(StaffDirectoryEntry entry) =>
        entry.Available && entry.Roles.Contains(BusinessRoles.FieldSalesperson, StringComparer.Ordinal);

    private static (string Subject, string[] Roles) Actor(ClaimsPrincipal actor) =>
        (actor.GetStaffSubject() ?? throw new CoverageReadForbiddenException(), BusinessRoles.All.Where(actor.IsInRole).ToArray());
}
