using System.Data;
using FieldSales.Api.Catalogue;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public sealed class GeographyStore(DirectoryDbContext db, GeographyUsageReader usage)
{
    public async Task<GeographyPage?> PageAsync(Guid? regionId, Guid? countyId, CancellationToken ct, bool showArchived = false)
    {
        if (countyId is Guid countyKey)
        {
            County? county = await db.Counties.AsNoTracking().SingleOrDefaultAsync(item => item.Id == countyKey, ct);
            if (county is null || regionId is Guid regionKey && county.RegionId != regionKey) return null;
            Region region = await db.Regions.AsNoTracking().SingleAsync(item => item.Id == county.RegionId, ct);
            Town[] towns = await db.Towns.AsNoTracking().Where(item => item.CountyId == countyKey).OrderBy(item => item.Name).ToArrayAsync(ct);
            return await PageAsync("towns", countyKey,
                [new(region.Id, region.Name, "regions", region.IsArchived), new(county.Id, county.Name, "counties", county.IsArchived)],
                towns.Select(item => Item(item, item.CountyId)).ToArray(), showArchived, ct);
        }
        if (regionId is Guid id)
        {
            Region? region = await db.Regions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
            if (region is null) return null;
            County[] counties = await db.Counties.AsNoTracking().Where(item => item.RegionId == id).OrderBy(item => item.Name).ToArrayAsync(ct);
            return await PageAsync("counties", id, [new(region.Id, region.Name, "regions", region.IsArchived)],
                counties.Select(item => Item(item, item.RegionId)).ToArray(), showArchived, ct);
        }
        Region[] roots = await db.Regions.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(ct);
        return await PageAsync("regions", null, [], roots.Select(item => Item(item)).ToArray(), showArchived, ct);
    }

    private async Task<GeographyPage> PageAsync(string level, Guid? parentId, GeographyPath[] path,
        GeographyItem[] all, bool showArchived, CancellationToken ct)
    {
        var visible = all.Where(item => showArchived || !item.IsArchived).ToArray();
        var counts = await usage.ReadManyAsync(level, visible.Select(item => item.Id).ToArray(), ct);
        return new(level, parentId, path, visible.Select(item => item with { Usage = counts[item.Id] }).ToArray(),
            all.Count(item => item.IsArchived), showArchived);
    }

    public async Task<TownChoice[]> ChoicesAsync(CancellationToken ct) =>
        (await TownReferencesAsync(null, ct)).Where(item => item.IsSelectable).ToArray();

    public async Task<TownChoice?> TownReferenceAsync(Guid id, CancellationToken ct) =>
        (await TownReferencesAsync(id, ct)).SingleOrDefault();

    /// <summary>WI-016 must call within the same serializable transaction as a Location write.
    /// Existing unchanged links survive archive; new links require an active complete hierarchy.</summary>
    public async Task<TownChoice> TownForAssignmentAsync(Guid id, Guid? existingTownId, CancellationToken ct)
    {
        var town = await TownReferenceAsync(id, ct);
        if (town is null || !town.IsSelectable && existingTownId != id)
            throw new GeographyValidationException("Choose an active town.");
        return town;
    }

    private async Task<TownChoice[]> TownReferencesAsync(Guid? id, CancellationToken ct)
    {
        var rows = await (from town in db.Towns.AsNoTracking().Where(town => id == null || town.Id == id)
            join county in db.Counties on town.CountyId equals county.Id
            join region in db.Regions on county.RegionId equals region.Id
            select new { Town = town.Id, TownName = town.Name, County = county.Id, CountyName = county.Name,
                Region = region.Id, RegionName = region.Name, TownArchived = town.IsArchived,
                CountyArchived = county.IsArchived, RegionArchived = region.IsArchived,
                town.Latitude, town.Longitude }).ToArrayAsync(ct);
        static string Label(string name, bool archived) => name + (archived ? " (archived)" : "");
        return rows.Select(row => new TownChoice(row.Town, row.TownName, row.County, row.CountyName, row.Region, row.RegionName,
            $"{Label(row.TownName, row.TownArchived)} — {Label(row.CountyName, row.CountyArchived)}, {Label(row.RegionName, row.RegionArchived)}",
            row.TownArchived, row.CountyArchived, row.RegionArchived, row.Latitude, row.Longitude))
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
    }

    public async Task<GeographyItem?> FindAsync<T>(Guid id, string level, CancellationToken ct) where T : GeographyEntity
    {
        var item = await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        return item is null ? null : Item(item, ParentId(item)) with { Usage = await usage.ReadAsync(new(level, id), ct) };
    }

    public Task<GeographyItem?> SetTownCoordinatesAsync(Guid id, SetTownCoordinatesRequest request, CancellationToken ct) =>
        GeographyTransactions.RunAsync(db, async () =>
        {
            var town = await db.Towns.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (town is null) return null;
            byte[] version;
            try { version = Convert.FromBase64String(request.Version ?? string.Empty); }
            catch (FormatException) { throw new GeographyValidationException("Reload this town before saving coordinates."); }
            if (version.Length != 8) throw new GeographyValidationException("Reload this town before saving coordinates.");
            if (!town.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
            town.SetCoordinates(request.Latitude, request.Longitude);
            await db.SaveChangesAsync(ct);
            return Item(town, town.CountyId);
        }, ct);

    public Task<ReferenceMutationStatus> RetireAsync<T>(Guid id, string level, RetireGeographyRequest request, CancellationToken ct)
        where T : GeographyEntity => GeographyTransactions.RunAsync(db, async () =>
        {
            var item = await db.Set<T>().SingleOrDefaultAsync(item => item.Id == id, ct);
            if (item is null) return ReferenceMutationStatus.Missing;
            byte[] version;
            try { version = Convert.FromBase64String(request.Version ?? string.Empty); }
            catch (FormatException) { throw new GeographyValidationException("Reload this page before continuing."); }
            if (version.Length != 8) throw new GeographyValidationException("Reload this page before continuing.");
            var currentUsage = await usage.ReadAsync(new(level, id), ct);
            if (!item.Version.SequenceEqual(version) || ReferenceRetirementPolicy.Decide(item.IsArchived, currentUsage) != request.Action)
                return ReferenceMutationStatus.Conflict;
            switch (request.Action)
            {
                case ReferenceAction.Delete: db.Set<T>().Remove(item); break;
                case ReferenceAction.Archive: item.Archive(); break;
                case ReferenceAction.Unarchive: item.Unarchive(); break;
                default: throw new GeographyValidationException("Choose a valid action.");
            }
            await db.SaveChangesAsync(ct);
            return ReferenceMutationStatus.Saved;
        }, ct);

    public async Task EnsureActiveParentAsync(string level, Guid? id, CancellationToken ct)
    {
        bool active = level switch
        {
            "regions" => id is null,
            "counties" => await db.Regions.AnyAsync(region => region.Id == id && !region.IsArchived, ct),
            "towns" => await (from county in db.Counties join region in db.Regions on county.RegionId equals region.Id
                where county.Id == id && !county.IsArchived && !region.IsArchived select county.Id).AnyAsync(ct),
            _ => false
        };
        if (!active) throw new GeographyValidationException(level == "counties" ? "Choose an active region." : "Choose an active county.");
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
            int regionsAdded = 0, countiesAdded = 0, townsAdded = 0, alreadyPresent = 0, coordinatesUpdated = 0;
            HashSet<(Guid, string)> seen = [];
            Dictionary<(Guid, string), Coordinates> coordinateValues = [];
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
                    if (region.IsArchived) throw new GeographyValidationException("Cannot add a county under an archived region. No changes were saved.");
                    county = new(region.Id, row.County); counties.Add(countyKey, county); db.Counties.Add(county); countiesAdded++;
                }
                var townKey = (county.Id, row.Town.ToUpperInvariant());
                if (row.Coordinates is { } supplied)
                {
                    if (coordinateValues.TryGetValue(townKey, out var previous) && previous != supplied)
                        throw new GeographyValidationException("The CSV contains conflicting coordinates for the same town. No changes were saved.");
                    coordinateValues[townKey] = supplied;
                }
                bool first = seen.Add(townKey);
                if (towns.TryGetValue(townKey, out var existing))
                {
                    if (first) alreadyPresent++;
                    if (row.Coordinates is { } coordinates && existing.SetCoordinates(coordinates.Latitude, coordinates.Longitude)
                        && db.Entry(existing).State != EntityState.Added) coordinatesUpdated++;
                    continue;
                }
                if (region.IsArchived || county.IsArchived)
                    throw new GeographyValidationException("Cannot add a town under archived geography. No changes were saved.");
                Town town = new(county.Id, row.Town);
                if (row.Coordinates is { } pin) town.SetCoordinates(pin.Latitude, pin.Longitude);
                towns.Add(townKey, town); db.Towns.Add(town); townsAdded++;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new GeographyImportResult(regionsAdded, countiesAdded, townsAdded, alreadyPresent, coordinatesUpdated);
        });
    }

    public static GeographyItem Item(GeographyEntity entity, Guid? parentId = null) =>
        new(entity.Id, entity.Name, parentId, Convert.ToBase64String(entity.Version), entity.IsArchived,
            Latitude: (entity as Town)?.Latitude, Longitude: (entity as Town)?.Longitude);

    private static Guid? ParentId(GeographyEntity entity) => entity switch
    { County county => county.RegionId, Town town => town.CountyId, _ => null };
}

internal static class GeographyTransactions
{
    public static Task<T> RunAsync<T>(DirectoryDbContext db, Func<Task<T>> operation, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            T result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });
}
