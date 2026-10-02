extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Security;
using FieldSales.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using CategoryTree = CatalogueApi::FieldSales.Api.Catalogue.CategoryTree;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using Brand = CatalogueApi::FieldSales.Api.Catalogue.Brand;
using Supplier = CatalogueApi::FieldSales.Api.Catalogue.Supplier;

namespace FieldSales.Web.Tests;

public sealed class ProductSearchEndToEndTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    [Fact]
    public async Task Should_ShowDistinctPathsTodayPriceAndRecordLinks_When_SPF30IsSearched()
    {
        var seed = await SeedAsync();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, "/HeadOffice/Products?query=SPF30");
        Assert.Contains("3 products", html);
        Assert.Contains("Health > Skincare > Suncare > Lotions", html);
        Assert.Contains("Health > Body Care > Lotions", html);
        Assert.Contains("SunCo", html);
        Assert.Contains("Active", html);
        Assert.Contains("€12.50", html);
        Assert.DoesNotContain("€99.00", html);
        Assert.Contains($"href=\"/HeadOffice/Products/{seed.First}\"", html);
        Assert.Contains($"href=\"/HeadOffice/Products/{seed.Other}\"", html);
        Assert.Contains("href=\"/HeadOffice/Products/Create\"", html);
        Assert.Contains("+ Product", html);
        Assert.Matches("<input(?=[^>]*type=\"checkbox\")(?=[^>]*disabled)[^>]*>", html);
        Assert.Contains("SUN-0342", await PageAsync(browser, $"/HeadOffice/Products/{seed.First}"));
        Assert.Contains("Create product", await PageAsync(browser, "/HeadOffice/Products/Create"));
        Assert.Contains("/HeadOffice/Products", await PageAsync(browser, "/HeadOffice"));
        string? output = Environment.GetEnvironmentVariable("WI013_LAYOUT_DIR");
        if (!string.IsNullOrWhiteSpace(output))
        {
            System.IO.Directory.CreateDirectory(output);
            using var rendered = await browser.GetAsync("/HeadOffice/Products?query=SPF30");
            string preview = Regex.Replace(await rendered.Content.ReadAsStringAsync(), "href=\"/css/site.css[^\"]*\"", "href=\"site.css\"");
            await File.WriteAllTextAsync(System.IO.Path.Combine(output, "index.html"), preview);
        }
    }

    [Fact]
    public async Task Should_CombineFiltersAndKeepSelectedValues_When_CategoryAndBrandNarrowSearch()
    {
        var seed = await SeedAsync();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string url = $"/HeadOffice/Products?query=spf30&categoryId={seed.Suncare}&brandId={seed.Primary}";
        string html = await PageAsync(browser, url);
        Assert.Contains("2 products", html);
        Assert.DoesNotContain($"href=\"/HeadOffice/Products/{seed.Other}\"", html);
        Assert.Matches($"<option(?=[^>]*value=\"{seed.Suncare}\")(?=[^>]*selected=\"selected\")[^>]*>", html);
        Assert.Matches($"<option(?=[^>]*value=\"{seed.Primary}\")(?=[^>]*selected=\"selected\")[^>]*>", html);
        Assert.Contains("Clear filters", html);
        Assert.Contains("No products match", await PageAsync(browser, "/HeadOffice/Products?query=missing"));
        Assert.Contains("3 products", await PageAsync(browser, "/HeadOffice/Products?query=%20%20"));
        Assert.Contains("No products match", await PageAsync(browser, "/HeadOffice/Products?query=%25"));
    }

    [Theory]
    [InlineData("SUN-0342", 1)]
    [InlineData("GlowCo", 2)]
    [InlineData("Irish Health Supplies", 1)]
    public async Task Should_SearchCodeBothBrandRolesAndSupplier_When_FormQueryIsSubmitted(string query, int count)
    {
        await SeedAsync();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, "/HeadOffice/Products?query=" + Uri.EscapeDataString(query));
        Assert.Equal(count, Regex.Matches(html, "data-product-result").Count);
    }

    [Fact]
    public async Task Should_LabelAndFindArchivedBrand_When_ExistingClassificationIsArchived()
    {
        var seed = await SeedAsync();
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            (await db.Brands.SingleAsync(b => b.Id == seed.Primary)).Archive();
            (await db.Suppliers.SingleAsync()).Archive();
            await db.SaveChangesAsync();
        }
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        Assert.Contains("SunCo (archived)", await PageAsync(browser, "/HeadOffice/Products?query=SunCo"));
        Assert.Contains("1 product", await PageAsync(browser, "/HeadOffice/Products?query=Irish%20Health%20Supplies"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_DenySearchWithoutProductContent_When_RoleIsRemovedOrSessionExpires(bool expired)
    {
        await SeedAsync();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        await PageAsync(browser, "/HeadOffice/Products");
        if (expired)
        {
            await using var scope = website.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            (await db.Tickets.SingleAsync()).ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        else website.Roles.SetRoles("niamh", StaffRoles.SalesManager);
        using var response = await browser.GetAsync("/HeadOffice/Products?query=SPF30");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("SUN-0342", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Should_ReturnMissingOrInvalid_When_FilterIsUnknownOrMalformed()
    {
        await SeedAsync();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var unknown = await browser.GetAsync($"/HeadOffice/Products?categoryId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var malformed = await browser.GetAsync("/HeadOffice/Products?categoryId=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnUnavailableWithoutFakeEmptyResults_When_SearchApiFails()
    {
        await SeedAsync();
        await using var website = new StaffWebsiteFactory(new SearchFailure(), "test-only-token");
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var response = await browser.GetAsync("/HeadOffice/Products?query=SPF30");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("No products match", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Should_RenderEveryDescendantWithoutNeighbours_When_CategoryHas180Products()
    {
        var seed = await SeedAsync(bulk: true);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, $"/HeadOffice/Products?categoryId={seed.Suncare}");
        Assert.Contains("180 products", html);
        Assert.Equal(180, Regex.Matches(html, "data-product-result").Count);
        Assert.Contains("Suncare > Sprays > Travel > Mini > Pocket", html);
        Assert.DoesNotContain($"href=\"/HeadOffice/Products/{seed.Other}\"", html);
        Assert.Contains("BULK-177", html);
    }

    [Fact]
    public async Task Should_ShowEmptyCatalogueAndCreateLink_When_NoProductsExist()
    {
        await SeedAsync();
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            await db.ProductAlternativeBrands.ExecuteDeleteAsync();
            await db.ProductBasePrices.ExecuteDeleteAsync();
            await db.Products.ExecuteDeleteAsync();
        }
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, "/HeadOffice/Products");
        Assert.Contains("0 products", html);
        Assert.Contains("No products yet.", html);
        Assert.Contains("href=\"/HeadOffice/Products/Create\"", html);
    }

    private async Task<Seed> SeedAsync(bool bulk = false)
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductAlternativeBrands.ExecuteDeleteAsync();
        await db.ProductAttributeValues.ExecuteDeleteAsync();
        Guid category = await app.ResetAsync();
        await db.Brands.ExecuteDeleteAsync();
        await db.Suppliers.ExecuteDeleteAsync();
        var categories = await db.Categories.ToListAsync();
        CategoryTree tree = new(categories);
        var suncare = categories.Single(c => c.Name == "Suncare");
        var health = categories.Single(c => c.Name == "Health");
        var body = tree.Add("Body Care", health.Id);
        var bodyLotions = tree.Add("Lotions", body.Id);
        db.Categories.AddRange(body, bodyLotions);
        Brand primary = Brand.Create("SunCo"), alternative = Brand.Create("GlowCo");
        Supplier supplier = Supplier.Create("Irish Health Supplies");
        db.AddRange(primary, alternative, supplier);
        Product first = Product.Create("SUN-0342", "SPF30 Sun Lotion v2 200ml", category, 12.50m, new(2026, 8, 1));
        first.SetBrands(primary, [alternative, primary]);
        first.SetClassification(null, supplier);
        first.AddBasePrice(99m, new(2026, 11, 1));
        Product second = Product.Create("SUN-0341", "SPF30 Sun Lotion 200ml", category, 11.20m, new(2026, 8, 1));
        second.SetBrands(alternative, [primary]);
        Product other = Product.Create("BODY-0342", first.Name, bodyLotions.Id, 10m, new(2026, 8, 1));
        db.AddRange(first, second, other);
        if (bulk)
        {
            var sprays = tree.Add("Sprays", suncare.Id);
            var travel = tree.Add("Travel", sprays.Id);
            var mini = tree.Add("Mini", travel.Id);
            var pocket = tree.Add("Pocket", mini.Id);
            db.Categories.AddRange(sprays, travel, mini, pocket);
            for (int i = 0; i < 178; i++)
                db.Products.Add(Product.Create($"BULK-{i:D3}", "Sun care", i % 2 == 0 ? suncare.Id : pocket.Id,
                    1m, new(2026, 8, 1)));
        }
        await db.SaveChangesAsync();
        return new(first.Id, other.Id, suncare.Id, primary.Id);
    }

    private static async Task SignInAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync($"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(StaffRoles.HeadOfficeUser)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private static async Task<string> PageAsync(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private sealed record Seed(Guid First, Guid Other, Guid Suncare, Guid Primary);
    private sealed class SearchFailure : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/staff/session"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new FieldSales.StaffAccess.StaffSessionResponse("niamh", [StaffRoles.HeadOfficeUser])) }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
