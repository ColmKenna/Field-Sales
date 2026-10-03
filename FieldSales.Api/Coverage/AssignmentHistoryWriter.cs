using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Api.Coverage;

public sealed class AssignmentHistoryWriter(DirectoryDbContext db, IStaffDirectory directory, IHttpContextAccessor request)
{
    public async Task<HistoryStaffSnapshot?> PrepareAsync(IEnumerable<EffectiveOwner?> owners, CancellationToken ct)
    {
        var subjects = owners.Where(owner => owner is not null).Select(owner => owner!.RepSubject)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (subjects.Length == 0) return null;
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Read identity data before starting the ownership transaction.");
        var context = request.HttpContext;
        string? actor = context?.User.GetStaffSubject();
        string authorization = context?.Request.Headers.Authorization.ToString() ?? "";
        if (!StaffDirectoryContract.ValidSubject(actor) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new CoverageIdentityUnavailableException();
        try
        {
            List<StaffDirectoryEntry> staff = [];
            foreach (var batch in subjects.Append(actor!).Distinct(StringComparer.Ordinal).Chunk(StaffDirectoryContract.MaximumSubjects))
                staff.AddRange(await directory.LookupAsync(authorization["Bearer ".Length..].Trim(), batch, ct));
            var snapshot = new HistoryStaffSnapshot(actor!, staff);
            foreach (var subject in subjects) snapshot.Find(subject);
            return snapshot;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not CoverageIdentityUnavailableException)
        { throw new CoverageIdentityUnavailableException(exception); }
    }

    public int AppendChanges(OwnershipSnapshot before, OwnershipSnapshot after, HistoryStaffSnapshot? staff,
        Guid operationId, OwnershipChangeCause cause, DateTimeOffset changedAt, string? reason = null)
    {
        int count = 0;
        foreach (var path in after.Locations)
        {
            before.Owners.TryGetValue(path.LocationId, out var oldOwner);
            if (!after.Owners.TryGetValue(path.LocationId, out var newOwner))
                throw new InvalidOperationException("The ownership snapshot is incomplete.");
            if (Append(path.LocationId, path.LocationName, oldOwner, newOwner, staff, operationId, cause, changedAt, reason)) count++;
        }
        return count;
    }

    public bool Append(Guid locationId, string name, EffectiveOwner? before, EffectiveOwner? after,
        HistoryStaffSnapshot? staff, Guid operationId, OwnershipChangeCause cause, DateTimeOffset changedAt, string? reason = null)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Ownership history requires the caller's write transaction.");
        if (string.Equals(before?.RepSubject, after?.RepSubject, StringComparison.Ordinal)) return false;
        if (staff is null) throw new CoverageIdentityUnavailableException();
        // Existing/previous identities may be inactive, but their trusted labels
        // remain valid historical facts. Eligibility for new assignments is checked by their handler.
        db.AssignmentHistory.Add(AssignmentHistory.Capture(operationId, locationId, name, before, after, staff, cause, changedAt, reason));
        return true;
    }
}
