using FieldSales.Directory.Contracts;
using FieldSales.Api.Coverage;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public sealed class CustomerStore(DirectoryDbContext db, GeographyStore geography,
    LocationPositionResolver positions, TimeProvider clock, CoverageOwnershipReader ownership, AssignmentHistoryWriter history)
{
    public Task<CustomerSummary[]> ListAsync(CancellationToken ct) => db.Customers.AsNoTracking()
        .OrderBy(customer => customer.Name).ThenBy(customer => customer.Id)
        .Select(customer => new CustomerSummary(customer.Id, customer.Name, customer.Locations.Count)).ToArrayAsync(ct);

    public async Task<CustomerDetails?> FindCustomerAsync(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(customer => customer.Id == id, ct);
        if (customer is null) return null;
        var locations = await LocationRows(customerId: id).ToArrayAsync(ct);
        return new(customer.Id, customer.Name, Convert.ToBase64String(customer.Version),
            locations.Select(location => new LocationSummary(location.Id, location.Name, Town(location),
                location.Eircode, Convert.ToBase64String(location.Version), Type(location), Position(location))).ToArray());
    }

    public async Task<LocationDetails?> FindLocationAsync(Guid id, CancellationToken ct)
    {
        var row = await LocationRows(locationId: id).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.Id, row.CustomerId, row.CustomerName, row.Name, Town(row),
            row.Eircode, Convert.ToBase64String(row.Version), Type(row), Position(row));
    }

    public async Task<CustomerDetails> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        if (request.FirstLocation is not { } first)
            throw new CustomerDirectoryValidationException("FirstLocation", "Add the first location before saving this customer.");
        var candidate = Customer.Create(request.Name, first.Name, first.TownId, first.Eircode).Locations[0];
        await CheckTownAsync(candidate.TownId, null, "FirstLocation.TownId", ct);
        await SetTypeAsync(candidate, first.LocationTypeId, null, "FirstLocation.LocationTypeId", ct);
        var resolved = await positions.ResolveAsync(candidate, ct);
        var staff = await history.PrepareAsync([await ownership.ResolveAsync(candidate, ct, "FirstLocation.TownId")], ct);
        var operationId = Guid.NewGuid();
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            Customer customer = Customer.Create(request.Name, first.Name, first.TownId, first.Eircode);
            await CheckTownAsync(customer.Locations[0].TownId, null, "FirstLocation.TownId", ct);
            await SetTypeAsync(customer.Locations[0], first.LocationTypeId, null, "FirstLocation.LocationTypeId", ct);
            await positions.ApplyAsync(customer.Locations[0], resolved, clock.GetUtcNow(), ct);
            db.Customers.Add(customer);
            var firstLocation = customer.Locations[0];
            history.Append(firstLocation.Id, firstLocation.Name, null, await ownership.ResolveAsync(firstLocation, ct, "FirstLocation.TownId"),
                staff, operationId, OwnershipChangeCause.TerritoryAssignment, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return (await FindCustomerAsync(customer.Id, ct))!;
        }, ct);
    }

    public async Task<LocationDetails?> AddLocationAsync(Guid customerId, CreateLocationRequest request, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, ct)) return null;
        var candidate = Location.Create(customerId, request.Name, request.TownId, request.Eircode);
        await CheckTownAsync(candidate.TownId, null, "TownId", ct);
        await SetTypeAsync(candidate, request.LocationTypeId, null, "LocationTypeId", ct);
        await CheckDuplicateAsync(candidate, request.ConfirmDuplicateName, ct);
        var resolved = await positions.ResolveAsync(candidate, ct);
        var staff = await history.PrepareAsync([await ownership.ResolveAsync(candidate, ct)], ct);
        var operationId = Guid.NewGuid();
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, ct)) return null;
            Location location = Location.Create(customerId, request.Name, request.TownId, request.Eircode);
            await CheckTownAsync(location.TownId, null, "TownId", ct);
            await SetTypeAsync(location, request.LocationTypeId, null, "LocationTypeId", ct);
            await CheckDuplicateAsync(location, request.ConfirmDuplicateName, ct);
            await positions.ApplyAsync(location, resolved, clock.GetUtcNow(), ct);
            db.Locations.Add(location);
            history.Append(location.Id, location.Name, null, await ownership.ResolveAsync(location, ct),
                staff, operationId, OwnershipChangeCause.TerritoryAssignment, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return await FindLocationAsync(location.Id, ct);
        }, ct);
    }

    public async Task<LocationDetails?> EditLocationAsync(Guid id, EditLocationRequest request, CancellationToken ct)
    {
        var candidate = await db.Locations.AsNoTracking().SingleOrDefaultAsync(location => location.Id == id, ct);
        if (candidate is null) return null;
        MatchVersion(candidate, request.Version);
        var previousOwner = await ownership.ResolveAsync(candidate, ct);
        Guid previousTown = candidate.TownId;
        string? previousEircode = candidate.Eircode;
        candidate.Edit(request.Name, request.TownId, request.Eircode);
        await CheckTownAsync(candidate.TownId, previousTown, "TownId", ct);
        await SetTypeAsync(candidate, request.LocationTypeId, candidate.LocationTypeId, "LocationTypeId", ct);
        await CheckDuplicateAsync(candidate, request.ConfirmDuplicateName, ct);
        bool addressChanged = previousTown != candidate.TownId || previousEircode != candidate.Eircode;
        var resolved = addressChanged && candidate.PositionPrecision != LocationPositionPrecision.ConfirmedOnSite
            ? await positions.ResolveAsync(candidate, ct) : null;
        var proposedOwner = await ownership.ResolveAsync(candidate, ct);
        var staff = string.Equals(previousOwner?.RepSubject, proposedOwner?.RepSubject, StringComparison.Ordinal)
            ? null : await history.PrepareAsync([previousOwner, proposedOwner], ct);
        var operationId = Guid.NewGuid();
        return await GeographyTransactions.RunAsync(db, async () =>
        {
            var location = await db.Locations.SingleOrDefaultAsync(location => location.Id == id, ct);
            if (location is null) return null;
            byte[] version = MatchVersion(location, request.Version);
            var before = await ownership.ResolveAsync(location, ct);
            Guid existingTownId = location.TownId;
            Guid? existingTypeId = location.LocationTypeId;
            location.Edit(request.Name, request.TownId, request.Eircode);
            await CheckTownAsync(location.TownId, existingTownId, "TownId", ct);
            await SetTypeAsync(location, request.LocationTypeId, existingTypeId, "LocationTypeId", ct);
            await CheckDuplicateAsync(location, request.ConfirmDuplicateName, ct);
            if (resolved is not null) await positions.ApplyAsync(location, resolved, clock.GetUtcNow(), ct);
            db.Entry(location).Property(item => item.Version).OriginalValue = version;
            // An unchanged form must still validate its version and advance it on save.
            db.Entry(location).Property(item => item.Name).IsModified = true;
            history.Append(location.Id, location.Name, before, await ownership.ResolveAsync(location, ct),
                staff, operationId, OwnershipChangeCause.GeographyChange, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return await FindLocationAsync(id, ct);
        }, ct);
    }

    private static byte[] MatchVersion(Location location, string? expected)
    {
        byte[] version;
        try { version = Convert.FromBase64String(expected ?? string.Empty); }
        catch (FormatException) { throw new CustomerDirectoryValidationException("Version", "Reload this location before saving."); }
        if (version.Length != 8)
            throw new CustomerDirectoryValidationException("Version", "Reload this location before saving.");
        if (!location.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
        return version;
    }

    private async Task CheckTownAsync(Guid id, Guid? existingId, string field, CancellationToken ct)
    {
        if (!await db.Towns.AnyAsync(town => town.Id == id, ct))
            throw new CustomerDirectoryValidationException(field, "Choose a town");
        try { await geography.TownForAssignmentAsync(id, existingId, ct); }
        catch (GeographyValidationException exception)
        { throw new CustomerDirectoryValidationException(field, exception.Message); }
    }

    private async Task CheckDuplicateAsync(Location location, bool confirmed, CancellationToken ct)
    {
        if (!confirmed && await db.Locations.AnyAsync(other => other.CustomerId == location.CustomerId
            && other.Id != location.Id && other.NormalizedName == location.NormalizedName, ct))
            throw new DuplicateLocationNameException();
    }

    private async Task SetTypeAsync(Location location, Guid? typeId, Guid? existingTypeId, string field, CancellationToken ct)
    {
        if (typeId is Guid id)
        {
            var type = await db.LocationTypes.SingleOrDefaultAsync(type => type.Id == id, ct);
            if (type is null || type.IsArchived && existingTypeId != id)
                throw new CustomerDirectoryValidationException(field, "Choose an active location type.");
        }
        location.SetType(typeId);
    }

    private IQueryable<LocationRow> LocationRows(Guid? customerId = null, Guid? locationId = null) =>
        from location in db.Locations.AsNoTracking()
        where (customerId == null || location.CustomerId == customerId) && (locationId == null || location.Id == locationId)
        join customer in db.Customers on location.CustomerId equals customer.Id
        join town in db.Towns on location.TownId equals town.Id
        join county in db.Counties on town.CountyId equals county.Id
        join region in db.Regions on county.RegionId equals region.Id
        join type in db.LocationTypes on location.LocationTypeId equals (Guid?)type.Id into types
        from type in types.DefaultIfEmpty()
        orderby location.Name, location.Id
        select new LocationRow(location.Id, location.CustomerId, customer.Name, location.Name, location.Eircode, location.Version,
            town.Id, town.Name, town.IsArchived, county.Id, county.Name, county.IsArchived, region.Id, region.Name, region.IsArchived,
            type == null ? null : (Guid?)type.Id, type == null ? null : type.Name, type == null ? null : type.Description,
            type == null ? null : (bool?)type.IsArchived,
            location.Latitude, location.Longitude, location.PositionPrecision, location.PositionedAt);

    private static TownChoice Town(LocationRow row)
    {
        static string Label(string name, bool archived) => name + (archived ? " (archived)" : "");
        return new(row.TownId, row.TownName, row.CountyId, row.CountyName, row.RegionId, row.RegionName,
            $"{Label(row.TownName, row.TownArchived)} — {Label(row.CountyName, row.CountyArchived)}, {Label(row.RegionName, row.RegionArchived)}",
            row.TownArchived, row.CountyArchived, row.RegionArchived);
    }

    private sealed record LocationRow(Guid Id, Guid CustomerId, string CustomerName, string Name, string? Eircode, byte[] Version,
        Guid TownId, string TownName, bool TownArchived, Guid CountyId, string CountyName, bool CountyArchived,
        Guid RegionId, string RegionName, bool RegionArchived, Guid? TypeId, string? TypeName, string? TypeDescription, bool? TypeArchived,
        decimal? Latitude, decimal? Longitude, LocationPositionPrecision? PositionPrecision, DateTimeOffset? PositionedAt);

    private static LocationPosition? Position(LocationRow row) => row.Latitude is decimal latitude
        ? new(latitude, row.Longitude!.Value, row.PositionPrecision!.Value, row.PositionedAt!.Value) : null;

    private static DirectoryTypeChoice? Type(LocationRow row) => row.TypeId is Guid id
        ? new(id, row.TypeName!, row.TypeDescription, row.TypeArchived == true) : null;
}

public sealed class DuplicateLocationNameException() : Exception("This customer already has a location with that name");
