extern alias CatalogueApi;

using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;

namespace FieldSales.Web.Tests;

/// <summary>WI-060 agreed scenario 11: head office creates Tier B and gives SPF30 a fixed override through the website, the real
/// API and SQL Server; refused values come back inline.</summary>
[Collection(CatalogueSqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class PriceTierIntegrationTests(ProductApplication app) : IClassFixture<ProductApplication>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => app.ClearTiersAsync();

    [Fact]
    public async Task Should_CreateTierBAndAddTheSpf30Override_When_HeadOfficeUsesTheForms()
    {
        Guid category = await app.ResetAsync();
        Guid spf30;
        await using (AsyncServiceScope scope = app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            Product product = Product.Create("SUN-030", "SPF30 Sun Lotion 200ml", category, 12.50m, new DateOnly(2026, 8, 1));
            db.Products.Add(product);
            await db.SaveChangesAsync();
            spf30 = product.Id;
        }
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        string list = await PageAsync(browser, "/HeadOffice/Tiers");
        Assert.Contains("No price tiers yet.", list);
        using HttpResponseMessage refusedTier = await PostAsync(browser, "/HeadOffice/Tiers", list,
            new() { ["Name"] = "Tier B", ["PercentOff"] = "100" });
        Assert.Equal(HttpStatusCode.OK, refusedTier.StatusCode);
        string refusedList = WebUtility.HtmlDecode(await refusedTier.Content.ReadAsStringAsync());
        Assert.Contains("Enter a percentage more than 0 and less than 100, with up to two decimal places.", refusedList);
        Assert.Contains("value=\"Tier B\"", refusedList);

        using HttpResponseMessage created = await PostAsync(browser, "/HeadOffice/Tiers", list,
            new() { ["Name"] = "Tier B", ["PercentOff"] = "8" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        string detailPath = created.Headers.Location!.OriginalString;
        Assert.Matches("^/HeadOffice/Tiers/[0-9a-f-]{36}$", detailPath);

        string found = await PageAsync(browser, detailPath + "?Find=SPF30");
        Assert.Contains("No overrides yet.", found);
        Assert.Contains("SUN-030 · SPF30 Sun Lotion 200ml</strong> · list €12.50", found);
        Dictionary<string, string> spf30Override(string value) => new()
        {
            ["Override.ProductId"] = spf30.ToString(), ["Override.Kind"] = "fixed", ["Override.Value"] = value, ["Find"] = "SPF30"
        };
        using HttpResponseMessage refusedOverride = await PostAsync(browser, detailPath + "?handler=Override", found, spf30Override("10.505"));
        Assert.Equal(HttpStatusCode.OK, refusedOverride.StatusCode);
        string refusedDetail = WebUtility.HtmlDecode(await refusedOverride.Content.ReadAsStringAsync());
        Assert.Contains("Enter a price more than €0.00, to the cent.", refusedDetail);
        Assert.Contains("value=\"10.505\"", refusedDetail);

        using HttpResponseMessage added = await PostAsync(browser, detailPath + "?handler=Override", found, spf30Override("10.50"));
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        string detail = await PageAsync(browser, detailPath);
        Assert.Contains("<strong>SPF30 Sun Lotion 200ml</strong> · fixed €10.50 · list €12.50", detail);
        Assert.Contains("Tier B</a> · 8% off base · 1 override", await PageAsync(browser, "/HeadOffice/Tiers"));

        await using AsyncServiceScope stored = app.Api.Services.CreateAsyncScope();
        CatalogueDbContext saved = stored.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var tier = await saved.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync();
        Assert.Equal(("Tier B", 8m), (tier.Name, tier.PercentOff));
        var entry = Assert.Single(tier.Overrides);
        Assert.Equal((spf30, (decimal?)null, (decimal?)10.50m), (entry.ProductId, entry.PercentOff, entry.FixedPrice));
    }

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

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, string html, Dictionary<string, string> form)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        form["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return browser.PostAsync(path, new FormUrlEncodedContent(form));
    }
}
