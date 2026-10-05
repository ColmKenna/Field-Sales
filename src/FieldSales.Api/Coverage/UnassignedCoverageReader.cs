using System.Security.Claims;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed class UnassignedCoverageReader(DirectoryDbContext db, CoverageOwnershipReader ownership)
{
    public Task<UnassignedCoveragePage> ReadAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor.GetStaffSubject())
            || !actor.IsInRole(BusinessRoles.SalesManager) && !actor.IsInRole(BusinessRoles.HeadOfficeUser))
            throw new CoverageReadForbiddenException();
        return GeographyTransactions.RunAsync(db, async () =>
        {
            // One serializable snapshot for resolution, labels, counts and action availability.
            // Unassigned rows need no staff-label lookup and cause no query per shop.
            var snapshot = await ownership.ReadAllAsync(ct);
            var customers = await (from location in db.Locations.AsNoTracking()
                join customer in db.Customers on location.CustomerId equals customer.Id
                select new { location.Id, CustomerId = customer.Id, CustomerName = customer.Name }).ToDictionaryAsync(row => row.Id, ct);
            var activeTowns = await (from town in db.Towns.AsNoTracking()
                join county in db.Counties on town.CountyId equals county.Id
                join region in db.Regions on county.RegionId equals region.Id
                where !town.IsArchived && !county.IsArchived && !region.IsArchived
                select town.Id).ToArrayAsync(ct);
            var active = activeTowns.ToHashSet();
            var towns = snapshot.Locations.Where(path => snapshot.Owners[path.LocationId] is null)
                .GroupBy(path => path.TownId).Select(group =>
                {
                    var town = group.First();
                    int count = group.Count();
                    var locations = group.OrderBy(path => path.LocationName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(path => customers[path.LocationId].CustomerName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(path => path.LocationId).Select(path =>
                        {
                            var customer = customers[path.LocationId];
                            var page = new LocationCoveragePage(path.LocationId, path.LocationName, path.TownId, path.TownName, null, count - 1);
                            return new UnassignedLocation(customer.CustomerId, customer.CustomerName,
                                new(page, null, 0, active.Contains(path.TownId), active.Contains(path.TownId), false));
                        }).ToArray();
                    return new UnassignedTown(town.TownId, town.TownName, town.CountyName, town.RegionName, locations);
                }).OrderBy(town => town.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(town => town.CountyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(town => town.RegionName, StringComparer.OrdinalIgnoreCase).ThenBy(town => town.Id).ToArray();
            return new UnassignedCoveragePage(towns);
        }, ct);
    }
}
