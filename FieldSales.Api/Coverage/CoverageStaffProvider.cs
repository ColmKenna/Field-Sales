using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.StaffAccess;

namespace FieldSales.Api.Coverage;

public sealed class CoverageStaffProvider(DirectoryDbContext db, IStaffDirectory directory, IHttpContextAccessor contexts)
{
    public async Task<IReadOnlyDictionary<string, StaffDirectoryEntry>> LookupAsync(IEnumerable<string> subjects, CancellationToken ct)
    {
        var (actor, token) = Request();
        try
        {
            List<StaffDirectoryEntry> entries = [];
            foreach (var batch in subjects.Append(actor).Distinct(StringComparer.Ordinal).Chunk(StaffDirectoryContract.MaximumSubjects))
                entries.AddRange(await directory.LookupAsync(token, batch, ct));
            var result = StaffDirectoryContract.Validate(entries);
            VerifyActor(actor, result);
            foreach (string subject in subjects) Find(result, subject);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not CoverageIdentityUnavailableException and not CoverageReadForbiddenException)
        { throw new CoverageIdentityUnavailableException(exception); }
    }

    public async Task<IReadOnlyDictionary<string, StaffDirectoryEntry>> ListAsync(CancellationToken ct)
    {
        var (actor, token) = Request();
        try
        {
            var result = StaffDirectoryContract.Validate(await directory.ListAsync(token, ct));
            VerifyActor(actor, result);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not CoverageIdentityUnavailableException and not CoverageReadForbiddenException)
        { throw new CoverageIdentityUnavailableException(exception); }
    }

    public static StaffDirectoryEntry Find(IReadOnlyDictionary<string, StaffDirectoryEntry> entries, string subject) =>
        entries.TryGetValue(subject, out var entry) ? entry : throw new CoverageIdentityUnavailableException();

    public static void RequireEligible(IReadOnlyDictionary<string, StaffDirectoryEntry> entries, string subject, string role, string field)
    {
        var entry = Find(entries, subject);
        if (!entry.Available || !entry.Roles.Contains(role, StringComparer.Ordinal))
            throw new CoverageValidationException(field, role == BusinessRoles.FieldSalesperson
                ? "Choose an active field salesperson." : "Choose an active Sales Manager.");
    }

    private void VerifyActor(string actor, IReadOnlyDictionary<string, StaffDirectoryEntry> entries)
    {
        var snapshot = new HistoryStaffSnapshot(actor, entries.Values);
        var principal = contexts.HttpContext!.User;
        string actingRole = principal.IsInRole(BusinessRoles.HeadOfficeUser) ? BusinessRoles.HeadOfficeUser : BusinessRoles.SalesManager;
        if (!snapshot.Actor.Roles.Contains(actingRole, StringComparer.Ordinal)) throw new CoverageReadForbiddenException();
    }

    private (string Actor, string Token) Request()
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Read identity data before starting the coverage transaction.");
        var context = contexts.HttpContext;
        string? actor = context?.User.GetStaffSubject();
        string authorization = context?.Request.Headers.Authorization.ToString() ?? "";
        if (!StaffDirectoryContract.ValidSubject(actor) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new CoverageIdentityUnavailableException();
        return (actor!, authorization["Bearer ".Length..].Trim());
    }
}
