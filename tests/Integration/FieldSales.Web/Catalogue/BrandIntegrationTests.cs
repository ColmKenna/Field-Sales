extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.ReferenceData;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Brand = CatalogueApi::FieldSales.Api.Catalogue.Brand;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using ProductBrandAssignments = CatalogueApi::FieldSales.Api.Catalogue.ProductBrandAssignments;
using ProductBrandUsageSource = CatalogueApi::FieldSales.Api.Catalogue.ProductBrandUsageSource;

namespace FieldSales.Web.Tests;

public sealed class BrandIntegrationTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    private const string Page = "/HeadOffice/ReferenceData";
    private const string ApiPath = "/catalogue/reference-data/brands";

    [Fact]
    public async Task Should_Return404OnEveryRoute_When_ReferenceKeyIsUnknown()
    {
        using var api = app.CreateApiClient();
        string root = "/catalogue/reference-data/unknown";
        Guid id = Guid.NewGuid();
        foreach (string path in new[] { root, root + "/choices", $"{root}/{id}" })
        {
            using var response = await api.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var create = await api.PostAsJsonAsync(root, new { Name = "name" });
        using var rename = await api.PutAsJsonAsync($"{root}/{id}/name", new { Name = "name" });
        using var retire = await api.PostAsJsonAsync($"{root}/{id}/retire", new { Action = ReferenceAction.Delete });
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, retire.StatusCode);
    }

    [Fact]
    public async Task Should_SaveBrand_When_NameIsValid()
    {
        await ResetAsync();
        Guid id;
        await using (var website = app.CreateWebsite())
        using (var browser = website.CreateBrowser())
        {
            await SignInAsync(browser);
            using var saved = await PostAsync(browser, "Save", await HtmlAsync(browser), new() { ["Name"] = " SunCo " });
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            id = (await ItemsAsync()).Single().Id;
            using var renamed = await PostAsync(browser, "Save", await HtmlAsync(browser), new()
                { ["Id"] = id.ToString(), ["Name"] = " Sunshine " });
            Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode);
        }
        await using var restartedApi = app.NewApi();
        await using var restartedWebsite = app.CreateWebsite(restartedApi);
        using var reopened = restartedWebsite.CreateBrowser();
        await SignInAsync(reopened);
        Assert.Contains("Sunshine", await HtmlAsync(reopened));
        Assert.DoesNotContain("SunCo", await HtmlAsync(reopened));
        Assert.Equal(id, (await ItemsAsync()).Single().Id);
        Assert.Equal("Sunshine", (await ItemsAsync()).Single().Name);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("too-long", false)]
    [InlineData(" sunco ", false)]
    [InlineData("SUNCO", true)]
    public async Task Should_RejectName_When_BlankTooLongOrDuplicate(string name, bool archived)
    {
        await ResetAsync();
        Guid id = await AddBrandAsync("SunCo", archived);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var saved = await PostAsync(browser, "Save", await HtmlAsync(browser), new()
            { ["Name"] = name == "too-long" ? new string('X', 201) : name });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        string html = WebUtility.HtmlDecode(await saved.Content.ReadAsStringAsync());
        Assert.Contains(name.Trim().Equals("SunCo", StringComparison.OrdinalIgnoreCase)
            ? "A brand with this name already exists." : "Enter a", html);
        Assert.Equal(id, (await ItemsAsync()).Single().Id);
        Assert.Equal("SunCo", (await ItemsAsync()).Single().Name);
        using var api = app.CreateApiClient();
        using var second = await api.PostAsJsonAsync(ApiPath, new { Name = name == "too-long" ? new string('X', 201) : name });
        Assert.Equal(name.Trim().Equals("SunCo", StringComparison.OrdinalIgnoreCase)
            ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, second.StatusCode);
        Guid other = await AddBrandAsync("OtherCo");
        using var renamed = await PostAsync(browser, "Save", await HtmlAsync(browser, Page + "?ShowArchived=true"),
            new() { ["Id"] = other.ToString(), ["Name"] = name == "too-long" ? new string('X', 201) : name });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("OtherCo", (await ItemsAsync()).Single(item => item.Id == other).Name);
    }

    [Fact]
    public async Task Should_DeleteBrand_When_UnreferencedAndConfirmed()
    {
        await ResetAsync();
        Guid id = await AddBrandAsync("TestCo");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string row = Row(await HtmlAsync(browser), id);
        Assert.Contains(">Delete</a>", row);
        Assert.DoesNotContain(">Archive</a>", row);
        string confirm = await HtmlAsync(browser, Confirm(id));
        Assert.Contains("Cancel", confirm);
        await HtmlAsync(browser); // Cancel is navigation, not a mutation.
        Assert.Single(await ItemsAsync());
        using var removed = await PostAsync(browser, "Retire", confirm, RetireFields(id, ReferenceAction.Delete));
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Empty(await ItemsAsync());
        using var api = app.CreateApiClient();
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync($"{ApiPath}/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("specialist")]
    [InlineData("failure")]
    [InlineData("missing-products")]
    public async Task Should_OfferArchive_When_ReferencesExist(string provider)
    {
        Guid category = await ResetAsync();
        Guid id = await AddBrandAsync("SunCo");
        await SeedProductsAsync(category, id, 24);
        await using var api = app.NewApi().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            if (provider == "missing-products") services.RemoveAll<IReferenceUsageSource>();
            else services.AddSingleton<IReferenceUsageSource>(new ExtraUsageSource(id, provider == "failure"));
        }));
        await using var website = app.CreateWebsite(api);
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        if (provider != "specialist")
        {
            using var page = await browser.GetAsync(Page);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
            using var client = Client(api);
            using var failed = await client.PostAsJsonAsync($"{ApiPath}/{id}/retire", new { Action = ReferenceAction.Delete });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            Assert.False((await ItemsAsync()).Single().IsArchived);
            return;
        }
        string row = Row(await HtmlAsync(browser), id);
        Assert.Contains("Used by 24 products and 1 specialist assignment", row);
        Assert.Contains(">Archive</a>", row);
        Assert.DoesNotContain(">Delete</a>", row);
        string confirmation = await HtmlAsync(browser, Confirm(id));
        Assert.Contains("It stays on existing records and stops appearing in new selections.", confirmation);
        using var archived = await PostAsync(browser, "Retire", confirmation, RetireFields(id, ReferenceAction.Archive));
        Assert.Equal(HttpStatusCode.Redirect, archived.StatusCode);
        Assert.True((await ItemsAsync()).Single().IsArchived);
        Assert.Equal(24, (await app.CountsAsync()).Products);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PreserveProductReferences_When_BrandIsArchived(bool alternativeOnly)
    {
        Guid category = await ResetAsync();
        Guid brand = await AddBrandAsync("SunCo");
        Guid primary = alternativeOnly ? await AddBrandAsync("OtherCo") : brand;
        Guid product = (await SeedProductsAsync(category, primary, 1, alternativeOnly ? brand : null)).Single();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var archived = await PostAsync(browser, "Retire", await HtmlAsync(browser, Confirm(brand)), RetireFields(brand, ReferenceAction.Archive));
        Assert.Equal(HttpStatusCode.Redirect, archived.StatusCode);
        Assert.Contains("SunCo (archived)", await HtmlAsync(browser, $"/HeadOffice/Products/{product}"));
        using var client = app.CreateApiClient();
        var details = (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{product}"))!;
        var linked = Assert.Single(details.Brands!, item => item.Id == brand);
        Assert.True(linked.IsArchived);
        Assert.Equal(!alternativeOnly, linked.IsPrimary);
        Assert.Equal(4.80m, Assert.Single(details.PriceHistory).Amount);
    }

    [Fact]
    public async Task Should_ExcludeArchivedBrand_When_NewSelectionsAreRequested()
    {
        Guid category = await ResetAsync();
        Guid brand = await AddBrandAsync("SunCo", archived: true);
        Guid product = (await SeedProductsAsync(category, null, 1)).Single();
        using var api = app.CreateApiClient();
        Assert.Empty((await api.GetFromJsonAsync<ReferenceChoice[]>($"{ApiPath}/choices"))!);
        await using var scope = app.Api.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<ProductBrandAssignments>()
            .SetAsync(product, brand, []));
        var persisted = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products.AsNoTracking().SingleAsync();
        Assert.Null(persisted.PrimaryBrandId);
    }

    [Fact]
    public async Task Should_HideArchivedBrands_When_ToggleIsOff()
    {
        await ResetAsync();
        for (int index = 1; index <= 3; index++) await AddBrandAsync($"Retired {index}", archived: true);
        await AddBrandAsync("ActiveCo");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string active = await HtmlAsync(browser);
        Assert.Contains("Show archived (3)", active);
        Assert.Contains("ActiveCo", active);
        Assert.DoesNotContain("Retired 1", active);
        string all = await HtmlAsync(browser, Page + "?ShowArchived=true");
        foreach (int index in new[] { 1, 2, 3 }) Assert.Contains($"Retired {index} (archived)", all);
        Assert.Contains(">Un-archive</a>", all);
    }

    [Fact]
    public async Task Should_RestoreSelection_When_BrandIsUnarchived()
    {
        await ResetAsync();
        Guid id = await AddBrandAsync("SunCo", archived: true);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string confirm = await HtmlAsync(browser, Confirm(id));
        Assert.Contains("Un-archive", confirm);
        using var restored = await PostAsync(browser, "Retire", confirm, RetireFields(id, ReferenceAction.Unarchive));
        Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        using var api = app.CreateApiClient();
        Assert.Equal(id, Assert.Single((await api.GetFromJsonAsync<ReferenceChoice[]>($"{ApiPath}/choices"))!).Id);
    }

    [Fact]
    public async Task Should_CountProductOnce_When_BrandHasMultipleLinks()
    {
        Guid category = await ResetAsync();
        Guid brand = await AddBrandAsync("SunCo");
        Guid other = await AddBrandAsync("OtherCo");
        await SeedProductsAsync(category, brand, 1, brand);
        await SeedProductsAsync(category, other, 1, brand);
        var item = (await ItemsAsync()).Single(item => item.Id == brand);
        Assert.Equal("Used by 2 products", item.Usage.Description);
        Assert.Equal(2, Assert.Single(item.Usage.Counts).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RefuseDelete_When_ReferenceAppearsAfterPageLoad(bool concurrent)
    {
        Guid category = await ResetAsync();
        Guid brand = await AddBrandAsync("TestCo");
        Guid product = (await SeedProductsAsync(category, null, 1)).Single();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string confirm = await HtmlAsync(browser, Confirm(brand));
        if (!concurrent)
        {
            await using var assign = app.Api.Services.CreateAsyncScope();
            await assign.ServiceProvider.GetRequiredService<ProductBrandAssignments>().SetAsync(product, brand, []);
            using var refused = await PostAsync(browser, "Retire", confirm, RetireFields(brand, ReferenceAction.Delete));
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains("now in use", await refused.Content.ReadAsStringAsync());
            Assert.Contains(">Archive</a>", Row(WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()), brand));
        }
        else
        {
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var concurrentApi = app.NewApi().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IReferenceUsageSource>();
                services.AddScoped<IReferenceUsageSource>(provider => new SignallingProductSource(
                    new ProductBrandUsageSource(provider.GetRequiredService<CatalogueDbContext>()), reached));
            }));
            await using var scope = app.Api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Products SET PrimaryBrandId = {brand} WHERE Id = {product}");
            using var client = Client(concurrentApi);
            var deletion = client.PostAsJsonAsync($"{ApiPath}/{brand}/retire", new { Action = ReferenceAction.Delete });
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await transaction.CommitAsync();
            using var refused = await deletion;
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        }
        Assert.False((await ItemsAsync()).Single().IsArchived);
        Assert.True((await ItemsAsync()).Single().Usage.IsUsed);
        await using var check = app.Api.Services.CreateAsyncScope();
        var saved = await check.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products.AsNoTracking().SingleAsync();
        Assert.Equal(brand, saved.PrimaryBrandId);
    }

    [Fact]
    public async Task Should_PreserveArchivedBrand_When_ExistingProductIsEdited()
    {
        Guid category = await ResetAsync();
        Guid brand = await AddBrandAsync("SunCo");
        Guid product = (await SeedProductsAsync(category, brand, 1, brand)).Single();
        using var api = app.CreateApiClient();
        using var archive = await api.PostAsJsonAsync($"{ApiPath}/{brand}/retire", new { Action = ReferenceAction.Archive });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        await using (var scope = app.Api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ProductBrandAssignments>().SetAsync(product, brand, [brand]);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string record = await HtmlAsync(browser, $"/HeadOffice/Products/{product}");
        using var edited = await PostToAsync(browser, $"/HeadOffice/Products/{product}?handler=Price", record,
            new() { ["BasePrice"] = "5.00", ["EffectiveFrom"] = "2026-11-01" });
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        Assert.Contains("SunCo (archived)", await HtmlAsync(browser, $"/HeadOffice/Products/{product}"));
        Assert.Equal(2, (await app.CountsAsync()).Prices);
    }

    [Theory]
    [InlineData(StaffRoles.FieldSalesperson)]
    [InlineData(StaffRoles.SalesManager)]
    [InlineData("none")]
    [InlineData("expired")]
    public async Task Should_DenyMutation_When_StaffAccessIsMissing(string access)
    {
        await ResetAsync();
        Guid id = await AddBrandAsync("TestCo");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string confirm = await HtmlAsync(browser, Confirm(id));
        using (var forged = await browser.PostAsync(Page + "?handler=Retire", new FormUrlEncodedContent(RetireFields(id, ReferenceAction.Delete))))
            Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        if (access == "expired")
        {
            await using var scope = website.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            var ticket = await db.Tickets.SingleAsync();
            ticket.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        else
        {
            string[] roles = access == "none" ? [] : [access];
            website.Roles.SetRoles("niamh", roles);
            app.Roles.SetRoles("niamh", roles);
        }
        using var denied = await PostAsync(browser, "Retire", confirm, RetireFields(id, ReferenceAction.Delete));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith(access == "expired" ? "https://localhost:7201/connect/authorize" : "/AccessChanged?state=",
            denied.Headers.Location!.OriginalString);
        using var api = access == "expired" ? app.Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }) : app.CreateApiClient();
        foreach (string path in new[] { ApiPath, $"{ApiPath}/{id}/name", $"{ApiPath}/{id}/retire" })
        {
            using var write = path.EndsWith("/name") ? await api.PutAsJsonAsync(path, new { Name = "Changed" })
                : await api.PostAsJsonAsync(path, new { Name = "Added", Action = ReferenceAction.Delete });
            Assert.Equal(access == "expired" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, write.StatusCode);
        }
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await SignInAsync(browser);
        await HtmlAsync(browser);
        Assert.Equal(id, (await ItemsAsync()).Single().Id);
        Assert.Equal("TestCo", (await ItemsAsync()).Single().Name);
    }

    private async Task<Guid> ResetAsync()
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductAlternativeBrands.ExecuteDeleteAsync();
        Guid category = await app.ResetAsync();
        await db.Brands.ExecuteDeleteAsync();
        return category;
    }
    private async Task<Guid> AddBrandAsync(string name, bool archived = false)
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var brand = Brand.Create(name);
        if (archived) brand.Archive();
        db.Brands.Add(brand);
        await db.SaveChangesAsync();
        return brand.Id;
    }
    private async Task<Guid[]> SeedProductsAsync(Guid category, Guid? primary, int count, Guid? alternative = null)
    {
        List<Guid> ids = [];
        for (int index = 0; index < count; index++)
        {
            await using var scope = app.Api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var product = Product.Create(Guid.NewGuid().ToString(), "Lotion", category, 4.80m, new DateOnly(2026, 10, 1));
            db.Products.Add(product);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ProductBrandAssignments>().SetAsync(product.Id, primary,
                alternative is Guid id ? [id] : []);
            ids.Add(product.Id);
        }
        return ids.ToArray();
    }
    private async Task<ReferenceListItem[]> ItemsAsync()
    {
        using var api = app.CreateApiClient();
        return (await api.GetFromJsonAsync<ReferenceListViewModel>(ApiPath + "?showArchived=true"))!.Items.ToArray();
    }
    private HttpClient Client(WebApplicationFactory<CatalogueDbContext> api)
    {
        using var original = app.CreateApiClient();
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        return client;
    }
    private static string Confirm(Guid id) => $"{Page}?handler=Confirm&id={id}&ShowArchived=true";
    private static Dictionary<string, string> RetireFields(Guid id, ReferenceAction action) => new()
        { ["Id"] = id.ToString(), ["Action"] = ((int)action).ToString() };
    private static string Row(string html, Guid id)
    {
        var match = Regex.Match(html, $"<section[^>]*data-reference-id=\"{id}\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
        Assert.True(match.Success);
        return match.Value;
    }
    private static async Task<string> HtmlAsync(HttpClient browser, string path = Page)
    {
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string handler, string html, Dictionary<string, string> fields)
    {
        fields["ListKey"] = "brands";
        fields["ShowArchived"] = "true";
        return PostToAsync(browser, Page + "?handler=" + handler, html, fields);
    }
    private static Task<HttpResponseMessage> PostToAsync(HttpClient browser, string path, string html, Dictionary<string, string> fields)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return browser.PostAsync(path, new FormUrlEncodedContent(fields));
    }
    private static async Task SignInAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync($"/__test/sign-in?subject=niamh&roles={StaffRoles.HeadOfficeUser}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private sealed record ReferenceChoice(Guid Id, string Name, bool IsArchived);
    private sealed class ExtraUsageSource(Guid brand, bool fail) : IReferenceUsageSource
    {
        public string SourceKey => "specialist-assignments";
        public bool Supports(string listKey) => listKey == "brands";
        public Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken) => fail
            ? throw new InvalidOperationException("Test unavailable source")
            : Task.FromResult(new ReferenceCount(SourceKey, "specialist assignment", "specialist assignments", item.ItemId == brand ? 1 : 0));
    }
    private sealed class SignallingProductSource(ProductBrandUsageSource inner, TaskCompletionSource reached) : IReferenceUsageSource
    {
        public string SourceKey => inner.SourceKey;
        public bool Supports(string listKey) => inner.Supports(listKey);
        public Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken)
        {
            reached.TrySetResult();
            return inner.CountAsync(item, cancellationToken);
        }
    }
}
