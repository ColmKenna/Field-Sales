using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class AssignmentReviewStore(DirectoryDbContext db, CoverageReadStore access, ReportingLineStore reporting,
    CoverageOwnershipReader ownership)
{
    public async Task<AssignmentReviewOptions> OptionsAsync(ClaimsPrincipal actor, string? rep, CancellationToken ct)
    {
        var reps = await reporting.RepsAsync(actor, ct);
        var assignments = string.IsNullOrEmpty(rep) ? [] : await access.ListAssignmentsAsync(actor, rep, ct);
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
            return new AssignmentReviewOptions(reps, targets, assignments);
        }, ct);
    }
}
