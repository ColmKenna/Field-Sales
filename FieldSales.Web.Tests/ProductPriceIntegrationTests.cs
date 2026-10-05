extern alias CatalogueApi;

using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;

namespace FieldSales.Web.Tests;

public sealed class ProductPriceIntegrationTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    [Fact]
    public async Task Should_PreserveHistory_When_PriceFormsAreSavedAndApplicationRestarts()
    {
        Guid category = await app.ResetAsync();
        Guid productId;
        // An existing product from August makes the source's September–October interval explicit.
        await using (AsyncServiceScope scope = app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            Product product = Product.Create("SUN-0342", "SPF30 Sun Lotion v2 200ml", category,
                12.50m, new DateOnly(2026, 8, 1));
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }
        string path = $"/HeadOffice/Products/{productId}";
        await using (StaffWebsiteFactory website = app.CreateWebsite())
        {
            using HttpClient browser = website.CreateBrowser();
            await SignInAsync(browser);
            string form = await PageAsync(browser, path);
            using HttpResponseMessage future = await PostPriceAsync(browser, path, form, "13.20", "2026-11-01");
            Assert.Equal(HttpStatusCode.Redirect, future.StatusCode);
            Assert.Equal(path, future.Headers.Location!.OriginalString);
            string changed = await PageAsync(browser, path);
            Assert.Contains("€12.50 · rising to €13.20 on 1 Nov", changed);
            using HttpResponseMessage duplicate = await PostPriceAsync(browser, path, changed, "14.00", "2026-11-01");
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            Assert.Contains("A price already starts on 1 Nov 2026 — edit it instead",
                WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync()));
            using HttpResponseMessage past = await PostPriceAsync(browser, path, changed, "12.00", "2026-09-01");
            Assert.Equal(HttpStatusCode.Redirect, past.StatusCode);
            string record = await PageAsync(browser, path);
            Assert.Contains("€12.00 · rising to €13.20 on 1 Nov", record);
            Assert.Contains("Existing orders keep the price captured", record);
        }

        await using (AsyncServiceScope persistedScope = app.Api.Services.CreateAsyncScope())
        {
            Product persisted = await persistedScope.ServiceProvider.GetRequiredService<CatalogueDbContext>()
                .Products.Include(product => product.BasePrices).SingleAsync(product => product.Id == productId);
            Assert.Equal(3, persisted.BasePrices.Count);
            Assert.Equal(category, persisted.CategoryId);
            Assert.Equal(12.50m, persisted.BasePriceOn(new DateOnly(2026, 8, 31))!.Amount);
            Assert.Equal(12m, persisted.BasePriceOn(new DateOnly(2026, 10, 31))!.Amount);
            Assert.Equal(13.20m, persisted.BasePriceOn(new DateOnly(2026, 11, 1))!.Amount);
            Assert.Equal(new DateOnly(2026, 10, 31), persisted.BasePricePeriods()[1].EffectiveThrough);
        }

        // Dispose the original API host; reopen both hosts against its existing SQL database.
        await app.Api.DisposeAsync();
        await using var restartedApi = app.NewApi();
        await using StaffWebsiteFactory restartedWebsite = app.CreateWebsite(restartedApi);
        using HttpClient reopened = restartedWebsite.CreateBrowser();
        await SignInAsync(reopened);
        string reopenedRecord = await PageAsync(reopened, path);
        Assert.Contains("€12.00 · rising to €13.20 on 1 Nov", reopenedRecord);
        Assert.Contains("Existing orders keep the price captured", reopenedRecord);
        Assert.True(reopenedRecord.IndexOf("datetime=\"2026-11-01\"", StringComparison.Ordinal)
            < reopenedRecord.IndexOf("datetime=\"2026-09-01\"", StringComparison.Ordinal));
        Assert.True(reopenedRecord.IndexOf("datetime=\"2026-09-01\"", StringComparison.Ordinal)
            < reopenedRecord.IndexOf("datetime=\"2026-08-01\"", StringComparison.Ordinal));
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

    private static Task<HttpResponseMessage> PostPriceAsync(HttpClient browser, string path, string html,
        string amount, string date)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return browser.PostAsync(path + "?handler=Price", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BasePrice"] = amount, ["EffectiveFrom"] = date,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));
    }
}
