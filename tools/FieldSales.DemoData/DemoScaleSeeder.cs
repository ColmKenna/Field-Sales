using FieldSales.Api.Catalogue;
using FieldSales.Api.Coverage;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.DemoData;

/// <summary>
/// WI-032 scenario 25: adds the target scale to the demo databases so tablet Sync and reopening can be timed on a
/// real tablet. Colm gets 500 more shops in Swords (which he already covers) and the catalogue gets 25,000 products
/// with 800 categories; each product has today's price and two future ones, so his snapshot carries 75,000 prices.
/// </summary>
/// <remarks>Its own batch, on top of the sample seed, so it is applied once and never changes the sample records.</remarks>
public static class DemoScaleSeeder
{
    public const string SeedName = "scale-v1";
    public const int TargetProducts = 25_000;
    public const int TargetLocations = 500;
    private const int TopCategories = 20;
    private const int ShelvesPerCategory = 39;
    private const int SaveEvery = 2_000;

    public static Task<bool> SeedCatalogueAsync(CatalogueDbContext db, int products = TargetProducts, DateOnly? today = null) =>
        DemoSeedBatch.RunAsync(db, async () =>
        {
            DateOnly current = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            CategoryTree tree = new(await db.Categories.ToArrayAsync());
            List<Category> shelves = [];
            for (int range = 1; range <= TopCategories; range++)
            {
                Category top = tree.Add($"Scale range {range:D2}");
                db.Categories.Add(top);
                for (int shelf = 1; shelf <= ShelvesPerCategory; shelf++)
                {
                    Category leaf = tree.Add($"Scale shelf {range:D2}-{shelf:D2}", top.Id);
                    db.Categories.Add(leaf);
                    shelves.Add(leaf);
                }
            }
            await db.SaveChangesAsync();
            for (int index = 1; index <= products; index++)
            {
                decimal price = 1.50m + index % 400 * 0.25m;
                Product product = Product.Create($"SCALE-{index:D5}", $"Scale product {index:D5}", shelves[index % shelves.Count].Id,
                    price, current.AddDays(-10));
                product.AddBasePrice(price + 0.10m, current.AddDays(30));
                product.AddBasePrice(price + 0.20m, current.AddDays(90));
                db.Products.Add(product);
                if (index % SaveEvery == 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
            }
        }, SeedName);

    public static Task<bool> SeedDirectoryAsync(DirectoryDbContext db, int locations = TargetLocations) => DemoSeedBatch.RunAsync(db, async () =>
    {
        if (!await DemoSeedBatch.IsSeededAsync(db))
            throw new InvalidOperationException("Run the sample seed first; the scale shops join Colm's Swords territory.");
        Town swords = await db.Towns.SingleAsync(town => town.Name == "Swords");
        TerritoryAssignment colm = await db.TerritoryAssignments.SingleAsync(assignment =>
            assignment.RepSubject == DemoStaffSeeder.ColmSubject && assignment.TownId == swords.Id);
        ContactType manager = await db.ContactTypes.SingleAsync(type => type.Name == "Demo Store Manager");
        List<Location> shops = [];
        Customer? chain = null;
        for (int index = 0; index < locations; index++)
        {
            // Ten shops per customer, like a regional chain.
            string name = $"Scale shop {index + 1:D3}";
            if (index % 10 == 0)
            {
                chain = Customer.Create($"Scale Retail {index / 10 + 1:D3}", name, swords.Id, null);
                db.Customers.Add(chain);
                shops.Add(chain.Locations.Single());
            }
            else shops.Add(chain!.AddLocation(name, swords.Id, null));
        }
        // Persist shops before their rosters; the SQL link trigger sets each first Main Contact.
        await db.SaveChangesAsync();
        db.Contacts.AddRange(shops.Select((shop, index) => Contact.Create($"Scale manager {index + 1:D3}", manager,
            $"+353 1 555 {index + 1:D4}", null, [shop])));
        db.ChangeTracker.DetectChanges();
        foreach (Location shop in shops) db.Entry(shop).Property(item => item.MainContactId).IsModified = false;
        await db.SaveChangesAsync();

        HistoryStaffSnapshot staff = new(DemoStaffSeeder.HeadOfficeSubject, DemoStaffSeeder.Staff);
        EffectiveOwner owner = new(colm.RepSubject, new(colm.Id, colm.Target, swords.Name));
        Guid operation = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (Location shop in shops)
            db.AssignmentHistory.Add(AssignmentHistory.Capture(operation, shop.Id, shop.Name, null, owner, staff,
                OwnershipChangeCause.TerritoryAssignment, now, "Scale test coverage"));
    }, SeedName);
}
