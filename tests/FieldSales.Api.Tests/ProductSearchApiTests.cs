using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Catalogue.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace FieldSales.Api.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductSearchApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly ProductPriceApplication app = new(fixture.CreateConnectionString("SearchApi"));

    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();
    [Theory]
    [InlineData("SPF30", 3)]
    [InlineData("  spf30  ", 3)]
    [InlineData("SUN-0342", 1)]
    [InlineData("SunCo", 2)]
    [InlineData("GlowCo", 2)]
    [InlineData("Irish Health Supplies", 1)]
    public async Task Should_FindDistinctProductsWithFullDetails_When_AnySearchFieldMatches(string query, int count)
    {
        var seed = await SeedAsync();
        using var api = app.CreateClient();
        var result = await SearchAsync(api, query);
        Assert.Equal(count, result.Items.Count);
        Assert.Equal(count, result.Items.Select(item => item.Id).Distinct().Count());
        Assert.All(result.Items, item => { Assert.Equal("Active", item.State); Assert.NotEmpty(item.Breadcrumb); });
        if (query.Trim().Equals("SPF30", StringComparison.OrdinalIgnoreCase))
        {
            var first = result.Items.Single(item => item.Id == seed.First);
            var other = result.Items.Single(item => item.Id == seed.Other);
            Assert.Equal(first.Name, other.Name);
            Assert.NotEqual(string.Join(" > ", first.Breadcrumb.Select(s => s.Name)), string.Join(" > ", other.Breadcrumb.Select(s => s.Name)));
            Assert.Equal("SunCo", first.PrimaryBrand!.Name);
            Assert.Equal(12.50m, first.CurrentPrice!.Amount);
        }
    }

    [Fact]
    public async Task Should_ListAll180AndNoNeighbours_When_CategorySubtreeIsSelected()
    {
        var seed = await SeedAsync(bulk: true);
        using var api = app.CreateClient();
        var result = await SearchAsync(api, category: seed.Suncare);
        Assert.Equal(180, result.Items.Count);
        Assert.DoesNotContain(result.Items, item => item.Id == seed.Other);
        Assert.All(result.Items, item => Assert.Contains(item.Breadcrumb, part => part.Id == seed.Suncare));
        Assert.Contains(result.Items, item => item.Breadcrumb.Count >= 7);
        Assert.Contains(result.Items, item => item.Breadcrumb.Last().Id == seed.Suncare);
        Assert.Equal(result.Items.OrderBy(item => item.Code).Select(item => item.Id), result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Should_IntersectFiltersAndReturnEmptyMatches_When_SearchCategoryAndBrandAreCombined()
    {
        var seed = await SeedAsync();
        using var api = app.CreateClient();
        Assert.Equal(2, (await SearchAsync(api, "SPF30", seed.Suncare, seed.Primary)).Items.Count);
        Assert.Single((await SearchAsync(api, "0342", seed.Suncare, seed.Primary)).Items);
        Assert.Empty((await SearchAsync(api, "Body", seed.Suncare)).Items);
        Assert.Empty((await SearchAsync(api, "missing")).Items);
        Assert.Equal(3, (await SearchAsync(api, "   ")).Items.Count);
        foreach (string literal in new[] { "%", "_", "[SPF]", "' OR 1=1 --", "Health & Care" })
            Assert.Empty((await SearchAsync(api, literal)).Items);
    }

    [Fact]
    public async Task Should_KeepArchivedNamesSearchable_When_BrandOrSupplierIsArchived()
    {
        var seed = await SeedAsync();
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            (await db.Brands.SingleAsync(b => b.Id == seed.Primary)).Archive();
            (await db.Suppliers.SingleAsync()).Archive();
            await db.SaveChangesAsync();
        }
        using var api = app.CreateClient();
        var byBrand = await SearchAsync(api, "SunCo");
        Assert.Equal(2, byBrand.Items.Count);
        Assert.True(byBrand.Items.Single(item => item.Id == seed.First).PrimaryBrand!.IsArchived);
        Assert.True(byBrand.Brands.Single(item => item.Id == seed.Primary).IsArchived);
        Assert.Single((await SearchAsync(api, "Irish Health Supplies")).Items);
    }

    [Theory]
    [InlineData(22, 59, 12.50)]
    [InlineData(23, 0, 13.20)]
    public async Task Should_UseDublinBusinessDateAndExcludeFuturePrices_When_MidnightChanges(int hour, int minute, double amount)
    {
        var seed = await SeedAsync();
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var product = await db.Products.Include(p => p.BasePrices).SingleAsync(p => p.Id == seed.First);
            product.AddBasePrice(13.20m, new(2026, 10, 1));
            product.AddBasePrice(99m, new(2026, 11, 1));
            await db.SaveChangesAsync();
        }
        await using var host = app.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new SearchClock(new(2026, 9, 30, hour, minute, 0, TimeSpan.Zero)));
        }));
        using var api = host.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProductPriceApplication.Token());
        var result = await SearchAsync(api, "SUN-0342");
        Assert.Equal((decimal)amount, Assert.Single(result.Items).CurrentPrice!.Amount);
    }

    [Fact]
    public async Task Should_ShowNoCurrentPrice_When_FirstPriceIsFutureDated()
    {
        var seed = await SeedAsync();
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            await db.ProductBasePrices.Where(p => p.ProductId == seed.First).ExecuteDeleteAsync();
            db.ProductBasePrices.Add(ProductBasePrice.Create(seed.First, 99m, new(2026, 11, 1)));
            await db.SaveChangesAsync();
        }
        using var api = app.CreateClient();
        Assert.Null(Assert.Single((await SearchAsync(api, "SUN-0342")).Items).CurrentPrice);
    }

    [Fact]
    public async Task Should_RejectInvalidFiltersAndUnauthorisedReads_When_InputOrAccessIsInvalid()
    {
        await SeedAsync();
        using var api = app.CreateClient();
        foreach (string filter in new[] { "categoryId", "brandId" })
        {
            using var unknown = await api.GetAsync($"/catalogue/products/search?{filter}={Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            using var malformed = await api.GetAsync($"/catalogue/products/search?{filter}=invalid");
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        }
        api.DefaultRequestHeaders.Authorization = null;
        using var missing = await api.GetAsync("/catalogue/products/search");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProductPriceApplication.Token(expired: true));
        using var expired = await api.GetAsync("/catalogue/products/search");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProductPriceApplication.Token());
        app.Roles.Current = [BusinessRoles.SalesManager];
        using var denied = await api.GetAsync("/catalogue/products/search");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private async Task<Seed> SeedAsync(bool bulk = false)
    {
        app.Roles.Current = [BusinessRoles.HeadOfficeUser];
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductAlternativeBrands.ExecuteDeleteAsync();
        await db.ProductAttributeValues.ExecuteDeleteAsync();
        await db.ProductBasePrices.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        foreach (var category in (await db.Categories.ToListAsync()).Reverse<Category>()) db.Categories.Remove(category);
        await db.SaveChangesAsync();
        await db.Brands.ExecuteDeleteAsync();
        await db.Suppliers.ExecuteDeleteAsync();
        CategoryTree tree = new([]);
        var health = tree.Add("Health");
        var skincare = tree.Add("Skincare", health.Id);
        var suncare = tree.Add("Suncare", skincare.Id);
        var lotions = tree.Add("Lotions", suncare.Id);
        var body = tree.Add("Body Care", health.Id);
        var bodyLotions = tree.Add("Lotions", body.Id);
        db.Categories.AddRange(health, skincare, suncare, lotions, body, bodyLotions);
        Brand primary = Brand.Create("SunCo"), alternative = Brand.Create("GlowCo");
        Supplier supplier = Supplier.Create("Irish Health Supplies");
        db.AddRange(primary, alternative, supplier);
        Product first = Product.Create("SUN-0342", "SPF30 Sun Lotion 200ml", lotions.Id, 12.50m, new(2026, 8, 1));
        first.SetBrands(primary, [alternative, primary]);
        first.SetClassification(null, supplier);
        Product second = Product.Create("SUN-0341", "SPF30 Sun Lotion v2 200ml", lotions.Id, 11.20m, new(2026, 8, 1));
        second.SetBrands(alternative, [primary]);
        Product other = Product.Create("BODY-0342", first.Name, bodyLotions.Id, 10m, new(2026, 8, 1));
        db.AddRange(first, second, other);
        if (bulk)
        {
            var kids = tree.Add("Kids", suncare.Id);
            var sprays = tree.Add("Sprays", suncare.Id);
            var deep = tree.Add("Travel", sprays.Id);
            var deeper = tree.Add("Mini", deep.Id);
            var leaf = tree.Add("Pocket", deeper.Id);
            db.Categories.AddRange(kids, sprays, deep, deeper, leaf);
            foreach (var pair in new[] { (suncare.Id, 12), (lotions.Id, 62), (kids.Id, 48), (leaf.Id, 56) })
                for (int i = 0; i < pair.Item2; i++)
                    db.Products.Add(Product.Create($"BULK-{pair.Id:N}-{i:D3}", "Sun care", pair.Id, 1m, new(2026, 8, 1)));
        }
        await db.SaveChangesAsync();
        return new(first.Id, other.Id, suncare.Id, primary.Id);
    }

    private static async Task<ProductSearchResponse> SearchAsync(HttpClient api, string query = "", Guid? category = null, Guid? brand = null)
    {
        string url = "/catalogue/products/search?q=" + Uri.EscapeDataString(query);
        if (category is Guid categoryId) url += "&categoryId=" + categoryId;
        if (brand is Guid brandId) url += "&brandId=" + brandId;
        return (await api.GetFromJsonAsync<ProductSearchResponse>(url))!;
    }
    private sealed record Seed(Guid First, Guid Other, Guid Suncare, Guid Primary);
    private sealed class SearchClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
