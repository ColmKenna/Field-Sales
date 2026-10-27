using System.Data;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Coverage;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.TabletSync;

/// <summary>Builds one rep's Morning Snapshot (contract v1) on demand.</summary>
/// <remarks>
/// Locations come from T-3.1.1's resolver, never a copy of its rule. Version 1 carries only products with
/// no Restriction Group; WI-043 adds permitted restricted products through <see cref="RestrictedProductVisibility"/>.
/// Each database is read in one serializable transaction. The two families do not reference each other.
/// </remarks>
public sealed class TabletSnapshotBuilder(DirectoryDbContext directory, CoverageOwnershipReader ownership,
    CatalogueDbContext catalogue, TimeProvider clock)
{
    public async Task<TabletSnapshot> BuildAsync(string repSubject, CancellationToken ct)
    {
        repSubject = CoverageSubjects.Validate(repSubject, "RepSubject");
        DateTimeOffset builtUtc = clock.GetUtcNow();
        DateOnly buildDate = ProductEndpoints.Today(clock);
        SnapshotLocation[] locations = await ReadLocationsAsync(repSubject, ct);
        (SnapshotCategory[] categories, SnapshotProduct[] products, SnapshotBasePrice[] prices, SnapshotProductAttribute[] attributes) =
            await ReadCatalogueAsync(buildDate, ct);
        var manifest = new SnapshotManifest(SnapshotContract.Schema, SnapshotContract.CurrentVersion, repSubject,
            builtUtc, buildDate, new(locations.Length, categories.Length, products.Length, prices.Length, attributes.Length));
        return new(manifest, locations, categories, products, prices, attributes);
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
                                  location.MainContactId
                              }).ToArrayAsync(ct);
            Guid[] mainIds = rows.Where(row => row.MainContactId != null).Select(row => row.MainContactId!.Value)
                .Distinct().ToArray();
            // A retired Main Contact leaves MainContactId empty; an inactive contact is never shown.
            var contacts = await (from contact in directory.Contacts.AsNoTracking()
                                  where mainIds.Contains(contact.Id) && contact.Status == ContactStatus.Active
                                  join type in directory.ContactTypes on contact.ContactTypeId equals type.Id
                                  select new SnapshotContact(contact.Id, contact.Name, type.Name, contact.Phone, contact.Email))
                .ToDictionaryAsync(contact => contact.Id, ct);
            return rows.Select(row => new SnapshotLocation(row.Id, row.Name, row.CustomerId, row.CustomerName, row.Town,
                    row.Eircode, row.MainContactId is Guid main ? contacts.GetValueOrDefault(main) : null))
                .OrderBy(location => location.Name, StringComparer.OrdinalIgnoreCase).ThenBy(location => location.Id)
                .ToArray();
        }, ct);

    private Task<(SnapshotCategory[], SnapshotProduct[], SnapshotBasePrice[], SnapshotProductAttribute[])> ReadCatalogueAsync(DateOnly buildDate,
        CancellationToken ct) =>
        CatalogueTransactions.RunAsync(catalogue, IsolationLevel.Serializable, async _ =>
        {
            var products = await catalogue.Products.AsNoTracking().Where(product => product.RestrictionGroupId == null)
                .Select(product => new
                {
                    product.Id, product.Code, product.Name, product.CategoryId, product.Unit, product.QuantityStep,
                    product.MinimumQuantity, product.ParentProductId
                }).ToArrayAsync(ct);
            HashSet<Guid> included = [.. products.Select(product => product.Id)];
            // Use the same inclusion predicate and catalogue transaction as the products themselves.
            SnapshotProductAttribute[] attributes = await (from value in catalogue.ProductAttributeValues.AsNoTracking()
                join product in catalogue.Products on value.ProductId equals product.Id
                join name in catalogue.AttributeNames on value.AttributeNameId equals name.Id
                where product.RestrictionGroupId == null
                orderby value.ProductId, value.Position
                select new SnapshotProductAttribute(value.ProductId, value.Position, name.Name, value.Value, name.IsArchived))
                .ToArrayAsync(ct);
            var prices = await (from price in catalogue.ProductBasePrices.AsNoTracking()
                                join product in catalogue.Products on price.ProductId equals product.Id
                                where product.RestrictionGroupId == null
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
