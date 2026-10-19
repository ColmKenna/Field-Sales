using FieldSales.Api.Catalogue;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Coverage;

// A rep's current grant for one Restriction Group, present only while it is held. It lives in
// CatalogueDb beside the groups, so retirement counts and delete protection share one transaction.
public sealed class RestrictionPermission
{
    private RestrictionPermission() { }
    public string RepSubject { get; private set; } = "";
    public Guid RestrictionGroupId { get; private set; }

    internal static RestrictionPermission Grant(string repSubject, Guid restrictionGroupId) =>
        new() { RepSubject = repSubject, RestrictionGroupId = restrictionGroupId };
}

// One append-only entry per grant or removal. Names are captured so the record stays readable
// after an account or group is renamed, or a group whose permissions were all removed is deleted.
public sealed class RestrictionPermissionChange
{
    private RestrictionPermissionChange() { }
    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public string RepSubject { get; private set; } = "";
    public string RepName { get; private set; } = "";
    public Guid RestrictionGroupId { get; private set; }
    public string RestrictionGroupName { get; private set; } = "";
    public RestrictionPermissionChangeKind Change { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public string ActorSubject { get; private set; } = "";
    public string ActorName { get; private set; } = "";
    public string? Reason { get; private set; }

    public static RestrictionPermissionChange Capture(RestrictionPermissionChangeKind change, string repSubject,
        RestrictionGroup group, HistoryStaffSnapshot staff, DateTimeOffset changedAt, string? reason = null)
    {
        if (!Enum.IsDefined(change)) throw new ArgumentException("Choose a valid permission change.", nameof(change));
        repSubject = CoverageSubjects.Validate(repSubject, "RepSubject");
        // Nobody records a change to their own permissions, Head Office included.
        if (string.Equals(repSubject, staff.Actor.Subject, StringComparison.Ordinal)) throw new CoverageReadForbiddenException();
        string? validReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (validReason is { Length: > 1000 } || validReason?.Any(char.IsControl) == true)
            throw new CoverageValidationException("Reason", "Enter a reason of up to 1000 characters.");
        return new()
        {
            Id = Guid.NewGuid(), RepSubject = repSubject, RepName = staff.Find(repSubject).DisplayName,
            RestrictionGroupId = group.Id, RestrictionGroupName = group.Name, Change = change,
            ChangedAt = changedAt.ToUniversalTime(), ActorSubject = staff.Actor.Subject, ActorName = staff.Actor.DisplayName,
            Reason = validReason
        };
    }
}
