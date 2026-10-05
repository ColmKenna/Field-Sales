using FieldSales.Api.Catalogue;
using FieldSales.Api.Coverage;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.Quantities;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.DemoData;

public static class DemoScenarioSeeder
{
    public static Task<bool> SeedCatalogueAsync(CatalogueDbContext db) => DemoSeedBatch.RunAsync(db, () =>
    {
        var health = new Category(Guid.NewGuid(), null, "Demo Health & Personal Care");
        var skin = new Category(Guid.NewGuid(), health.Id, "Skin Care");
        var essentials = new Category(Guid.NewGuid(), health.Id, "Everyday Essentials");
        db.Categories.AddRange(health, skin, essentials);
        var brand = Brand.Create("Demo Wicklow Care");
        var alternative = Brand.Create("Demo Coast Care");
        var archived = Brand.Create("Demo Retired Brand");
        archived.Archive();
        db.Brands.AddRange(brand, alternative, archived);
        var profile = ProductProfile.Create("Demo Retail");
        var supplier = Supplier.Create("Demo Supplies Ltd");
        var restriction = RestrictionGroup.Create("Demo Specialist Range");
        var size = AttributeName.Create("Demo Pack Size");
        db.ProductProfiles.Add(profile);
        db.Suppliers.Add(supplier);
        db.RestrictionGroups.Add(restriction);
        db.AttributeNames.Add(size);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cream = Product.Create("DEMO-001", "Demo Moisturising Cream", skin.Id, 8.50m, today.AddMonths(-3));
        cream.AddBasePrice(9.25m, today.AddMonths(-1));
        cream.AddBasePrice(9.75m, today.AddMonths(1));
        cream.SetBrands(brand, [alternative]);
        cream.SetClassification(profile, supplier);
        cream.AddAttribute(size, "100 ml");
        var soap = Product.Create("DEMO-002", "Demo Hand Soap", essentials.Id, 3.20m, today.AddMonths(-1));
        soap.SetBrands(brand, []);
        soap.SetClassification(profile, supplier);
        soap.AddAttribute(size, "250 ml");
        var specialist = Product.Create("DEMO-003", "Demo Specialist Balm", skin.Id, 14m, today.AddMonths(-1));
        specialist.SetRestrictionGroup(restriction);
        if (!QuantityRules.TryCreate("litre", 0.25m, 0.5m, out var rules, out _))
            throw new InvalidOperationException("Invalid demo quantity rules.");
        var bulk = Product.Create("DEMO-004", "Demo Bulk Hand Wash", essentials.Id, 6m, today.AddMonths(-1), rules);
        db.Products.AddRange(cream, soap, specialist, bulk);
        return Task.CompletedTask;
    });

