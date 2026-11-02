using FieldSales.Api.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.DemoData;

/// <summary>
/// WI-044: a five-level catalogue for looking at the tablet's Order Pad and timing its search on a device. "Suncare" has
/// 4 subcategories and 12 products directly in it, 180 beneath; "Lotions" sits under both Suncare and Body Care; and
/// "Health > Skincare > Suncare > Lotions > SPF50" is five levels deep. 256 products, each priced from 10 days ago.
/// </summary>
/// <remarks>Its own batch, on top of the sample seed, so it is applied once and never changes the sample records.</remarks>
public static class DemoPadSeeder
{
    public const string SeedName = "order-pad-v1";
    public const int Products = 256;

    public static Task<bool> SeedCatalogueAsync(CatalogueDbContext db, DateOnly? today = null) => DemoSeedBatch.RunAsync(db, async () =>
    {
        DateOnly from = (today ?? DateOnly.FromDateTime(DateTime.UtcNow)).AddDays(-10);
        CategoryTree tree = new(await db.Categories.ToArrayAsync());
        int code = 0;
        Category Add(string name, Category? parent = null)
        {
            Category category = tree.Add(name, parent?.Id);
            db.Categories.Add(category);
            return category;
        }
        void Stock(Category category, string name, int count)
        {
            for (int index = 1; index <= count; index++)
            {
                code++;
                db.Products.Add(Product.Create($"PAD-{code:D3}", $"{name} {index:D2}", category.Id, 2.00m + code % 40 * 0.25m, from));
            }
        }

        Category health = Add("Health");
        Category skincare = Add("Skincare", health);
        Category suncare = Add("Suncare", skincare);
        Stock(suncare, "Sun Care Essentials", 12);
        Category sunLotions = Add("Lotions", suncare);
        Stock(sunLotions, "Sun Lotion", 4);
        Stock(Add("SPF30", sunLotions), "SPF30 Sun Lotion", 28);
        Stock(Add("SPF50", sunLotions), "SPF50 Sun Lotion", 28);
        Stock(Add("Sprays", suncare), "Sun Spray", 48);
        Stock(Add("Kids", suncare), "Kids Sun Cream", 40);
        Stock(Add("Aftersun", suncare), "Aftersun Gel", 20);
        Category bodyCare = Add("Body Care", skincare);
        Stock(bodyCare, "Body Wash", 4);
        Stock(Add("Lotions", bodyCare), "Body Lotion", 20);
        Stock(Add("Hand Cream", bodyCare), "Hand Cream", 16);
        Category vitamins = Add("Vitamins", health);
        Stock(Add("Vitamin D", vitamins), "Vitamin D 1000IU", 18);
        Stock(Add("Vitamin C", vitamins), "Vitamin C 500mg", 18);
        if (code != Products) throw new InvalidOperationException($"The Order Pad sample has {code} products, not {Products}.");
        await db.SaveChangesAsync();
    }, SeedName);
}
