extern alias CatalogueApi;

using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using PriceTierShape = CatalogueApi::FieldSales.Api.Pricing.PriceTierShape;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;

namespace FieldSales.Web.Tests;

/// <summary>WI-061 agreed scenario 12: through the website, the real API and SQL Server, head office creates "Suncare Deal 2027"
/// as a price list, saves SPF30 at €13.00 and sees it flagged, corrects it, then changes Tier B to a price list through the confirm
/// step.</summary>
[Collection(CatalogueSqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class PriceListTierIntegrationTests(ProductApplication app) : IClassFixture<ProductApplication>, IAsyncLifetime
{
    private const string AboveList = "This is above list price and will never apply.";

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => app.ClearTiersAsync();

    [Fact]
    public async Task Should_CreateAPriceListFlagAndCorrectAPriceAndChangeTierBsShape_When_HeadOfficeUsesTheForms()
    {
        Guid category = await app.ResetAsync();
        Guid spf30, handCream;
        await using (AsyncServiceScope scope = app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            Product sun = Product.Create("SUN-030", "SPF30 Sun Lotion 200ml", category, 12.50m, new DateOnly(2026, 8, 1));
            Product hand = Product.Create("HAND-75", "Hand Cream 75ml", category, 4.00m, new DateOnly(2026, 8, 1));
            db.Products.AddRange(sun, hand);
            await db.SaveChangesAsync();
            (spf30, handCream) = (sun.Id, hand.Id);
        }
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        // The price list, with SPF30 above its list price, then corrected.
        string list = await PageAsync(browser, "/HeadOffice/Tiers");
        string deal = await CreatedAsync(browser, list, new() { ["Name"] = "Suncare Deal 2027", ["Shape"] = "PriceList" });
        string found = await PageAsync(browser, deal + "?Find=SPF30");
        Assert.Contains("No listed prices yet. Every product is at base price.", found);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(browser, deal + "?handler=Override", found, Fixed(spf30, "13.00"))).StatusCode);
        string flagged = await PageAsync(browser, deal);
        Assert.Contains("<strong>SPF30 Sun Lotion 200ml</strong> · fixed €13.00 · list €12.50", flagged);
        Assert.Contains(AboveList, flagged);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(browser, deal + "?handler=Override", flagged, Fixed(spf30, "10.00"))).StatusCode);
        string corrected = await PageAsync(browser, deal);
        Assert.Contains("<strong>SPF30 Sun Lotion 200ml</strong> · fixed €10.00 · list €12.50", corrected);
        Assert.DoesNotContain(AboveList, corrected);

        // Tier B, with a fixed and a percentage override, changed to a price list.
        string tierB = await CreatedAsync(browser, list, new() { ["Name"] = "Tier B", ["Shape"] = "Percentage", ["PercentOff"] = "8" });
        string tierBPage = await PageAsync(browser, tierB);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(browser, tierB + "?handler=Override", tierBPage, Fixed(spf30, "10.50"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(browser, tierB + "?handler=Override", tierBPage, new()
        {
            ["Override.ProductId"] = handCream.ToString(), ["Override.Kind"] = "percent", ["Override.Value"] = "15"
        })).StatusCode);
        string confirm = await PageAsync(browser, tierB + "/Shape");
        Assert.Contains("Tier B's 8% off base will no longer apply. Products not listed will revert to base price.", confirm);
        Assert.Contains("This percentage override will be removed: Hand Cream 75ml.", confirm);
        using HttpResponseMessage changed = await PostAsync(browser, tierB + "/Shape", confirm, []);
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Equal(tierB, changed.Headers.Location!.OriginalString);
        string tiers = await PageAsync(browser, "/HeadOffice/Tiers");
        Assert.Contains("Suncare Deal 2027</a> · price list · 1 product", tiers);
        Assert.Contains("Tier B</a> · price list · 1 product", tiers);

        await using AsyncServiceScope stored = app.Api.Services.CreateAsyncScope();
        CatalogueDbContext saved = stored.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var tier = await saved.PriceTiers.AsNoTracking().Include(row => row.Overrides).SingleAsync(row => row.Name == "Tier B");
        Assert.Equal((PriceTierShape.PriceList, (decimal?)null), (tier.Shape, tier.PercentOff));
        var kept = Assert.Single(tier.Overrides);
        Assert.Equal((spf30, (decimal?)null, (decimal?)10.50m), (kept.ProductId, kept.PercentOff, kept.FixedPrice));
    }

    private static Dictionary<string, string> Fixed(Guid product, string price) => new()
    {
        ["Override.ProductId"] = product.ToString(), ["Override.Kind"] = "fixed", ["Override.Value"] = price
    };

    private static async Task<string> CreatedAsync(HttpClient browser, string list, Dictionary<string, string> form)
    {
        using HttpResponseMessage created = await PostAsync(browser, "/HeadOffice/Tiers", list, form);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        return created.Headers.Location!.OriginalString;
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