    public static Task<bool> SeedDirectoryAsync(DirectoryDbContext db) => DemoSeedBatch.RunAsync(db, async () =>
    {
        var leinster = new Region("Demo Leinster");
        var munster = new Region("Demo Munster");
        var wicklow = new County(leinster.Id, "Wicklow");
        var dublin = new County(leinster.Id, "Dublin");
        var cork = new County(munster.Id, "Cork");
        var rathdrum = new Town(wicklow.Id, "Rathdrum");
        rathdrum.SetCoordinates(52.9319m, -6.2281m);
        var arklow = new Town(wicklow.Id, "Arklow");
        arklow.SetCoordinates(52.7982m, -6.1544m);
        var swords = new Town(dublin.Id, "Swords");
        swords.SetCoordinates(53.4597m, -6.2181m);
        var cobh = new Town(cork.Id, "Cobh");
        cobh.SetCoordinates(51.8503m, -8.2943m);
        var retiredTown = new Town(wicklow.Id, "Demo Archived Town");
        retiredTown.Archive();
        db.Regions.AddRange(leinster, munster);
        db.Counties.AddRange(wicklow, dublin, cork);
        db.Towns.AddRange(rathdrum, arklow, swords, cobh, retiredTown);
        var pharmacy = LocationType.Create("Demo Pharmacy", "Sample retail pharmacy");
        var shop = LocationType.Create("Demo Shop", "Sample general retailer");
        var buyerType = ContactType.Create("Demo Buyer", "Purchasing contact");
        var managerType = ContactType.Create("Demo Store Manager", "Day-to-day contact");
        db.LocationTypes.AddRange(pharmacy, shop);
        db.ContactTypes.AddRange(buyerType, managerType);
        var customer = Customer.Create("Demo Valley Pharmacies", "Rathdrum Branch", rathdrum.Id, null);
        var valley = customer.Locations.Single();
        var coast = customer.AddLocation("Arklow Branch", arklow.Id, null);
        var northCustomer = Customer.Create("Demo Northside Retail", "Swords Shop", swords.Id, null);
        var north = northCustomer.Locations.Single();
        var harbourCustomer = Customer.Create("Demo Harbour Stores", "Cobh Shop — Unassigned", cobh.Id, null);
        var harbour = harbourCustomer.Locations.Single();
        valley.SetType(pharmacy.Id);
        coast.SetType(pharmacy.Id);
        north.SetType(shop.Id);
        harbour.SetType(shop.Id);
        var now = DateTimeOffset.UtcNow;
        valley.ApplyDefaultPosition(new(52.9319m, -6.2281m), LocationPositionPrecision.Town, now);
        coast.ApplyDefaultPosition(new(52.7982m, -6.1544m), LocationPositionPrecision.Town, now);
        north.ApplyDefaultPosition(new(53.4597m, -6.2181m), LocationPositionPrecision.Town, now);
        db.Customers.AddRange(customer, northCustomer, harbourCustomer);
        // Persist locations before their rosters. The SQL link trigger sets the first
        // Main Contact, following the same write pattern as ContactStore.
        await db.SaveChangesAsync();
        db.Contacts.AddRange(
            Contact.Create("Niamh Demo", buyerType, null, "niamh@example.invalid", [valley, coast]),
            Contact.Create("Liam Demo", managerType, null, "liam@example.invalid", [valley]),
            Contact.Create("Sarah Demo", managerType, null, "sarah@example.invalid", [north]));
        db.ChangeTracker.DetectChanges();
        foreach (var location in new[] { valley, coast, north })
            db.Entry(location).Property(item => item.MainContactId).IsModified = false;
        await db.SaveChangesAsync();
        // Reload trigger-generated Main Contact and rowversion values.
        foreach (var location in new[] { valley, coast, north })
            await db.Entry(location).ReloadAsync();

        db.RepReportingLines.AddRange(
            RepReportingLine.Create(DemoStaffSeeder.AoifeSubject, DemoStaffSeeder.ManagerSubject),
            RepReportingLine.Create(DemoStaffSeeder.ColmSubject, DemoStaffSeeder.ManagerSubject));
        var countyAssignment = TerritoryAssignment.Create(DemoStaffSeeder.AoifeSubject, new(TerritoryLevel.County, wicklow.Id));
        var townAssignment = TerritoryAssignment.Create(DemoStaffSeeder.ColmSubject, new(TerritoryLevel.Town, swords.Id));
        var overrideAssignment = TerritoryAssignment.Create(DemoStaffSeeder.ColmSubject, new(TerritoryLevel.Location, coast.Id));
        db.TerritoryAssignments.AddRange(countyAssignment, townAssignment, overrideAssignment);
        var staff = new HistoryStaffSnapshot(DemoStaffSeeder.HeadOfficeSubject, DemoStaffSeeder.Staff);
        var operation = Guid.NewGuid();
        foreach (var (location, assignment, sourceName) in new[]
        {
            (valley, countyAssignment, wicklow.Name),
            (coast, overrideAssignment, coast.Name),
            (north, townAssignment, swords.Name)
        })
        {
            var owner = new EffectiveOwner(assignment.RepSubject, new(assignment.Id, assignment.Target, sourceName));
            db.AssignmentHistory.Add(AssignmentHistory.Capture(operation, location.Id, location.Name,
                null, owner, staff, OwnershipChangeCause.TerritoryAssignment, now, "Initial demo coverage"));
        }
    });
}
