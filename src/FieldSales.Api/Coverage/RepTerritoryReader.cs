using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class RepTerritoryReader(DirectoryDbContext db, CoverageReadStore access,
    CoverageOwnershipReader ownership, CoverageStaffProvider staff)
{
    public async Task<RepTerritoryPage> ReadAsync(ClaimsPrincipal actor, string rep, CancellationToken ct)
    {
        rep = CoverageSubjects.Validate(rep, "RepSubject");
        // Identity requests cannot hold business SQL locks. Missing labels fail
        // closed; reporting authority is rechecked with the territory snapshot.
        var people = await staff.ListAsync(ct);
        StaffChoice Person(string subject)
        {
            var entry = CoverageStaffProvider.Find(people, subject);
            return new(entry.Subject, entry.DisplayName);
        }
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            await access.RequireRepAccessAsync(actor, rep, ct);
            var manager = await db.RepReportingLines.AsNoTracking().Where(row => row.RepSubject == rep)
                .Select(row => row.ManagerSubject).SingleOrDefaultAsync(ct);
            var assignments = await db.TerritoryAssignments.AsNoTracking().ToArrayAsync(ct);
            var regions = (await db.Regions.AsNoTracking().ToArrayAsync(ct)).ToDictionary(row => row.Id);
            var counties = (await db.Counties.AsNoTracking().ToArrayAsync(ct)).ToDictionary(row => row.Id);
            var towns = (await db.Towns.AsNoTracking().ToArrayAsync(ct)).ToDictionary(row => row.Id);
            var paths = await ownership.Paths().ToArrayAsync(ct);
            var resolver = new EffectiveOwnerResolver(assignments);
            var owners = paths.ToDictionary(row => row.LocationId, resolver.Resolve);
            // Aggregate the existing resolver's answers, never copy its rule.
            TerritoryCarveOut[] CarveOuts(IEnumerable<LocationOwnershipPath> locations) => locations
                .Select(row => owners[row.LocationId]).Where(owner => owner is not null && owner.RepSubject != rep)
                .GroupBy(owner => owner!.RepSubject, StringComparer.Ordinal)
                .Select(group => new TerritoryCarveOut(Person(group.Key), group.Count()))
                .OrderBy(row => row.Rep.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Rep.Subject, StringComparer.Ordinal).ToArray();
            var townAssignments = assignments.Where(row => row.TownId != null).ToDictionary(row => row.TownId!.Value);
            List<RepTerritoryAssignment> rows = [];
            foreach (var assignment in assignments.Where(row => row.RepSubject == rep))
            {
                var target = assignment.Target;
                var locations = paths.Where(path => target.Level switch
                {
                    TerritoryLevel.Region => path.RegionId == target.UnitId,
                    TerritoryLevel.County => path.CountyId == target.UnitId,
                    TerritoryLevel.Town => path.TownId == target.UnitId,
                    _ => path.LocationId == target.UnitId
                }).ToArray();
                string name; string context; bool archived;
                switch (target.Level)
                {
                    case TerritoryLevel.Region:
                        var region = regions[target.UnitId]; name = region.Name; context = ""; archived = region.IsArchived; break;
                    case TerritoryLevel.County:
                        var county = counties[target.UnitId]; var parent = regions[county.RegionId];
                        name = county.Name; context = parent.Name; archived = county.IsArchived || parent.IsArchived; break;
                    case TerritoryLevel.Town:
                        var town = towns[target.UnitId]; var townCounty = counties[town.CountyId]; var townRegion = regions[townCounty.RegionId];
                        name = town.Name; context = $"{townCounty.Name} · {townRegion.Name}";
                        archived = town.IsArchived || townCounty.IsArchived || townRegion.IsArchived; break;
                    default:
                        var path = locations.Single(); var locationTown = towns[path.TownId]; var locationCounty = counties[path.CountyId];
                        name = path.LocationName; context = $"{path.TownName} · {path.CountyName} · {path.RegionName}";
                        archived = locationTown.IsArchived || locationCounty.IsArchived || regions[path.RegionId].IsArchived; break;
                }
                var children = target.Level == TerritoryLevel.County ? towns.Values.Where(town => town.CountyId == target.UnitId)
                    .OrderBy(town => town.Name, StringComparer.OrdinalIgnoreCase).ThenBy(town => town.Id)
                    .Select(town =>
                    {
                        var townLocations = locations.Where(path => path.TownId == town.Id).ToArray();
                        var other = townAssignments.GetValueOrDefault(town.Id);
                        return new TerritoryTown(town.Id, town.Name, archived || town.IsArchived, townLocations.Length,
                            other is not null && other.RepSubject != rep ? Person(other.RepSubject) : null, CarveOuts(townLocations));
                    }).ToArray() : [];
                var details = new AssignedTerritoryDetails(new(assignment.Id, rep, target, Convert.ToBase64String(assignment.Version)), name);
                rows.Add(new(details, context, archived, locations.Length, CarveOuts(locations), children));
            }
            return new RepTerritoryPage(Person(rep), manager is null ? null : Person(manager),
                owners.Values.Count(owner => owner?.RepSubject == rep), rows.OrderBy(row => row.Assignment.Assignment.Target.Level)
                    .ThenBy(row => row.Assignment.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.Assignment.Assignment.Id).ToArray());
        }, ct);
    }
}
