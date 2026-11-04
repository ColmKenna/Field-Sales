using System.Data;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Coverage;
using FieldSales.Api.Directory;
using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Directory.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FieldSales.Api.TabletSync;

/// <summary>Builds one rep's Morning Snapshot on demand.</summary>
/// <remarks>
/// Locations come from T-3.1.1's resolver, never a copy of its rule. Product visibility uses
/// <see cref="RestrictedProductVisibility"/> with the rep's current permissions, read in the same
/// catalogue transaction as the products and their facts.
/// The directory and catalogue are each read in one serializable transaction. The 1.3 history families (T-5.4.1) are read
/// from received work afterwards and keep only the rep's Locations and the products included here.
/// </remarks>
public sealed class TabletSnapshotBuilder(DirectoryDbContext directory, CoverageOwnershipReader ownership,
    CatalogueDbContext catalogue, RestrictionPermissionReader permissions, OrderingDbContext ordering,
    IOptions<TabletSnapshotOptions> options, TimeProvider clock)
{
    public async Task<TabletSnapshot> BuildAsync(string repSubject, CancellationToken ct)
    {
        repSubject = CoverageSubjects.Validate(repSubject, "RepSubject");
        DateTimeOffset builtUtc = clock.GetUtcNow();
        DateOnly buildDate = ProductEndpoints.Today(clock);
        SnapshotLocation[] locations = await ReadLocationsAsync(repSubject, ct);
        (SnapshotCategory[] categories, SnapshotProduct[] products, SnapshotBasePrice[] prices, SnapshotProductAttribute[] attributes) =
            await ReadCatalogueAsync(repSubject, buildDate, ct);
        HashSet<Guid> shops = [.. locations.Select(location => location.Id)];
        HashSet<Guid> included = [.. products.Select(product => product.Id)];
        SnapshotStockCheckLine[] lastChecks = await ReadLastStockChecksAsync(shops, included, ct);
        SnapshotRecentOrderLine[] recentOrders = await ReadRecentOrdersAsync(shops, included, ct);
        var manifest = new SnapshotManifest(SnapshotContract.Schema, SnapshotContract.CurrentVersion, repSubject,
            builtUtc, buildDate, new(locations.Length, categories.Length, products.Length, prices.Length, attributes.Length,
                lastChecks.Length, recentOrders.Length));
        return new(manifest, locations, categories, products, prices, attributes, lastChecks, recentOrders);
    }

    // Each Location's last Stock Check: its newest In-person Call with stock entries, whoever recorded it. Received Calls
    // never change and a Call's entries are stored with it, so a plain read sees whole Stock Checks.
    private async Task<SnapshotStockCheckLine[]> ReadLastStockChecksAsync(HashSet<Guid> shops, HashSet<Guid> included,
        CancellationToken ct)
    {
        Guid[] locationIds = [.. shops];
        Guid[] lastCallIds = await ordering.Calls.AsNoTracking()
            .Where(call => locationIds.Contains(call.LocationId) && call.Channel == CallChannel.InPerson && call.StockEntries.Any())
            .GroupBy(call => call.LocationId)
            .Select(group => group.OrderByDescending(call => call.SavedAt).ThenByDescending(call => call.Id).Select(call => call.Id).First())
            .ToArrayAsync(ct);
        var lines = await (from entry in ordering.CallStockEntries.AsNoTracking()
                           where lastCallIds.Contains(entry.CallId)
                           join call in ordering.Calls on entry.CallId equals call.Id
                           select new { call.LocationId, entry.ProductId }).ToArrayAsync(ct);
        return [.. lines.Where(line => included.Contains(line.ProductId))
            .Select(line => new SnapshotStockCheckLine(line.LocationId, line.ProductId))
            .OrderBy(line => line.LocationId).ThenBy(line => line.ProductId)];
    }

    // The products on each Location's most recent Accepted Orders, once per Location. Pending orders never count.
    private async Task<SnapshotRecentOrderLine[]> ReadRecentOrdersAsync(HashSet<Guid> shops, HashSet<Guid> included,
        CancellationToken ct)
    {
        Guid[] locationIds = [.. shops];
        int count = options.Value.SuggestedListOrderCount;
        // An order is recent when fewer than `count` Accepted orders at its Location are newer (by date taken, then
        // received), which SQL Server answers per Location from the (LocationId, Status, TakenOn) index.
        IQueryable<ReceivedOrder> accepted = ordering.Orders.AsNoTracking().Where(order => order.Status == OrderLifecycleStatus.Accepted);
        Guid[] orderIds = await accepted
            .Where(order => locationIds.Contains(order.LocationId))
            .Where(order => accepted.Count(newer => newer.LocationId == order.LocationId
                && (newer.TakenOn > order.TakenOn
                    || (newer.TakenOn == order.TakenOn && (newer.ReceivedUtc > order.ReceivedUtc
                        || (newer.ReceivedUtc == order.ReceivedUtc && newer.Id.CompareTo(order.Id) > 0))))) < count)
            .Select(order => order.Id)
            .ToArrayAsync(ct);
        // An order's lines stay in its received body, as accepted from the tablet.
        string[] bodies = await ordering.ReceivedWork.AsNoTracking().Where(item => orderIds.Contains(item.Id))
            .Select(item => item.Body).ToArrayAsync(ct);
        return [.. bodies.Select(body => JsonSerializer.Deserialize(body, OrderJsonContext.Default.TabletOrder)!)
            .SelectMany(order => order.Lines.Select(line => (order.LocationId, line.ProductId)))
            .Where(line => shops.Contains(line.LocationId) && included.Contains(line.ProductId))
            .Distinct()
            .Select(line => new SnapshotRecentOrderLine(line.LocationId, line.ProductId))
            .OrderBy(line => line.LocationId).ThenBy(line => line.ProductId)];
    }

    private Task<SnapshotLocation[]> ReadLocationsAsync(string repSubject, CancellationToken ct) =>
        GeographyTransactions.RunAsync(directory, async () =>
        {
            OwnershipSnapshot owners = await ownership.ReadAllAsync(ct);
            Guid[] assigned = owners.Owners.Where(owner => owner.Value?.RepSubject == repSubject)
                .Select(owner => owner.Key).ToArray();
            var rows = await (from location in directory.Locations.AsNoTracking()
                              where assigned.Contains(location.Id)
                              join customer in directory.Customers on location.CustomerId equals customer.Id
                              join town in directory.Towns on location.TownId equals town.Id
                              join county in directory.Counties on town.CountyId equals county.Id
                              select new
                              {
                                  location.Id, location.Name, location.CustomerId, CustomerName = customer.Name,
                                  Town = new SnapshotTown(town.Id, town.Name, county.Name), location.Eircode,
                                  location.MainContactId, location.RetiredMainContactId
                              }).ToArrayAsync(ct);
            Guid[] mainIds = rows.Where(row => row.MainContactId != null).Select(row => row.MainContactId!.Value)
                .Distinct().ToArray();
            // Preserve the active-only MainContact field for older apps. The optional 1.2 field
            // carries the outgoing identity/status without presenting that person as an active Main.
            var contacts = await (from contact in directory.Contacts.AsNoTracking()
                                  where mainIds.Contains(contact.Id) && contact.Status == ContactStatus.Active
                                  join type in directory.ContactTypes on contact.ContactTypeId equals type.Id
                                  select new SnapshotContact(contact.Id, contact.Name, type.Name, contact.Phone, contact.Email))
                .ToDictionaryAsync(contact => contact.Id, ct);
            Guid[] retiredIds = rows.Where(row => row.RetiredMainContactId != null)
                .Select(row => row.RetiredMainContactId!.Value).Distinct().ToArray();
            var replacements = await directory.Contacts.AsNoTracking()
                .Where(contact => retiredIds.Contains(contact.Id))
                .Select(contact => new SnapshotMainContactReplacement(contact.Id, contact.Name, contact.Status == ContactStatus.Inactive))
                .ToDictionaryAsync(contact => contact.ContactId, ct);
            return rows.Select(row => new SnapshotLocation(row.Id, row.Name, row.CustomerId, row.CustomerName, row.Town,
                    row.Eircode, row.MainContactId is Guid main ? contacts.GetValueOrDefault(main) : null,
                    row.RetiredMainContactId is Guid retired ? replacements[retired] : null))
                .OrderBy(location => location.Name, StringComparer.OrdinalIgnoreCase).ThenBy(location => location.Id)
                .ToArray();
        }, ct);

    private Task<(SnapshotCategory[], SnapshotProduct[], SnapshotBasePrice[], SnapshotProductAttribute[])> ReadCatalogueAsync(
        string repSubject, DateOnly buildDate, CancellationToken ct) =>
        CatalogueTransactions.RunAsync(catalogue, IsolationLevel.Serializable, async () =>
        {
            IReadOnlyCollection<Guid> permittedGroupIds = await permissions.CurrentGroupIdsAsync(repSubject, ct);
            IQueryable<Product> visibleProducts = catalogue.Products.AsNoTracking()
                .VisibleToRep(catalogue.RestrictionGroups, permittedGroupIds);
            var products = await visibleProducts
                .Select(product => new
                {
                    product.Id, product.Code, product.Name, product.CategoryId, product.Unit, product.QuantityStep,
                    product.MinimumQuantity, product.ParentProductId
                }).ToArrayAsync(ct);
            HashSet<Guid> included = [.. products.Select(product => product.Id)];
            // Use the same inclusion predicate and catalogue transaction as the products themselves.
            SnapshotProductAttribute[] attributes = await (from value in catalogue.ProductAttributeValues.AsNoTracking()
                join product in visibleProducts on value.ProductId equals product.Id
                join name in catalogue.AttributeNames on value.AttributeNameId equals name.Id
                orderby value.ProductId, value.Position
                select new SnapshotProductAttribute(value.ProductId, value.Position, name.Name, value.Value, name.IsArchived))
                .ToArrayAsync(ct);
            var prices = await (from price in catalogue.ProductBasePrices.AsNoTracking()
                                join product in visibleProducts on price.ProductId equals product.Id
                                select new { price.ProductId, price.EffectiveFrom, price.Amount }).ToArrayAsync(ct);
            Dictionary<Guid, SnapshotCategory> categories = await catalogue.Categories.AsNoTracking()
                .Select(category => new SnapshotCategory(category.Id, category.ParentId, category.Name))
                .ToDictionaryAsync(category => category.Id, ct);

            // Only the categories on the path to an included product, so breadcrumbs resolve on the tablet.
            HashSet<Guid> path = [];
            foreach (Guid categoryId in products.Select(product => product.CategoryId).Distinct())
            {
                Guid? next = categoryId;
                while (next is Guid id && path.Add(id)) next = categories[id].ParentId;
            }

            // The price in effect on the build date and every later one; superseded prices are omitted.
            // A product's dates are unique, so the effective entry is identified by its date.
            SnapshotBasePrice[] basePrices = prices.GroupBy(price => price.ProductId).SelectMany(group =>
            {
                DateOnly? effective = group.Where(price => price.EffectiveFrom <= buildDate)
                    .Select(price => (DateOnly?)price.EffectiveFrom).Max();
                return group.Where(price => price.EffectiveFrom > buildDate || price.EffectiveFrom == effective)
                    .Select(price => new SnapshotBasePrice(price.ProductId, price.EffectiveFrom, price.Amount));
            }).OrderBy(price => price.ProductId).ThenBy(price => price.EffectiveFrom).ToArray();

            return (path.Select(id => categories[id]).OrderBy(category => category.Id).ToArray(),
                products.Select(product => new SnapshotProduct(product.Id, product.Code, product.Name, product.CategoryId,
                        product.Unit, product.QuantityStep, product.MinimumQuantity,
                        product.ParentProductId is Guid parent && included.Contains(parent) ? parent : null))
                    .OrderBy(product => product.Code, StringComparer.Ordinal).ThenBy(product => product.Id).ToArray(),
                basePrices, attributes);
        }, ct);
}
