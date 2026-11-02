using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-044 agreed scenarios 1, 2, 3, 7, 8, 11, 12 and 14: New Order opens on the Order Pad by category (T-07, T7.3).</summary>
public sealed partial class NewOrderComponentTests
{
    private const string Suncare = "Health > Skincare > Suncare";
    private static readonly string[] SuncareSubcategories =
        [$"{Suncare} > Aftersun", $"{Suncare} > Kids", $"{Suncare} > Lotions", $"{Suncare} > Sprays"];

    /// <summary>S1b's tree: Suncare has 4 subcategories and 12 products directly in it, 180 beneath; "Lotions" also sits under
    /// Body Care; one product on Vitamins has no price today.</summary>
    private static (ProductSearchIndex Index, ProductSearchResult[] Products) PadCatalogue()
    {
        List<PadCategory> categories = [];
        List<ProductSearchResult> products = [];
        Dictionary<Guid, string> paths = [];
        PadCategory Add(string name, PadCategory? parent = null)
        {
            PadCategory category = new(Guid.NewGuid(), parent?.Id, name);
            categories.Add(category);
            paths[category.Id] = parent is null ? name : $"{paths[parent.Id]} > {name}";
            return category;
        }
        void Stock(PadCategory category, string name, int count, decimal? price = 2.50m)
        {
            for (int index = 1; index <= count; index++)
                products.Add(new(Guid.NewGuid(), Code(products.Count), $"{name} {index:D2}", paths[category.Id], "Each", price, category.Id));
        }

        // Letter codes, so a number typed in search matches product names only.
        static string Code(int n) => $"SKU-{(char)('A' + n / 676)}{(char)('A' + n / 26 % 26)}{(char)('A' + n % 26)}";
        PadCategory health = Add("Health"), skincare = Add("Skincare", health), suncare = Add("Suncare", skincare);
        Stock(suncare, "Sun Care Essentials", 12);
        Stock(Add("Lotions", suncare), "Sun Lotion", 60);
        Stock(Add("Sprays", suncare), "Sun Spray", 48);
        Stock(Add("Kids", suncare), "Kids Sun Cream", 40);
        Stock(Add("Aftersun", suncare), "Aftersun Gel", 20);
        Stock(Add("Lotions", Add("Body Care", skincare)), "Body Lotion", 20);
        Stock(Add("Vitamins", health), "Vitamin K", 1, price: null);
        return (new ProductSearchIndex("generation", products, categories), products.ToArray());
    }

