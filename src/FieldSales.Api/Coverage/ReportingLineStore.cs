using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class ReportingLineStore(DirectoryDbContext db, CoverageStaffProvider staff)
{
    public async Task<ReportingLinesPage> ListAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        RequireHeadOffice(actor);
        var entries = (await staff.ListAsync(ct)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var lines = await db.RepReportingLines.AsNoTracking().ToArrayAsync(ct);
        var missing = lines.SelectMany(line => new[] { line.RepSubject, line.ManagerSubject }).Where(subject => !entries.ContainsKey(subject))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            foreach (var pair in await staff.LookupAsync(missing, ct)) entries[pair.Key] = pair.Value;
        RequireHeadOfficeIdentity(actor, entries);
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            var current = await db.RepReportingLines.AsNoTracking().ToArrayAsync(ct);
            var named = current.Select(line => new NamedRepReportingLine(Details(line),
                CoverageStaffProvider.Find(entries, line.RepSubject).DisplayName,
                CoverageStaffProvider.Find(entries, line.ManagerSubject).DisplayName))
                .OrderBy(line => line.RepName, StringComparer.OrdinalIgnoreCase).ThenBy(line => line.Line.RepSubject, StringComparer.Ordinal).ToArray();
            return new ReportingLinesPage(Choices(entries, BusinessRoles.FieldSalesperson), Choices(entries, BusinessRoles.SalesManager), named);
        }, ct);
    }

    public async Task<RepReportingLineDetails> SetAsync(ClaimsPrincipal actor, string rep, SetRepReportingLineRequest request, CancellationToken ct)
    {
        RequireHeadOffice(actor);
        rep = CoverageSubjects.Validate(rep, "RepSubject");
        string manager = CoverageSubjects.Validate(request.ManagerSubject, "ManagerSubject");
        var entries = await staff.LookupAsync([rep, manager], ct);
        RequireHeadOfficeIdentity(actor, entries);
        CoverageStaffProvider.RequireEligible(entries, rep, BusinessRoles.FieldSalesperson, "RepSubject");
        CoverageStaffProvider.RequireEligible(entries, manager, BusinessRoles.SalesManager, "ManagerSubject");
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            var line = await db.RepReportingLines.SingleOrDefaultAsync(row => row.RepSubject == rep, ct);
            if (line is null)
            {
                if (!string.IsNullOrEmpty(request.Version)) throw new DbUpdateConcurrencyException();
                line = RepReportingLine.Create(rep, manager); db.RepReportingLines.Add(line);
            }
            else
            {
                if (!line.Version.SequenceEqual(TerritoryAssignmentStore.Version(request.Version))) throw new DbUpdateConcurrencyException();
                line.SetManager(manager);
                db.Entry(line).Property(row => row.ManagerSubject).IsModified = true;
            }
            await db.SaveChangesAsync(ct);
            return Details(line);
        }, ct);
    }

    public async Task<IReadOnlyList<StaffChoice>> RepsAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var entries = await staff.ListAsync(ct);
        return await GeographyTransactions.RunAsync<IReadOnlyList<StaffChoice>>(db, async () =>
        {
            var candidates = Choices(entries, BusinessRoles.FieldSalesperson);
            if (actor.IsInRole(BusinessRoles.HeadOfficeUser)) return candidates;
            string? manager = actor.GetStaffSubject();
            var team = await db.RepReportingLines.Where(row => row.ManagerSubject == manager).Select(row => row.RepSubject).ToArrayAsync(ct);
            return candidates.Where(rep => team.Contains(rep.Subject, StringComparer.Ordinal)).ToArray();
        }, ct);
    }

    private static StaffChoice[] Choices(IReadOnlyDictionary<string, StaffDirectoryEntry> entries, string role) => entries.Values
        .Where(entry => entry.Available && entry.Roles.Contains(role, StringComparer.Ordinal))
        .Select(entry => new StaffChoice(entry.Subject, entry.DisplayName)).OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Subject, StringComparer.Ordinal).ToArray();
    private static RepReportingLineDetails Details(RepReportingLine line) => new(line.RepSubject, line.ManagerSubject, Convert.ToBase64String(line.Version));
    private static void RequireHeadOffice(ClaimsPrincipal actor)
    { if (!actor.IsInRole(BusinessRoles.HeadOfficeUser)) throw new CoverageReadForbiddenException(); }
    private static void RequireHeadOfficeIdentity(ClaimsPrincipal actor, IReadOnlyDictionary<string, StaffDirectoryEntry> entries)
    {
        var current = CoverageStaffProvider.Find(entries, actor.GetStaffSubject()!);
        if (!current.Available || !current.Roles.Contains(BusinessRoles.HeadOfficeUser, StringComparer.Ordinal))
            throw new CoverageReadForbiddenException();
    }
}
