extern alias CatalogueApi;

using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;

namespace FieldSales.Web.Tests;

/// <summary>WI-062 agreed scenario 13: through the website, the real API and SQL Server, head office gives Throat Lozenges breaks
/// from 10 at €2.00 and from 30 at €2.30, sees the 30 break flagged, and corrects it to €1.75.</summary>
[Collection(CatalogueSqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class QuantityBreakIntegrationTests(ProductApplication app) : IClassFixture<ProductApplication>, IAsyncLifetime
{
    private const string HigherThanBelow = "This break is higher than the one below it and will never apply.";

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await app.ResetAsync();

    [Fact]
    public async Task Should_SaveFlagAndCorrectBreaks_When_HeadOfficeUsesTheProductRecord()
    {
        Guid category = await app.ResetAsync();
        Guid lozenges;
        await using (AsyncServiceScope scope = app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            Product product = Product.Create("LOZ-36", "Throat Lozenges 36s", category, 2.20m, new DateOnly(2026, 8, 1));
            db.Products.Add(product);
            await db.SaveChangesAsync();
            lozenges = product.Id;
        }
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = $"/HeadOffice/Products/{lozenges}";

        string empty = await PageAsync(browser, path);
        Assert.Contains("No quantity breaks yet. Every quantity is at the base price.", empty);
        using HttpResponseMessage saved = await PostAsync(browser, path + "?handler=Breaks", empty, new()
        {
            ["Breaks[0].FromQuantity"] = "30", ["Breaks[0].Price"] = "2.30"
        });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string one = await PageAsync(browser, path);
        using HttpResponseMessage added = await PostAsync(browser, path + "?handler=Breaks", one, new()
        {
            ["Breaks[0].FromQuantity"] = "30", ["Breaks[0].Price"] = "2.30", ["Breaks[1].FromQuantity"] = "10", ["Breaks[1].Price"] = "2.00"
        });
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);

        string flagged = await PageAsync(browser, path);
        Assert.True(flagged.IndexOf("value=\"10\"", StringComparison.Ordinal) < flagged.IndexOf("value=\"30\"", StringComparison.Ordinal));
        Assert.Contains(HigherThanBelow, flagged);

        using HttpResponseMessage corrected = await PostAsync(browser, path + "?handler=Breaks", flagged, new()
        {
            ["Breaks[0].FromQuantity"] = "10", ["Breaks[0].Price"] = "2.00", ["Breaks[1].FromQuantity"] = "30", ["Breaks[1].Price"] = "1.75"
        });
        Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);
        Assert.DoesNotContain("will never apply", await PageAsync(browser, path));

        await using AsyncServiceScope stored = app.Api.Services.CreateAsyncScope();
        CatalogueDbContext saved2 = stored.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Assert.Equal([(10m, 2.00m), (30m, 1.75m)], (await saved2.ProductQuantityBreaks.AsNoTracking()
            .Where(entry => entry.ProductId == lozenges).OrderBy(entry => entry.FromQuantity).ToListAsync())
            .Select(entry => (entry.FromQuantity, entry.Price)));
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