    private static async Task<(Screen Screen, ProductSearchResult[] Products, FakeOrders Orders)> OpenPad()
    {
        (ProductSearchIndex index, ProductSearchResult[] products) = PadCatalogue();
        FakeOrders orders = new() { Products = products };
        Screen screen = Open(orders, index: index);
        await screen.Show();
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("Health"));
        return (screen, products, orders);
    }

    private static async Task Expand(Screen screen, params string[] breadcrumbs)
    {
        foreach (string breadcrumb in breadcrumbs) await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Renderer.ClickContaining(screen.Id, breadcrumb));
    }

    [Fact]
    public async Task Should_ListEveryDownloadedProductUnderItsCategoryWithBreadcrumbPriceAndAdd_When_NewOrderOpens()
    {
        (Screen screen, ProductSearchResult[] products, _) = await OpenPad();
        await using Screen disposed = screen;
        Assert.Contains("product-search", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.Contains($"{products.Length} products", screen.Renderer.Text(screen.Id)); // Every product sits beneath "Health".

        // Open every header until none is closed; every downloaded product is then on the pad exactly once.
        while (screen.Renderer.AttributeValues(screen.Id, "aria-expanded").Contains("false"))
        {
            int closed = screen.Renderer.AttributeValues(screen.Id, "aria-expanded").ToList().IndexOf("false");
            string label = screen.Renderer.Buttons(screen.Id).Where(text => text.Contains(" product")).ElementAt(closed);
            await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Renderer.Click(screen.Id, label));
        }

        string text = screen.Renderer.Text(screen.Id);
        Assert.Equal(products.Length, screen.Renderer.Buttons(screen.Id).Count(button => button == "Add"));
        Assert.All(products, product =>
        {
            Assert.NotEmpty(screen.Renderer.LabelledButton(screen.Id, $"Add {product.Name}")); // Its own Add, found by name.
            Assert.Contains($"{product.Code} · {product.Breadcrumb}", text);
        });
        Assert.Contains("€2.50", text);
    }

    [Fact]
    public async Task Should_ShowFourSubcategoriesThenInSuncare12And180Products_When_SuncareIsExpanded()
    {
        (Screen screen, _, _) = await OpenPad();
        await using Screen disposed = screen;

        await Expand(screen, "Health", "Health > Skincare", Suncare);

        string text = screen.Renderer.Text(screen.Id);
        string header = Assert.Single(screen.Renderer.Buttons(screen.Id), button => button.StartsWith(Suncare) && !button.StartsWith(Suncare + " >"));
        Assert.Contains("180 products", header);
        int headerAt = text.IndexOf(Suncare, StringComparison.Ordinal);
        int[] subcategories = SuncareSubcategories.Select(breadcrumb => text.IndexOf(breadcrumb, StringComparison.Ordinal)).ToArray();
        int here = text.IndexOf("In Suncare (12)", StringComparison.Ordinal);
        Assert.All(subcategories, position => Assert.InRange(position, headerAt + 1, here)); // Subcategories first, then "In Suncare".
        Assert.Equal(subcategories.Order(), subcategories); // Subcategories by name.
        Assert.Equal(12, screen.Renderer.Buttons(screen.Id).Count(button => button == "Add"));
    }

    [Fact]
    public async Task Should_ShowEachResultsBreadcrumb_When_SearchMatchesProductsUnderBothLotionsCategories()
    {
        (Screen screen, _, _) = await OpenPad();
        await using Screen disposed = screen;

        await screen.Type("product-search", "lotion 01");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("2 matching products"));

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Body Lotion 01", text);
        Assert.Contains("Health > Skincare > Body Care > Lotions", text);
        Assert.Contains("Sun Lotion 01", text);
        Assert.Contains($"{Suncare} > Lotions", text);
    }

    [Fact]
    public async Task Should_HideAndRevealChildren_When_HeaderIsCollapsedAndExpanded()
    {
        (Screen screen, _, _) = await OpenPad();
        await using Screen disposed = screen;

        await Expand(screen, "Health");
        Assert.Equal("true", screen.Renderer.AttributeValues(screen.Id, "aria-expanded")[0]);
        Assert.Contains("Health > Skincare", screen.Renderer.Text(screen.Id));
        Assert.Contains("Health > Vitamins", screen.Renderer.Text(screen.Id));

        await Expand(screen, "Health");
        Assert.Equal(["false"], screen.Renderer.AttributeValues(screen.Id, "aria-expanded"));
        Assert.DoesNotContain("Health > Skincare", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_StartWithTopLevelCategoriesCollapsed_When_PadOpens()
    {
        (Screen screen, _, _) = await OpenPad();
        await using Screen disposed = screen;

        Assert.Equal(["false"], screen.Renderer.AttributeValues(screen.Id, "aria-expanded"));
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("201 products", text);
        Assert.DoesNotContain("Health > ", text);
        Assert.DoesNotContain("Add", screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_AddLineAndShowQuantityOnRow_When_RepAddsFromPadRow()
    {
        (Screen screen, ProductSearchResult[] products, FakeOrders orders) = await OpenPad();
        await using Screen disposed = screen;
        await Expand(screen, "Health", "Health > Skincare", Suncare);

        await screen.ClickLabelled("Add Sun Care Essentials 01");
        Assert.Equal("", screen.Renderer.InputValue(screen.Id, "line-quantity")); // T7.7: never prefilled.
        await screen.Type("line-quantity", "3");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("3 on order"));

        await screen.ClickLabelled("Add Sun Care Essentials 01");
        await screen.Type("line-quantity", "2");
        await screen.Click("Add to order");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("5 on order"));

        ProductSearchResult added = products.Single(product => product.Name == "Sun Care Essentials 01");
        Assert.Equal([(added.Id, 3m), (added.Id, 2m)], orders.Calls.Select(call => (call.Product, call.Quantity)));
        Assert.Contains("In Suncare (12)", screen.Renderer.Text(screen.Id)); // The open headers stay open after an add.
    }

    [Fact]
    public async Task Should_ShowNoPriceYetAndDisableAdd_When_PadProductHasNoPriceToday()
    {
        (Screen screen, _, FakeOrders orders) = await OpenPad();
        await using Screen disposed = screen;

        await Expand(screen, "Health", "Health > Vitamins");

        Assert.Contains("No price yet", screen.Renderer.Text(screen.Id));
        Assert.Contains("disabled", screen.Renderer.LabelledButton(screen.Id, "Add Vitamin K 01").Keys);
        Assert.Empty(orders.Calls);
    }

    [Fact]
    public async Task Should_RestorePadWithExpansionKept_When_SearchIsCleared()
    {
        (Screen screen, _, _) = await OpenPad();
        await using Screen disposed = screen;
        await Expand(screen, "Health", "Health > Skincare", Suncare);

        await screen.Type("product-search", "lotion");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("matching products"));
        Assert.DoesNotContain("In Suncare (12)", screen.Renderer.Text(screen.Id));
        Assert.Empty(screen.Renderer.AttributeValues(screen.Id, "aria-expanded"));

        await screen.Type("product-search", "");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("In Suncare (12)"));
        Assert.Equal(3, screen.Renderer.AttributeValues(screen.Id, "aria-expanded").Count(value => value == "true"));
    }
}
