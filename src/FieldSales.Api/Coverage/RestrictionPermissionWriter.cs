using FieldSales.Api.Catalogue;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

// Appends one grant or removal and keeps the current grants in step. Authorization is the caller's.
public sealed class RestrictionPermissionWriter(CatalogueDbContext db)
{
    public const string ChangedMessage = "This permission changed. Reload before saving.";

    /// <returns>The appended change, or null when the Restriction Group does not exist.</returns>
    public async Task<RestrictionPermissionChange?> ApplyAsync(RestrictionPermissionChangeKind change, string repSubject,
        Guid restrictionGroupId, Guid? expectedLastChangeId, HistoryStaffSnapshot staff, DateTimeOffset changedAt,
        string? reason, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Restriction permission changes require the caller's write transaction.");
        var group = await db.RestrictionGroups.SingleOrDefaultAsync(item => item.Id == restrictionGroupId, ct);
        if (group is null) return null;
        var current = await db.RestrictionPermissions.SingleOrDefaultAsync(item =>
            item.RepSubject == repSubject && item.RestrictionGroupId == restrictionGroupId, ct);
        Guid? lastChangeId = await db.RestrictionPermissionChanges.Where(item =>
                item.RepSubject == repSubject && item.RestrictionGroupId == restrictionGroupId)
            .OrderByDescending(item => item.Sequence).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);
        // The page that asked must still show the current state, and the change must alter it.
        if (lastChangeId != expectedLastChangeId || (change == RestrictionPermissionChangeKind.Granted) == (current is not null))
            throw new CoverageConflictException(ChangedMessage);
        // An archived group's permissions grant nothing (MI-45), so it takes no new grants; removal stays possible.
        if (change == RestrictionPermissionChangeKind.Granted && group.IsArchived)
            throw new CoverageValidationException("RestrictionGroupId", "Choose an active Restriction Group.");
        var entry = RestrictionPermissionChange.Capture(change, repSubject, group, staff, changedAt, reason);
        db.RestrictionPermissionChanges.Add(entry);
        if (current is null) db.RestrictionPermissions.Add(RestrictionPermission.Grant(entry.RepSubject, group.Id));
        else db.RestrictionPermissions.Remove(current);
        await db.SaveChangesAsync(ct);
        return entry;
    }
}
