extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using CategoryTree = CatalogueApi::FieldSales.Api.Catalogue.CategoryTree;
using Category = CatalogueApi::FieldSales.Api.Catalogue.Category;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using ProductBasePrice = CatalogueApi::FieldSales.Api.Catalogue.ProductBasePrice;

namespace FieldSales.Web.Tests;

public sealed class CategoryBrowseIntegrationTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    [Fact]
    public async Task Should_Show180Beneath12HereAndChildrenBeforeProducts_When_SuncareIsOpened()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, CategoryUrl(seed.Suncare));
        Assert.Contains("180 products beneath · 12 here", html);
        int childrenStart = html.IndexOf("<h2>Subcategories</h2>", StringComparison.Ordinal);
        int productsStart = html.IndexOf("<h2>In Suncare</h2>", StringComparison.Ordinal);
        Assert.True(childrenStart >= 0 && productsStart > childrenStart);
        string children = html[childrenStart..productsStart];
        Assert.Equal(4, Regex.Matches(children, "href=\"/HeadOffice/Categories/").Count);
        Assert.Contains("64 beneath", children);
        Assert.Contains("48 beneath", children);
        Assert.Contains("56 beneath", children);
        Assert.Contains("0 beneath", children);
        string products = html[productsStart..];
        Assert.Equal(12, Regex.Matches(products, "href=\"/HeadOffice/Products/").Count);
        for (int number = 1; number <= 12; number++)
            Assert.Single(Regex.Matches(products, $"SUN-{number:D4} · Direct product {number}"));
        Assert.True(products.IndexOf("SUN-0001", StringComparison.Ordinal) < products.IndexOf("SUN-0012", StringComparison.Ordinal));
        Assert.DoesNotContain("DEEP-", products);
        Assert.DoesNotContain("BODY-", products);
        Assert.Contains("SUN-0001 · Direct product 1", await PageAsync(browser, $"/HeadOffice/Products/{seed.FirstProduct}"));

        using HttpClient api = app.CreateApiClient();
        CategoryDetails details = (await api.GetFromJsonAsync<CategoryDetails>($"/catalogue/categories/{seed.Suncare}"))!;
        Assert.Equal((12, 180), (details.Category.Here, details.Category.Beneath));
        Assert.Equal(12, details.Products.Count);
        Assert.Equal(new[] { "After Sun", "Kids", "Lotions", "Sprays" }, details.Children.Select(child => child.Name));
        CategoryItem[] roots = (await api.GetFromJsonAsync<CategoryItem[]>("/catalogue/categories/"))!;
        Assert.Equal(203, roots.Single(root => root.Id == seed.Health).Beneath);
        Assert.Equal(9, roots.Single(root => root.Name == "Unrelated").Beneath);
    }

    [Theory]
    [InlineData("Lotions")]
    [InlineData("  lOtIoNs  ")]
    [InlineData("lotion")]
    public async Task Should_ShowBothFullBreadcrumbs_When_CategoryNamesMatchSearch(string query)
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, $"/HeadOffice/Categories?query={Uri.EscapeDataString(query)}");
        Assert.Contains($"href=\"{CategoryUrl(seed.Lotions)}\">Health > Skincare > Suncare > Lotions", html);
        Assert.Contains($"href=\"{CategoryUrl(seed.BodyLotions)}\">Health > Body Care > Lotions", html);
        Assert.Contains("Clear search", html);
        Assert.DoesNotContain("<h2>Root categories</h2>", html);
        using HttpClient api = app.CreateApiClient();
        CategorySearchResult[] results = (await api.GetFromJsonAsync<CategorySearchResult[]>(
            $"/catalogue/categories/search?q={Uri.EscapeDataString(query)}"))!;
        Assert.Equal(2, results.Length);
        Assert.Equal(new[] { "Health > Body Care > Lotions", "Health > Skincare > Suncare > Lotions" }, results.Select(result => result.Path));
    }

    [Fact]
    public async Task Should_RefreshSearchBreadcrumbsAndKeepCounts_When_AncestorIsRenamed()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CategoryUrl(seed.Suncare));
        using HttpResponseMessage renamed = await PostAsync(browser, $"{CategoryUrl(seed.Suncare)}?handler=Rename", form,
            "RenameName", "Sun Care & Protection");
        Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode);
        string search = await PageAsync(browser, "/HeadOffice/Categories?query=Lotions");
        Assert.Contains("Health > Skincare > Sun Care & Protection > Lotions", search);
        Assert.DoesNotContain("Suncare > Lotions", search);
        Assert.Contains($"href=\"{CategoryUrl(seed.Lotions)}\"", search);
        Assert.Contains("180 products beneath · 12 here", await PageAsync(browser, CategoryUrl(seed.Suncare)));
        Assert.Contains("Sun Care & Protection", await PageAsync(browser, $"/HeadOffice/Products/{seed.FirstProduct}"));
    }

    [Fact]
    public async Task Should_KeepDirectProductsAndPrices_When_AfterSunIsAdded()
    {
        Seed seed = await SeedAsync(includeAfterSun: false);
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var productsBefore = await db.Products.OrderBy(product => product.Id)
            .Select(product => new { product.Id, product.Code, product.CategoryId }).ToArrayAsync();
        var pricesBefore = await db.ProductBasePrices.OrderBy(price => price.ProductId).ThenBy(price => price.EffectiveFrom)
            .Select(price => new { price.ProductId, price.EffectiveFrom, price.Amount }).ToArrayAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CategoryUrl(seed.Suncare));
        using HttpResponseMessage added = await PostAsync(browser, CategoryUrl(seed.Suncare), form, "Name", "After Sun");
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        string child = await PageAsync(browser, added.Headers.Location!.OriginalString);
        Assert.Contains("0 products beneath · 0 here", child);
        Assert.Contains("No subcategories yet.", child);
        Assert.Contains("No products directly in this category.", child);
        string parent = await PageAsync(browser, CategoryUrl(seed.Suncare));
        Assert.Contains("180 products beneath · 12 here", parent);
        Assert.Equal(12, Regex.Matches(parent, "href=\"/HeadOffice/Products/").Count);
        Assert.DoesNotContain("Recategorise", parent);
        Assert.Equal(productsBefore, await db.Products.OrderBy(product => product.Id)
            .Select(product => new { product.Id, product.Code, product.CategoryId }).ToArrayAsync());
        Assert.Equal(pricesBefore, await db.ProductBasePrices.OrderBy(price => price.ProductId).ThenBy(price => price.EffectiveFrom)
            .Select(price => new { price.ProductId, price.EffectiveFrom, price.Amount }).ToArrayAsync());
    }

    [Fact]
    public async Task Should_CountSixthLevelWithoutCountingPriceRows_When_ProductsHavePriceHistory()
    {
        Seed seed = await SeedAsync();
        using HttpClient api = app.CreateApiClient();
        foreach ((Guid id, int here, int beneath) in new[]
                 { (seed.Lotions, 0, 64), (seed.Face, 0, 64), (seed.Daily, 64, 64), (seed.Kids, 0, 0) })
        {
            CategoryDetails details = (await api.GetFromJsonAsync<CategoryDetails>($"/catalogue/categories/{id}"))!;
            Assert.Equal((here, beneath), (details.Category.Here, details.Category.Beneath));
            Assert.Equal(here, details.Products.Count);
        }
        using HttpResponseMessage missing = await api.GetAsync($"/catalogue/categories/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Should_ShowRootBrowsingOrNoMatches_When_SearchIsBlankOrUnknown()
    {
        await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        Assert.Contains("<h2>Root categories</h2>", await PageAsync(browser, "/HeadOffice/Categories?query=%20%20"));
        Assert.Contains("No categories match “Missing”.", await PageAsync(browser, "/HeadOffice/Categories?query=Missing"));
        // Search matches the category name rather than every descendant's path.
        using HttpClient api = app.CreateApiClient();
        Assert.Single((await api.GetFromJsonAsync<CategorySearchResult[]>("/catalogue/categories/search?q=Suncare"))!);
        Assert.Empty((await api.GetFromJsonAsync<CategorySearchResult[]>("/catalogue/categories/search?q=%20%20"))!);
        Assert.Empty((await api.GetFromJsonAsync<CategorySearchResult[]>("/catalogue/categories/search?q=%25"))!);
    }

    [Theory]
    [InlineData(StaffRoles.FieldSalesperson)]
    [InlineData(StaffRoles.SalesManager)]
    [InlineData("")]
    public async Task Should_DenyCategoryReads_When_HeadOfficeRoleIsMissing(string role)
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        website.Roles.SetRoles("niamh", role.Length == 0 ? [] : [role]);
        app.Roles.SetRoles("niamh", role.Length == 0 ? [] : [role]);
        using HttpClient api = app.CreateApiClient();
        using HttpClient anonymous = app.Api.CreateClient();
        foreach (string path in new[] { "/catalogue/categories/", "/catalogue/categories/search?q=Lotions", $"/catalogue/categories/{seed.Suncare}" })
        {
            using HttpResponseMessage denied = await api.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using HttpResponseMessage unsigned = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, unsigned.StatusCode);
        }
        foreach (string path in new[] { "/HeadOffice/Categories?query=Lotions", CategoryUrl(seed.Suncare) })
        {
            using HttpResponseMessage denied = await browser.GetAsync(path);
            Assert.StartsWith("/AccessChanged?state=", denied.Headers.Location?.OriginalString);
        }
    }

    private async Task<Seed> SeedAsync(bool includeAfterSun = true)
    {
        Guid lotionsId = await app.ResetAsync();
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        List<Category> categories = await db.Categories.ToListAsync();
        CategoryTree tree = new(categories);
        Category suncare = categories.Single(category => category.Name == "Suncare");
        Category health = categories.Single(category => category.Name == "Health");
        Category Add(string name, Guid? parent = null)
        {
            Category category = tree.Add(name, parent);
            db.Categories.Add(category);
            return category;
        }
        Category sprays = Add("Sprays", suncare.Id);
        Category afterSun = Add(includeAfterSun ? "After Sun" : "Creams", suncare.Id);
        Category kids = Add("Kids", suncare.Id);
        Category face = Add("Face", lotionsId);
        Category daily = Add("Daily", face.Id);
        Category body = Add("Body Care", health.Id);
        Category bodyLotions = Add("Lotions", body.Id);
        Category unrelated = Add("Unrelated");
        Guid firstProduct = Guid.Empty;
        void Products(Guid categoryId, int count, string prefix, string name)
        {
            for (int number = 1; number <= count; number++)
            {
                Product product = Product.Create($"{prefix}-{number:D4}", $"{name} {number}", categoryId,
                    12.50m, new DateOnly(2026, 10, 1));
                db.Products.Add(product);
                if (prefix == "SUN" && number == 1)
                {
                    firstProduct = product.Id;
                    db.ProductBasePrices.Add(ProductBasePrice.Create(product.Id, 13m, new DateOnly(2026, 11, 1)));
                }
            }
        }
        Products(suncare.Id, 12, "SUN", "Direct product");
        Products(daily.Id, 64, "DEEP", "Deep lotion");
        Products(sprays.Id, 48, "SPRAY", "Spray");
        Products(afterSun.Id, 56, "AFTER", "After sun");
        Products(bodyLotions.Id, 20, "BODY", "Body lotion");
        Products(health.Id, 3, "HEALTH", "Health product");
        Products(unrelated.Id, 9, "OTHER", "Unrelated product");
        await db.SaveChangesAsync();
        return new(health.Id, suncare.Id, lotionsId, bodyLotions.Id, face.Id, daily.Id, kids.Id, firstProduct);
    }

    private sealed record Seed(Guid Health, Guid Suncare, Guid Lotions, Guid BodyLotions,
        Guid Face, Guid Daily, Guid Kids, Guid FirstProduct);
    private static string CategoryUrl(Guid id) => $"/HeadOffice/Categories/{id}";

    private static async Task SignInAsync(HttpClient browser)
    {
        using HttpResponseMessage response = await browser.GetAsync(
            $"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(StaffRoles.HeadOfficeUser)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> PageAsync(HttpClient browser, string path)
    {
        using HttpResponseMessage response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, string form, string field, string value)
    {
        Match token = Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "The category form must include an antiforgery token.");
        return browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [field] = value, ["__RequestVerificationToken"] = token.Groups[1].Value
        }));
    }
}
