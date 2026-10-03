using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Api.Coverage;

public sealed class AssignmentHistory
{
    private AssignmentHistory() { }
    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid LocationId { get; private set; }
    public string LocationName { get; private set; } = "";
    public DateTimeOffset ChangedAt { get; private set; }
    public string ActorSubject { get; private set; } = "";
    public string ActorName { get; private set; } = "";
    public OwnershipChangeCause Cause { get; private set; }
    public string? Reason { get; private set; }
    public string? PreviousRepSubject { get; private set; }
    public string? PreviousRepName { get; private set; }
    public Guid? PreviousAssignmentId { get; private set; }
    public TerritoryLevel? PreviousLevel { get; private set; }
    public Guid? PreviousUnitId { get; private set; }
    public string? PreviousSourceName { get; private set; }
    public string? NewRepSubject { get; private set; }
    public string? NewRepName { get; private set; }
    public Guid? NewAssignmentId { get; private set; }
    public TerritoryLevel? NewLevel { get; private set; }
    public Guid? NewUnitId { get; private set; }
    public string? NewSourceName { get; private set; }

    public static AssignmentHistory Capture(Guid operationId, Guid locationId, string locationName,
        EffectiveOwner? before, EffectiveOwner? after, HistoryStaffSnapshot staff,
        OwnershipChangeCause cause, DateTimeOffset changedAt, string? reason = null)
    {
        if (operationId == Guid.Empty || locationId == Guid.Empty || string.IsNullOrWhiteSpace(locationName)
            || locationName.Length > 200 || !Enum.IsDefined(cause))
            throw new ArgumentException("Choose valid ownership history details.");
        if (string.Equals(before?.RepSubject, after?.RepSubject, StringComparison.Ordinal))
            throw new InvalidOperationException("History requires a changed effective owner.");
        string? validReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (validReason is { Length: > 1000 } || validReason?.Any(char.IsControl) == true)
            throw new CoverageValidationException("Reason", "Enter a reason of up to 1000 characters.");
        return new()
        {
            Id = Guid.NewGuid(), OperationId = operationId, LocationId = locationId, LocationName = locationName,
            ChangedAt = changedAt.ToUniversalTime(), ActorSubject = staff.Actor.Subject, ActorName = staff.Actor.DisplayName,
            Cause = cause, Reason = validReason,
            PreviousRepSubject = before?.RepSubject, PreviousRepName = before is null ? null : staff.Find(before.RepSubject).DisplayName,
            PreviousAssignmentId = before?.Source.AssignmentId, PreviousLevel = before?.Source.Target.Level,
            PreviousUnitId = before?.Source.Target.UnitId, PreviousSourceName = before?.Source.Name,
            NewRepSubject = after?.RepSubject, NewRepName = after is null ? null : staff.Find(after.RepSubject).DisplayName,
            NewAssignmentId = after?.Source.AssignmentId, NewLevel = after?.Source.Target.Level,
            NewUnitId = after?.Source.Target.UnitId, NewSourceName = after?.Source.Name
        };
    }
}

public sealed class HistoryStaffSnapshot
{
    private readonly IReadOnlyDictionary<string, StaffDirectoryEntry> entries;
    public StaffDirectoryEntry Actor { get; }
    public HistoryStaffSnapshot(string actorSubject, IEnumerable<StaffDirectoryEntry> staff)
    {
        entries = StaffDirectoryContract.Validate(staff);
        Actor = Find(actorSubject);
        if (!Actor.Available || !Actor.Roles.Any(role => role is BusinessRoles.HeadOfficeUser or BusinessRoles.SalesManager))
            throw new CoverageIdentityUnavailableException();
    }
    public StaffDirectoryEntry Find(string subject) => entries.TryGetValue(subject, out var entry)
        ? entry : throw new CoverageIdentityUnavailableException();
}

public sealed class CoverageIdentityUnavailableException : Exception
{
    public CoverageIdentityUnavailableException() : base("Staff identities could not be verified. Try again before saving.") { }
    public CoverageIdentityUnavailableException(Exception inner) : base("Staff identities could not be verified. Try again before saving.", inner) { }
}
