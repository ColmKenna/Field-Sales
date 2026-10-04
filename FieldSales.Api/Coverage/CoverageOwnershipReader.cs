using FieldSales.Api.Directory;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed record OwnershipSnapshot(IReadOnlyList<LocationOwnershipPath> Locations,
    IReadOnlyDictionary<Guid, EffectiveOwner?> Owners);

// Uses the caller's context/transaction; never starts a nested transaction.
public sealed class CoverageOwnershipReader(DirectoryDbContext db)
{
    public IQueryable<LocationOwnershipPath> Paths(Guid? id = null, Guid? townId = null) =>
        from location in db.Locations.AsNoTracking()
        where (id == null || location.Id == id) && (townId == null || location.TownId == townId)
        join town in db.Towns on location.TownId equals town.Id
        join county in db.Counties on town.CountyId equals county.Id
        join region in db.Regions on county.RegionId equals region.Id
        select new LocationOwnershipPath(location.Id, location.Name, town.Id, town.Name,
            county.Id, county.Name, region.Id, region.Name);

    public async Task<OwnershipSnapshot> ReadAllAsync(CancellationToken ct)
    {
        var paths = await Paths().ToArrayAsync(ct);
        var resolver = new EffectiveOwnerResolver(await db.TerritoryAssignments.AsNoTracking().ToArrayAsync(ct));
        return new(paths, paths.ToDictionary(path => path.LocationId, resolver.Resolve));
    }

    public async Task<EffectiveOwner?> ResolveAsync(Location location, CancellationToken ct, string townField = "TownId")
    {
        var path = await (from town in db.Towns.AsNoTracking()
            where town.Id == location.TownId
            join county in db.Counties on town.CountyId equals county.Id
            join region in db.Regions on county.RegionId equals region.Id
            select new LocationOwnershipPath(location.Id, location.Name, town.Id, town.Name,
                county.Id, county.Name, region.Id, region.Name)).SingleOrDefaultAsync(ct);
        // Preflight runs before the write transaction; retirement may win after
        // the form's Town validation. Preserve the existing validation response.
        if (path is null) throw new CustomerDirectoryValidationException(townField, "Choose a town");
        var assignments = await db.TerritoryAssignments.AsNoTracking().Where(assignment =>
            assignment.LocationId == path.LocationId || assignment.TownId == path.TownId
            || assignment.CountyId == path.CountyId || assignment.RegionId == path.RegionId).ToArrayAsync(ct);
        return new EffectiveOwnerResolver(assignments).Resolve(path);
    }
}
