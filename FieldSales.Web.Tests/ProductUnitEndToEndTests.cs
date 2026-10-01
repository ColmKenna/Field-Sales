extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Quantities;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;

namespace FieldSales.Web.Tests;

public sealed class ProductUnitEndToEndTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    private const string CreateUrl = "/HeadOffice/Products/Create";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreationEach_IgnoresStaleNonnumericMeasuredFields(bool legacy)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        var fields = Fields(category, "Each");
        fields["QuantityStep"] = "not a number";
        fields["MinimumQuantity"] = "not a number";
        if (legacy) fields.Remove("Unit");
        using HttpResponseMessage saved = await PostAsync(browser, CreateUrl, await PageAsync(browser, CreateUrl), fields);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products.SingleAsync();
        Assert.Equal("Each", product.Unit);
        Assert.Null(product.QuantityStep);
        Assert.Null(product.MinimumQuantity);
    }

    [Fact]
    public async Task Should_KeepEnteredPriceExactAndRejectSave_When_PreviewHasExcessPricePrecision()
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        var fields = Fields(category, "kg");
        fields["BasePrice"] = "4.805";
        string form = await PageAsync(browser, CreateUrl);
        using HttpResponseMessage preview = await PostAsync(browser, CreateUrl + "?handler=Preview", form, fields);
        string html = WebUtility.HtmlDecode(await preview.Content.ReadAsStringAsync());
        Assert.Contains("Resulting price: €4.805 per kg", html);
        using HttpResponseMessage rejected = await PostAsync(browser, CreateUrl, html, fields);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Enter a non-negative base price with up to two decimal places", await rejected.Content.ReadAsStringAsync());
        Assert.Equal((0, 0), await app.CountsAsync());
    }

    [Theory]
    [InlineData("kg")]
    [InlineData("litre")]
    [InlineData("metre")]
    public async Task Should_SaveMeasuredProductWithPricePerUnit_When_ValidRulesAreEntered(string unit)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CreateUrl);
        Assert.Contains("hidden", Tag(form, "fieldset", "data-measure-fields"));
        Assert.Contains("disabled", Tag(form, "fieldset", "data-measure-fields"));
        var fields = Fields(category, unit);
        using HttpResponseMessage preview = await PostAsync(browser, CreateUrl + "?handler=Preview", form, fields);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        string reviewed = WebUtility.HtmlDecode(await preview.Content.ReadAsStringAsync());
        Assert.Contains($"Base price (€ per {unit})", reviewed);
        Assert.Contains($"Resulting price: €4.80 per {unit}", reviewed);
        Assert.DoesNotContain("hidden", Tag(reviewed, "fieldset", "data-measure-fields"));
        Assert.Equal((0, 0), await app.CountsAsync());
        using HttpResponseMessage saved = await PostAsync(browser, CreateUrl, reviewed, fields);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string record = await PageAsync(browser, saved.Headers.Location!.OriginalString);
        Assert.Contains($"€4.80 per {unit}", record);
        Assert.Contains("name=\"QuantityStep\"", record);
        Assert.Contains("product-unit-form.js", record);
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products
            .Include(product => product.BasePrices).SingleAsync();
        Assert.Equal(unit, product.Unit);
        Assert.Equal(0.5m, product.QuantityStep);
        Assert.Equal(1m, product.MinimumQuantity);
        Assert.Equal(4.80m, Assert.Single(product.BasePrices).Amount);
        foreach (decimal quantity in new[] { 1m, 1.5m, 2m })
            Assert.True(product.GetQuantityRules().ValidateOrder(quantity).IsValid);
        Assert.False(product.GetQuantityRules().ValidateOrder(1.2m).IsValid);
        Assert.False(product.GetQuantityRules().ValidateOrder(0m).IsValid);
        using HttpClient api = app.CreateApiClient();
        CategoryDetails branch = (await api.GetFromJsonAsync<CategoryDetails>($"/catalogue/categories/{category}"))!;
        ProductItem row = Assert.Single(branch.Products);
        Assert.Equal((unit, 0.5m, 1m), (row.Unit, row.QuantityStep, row.MinimumQuantity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_DefaultMinimumToStep_When_MinimumIsBlank(bool editing)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = editing ? await CreateEachAsync(browser, category) : CreateUrl;
        string form = await PageAsync(browser, path);
        var fields = editing ? UnitFields("kg", "0.5", "") : Fields(category, "kg", minimum: "");
        using HttpResponseMessage saved = await PostAsync(browser, editing ? path + "?handler=Unit" : path, form, fields);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products.SingleAsync();
        Assert.Equal(0.5m, product.MinimumQuantity);
        Assert.True(product.GetQuantityRules().ValidateOrder(0.5m).IsValid);
        Assert.False(product.GetQuantityRules().ValidateOrder(0m).IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RejectMinimum_When_NotAMultipleOfStep(bool editing)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = editing ? await CreateEachAsync(browser, category) : CreateUrl;
        string form = await PageAsync(browser, path);
        var fields = editing ? UnitFields("kg", "0.5", "0.7") : Fields(category, "kg", minimum: "0.7");
        using HttpResponseMessage rejected = await PostAsync(browser, editing ? path + "?handler=Unit" : path, form, fields);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains("Minimum must be a multiple of the step", html);
        Assert.Contains("value=\"0.7\"", html);
        Assert.Equal(editing ? (1, 1) : (0, 0), await app.CountsAsync());
        if (editing) Assert.Contains("€4.80 per Each", await PageAsync(browser, path));
    }

    [Fact]
    public async Task Should_PreviewAndFlagPriceBasisWithoutSaving_When_UnitChanges()
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = await CreateEachAsync(browser, category);
        string form = await PageAsync(browser, path);
        using HttpResponseMessage preview = await PostAsync(browser, path + "?handler=PreviewUnit", form, UnitFields("kg", "0.5", "1"));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        string html = WebUtility.HtmlDecode(await preview.Content.ReadAsStringAsync());
        Assert.Contains("Resulting price: €4.80 per kg", html);
        Assert.Contains("Price basis changes from €4.80 per Each to €4.80 per kg. The amount stays the same.", html);
        Assert.DoesNotContain("hidden", Tag(html, "p", "data-price-basis-change"));
        Assert.Contains("€4.80 per Each", await PageAsync(browser, path));
        using HttpResponseMessage saved = await PostAsync(browser, path + "?handler=Unit", html, UnitFields("kg", "0.5", "1"));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string record = await PageAsync(browser, path);
        Assert.Contains("€4.80 per kg", record);
        Assert.Contains("hidden", Tag(record, "p", "data-price-basis-change"));
        // Rule-only changes do not change the price basis.
        using HttpResponseMessage sameUnit = await PostAsync(browser, path + "?handler=PreviewUnit", record, UnitFields("kg", "0.25", "0.5"));
        Assert.Contains("hidden", Tag(await sameUnit.Content.ReadAsStringAsync(), "p", "data-price-basis-change"));
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Assert.Equal(4.80m, (await db.ProductBasePrices.SingleAsync()).Amount);
    }

    [Fact]
    public async Task Should_ClearMeasuredRulesAndHideInputs_When_ProductReturnsToEach()
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = await CreateEachAsync(browser, category);
        using HttpResponseMessage kg = await PostAsync(browser, path + "?handler=Unit", await PageAsync(browser, path), UnitFields("kg", "0.5", "1"));
        Assert.Equal(HttpStatusCode.Redirect, kg.StatusCode);
        using HttpResponseMessage each = await PostAsync(browser, path + "?handler=Unit", await PageAsync(browser, path), UnitFields("Each", "not a number", "not a number"));
        Assert.Equal(HttpStatusCode.Redirect, each.StatusCode);
        string record = await PageAsync(browser, path);
        Assert.Contains("hidden", Tag(record, "fieldset", "data-measure-fields"));
        Assert.Contains("disabled", Tag(record, "fieldset", "data-measure-fields"));
        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products.SingleAsync();
        Assert.Null(product.QuantityStep);
        Assert.Null(product.MinimumQuantity);
        Assert.False(product.GetQuantityRules().ValidateOrder(0m).IsValid);
        Assert.False(product.GetQuantityRules().ValidateOrder(1.5m).IsValid);
        Assert.True(product.GetQuantityRules().ValidateOrder(1m).IsValid);
    }

    [Theory]
    [InlineData(null, "1", "QuantityStep")]
    [InlineData("0", "1", "QuantityStep")]
    [InlineData("-0.5", "1", "QuantityStep")]
    [InlineData("0.0000001", "1", "QuantityStep")]
    [InlineData("0.5", "0", "MinimumQuantity")]
    [InlineData("0.5", "-1", "MinimumQuantity")]
    [InlineData("0.5", "1.0000001", "MinimumQuantity")]
    [InlineData("0.5", "1000000000000", "MinimumQuantity")]
    public async Task Should_PreserveProduct_When_InvalidRuleIsSubmitted(string? step, string minimum, string field)
    {
        Guid category = await app.ResetAsync();
        using HttpClient api = app.CreateApiClient();
        using HttpResponseMessage created = await api.PostAsJsonAsync("/catalogue/products/",
            new { Code = "TEA", Name = "Loose tea", CategoryId = category, Unit = "Each", BasePrice = 4.80m });
        ProductItem product = (await created.Content.ReadFromJsonAsync<ProductItem>())!;
        using HttpResponseMessage rejected = await api.PutAsJsonAsync($"/catalogue/products/{product.Id}/unit",
            new { Unit = "kg", QuantityStep = step is null ? (decimal?)null : decimal.Parse(step, System.Globalization.CultureInfo.InvariantCulture),
                MinimumQuantity = decimal.Parse(minimum, System.Globalization.CultureInfo.InvariantCulture) });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains($"\"{field}\"", await rejected.Content.ReadAsStringAsync());
        ProductDetails after = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{product.Id}"))!;
        Assert.Equal(product, after.Product);
        Assert.Equal(4.80m, Assert.Single(after.PriceHistory).Amount);
    }

    [Fact]
    public async Task Should_PreserveRulesAndPriceHistory_When_BothHostsRestart()
    {
        Guid category = await app.ResetAsync();
        string path;
        await using (StaffWebsiteFactory website = app.CreateWebsite())
        {
            using HttpClient browser = website.CreateBrowser();
            await SignInAsync(browser);
            path = await CreateEachAsync(browser, category);
            using HttpResponseMessage saved = await PostAsync(browser, path + "?handler=Unit", await PageAsync(browser, path), UnitFields("kg", "0.25", "0.5"));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        }
        await using var restartedApi = app.NewApi();
        await using StaffWebsiteFactory reopened = app.CreateWebsite(restartedApi);
        using HttpClient secondBrowser = reopened.CreateBrowser();
        await SignInAsync(secondBrowser);
        Assert.Contains("€4.80 per kg", await PageAsync(secondBrowser, path));
        await using AsyncServiceScope scope = restartedApi.Services.CreateAsyncScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products.Include(product => product.BasePrices).SingleAsync();
        Assert.Equal(0.25m, product.QuantityStep);
        Assert.Equal(0.5m, product.MinimumQuantity);
        Assert.True(product.GetQuantityRules().ValidateOrder(0.75m).IsValid);
        Assert.Equal(4.80m, Assert.Single(product.BasePrices).Amount);
    }

    [Theory]
    [InlineData(StaffRoles.FieldSalesperson)]
    [InlineData(StaffRoles.SalesManager)]
    [InlineData("")]
    public async Task Should_RejectUnitUpdate_When_HeadOfficeRoleIsRemoved(string role)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = await CreateEachAsync(browser, category);
        string form = await PageAsync(browser, path);
        website.Roles.SetRoles("niamh", role.Length == 0 ? [] : [role]);
        app.Roles.SetRoles("niamh", role.Length == 0 ? [] : [role]);
        using HttpResponseMessage denied = await PostAsync(browser, path + "?handler=Unit", form, UnitFields("kg", "0.5", "1"));
        Assert.StartsWith("/AccessChanged?state=", denied.Headers.Location?.OriginalString);
        using HttpResponseMessage preview = await PostAsync(browser, path + "?handler=PreviewUnit", form, UnitFields("kg", "0.5", "1"));
        Assert.StartsWith("/AccessChanged?state=", preview.Headers.Location?.OriginalString);
        using HttpClient api = app.CreateApiClient();
        string apiPath = path.Replace("/HeadOffice/Products/", "/catalogue/products/", StringComparison.Ordinal) + "/unit";
        using HttpResponseMessage direct = await api.PutAsJsonAsync(apiPath, new { Unit = "kg", QuantityStep = 0.5m, MinimumQuantity = 1m });
        Assert.Equal(HttpStatusCode.Forbidden, direct.StatusCode);
        using HttpClient unsigned = app.Api.CreateClient();
        using HttpResponseMessage anonymous = await unsigned.PutAsJsonAsync(apiPath, new { Unit = "kg", QuantityStep = 0.5m, MinimumQuantity = 1m });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await SignInAsync(browser);
        Assert.Contains("€4.80 per Each", await PageAsync(browser, path));
    }

    [Fact]
    public async Task Should_RejectUnitUpdateWithoutReplay_When_SessionExpired()
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string path = await CreateEachAsync(browser, category);
        string form = await PageAsync(browser, path);
        await using (AsyncServiceScope scope = website.Services.CreateAsyncScope())
        {
            StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            (await db.Tickets.SingleAsync()).ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using HttpResponseMessage rejected = await PostAsync(browser, path + "?handler=Unit", form, UnitFields("kg", "0.5", "1"));
        Assert.StartsWith("https://localhost:7201/connect/authorize", rejected.Headers.Location?.OriginalString);
        await SignInAsync(browser);
        Assert.Contains("€4.80 per Each", await PageAsync(browser, path));
    }

    private static Dictionary<string, string> Fields(Guid category, string unit, string minimum = "1") => new()
    {
        ["Code"] = "TEA", ["Name"] = "Loose tea", ["CategoryId"] = category.ToString(),
        ["BasePrice"] = "4.80", ["Unit"] = unit, ["QuantityStep"] = "0.5", ["MinimumQuantity"] = minimum
    };

    private static Dictionary<string, string> UnitFields(string unit, string step, string minimum) => new()
    { ["Unit"] = unit, ["QuantityStep"] = step, ["MinimumQuantity"] = minimum };

    private static async Task<string> CreateEachAsync(HttpClient browser, Guid category)
    {
        using HttpResponseMessage created = await PostAsync(browser, CreateUrl, await PageAsync(browser, CreateUrl), Fields(category, "Each"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        return created.Headers.Location!.OriginalString;
    }

    private static async Task SignInAsync(HttpClient browser)
    {
        using HttpResponseMessage signed = await browser.GetAsync($"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(StaffRoles.HeadOfficeUser)}");
        Assert.Equal(HttpStatusCode.NoContent, signed.StatusCode);
    }

    private static async Task<string> PageAsync(HttpClient browser, string path)
    {
        using HttpResponseMessage page = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        return WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, string html, Dictionary<string, string> fields)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return browser.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static string Tag(string html, string element, string marker)
    {
        Match tag = Regex.Match(html, $"<{element}[^>]*{marker}[^>]*>");
        Assert.True(tag.Success, $"Expected {element} with {marker}.");
        return tag.Value;
    }
}
