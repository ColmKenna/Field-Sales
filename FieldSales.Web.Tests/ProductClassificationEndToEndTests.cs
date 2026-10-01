extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.ReferenceData;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using Brand = CatalogueApi::FieldSales.Api.Catalogue.Brand;
using ProductProfile = CatalogueApi::FieldSales.Api.Catalogue.ProductProfile;
using Supplier = CatalogueApi::FieldSales.Api.Catalogue.Supplier;
using RestrictionGroup = CatalogueApi::FieldSales.Api.Catalogue.RestrictionGroup;
using NamedReferenceItem = CatalogueApi::FieldSales.Api.Catalogue.NamedReferenceItem;

namespace FieldSales.Web.Tests;

public sealed class ProductClassificationEndToEndTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    [Fact]
    public async Task Should_SaveAndShowClassification_When_SourceExampleIsSubmitted()
    {
        Seed seed = await SeedAsync();
        await using (StaffWebsiteFactory website = app.CreateWebsite())
        {
            using HttpClient browser = website.CreateBrowser();
            await SignInAsync(browser);
            string form = await PageAsync(browser, seed.Path);
            using var saved = await PostAsync(browser, seed, form, Fields(seed, group: false));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.Equal(seed.Path, saved.Headers.Location!.OriginalString);
            string record = await PageAsync(browser, seed.Path);
            Assert.Contains("Chilled", record);
            Assert.Contains("SunCo · Primary", record);
            Assert.Contains("GlowCo · Alternative", record);
            Assert.Contains("Irish Health Supplies", record);
            Assert.Contains("No restriction group yet.", record);
            AssertSelected(record, "ProfileId", seed.Profile.Id);
            AssertSelected(record, "PrimaryBrandId", seed.Primary.Id);
            Assert.Contains($"value=\"{seed.Alternative.Id}\" checked=\"checked\"", record);
            Assert.True(record.IndexOf("id=\"price-heading\"", StringComparison.Ordinal) < record.IndexOf("id=\"classification-heading\"", StringComparison.Ordinal));
            Assert.True(record.IndexOf("id=\"classification-heading\"", StringComparison.Ordinal) < record.IndexOf("id=\"attributes-heading\"", StringComparison.Ordinal));
            using var grouped = await PostAsync(browser, seed, record, Fields(seed));
            Assert.Equal(HttpStatusCode.Redirect, grouped.StatusCode);
            string checkpoint = await PageAsync(browser, seed.Path, decode: false);
            string? output = Environment.GetEnvironmentVariable("WI012_LAYOUT_DIR");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                string preview = Regex.Replace(checkpoint, "href=\"/css/site.css[^\"]*\"", "href=\"site.css\"");
                preview = Regex.Replace(preview, "src=\"/js/product-unit-form.js[^\"]*\"", "src=\"product-unit-form.js\"");
                await File.WriteAllTextAsync(System.IO.Path.Combine(output, "index.html"), preview);
            }
        }
        // Reopen both hosts against the same SQL database, with a fresh staff cookie.
        await app.RestartAsync();
        await using StaffWebsiteFactory restarted = app.CreateWebsite();
        using HttpClient reopened = restarted.CreateBrowser();
        await SignInAsync(reopened);
        string afterRestart = await PageAsync(reopened, seed.Path);
        Assert.Contains("SunCo · Primary", afterRestart);
        Assert.Contains("GlowCo · Alternative", afterRestart);
        Assert.Contains("Pharmacy-only medicines", afterRestart);
        using HttpClient api = app.CreateApiClient();
        foreach (var item in new[] { ("profiles", seed.Profile.Id), ("brands", seed.Primary.Id), ("brands", seed.Alternative.Id), ("suppliers", seed.Supplier.Id), ("restriction-groups", seed.Group.Id) })
        {
            var list = (await api.GetFromJsonAsync<ReferenceListViewModel>($"/catalogue/reference-data/{item.Item1}"))!;
            Assert.Equal("Used by 1 product", list.Items.Single(entry => entry.Id == item.Item2).Usage.Description);
        }
    }

    [Fact]
    public async Task Should_KeepEnteredValuesAndSavedRecord_When_PrimaryBrandIsMissing()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, seed.Path);
        var fields = Fields(seed).Where(field => field.Key != "Classification.PrimaryBrandId").ToList();
        using var rejected = await PostAsync(browser, seed, form, fields);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains("Choose a primary brand first", html);
        AssertSelected(html, "ProfileId", seed.Profile.Id);
        AssertSelected(html, "SupplierId", seed.Supplier.Id);
        Assert.Contains($"value=\"{seed.Alternative.Id}\" checked=\"checked\"", html);
        using HttpClient api = app.CreateApiClient();
        var saved = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!;
        Assert.Null(saved.Profile);
        Assert.Null(saved.Supplier);
        Assert.Null(saved.RestrictionGroup);
        Assert.Empty(saved.Brands!);
        Assert.Equal(12.50m, saved.CurrentPrice!.Amount);
    }

    [Fact]
    public async Task Should_ShowBothRoles_When_PrimaryBrandIsAlsoAnAlternative()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        var fields = Fields(seed).Where(field => field.Key != "Classification.AlternativeBrandIds").ToList();
        fields.Add(new("Classification.AlternativeBrandIds", seed.Primary.Id.ToString()));
        using var saved = await PostAsync(browser, seed, await PageAsync(browser, seed.Path), fields);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string record = await PageAsync(browser, seed.Path);
        Assert.Contains("SunCo · Primary · Alternative", record);
        Assert.Contains($"value=\"{seed.Primary.Id}\" checked=\"checked\"", record);
    }

    [Fact]
    public async Task Should_PreserveRecordAndLabels_When_ArchivedLinksAreRetainedThenReplaced()
    {
        Seed seed = await SeedAsync();
        using HttpClient api = app.CreateApiClient();
        using var initial = await api.PutAsJsonAsync($"/catalogue/products/{seed.Id}/classification",
            new SetProductClassificationRequest(seed.Profile.Id, seed.Primary.Id, [seed.Alternative.Id], seed.Supplier.Id, seed.Group.Id));
        Assert.Equal(HttpStatusCode.NoContent, initial.StatusCode);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            foreach (NamedReferenceItem item in new NamedReferenceItem[] { seed.Profile, seed.Primary, seed.Alternative, seed.Supplier, seed.Group })
            {
                db.Attach(item); item.Archive();
            }
            var unusedArchived = ProductProfile.Create("Frozen");
            unusedArchived.Archive();
            db.ProductProfiles.Add(unusedArchived);
            await db.SaveChangesAsync();
        }
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, seed.Path);
        Assert.Contains("Chilled (archived)", form);
        Assert.Contains("SunCo (archived) · Primary", form);
        Assert.Contains("GlowCo (archived) · Alternative", form);
        Assert.Contains("Irish Health Supplies (archived)", form);
        Assert.Contains("Pharmacy-only medicines (archived)", form);
        Assert.DoesNotContain("Frozen", form);
        using var retained = await PostAsync(browser, seed, form, Fields(seed));
        Assert.Equal(HttpStatusCode.Redirect, retained.StatusCode);
        ProductProfile active = ProductProfile.Create("Ambient");
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            db.ProductProfiles.Add(active);
            await db.SaveChangesAsync();
        }
        var replacements = new List<KeyValuePair<string, string>> { new("Classification.ProfileId", active.Id.ToString()) };
        using var cleared = await PostAsync(browser, seed, await PageAsync(browser, seed.Path), replacements);
        Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        string record = await PageAsync(browser, seed.Path);
        Assert.Contains("Ambient", record);
        Assert.DoesNotContain("Chilled (archived)", record);
        Assert.DoesNotContain("SunCo (archived)", record);
        Assert.Contains("No brands yet.", record);
        Assert.Contains("No supplier yet.", record);
        Assert.Contains("No restriction group yet.", record);
    }

    [Fact]
    public async Task Should_RejectWithoutPartialSave_When_ChoiceIsDeletedAfterPageOpens()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, seed.Path);
        await using (var scope = app.Api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Suppliers.ExecuteDeleteAsync();
        using var rejected = await PostAsync(browser, seed, form, Fields(seed));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains("The selected supplier no longer exists. Choose another.", html);
        Assert.Contains("Selection unavailable", html);
        AssertSelected(html, "SupplierId", seed.Supplier.Id);
        using HttpClient api = app.CreateApiClient();
        var details = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!;
        Assert.Null(details.Profile);
        Assert.Empty(details.Brands!);
        Assert.Null(details.RestrictionGroup);
    }

    [Fact]
    public async Task Should_PreserveIndependentForms_When_PriceUnitAndClassificationAreSaved()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var classified = await PostAsync(browser, seed, await PageAsync(browser, seed.Path), Fields(seed));
        Assert.Equal(HttpStatusCode.Redirect, classified.StatusCode);
        using var price = await PostAsync(browser, seed, await PageAsync(browser, seed.Path),
            [new("BasePrice", "13.20"), new("EffectiveFrom", "2026-11-01")], "Price");
        Assert.Equal(HttpStatusCode.Redirect, price.StatusCode);
        using var unit = await PostAsync(browser, seed, await PageAsync(browser, seed.Path),
            [new("Unit", "kg"), new("QuantityStep", "0.5"), new("MinimumQuantity", "1")], "Unit");
        Assert.Equal(HttpStatusCode.Redirect, unit.StatusCode);
        using HttpClient api = app.CreateApiClient();
        var record = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!;
        Assert.Equal("kg", record.Product.Unit);
        Assert.Equal(0.5m, record.Product.QuantityStep);
        Assert.Equal(2, record.PriceHistory.Count);
        Assert.Equal(seed.Profile.Id, record.Profile!.Id);
        Assert.Equal(seed.Primary.Id, record.Brands!.Single(brand => brand.IsPrimary).Id);
        Assert.Equal(seed.Group.Id, record.RestrictionGroup!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_DenyClassificationWithoutReplay_When_RoleIsRemovedOrSessionExpires(bool expired)
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, seed.Path);
        if (expired)
        {
            await using var scope = website.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            StoredTicket ticket = await db.Tickets.SingleAsync();
            ticket.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        else website.Roles.SetRoles("niamh", StaffRoles.SalesManager);
        using var rejected = await PostAsync(browser, seed, form, Fields(seed));
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.DoesNotContain(seed.Path, rejected.Headers.Location!.OriginalString);
        await SignInAsync(browser);
        Assert.Contains("No profile yet.", await PageAsync(browser, seed.Path));
        using HttpClient api = app.CreateApiClient();
        var details = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!;
        Assert.Null(details.Profile);
        Assert.Empty(details.Brands!);
    }

    [Fact]
    public async Task Should_RetainMalformedSelectionWithoutSaving_When_IdCannotBeBound()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        var fields = Fields(seed).Where(field => field.Key != "Classification.ProfileId").ToList();
        fields.Add(new("Classification.ProfileId", "invalid-id"));
        using var rejected = await PostAsync(browser, seed, await PageAsync(browser, seed.Path), fields);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("invalid-id", WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync()));
        using HttpClient api = app.CreateApiClient();
        var details = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!;
        Assert.Null(details.Profile);
        Assert.Empty(details.Brands!);
    }

    private async Task<Seed> SeedAsync()
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductAlternativeBrands.ExecuteDeleteAsync();
        await db.ProductAttributeValues.ExecuteDeleteAsync();
        Guid category = await app.ResetAsync();
        await db.ProductProfiles.ExecuteDeleteAsync(); await db.Suppliers.ExecuteDeleteAsync();
        await db.RestrictionGroups.ExecuteDeleteAsync(); await db.Brands.ExecuteDeleteAsync();
        Product product = Product.Create("SUN-0342", "SPF30 Sun Lotion v2 200ml", category, 12.50m, new DateOnly(2026, 8, 1));
        Seed seed = new(product.Id, ProductProfile.Create("Chilled"), Brand.Create("SunCo"), Brand.Create("GlowCo"),
            Supplier.Create("Irish Health Supplies"), RestrictionGroup.Create("Pharmacy-only medicines"));
        db.AddRange(product, seed.Profile, seed.Primary, seed.Alternative, seed.Supplier, seed.Group);
        await db.SaveChangesAsync();
        return seed;
    }

    private static List<KeyValuePair<string, string>> Fields(Seed seed, bool group = true) =>
    [new("Classification.ProfileId", seed.Profile.Id.ToString()), new("Classification.PrimaryBrandId", seed.Primary.Id.ToString()),
     new("Classification.AlternativeBrandIds", seed.Alternative.Id.ToString()), new("Classification.SupplierId", seed.Supplier.Id.ToString()),
     new("Classification.RestrictionGroupId", group ? seed.Group.Id.ToString() : "")];
    private static async Task SignInAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync($"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(StaffRoles.HeadOfficeUser)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private static async Task<string> PageAsync(HttpClient browser, string path, bool decode = true)
    {
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync();
        return decode ? WebUtility.HtmlDecode(html) : html;
    }
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, Seed seed, string html,
        IEnumerable<KeyValuePair<string, string>> fields, string handler = "Classification")
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return browser.PostAsync(seed.Path + "?handler=" + handler,
            new FormUrlEncodedContent(fields.Append(new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)))));
    }
    private static void AssertSelected(string html, string field, Guid id)
    {
        var select = Regex.Match(html, $"<select[^>]*id=\"Classification_{field}\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(select.Success);
        Assert.Matches($"<option(?=[^>]*value=\"{id}\")(?=[^>]*selected=\"selected\")[^>]*>", select.Value);
    }
    private sealed record Seed(Guid Id, ProductProfile Profile, Brand Primary, Brand Alternative, Supplier Supplier, RestrictionGroup Group)
    {
        public string Path => $"/HeadOffice/Products/{Id}";
    }
}
