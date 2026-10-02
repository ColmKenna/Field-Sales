using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public sealed class CustomerStore(DirectoryDbContext db, GeographyStore geography)
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
                location.Eircode, Convert.ToBase64String(location.Version))).ToArray());
    }

    public async Task<LocationDetails?> FindLocationAsync(Guid id, CancellationToken ct)
    {
        var row = await LocationRows(locationId: id).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.Id, row.CustomerId, row.CustomerName, row.Name, Town(row),
            row.Eircode, Convert.ToBase64String(row.Version));
    }

    public Task<CustomerDetails> CreateAsync(CreateCustomerRequest request, CancellationToken ct) =>
        GeographyTransactions.RunAsync(db, async () =>
        {
            if (request.FirstLocation is not { } first)
                throw new CustomerDirectoryValidationException("FirstLocation", "Add the first location before saving this customer.");
            Customer customer = Customer.Create(request.Name, first.Name, first.TownId, first.Eircode);
            await CheckTownAsync(customer.Locations[0].TownId, null, "FirstLocation.TownId", ct);
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            return (await FindCustomerAsync(customer.Id, ct))!;
        }, ct);

    public Task<LocationDetails?> AddLocationAsync(Guid customerId, CreateLocationRequest request, CancellationToken ct) =>
        GeographyTransactions.RunAsync(db, async () =>
        {
            if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, ct)) return null;
            Location location = Location.Create(customerId, request.Name, request.TownId, request.Eircode);
            await CheckTownAsync(location.TownId, null, "TownId", ct);
            await CheckDuplicateAsync(location, request.ConfirmDuplicateName, ct);
            db.Locations.Add(location);
            await db.SaveChangesAsync(ct);
            return await FindLocationAsync(location.Id, ct);
        }, ct);

    public Task<LocationDetails?> EditLocationAsync(Guid id, EditLocationRequest request, CancellationToken ct) =>
        GeographyTransactions.RunAsync(db, async () =>
        {
            var location = await db.Locations.SingleOrDefaultAsync(location => location.Id == id, ct);
            if (location is null) return null;
            byte[] version;
            try { version = Convert.FromBase64String(request.Version ?? string.Empty); }
            catch (FormatException) { throw new CustomerDirectoryValidationException("Version", "Reload this location before saving."); }
            if (version.Length != 8)
                throw new CustomerDirectoryValidationException("Version", "Reload this location before saving.");
            if (!location.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
            Guid existingTownId = location.TownId;
            location.Edit(request.Name, request.TownId, request.Eircode);
            await CheckTownAsync(location.TownId, existingTownId, "TownId", ct);
            await CheckDuplicateAsync(location, request.ConfirmDuplicateName, ct);
            db.Entry(location).Property(item => item.Version).OriginalValue = version;
            // An unchanged form must still validate its version and advance it on save.
            db.Entry(location).Property(item => item.Name).IsModified = true;
            await db.SaveChangesAsync(ct);
            return await FindLocationAsync(id, ct);
        }, ct);

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

    private IQueryable<LocationRow> LocationRows(Guid? customerId = null, Guid? locationId = null) =>
        from location in db.Locations.AsNoTracking()
        where (customerId == null || location.CustomerId == customerId) && (locationId == null || location.Id == locationId)
        join customer in db.Customers on location.CustomerId equals customer.Id
        join town in db.Towns on location.TownId equals town.Id
        join county in db.Counties on town.CountyId equals county.Id
        join region in db.Regions on county.RegionId equals region.Id
        orderby location.Name, location.Id
        select new LocationRow(location.Id, location.CustomerId, customer.Name, location.Name, location.Eircode, location.Version,
            town.Id, town.Name, town.IsArchived, county.Id, county.Name, county.IsArchived, region.Id, region.Name, region.IsArchived);

    private static TownChoice Town(LocationRow row)
    {
        static string Label(string name, bool archived) => name + (archived ? " (archived)" : "");
        return new(row.TownId, row.TownName, row.CountyId, row.CountyName, row.RegionId, row.RegionName,
            $"{Label(row.TownName, row.TownArchived)} — {Label(row.CountyName, row.CountyArchived)}, {Label(row.RegionName, row.RegionArchived)}",
            row.TownArchived, row.CountyArchived, row.RegionArchived);
    }

    private sealed record LocationRow(Guid Id, Guid CustomerId, string CustomerName, string Name, string? Eircode, byte[] Version,
        Guid TownId, string TownName, bool TownArchived, Guid CountyId, string CountyName, bool CountyArchived,
        Guid RegionId, string RegionName, bool RegionArchived);
}

public sealed class DuplicateLocationNameException() : Exception("This customer already has a location with that name");
