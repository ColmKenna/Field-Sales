extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Category = CatalogueApi::FieldSales.Api.Catalogue.Category;
using CategoryTree = CatalogueApi::FieldSales.Api.Catalogue.CategoryTree;
using PriceTier = CatalogueApi::FieldSales.Api.Pricing.PriceTier;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using TierTerm = CatalogueApi::FieldSales.Api.Pricing.TierTerm;

namespace FieldSales.Web.Tests;

/// <summary>WI-063 agreed scenario 11: through the website, the real API and SQL Server, head office ticks Tier B and "Suncare Deal
/// 2027" on Hickey's Pharmacies, updates the sample, saves, then unticks the deal and saves again.</summary>
[Collection(DirectorySqlServerCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Requires", "SqlServer")]
[Trait("Area", "Pricing")]
public sealed class CustomerTierIntegrationTests(CustomerApplication app) : IClassFixture<CustomerApplication>
{
    [Fact]
    public async Task Should_TickSampleAndSaveTiers_When_HeadOfficeUsesTheCustomerRecord()
    {
        await app.ResetAsync();
        Guid customer = await HickeysAsync();
        (Guid tierB, Guid deal) = await TiersAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = $"/HeadOffice/Customers/Detail/{customer}";

        string empty = await PageAsync(browser, path);
        Assert.Contains("Tiers: none — Base Price plus any break", empty);

        using HttpResponseMessage sampled = await PostAsync(browser, path + "?handler=Sample", empty,
            [("TierIds", tierB.ToString()), ("TierIds", deal.ToString()), ("Find", "SPF30")]);
        Assert.Equal(HttpStatusCode.OK, sampled.StatusCode);
        string sample = WebUtility.HtmlDecode(await sampled.Content.ReadAsStringAsync());
        Assert.Contains("The sample shows the ticked tiers. They aren't saved yet.", sample);
        Assert.Matches(@"SPF30 Sun Lotion 200ml[\s\S]*?<td>€12\.50</td>\s*<td>€10\.00</td>\s*<td>Suncare Deal 2027</td>", sample);
        Assert.Contains("Tiers: none — Base Price plus any break", sample); // Nothing saved yet.

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(browser, path + "?handler=Tiers", sample,
            [("TierIds", tierB.ToString()), ("TierIds", deal.ToString())])).StatusCode);
        string both = await PageAsync(browser, path);
        Assert.Contains("Tiers: Suncare Deal 2027, Tier B", both);

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(browser, path + "?handler=Tiers", both,
            [("TierIds", tierB.ToString())])).StatusCode);
        Assert.Contains("Tiers: Tier B", await PageAsync(browser, path));
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        Assert.Equal([tierB], await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().CustomerPriceTiers
            .Where(link => link.CustomerId == customer).Select(link => link.PriceTierId).ToListAsync());
    }

    private async Task<Guid> HickeysAsync()
    {
        using HttpClient api = app.CreateApiClient();
        Guid region = await PlaceAsync(api, "regions", "Leinster", null);
        Guid county = await PlaceAsync(api, "counties", "Wicklow", region);
        Guid town = await PlaceAsync(api, "towns", "Arklow", county);
        using HttpResponseMessage created = await api.PostAsJsonAsync("/directory/customers",
            new CreateCustomerRequest("Hickey's Pharmacies", new("Hickey's Arklow", town, null)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<CustomerDetails>())!.Id;
    }

    private static async Task<Guid> PlaceAsync(HttpClient api, string level, string name, Guid? parent)
    {
        using HttpResponseMessage response = await api.PostAsJsonAsync($"/directory/geography/{level}", new CreateGeographyRequest(name, parent));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GeographyItem>())!.Id;
    }

    private async Task<(Guid TierB, Guid Deal)> TiersAsync()
    {
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.CustomerPriceTiers.ExecuteDeleteAsync();
        await db.PriceTierOverrides.ExecuteDeleteAsync();
        await db.PriceTiers.ExecuteDeleteAsync();
        await db.ProductQuantityBreaks.ExecuteDeleteAsync();
        await db.ProductBasePrices.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.Categories.ExecuteDeleteAsync();
        Category suncare = new CategoryTree([]).Add("Suncare");
        db.Categories.Add(suncare);
        Product spf30 = Product.Create("SUN-030", "SPF30 Sun Lotion 200ml", suncare.Id, 12.50m, new DateOnly(2026, 8, 1));
        db.Products.Add(spf30);
        PriceTier tierB = PriceTier.CreatePercentage("Tier B", 8m);
        PriceTier deal = PriceTier.CreatePriceList("Suncare Deal 2027");
        deal.SetOverride(spf30.Id, TierTerm.Fixed(10.00m), new DateOnly(2026, 8, 1));
        db.PriceTiers.AddRange(tierB, deal);
        await db.SaveChangesAsync();
        return (tierB.Id, deal.Id);
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

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, string html, List<(string Name, string Value)> form)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        form.Add(("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)));
        return browser.PostAsync(path, new FormUrlEncodedContent(form.Select(field => KeyValuePair.Create(field.Name, field.Value))));
    }
}
