using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public sealed record ResolvedLocationPosition(Guid TownId, byte[] TownVersion, string? Eircode,
    Coordinates? Coordinates, LocationPositionPrecision? Precision);

public sealed class LocationPositionResolver(DirectoryDbContext db, IEircodeLookup lookup)
{
    // Called before the write transaction. Capture exactly which Town/input
    // supplied the result so even a delayed lookup cannot silently win a race.
    public async Task<ResolvedLocationPosition> ResolveAsync(Location location, CancellationToken ct)
    {
        var town = await db.Towns.AsNoTracking().SingleOrDefaultAsync(item => item.Id == location.TownId, ct)
            ?? throw new DbUpdateConcurrencyException();
        Coordinates? coordinates = Coordinates.FromPair(town.Latitude, town.Longitude);
        LocationPositionPrecision? precision = coordinates is null ? null : LocationPositionPrecision.Town;
        if (coordinates is null && location.Eircode is { } eircode)
        {
            var result = await lookup.LookupAsync(eircode, ct);
            if (result.Status == EircodeLookupStatus.Found && result.Coordinates is { } found)
            { coordinates = found; precision = LocationPositionPrecision.Eircode; }
        }
        return new(town.Id, town.Version, location.Eircode, coordinates, precision);
    }

    // The caller's serializable transaction holds the Town read lock through
    // SaveChanges, and the Location update still uses its submitted rowversion.
    public async Task ApplyAsync(Location location, ResolvedLocationPosition resolved, DateTimeOffset now, CancellationToken ct)
    {
        var version = await db.Towns.Where(item => item.Id == resolved.TownId).Select(item => item.Version).SingleOrDefaultAsync(ct);
        if (location.TownId != resolved.TownId || location.Eircode != resolved.Eircode
            || version is null || !version.SequenceEqual(resolved.TownVersion))
            throw new DbUpdateConcurrencyException();
        location.ApplyDefaultPosition(resolved.Coordinates, resolved.Precision, now);
    }
}
