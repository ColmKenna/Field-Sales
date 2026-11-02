using System.Diagnostics;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-044 agreed scenarios 4, 5, 6, 9, 13 and 15: the Order Pad's category tree and search speed.</summary>
public sealed class OrderPadTests
{
    private readonly List<PadCategory> categories = [];
    private readonly List<ProductSearchResult> products = [];

    private PadCategory Category(string name, PadCategory? parent = null)
    {
        PadCategory category = new(Guid.NewGuid(), parent?.Id, name);
        categories.Add(category);
        return category;
    }

    private void Stock(PadCategory category, int count, string name = "Product")
    {
        for (int index = 1; index <= count; index++)
            products.Add(new(Guid.NewGuid(), $"C-{products.Count + 1:D4}", $"{name} {index:D2}", category.Name, "Each", 1m, category.Id));
    }

    private OrderPad Build() => OrderPad.Build(categories, products);

    private static OrderPadCategory Find(OrderPad pad, params string[] path)
    {
        OrderPadCategory current = pad.Categories.Single(category => category.Name == path[0]);
        foreach (string name in path.Skip(1)) current = current.Subcategories.Single(category => category.Name == name);
        return current;
    }

    [Fact]
    public void Should_ShowNoInGroup_When_CategoryHasOnlySubcategories()
    {
        PadCategory health = Category("Health");
        Stock(Category("Vitamins", health), 3);

        OrderPadCategory top = Find(Build(), "Health");

        Assert.Equal(0, top.Here);
        Assert.Empty(top.Products);
        Assert.Equal(3, top.Beneath);
        Assert.Single(top.Subcategories);
    }

    [Fact]
    public void Should_ShowInGroupAfterSubcategories_When_ProductSitsOnBranchCategory()
    {
        PadCategory health = Category("Health");
        PadCategory suncare = Category("Suncare", Category("Skincare", health));
        Stock(health, 1, "First Aid Kit");
        Stock(suncare, 12, "Sun Care Essentials");
        foreach (string name in new[] { "Lotions", "Sprays", "Kids", "Aftersun" }) Stock(Category(name, suncare), 42);

        OrderPad pad = Build();
        OrderPadCategory sun = Find(pad, "Health", "Skincare", "Suncare");

        // S1b: 4 subcategories, then "In Suncare (12)", 180 beneath.
        Assert.Equal(4, sun.Subcategories.Count);
        Assert.Equal(12, sun.Here);
        Assert.Equal(180, sun.Beneath);
        Assert.Equal("Health > Skincare > Suncare", sun.Breadcrumb);
        // A product on a top-level branch category is "In Health (1)" beside its subcategory.
        OrderPadCategory top = Find(pad, "Health");
        Assert.Equal("First Aid Kit 01", Assert.Single(top.Products).Name);
        Assert.Equal(181, top.Beneath);
    }

    [Fact]
    public void Should_CountEveryProductBeneathOnce_When_TreeIsFiveOrSixLevelsDeep()
    {
        PadCategory level = Category("L1");
        int expected = 0;
        for (int depth = 2; depth <= 6; depth++)
        {
            Stock(level, depth);
            expected += depth;
            Stock(Category($"Side {depth}", level), 1);
            expected++;
            level = Category($"L{depth}", level);
        }
        Stock(level, 7);
        expected += 7;

        OrderPad pad = Build();
        OrderPadCategory deepest = Find(pad, "L1", "L2", "L3", "L4", "L5", "L6");

        Assert.Equal(expected, Find(pad, "L1").Beneath);
        Assert.Equal(7, deepest.Beneath);
        Assert.Equal(14, Find(pad, "L1", "L2", "L3", "L4", "L5").Beneath); // 6 here + 1 in its side branch + 7 below.
        Assert.Equal("L1 > L2 > L3 > L4 > L5 > L6", deepest.Breadcrumb);
        Assert.Equal(products.Count, pad.Categories.Sum(category => category.Beneath));
    }

    [Fact]
    public void Should_HideCategory_When_NoProductsBeneathItAreDownloaded()
    {
        PadCategory health = Category("Health");
        Stock(Category("Vitamins", health), 2);
        Category("Specialist", health); // Its products are restricted, so none came down.
        Category("Empty branch", Category("Empty top"));

        OrderPad pad = Build();

        Assert.Equal(["Health"], pad.Categories.Select(category => category.Name));
        Assert.Equal(["Vitamins"], Find(pad, "Health").Subcategories.Select(category => category.Name));
    }

    [Fact]
    public void Should_OrderSubcategoriesAndProductsByName_When_CategoryIsExpanded()
    {
        PadCategory health = Category("Health");
        foreach (string name in new[] { "vitamins", "Éclat", "Baby", "Allergy" }) Stock(Category(name, health), 1);
        products.Add(new(Guid.NewGuid(), "B-2", "beta", "Health", "Each", 1m, health.Id));
        products.Add(new(Guid.NewGuid(), "A-9", "Alpha", "Health", "Each", 1m, health.Id));
        products.Add(new(Guid.NewGuid(), "B-1", "Beta", "Health", "Each", 1m, health.Id));

        OrderPadCategory top = Find(Build(), "Health");

        Assert.Equal(["Allergy", "Baby", "Éclat", "vitamins"], top.Subcategories.Select(category => category.Name));
        Assert.Equal(["A-9", "B-1", "B-2"], top.Products.Select(product => product.Code)); // Same name ignoring case: by code.
    }

    [Fact]
    public void Should_ReturnResultsWithinOneSecond_When_260ProductsAreSearched()
    {
        PadCategory skincare = Category("Skincare", Category("Health"));
        PadCategory suncare = Category("Suncare", skincare);
        Stock(Category("Lotions", suncare), 130, "Sun Lotion");
        Stock(Category("Lotions", Category("Body Care", skincare)), 130, "Body Lotion");
        ProductSearchIndex index = new("generation", products, categories);

        foreach (string query in new[] { "lotion", "sun lotion 07", "C-02", "body", "nothing like this" })
        {
            long started = Stopwatch.GetTimestamp();
            ProductSearchOutcome found = index.Search(query);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
            Assert.True(elapsed < TimeSpan.FromSeconds(1), $"'{query}' took {elapsed.TotalMilliseconds} ms over {index.Count} products.");
            Assert.True(found.Total >= 0);
        }
        Assert.Equal(260, index.Count);
        Assert.Equal(260, index.Search("lotion").Total);
    }
}
