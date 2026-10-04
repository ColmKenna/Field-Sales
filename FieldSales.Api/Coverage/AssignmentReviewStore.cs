using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class AssignmentReviewStore(DirectoryDbContext db, CoverageReadStore access, ReportingLineStore reporting,
    CoverageOwnershipReader ownership, CoverageStaffProvider staff)
{
    public async Task<AssignmentReviewOptions> OptionsAsync(ClaimsPrincipal actor, string? rep, CancellationToken ct)
    {
        var reps = await reporting.RepsAsync(actor, ct);
        var assignments = string.IsNullOrEmpty(rep) ? [] : await access.ListAssignmentsAsync(actor, rep, ct);
        var identities = string.IsNullOrEmpty(rep) ? null : await staff.ListAsync(ct);
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            if (!string.IsNullOrEmpty(rep)) await access.RequireRepAccessAsync(actor, rep, ct);
            var regions = await db.Regions.AsNoTracking().Where(row => !row.IsArchived).ToArrayAsync(ct);
            var regionIds = regions.Select(row => row.Id).ToArray();
            var counties = await db.Counties.AsNoTracking().Where(row => !row.IsArchived && regionIds.Contains(row.RegionId)).ToArrayAsync(ct);
            var countyIds = counties.Select(row => row.Id).ToArray();
            var towns = await db.Towns.AsNoTracking().Where(row => !row.IsArchived && countyIds.Contains(row.CountyId)).ToArrayAsync(ct);
            var townIds = towns.Select(row => row.Id).ToArray();
            var paths = await ownership.Paths().ToArrayAsync(ct);
            var activeTownIds = townIds.ToHashSet();
            var locations = paths.Where(row => activeTownIds.Contains(row.TownId)).ToArray();
            var targets = regions.Select(row => new CoverageTargetChoice(new(TerritoryLevel.Region, row.Id), row.Name, $"{row.Name} (Region)"))
                .Concat(counties.Select(row => new CoverageTargetChoice(new(TerritoryLevel.County, row.Id), row.Name,
                    $"{row.Name} (County) · {regions.Single(region => region.Id == row.RegionId).Name}")))
                .Concat(towns.Select(row => new CoverageTargetChoice(new(TerritoryLevel.Town, row.Id), row.Name,
                    $"{row.Name} (Town) · {counties.Single(county => county.Id == row.CountyId).Name}")))
                .Concat(locations.Select(row => new CoverageTargetChoice(new(TerritoryLevel.Location, row.LocationId), row.LocationName,
                    $"{row.LocationName} (Location) · {row.TownName}, {row.CountyName}")))
                .OrderBy(row => row.Target.Level).ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Target.UnitId).ToArray();
            if (identities is not null)
            {
                var held = (await db.TerritoryAssignments.AsNoTracking().ToArrayAsync(ct)).ToDictionary(row => row.Target);
                var team = await db.RepReportingLines.Where(row => row.ManagerSubject == actor.GetStaffSubject())
                    .Select(row => row.RepSubject).ToArrayAsync(ct);
                targets = targets.Where(row => !held.TryGetValue(row.Target, out var holder)
                        || actor.IsInRole(BusinessRoles.HeadOfficeUser) || team.Contains(holder.RepSubject, StringComparer.Ordinal))
                    .Select(row => held.TryGetValue(row.Target, out var holder)
                        ? row with { Holder = new(holder.Id, new(holder.RepSubject, CoverageStaffProvider.Find(identities, holder.RepSubject).DisplayName)),
                            Label = $"{row.Name} — {CoverageStaffProvider.Find(identities, holder.RepSubject).DisplayName}'s · {row.Label}" }
                        : row).ToArray();
            }
            return new AssignmentReviewOptions(reps, targets, assignments);
        }, ct);
    }

    public async Task<TransferSourceOptions> SourcesAsync(ClaimsPrincipal actor, string recipient, CancellationToken ct)
    {
        recipient = CoverageSubjects.Validate(recipient, "ReceivingRepSubject");
        await access.RequireRepAccessAsync(actor, recipient, ct);
        var identities = await staff.ListAsync(ct);
        CoverageStaffProvider.RequireEligible(identities, recipient, BusinessRoles.FieldSalesperson, "ReceivingRepSubject");
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            await access.RequireRepAccessAsync(actor, recipient, ct);
            if (!await db.RepReportingLines.AnyAsync(row => row.RepSubject == recipient, ct))
                throw new CoverageValidationException("ReceivingRepSubject", "Set this rep's reporting line before transferring assignments.");
            var team = await db.RepReportingLines.Where(row => row.ManagerSubject == actor.GetStaffSubject())
                .Select(row => row.RepSubject).ToArrayAsync(ct);
            var subjects = await db.TerritoryAssignments.Select(row => row.RepSubject).Distinct().ToArrayAsync(ct);
            StaffChoice Person(string subject) => new(subject, CoverageStaffProvider.Find(identities, subject).DisplayName);
            var sources = subjects.Where(subject => subject != recipient && (actor.IsInRole(BusinessRoles.HeadOfficeUser)
                    || team.Contains(subject, StringComparer.Ordinal))).Select(Person)
                .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Subject, StringComparer.Ordinal).ToArray();
            return new TransferSourceOptions(Person(recipient), sources);
        }, ct);
    }
}
