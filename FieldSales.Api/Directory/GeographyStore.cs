using System.Data;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public sealed class GeographyStore(DirectoryDbContext db)
{
    public async Task<GeographyPage?> PageAsync(Guid? regionId, Guid? countyId, CancellationToken ct)
    {
        if (countyId is Guid countyKey)
        {
            County? county = await db.Counties.AsNoTracking().SingleOrDefaultAsync(item => item.Id == countyKey, ct);
            if (county is null || regionId is Guid regionKey && county.RegionId != regionKey) return null;
            Region region = await db.Regions.AsNoTracking().SingleAsync(item => item.Id == county.RegionId, ct);
            Town[] towns = await db.Towns.AsNoTracking().Where(item => item.CountyId == countyKey).OrderBy(item => item.Name).ToArrayAsync(ct);
            return new("towns", countyKey, [new(region.Id, region.Name, "regions"), new(county.Id, county.Name, "counties")],
                towns.Select(item => Item(item, item.CountyId)).ToArray());
        }
        if (regionId is Guid id)
        {
            Region? region = await db.Regions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
            if (region is null) return null;
            County[] counties = await db.Counties.AsNoTracking().Where(item => item.RegionId == id).OrderBy(item => item.Name).ToArrayAsync(ct);
            return new("counties", id, [new(region.Id, region.Name, "regions")], counties.Select(item => Item(item, item.RegionId)).ToArray());
        }
        Region[] roots = await db.Regions.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(ct);
        return new("regions", null, [], roots.Select(item => Item(item)).ToArray());
    }

    public async Task<TownChoice[]> ChoicesAsync(CancellationToken ct)
    {
        var rows = await (from town in db.Towns.AsNoTracking()
            join county in db.Counties on town.CountyId equals county.Id
            join region in db.Regions on county.RegionId equals region.Id
            select new { Town = town.Id, TownName = town.Name, County = county.Id, CountyName = county.Name,
                Region = region.Id, RegionName = region.Name }).ToArrayAsync(ct);
        return rows.Select(row => new TownChoice(row.Town, row.TownName, row.County, row.CountyName, row.Region, row.RegionName,
            $"{row.TownName} — {row.CountyName}, {row.RegionName}"))
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
    }

    public async Task<GeographyImportResult> ImportAsync(IReadOnlyList<GeographySeedRow> rows, CancellationToken ct)
    {
        // Retrying SQL providers require the complete transaction inside their execution strategy.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            Dictionary<string, Region> regions = (await db.Regions.ToListAsync(ct)).ToDictionary(item => item.NormalizedName);
            Dictionary<(Guid, string), County> counties = (await db.Counties.ToListAsync(ct)).ToDictionary(item => (item.RegionId, item.NormalizedName));
            Dictionary<(Guid, string), Town> towns = (await db.Towns.ToListAsync(ct)).ToDictionary(item => (item.CountyId, item.NormalizedName));
            int regionsAdded = 0, countiesAdded = 0, townsAdded = 0, alreadyPresent = 0;
            HashSet<(Guid, string)> seen = [];
            foreach (var row in rows)
            {
                string regionName = row.Region.ToUpperInvariant();
                if (!regions.TryGetValue(regionName, out Region? region))
                {
                    region = new(row.Region); regions.Add(regionName, region); db.Regions.Add(region); regionsAdded++;
                }
                var countyKey = (region.Id, row.County.ToUpperInvariant());
                if (!counties.TryGetValue(countyKey, out County? county))
                {
                    county = new(region.Id, row.County); counties.Add(countyKey, county); db.Counties.Add(county); countiesAdded++;
                }
                var townKey = (county.Id, row.Town.ToUpperInvariant());
                if (!seen.Add(townKey)) continue;
                if (towns.ContainsKey(townKey)) { alreadyPresent++; continue; }
                Town town = new(county.Id, row.Town); towns.Add(townKey, town); db.Towns.Add(town); townsAdded++;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new GeographyImportResult(regionsAdded, countiesAdded, townsAdded, alreadyPresent);
        });
    }

    public static GeographyItem Item(GeographyEntity entity, Guid? parentId = null) =>
        new(entity.Id, entity.Name, parentId, Convert.ToBase64String(entity.Version));
}
