namespace FieldSales.Directory.Contracts;

public enum RestrictionPermissionChangeKind { Granted, Removed }

// Rep, group and actor names are the ones captured when the change was recorded.
public sealed record RestrictionPermissionChangeDetails(Guid Id, long Sequence, HistoryIdentityDetails Rep,
    Guid RestrictionGroupId, string RestrictionGroupName, RestrictionPermissionChangeKind Change,
    DateTimeOffset ChangedAt, HistoryIdentityDetails Actor, string? Reason, string Display);

// M-14 per rep. The Can* flags are navigation context only; every change rechecks the rule.
public sealed record RepPermissionGroup(Guid RestrictionGroupId, string Name, bool Archived, bool Granted,
    RestrictionPermissionChangeDetails? LastChange, bool CanGrant, bool CanRemove);
// Active means an available Field Salesperson, the only kind of rep who can be granted a permission.
public sealed record RepPermissionsPage(StaffChoice Rep, bool Active, bool CanChange, IReadOnlyList<RepPermissionGroup> Groups);
public sealed record PermissionRepChoice(StaffChoice Rep, bool Active, bool CanChange);

// ExpectedLastChangeId is the last change the page showed for this rep and group, or null when it showed none.
public sealed record ChangeRestrictionPermissionRequest(RestrictionPermissionChangeKind Change, string? Reason,
    Guid? ExpectedLastChangeId);

// M-14 per group, using the same record and rule as the per-rep view. CanGrant is display context;
// every selected rep is checked again before the whole cohort is written in one transaction.
public sealed record PermissionGroupChoice(Guid RestrictionGroupId, string Name, bool Archived);
public sealed record GroupPermissionRep(StaffChoice Rep, bool Active, bool Granted,
    RestrictionPermissionChangeDetails? LastChange, bool CanGrant);
public sealed record GroupPermissionsPage(PermissionGroupChoice Group, IReadOnlyList<GroupPermissionRep> Reps);
public sealed record CohortPermissionSelection(string RepSubject, Guid? ExpectedLastChangeId);
public sealed record GrantCohortPermissionRequest(IReadOnlyList<CohortPermissionSelection> Reps, string? Reason);
public sealed record CohortPermissionGrantResult(Guid RestrictionGroupId, IReadOnlyList<RestrictionPermissionChangeDetails> Changes);
