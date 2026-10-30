extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.StaffAccess;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using FieldSales.Tablet.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using AttributeName = CatalogueApi::FieldSales.Api.Catalogue.AttributeName;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;

namespace FieldSales.Web.Tests;

[Collection(CatalogueSqlServerCollection.Name)]
public sealed class ProductAttributeIntegrationTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    [Fact]
    public async Task Should_ShowHeadOfficeFactsOfflineAfterSync_When_RepDownloadsTheUpdatedProduct()
    {
        Seed seed = await SeedAsync();
        await using var api = app.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DirectoryDbContext>();
            services.AddScoped(_ => new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
                .UseSqlServer(app.ConnectionString).Options));
        }));
        await using (var scope = api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Database.MigrateAsync();
        await using (StaffWebsiteFactory website = app.CreateWebsite(api))
        {
            using HttpClient browser = website.CreateBrowser();
            await SignInAsync(browser);
            foreach (var pair in seed.Names.Zip(new[] { "200 g", "24 months", "5391234567890" }))
            {
                using var response = await AddAsync(browser, seed, await PageAsync(browser, seed), pair.First.Id.ToString(), pair.Second);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }
        }
        using HttpClient tokenClient = app.CreateApiClient();
        string token = tokenClient.DefaultRequestHeaders.Authorization!.Parameter!;
        app.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        using HttpClient download = api.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wi033-flow", Guid.NewGuid().ToString("N"));
        RepWorkspace workspace = new(root, "niamh");
        try
        {
            UnsentStore unsent = new(workspace);
            // Sync sends Calls and Ready to Send Orders first (WI-039) but never schedule changes (E13), so this saved work
            // must come through the download untouched.
            unsent.Save(new(Guid.NewGuid(), UnsentKind.ScheduleChange, DateTimeOffset.UtcNow, "{\"outcome\":\"Visited\"}"));
            string fingerprint = unsent.Fingerprint();
            TabletSync sync = new(workspace, download, new FlowTokens(token), new DriveStorageSpace());
            SyncOutcome outcome = await sync.SyncAsync();
            Assert.Equal(SyncStatus.Completed, outcome.Status);
            download.Dispose(); // The next read uses the persisted snapshot, with no server available.
            var reopened = new SnapshotStore(workspace);
            var detail = reopened.ReadProduct(seed.Id)!;
            Assert.Equal(["Weight", "Shelf life", "Barcode"], detail.Attributes.Select(row => row.Name));
            Assert.Equal(["200 g", "24 months", "5391234567890"], detail.Attributes.Select(row => row.Value));
            Assert.Equal(3, reopened.ReadActive()!.Manifest.Counts.ProductAttributes);
            Assert.Equal(fingerprint, unsent.Fingerprint());
        }
        finally
        {
            app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Should_ShowSourceNameValueRowsAcrossRestart_When_HeadOfficeAddsAttributes()
    {
        Seed seed = await SeedAsync();
        string[] values = ["200 g", "24 months", "5391234567890"];
        await using (StaffWebsiteFactory website = app.CreateWebsite())
        {
            using HttpClient browser = website.CreateBrowser();
            await SignInAsync(browser);
            for (int index = 0; index < values.Length; index++)
            {
                using var added = await AddAsync(browser, seed, await PageAsync(browser, seed), seed.Names[index].Id.ToString(), values[index]);
                Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
                Assert.Equal(seed.Path, added.Headers.Location!.OriginalString);
            }
            string html = await PageAsync(browser, seed);
            AssertRows(html, seed.Names.Select(name => name.Name).ToArray(), values);
            // Export the actual Razor output for optional visual inspection.
            if (Environment.GetEnvironmentVariable("WI033_LAYOUT_DIR") is { Length: > 0 } directory)
            {
                System.IO.Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(System.IO.Path.Combine(directory, "index.html"), html);
            }
        }
        await app.RestartAsync();
        await using StaffWebsiteFactory reopened = app.CreateWebsite();
        using HttpClient restarted = reopened.CreateBrowser();
        await SignInAsync(restarted);
        AssertRows(await PageAsync(restarted, seed), seed.Names.Select(name => name.Name).ToArray(), values);
        using HttpClient api = app.CreateApiClient();
        var details = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!;
        Assert.Equal(values, details.Attributes.Select(row => row.Value));
        Assert.Equal(12.50m, details.CurrentPrice!.Amount);
    }

    [Fact]
    public async Task Should_KeepExactTextAndIndependentForms_When_AttributesAndOtherSectionsAreSaved()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string value = "  <script>alert(1)</script> \"quoted\"\n" + new string('界', 6000);
        foreach (string text in new[] { "", "  ", value })
        {
            using var added = await AddAsync(browser, seed, await PageAsync(browser, seed), seed.Names[0].Id.ToString(), text);
            Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        }
        using var price = await PostAsync(browser, seed, await PageAsync(browser, seed), "Price",
            [new("BasePrice", "13.20"), new("EffectiveFrom", "2026-11-01")]);
        Assert.Equal(HttpStatusCode.Redirect, price.StatusCode);
        using var unit = await PostAsync(browser, seed, await PageAsync(browser, seed), "Unit",
            [new("Unit", "kg"), new("QuantityStep", "0.5"), new("MinimumQuantity", "1")]);
        Assert.Equal(HttpStatusCode.Redirect, unit.StatusCode);
        using var classification = await PostAsync(browser, seed, await PageAsync(browser, seed), "Classification", []);
        Assert.Equal(HttpStatusCode.Redirect, classification.StatusCode);
        using HttpClient api = app.CreateApiClient();
        var details = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!;
        Assert.Equal(["", "  ", value], details.Attributes.Select(row => row.Value));
        Assert.Equal("kg", details.Product.Unit);
        Assert.Equal(2, details.PriceHistory.Count);
        string record = await PageAsync(browser, seed);
        Assert.Contains("&lt;script&gt;", record);
        Assert.DoesNotContain("<script>alert(1)</script>", record);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RetainValueAndShowError_When_NameIsRetiredAfterFormLoad(bool deleted)
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, seed);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var name = await db.AttributeNames.SingleAsync(item => item.Id == seed.Names[0].Id);
            if (deleted) db.Remove(name); else name.Archive();
            await db.SaveChangesAsync();
        }
        using var rejected = await AddAsync(browser, seed, form, seed.Names[0].Id.ToString(), "200 g");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = await rejected.Content.ReadAsStringAsync();
        Assert.Contains(deleted ? "no longer exists" : "is archived", html);
        Assert.Contains("200 g", html);
        Assert.Contains("Selection unavailable", html);
        Assert.DoesNotContain(">Weight</option>", html);
        using HttpClient api = app.CreateApiClient();
        Assert.Empty((await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!.Attributes);
    }

    [Fact]
    public async Task Should_LabelExistingValuesAndExcludeArchivedNames_When_RecordIsOpened()
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var added = await AddAsync(browser, seed, await PageAsync(browser, seed), seed.Names[0].Id.ToString(), "200 g");
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var name = await db.AttributeNames.SingleAsync(item => item.Id == seed.Names[0].Id);
            name.Rename("Mass"); name.Archive();
            await db.SaveChangesAsync();
        }
        string record = await PageAsync(browser, seed);
        Assert.Contains("Mass (archived)</dt><dd>200 g</dd>", record);
        Assert.DoesNotContain(">Mass</option>", record);
        Assert.Contains(">Shelf life</option>", record);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("antiforgery")]
    [InlineData("role")]
    [InlineData("expired")]
    public async Task Should_RejectWithoutWritingOrReplay_When_AttributeSubmissionIsInvalidOrAccessIsLost(string condition)
    {
        Seed seed = await SeedAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, seed);
        if (condition == "role") website.Roles.SetRoles("niamh", StaffRoles.SalesManager);
        if (condition == "expired")
        {
            await using var scope = website.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            var ticket = await db.Tickets.SingleAsync();
            ticket.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        string nameId = condition == "missing" ? "" : condition == "malformed" ? "not-an-id" : seed.Names[0].Id.ToString();
        using var rejected = await AddAsync(browser, seed, form, nameId, "kept value", condition != "antiforgery");
        Assert.Equal(condition switch
        {
            "antiforgery" => HttpStatusCode.BadRequest,
            "role" or "expired" => HttpStatusCode.Redirect,
            _ => HttpStatusCode.OK
        }, rejected.StatusCode);
        if (condition is "missing" or "malformed") Assert.Contains("kept value", await rejected.Content.ReadAsStringAsync());
        await SignInAsync(browser);
        Assert.Contains("No attributes yet.", await PageAsync(browser, seed));
        using HttpClient api = app.CreateApiClient();
        Assert.Empty((await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{seed.Id}"))!.Attributes);
    }

    private async Task<Seed> SeedAsync()
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductAttributeValues.ExecuteDeleteAsync();
        Guid category = await app.ResetAsync();
        await db.AttributeNames.ExecuteDeleteAsync();
        Product product = Product.Create("SUN-0342", "SPF30 Sun Lotion v2 200ml", category, 12.50m, new DateOnly(2026, 8, 1));
        AttributeName[] names = [AttributeName.Create("Weight"), AttributeName.Create("Shelf life"), AttributeName.Create("Barcode")];
        db.Add(product); db.AddRange(names);
        await db.SaveChangesAsync();
        return new(product.Id, names);
    }
    private static async Task SignInAsync(HttpClient browser)
    {
        using var signedIn = await browser.GetAsync($"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(StaffRoles.HeadOfficeUser)}");
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
    }
    private static async Task<string> PageAsync(HttpClient browser, Seed seed)
    {
        using var response = await browser.GetAsync(seed.Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
    private static Task<HttpResponseMessage> AddAsync(HttpClient browser, Seed seed, string html, string nameId, string value,
        bool antiforgery = true) => PostAsync(browser, seed, html, "Attribute",
        [new("Attribute.NameId", nameId), new("Attribute.Value", value)], antiforgery);
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, Seed seed, string html, string handler,
        IEnumerable<KeyValuePair<string, string>> fields, bool antiforgery = true)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return browser.PostAsync(seed.Path + "?handler=" + handler, new FormUrlEncodedContent(antiforgery
            ? fields.Append(new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value))) : fields));
    }
    private static void AssertRows(string html, string[] names, string[] values)
    {
        for (int index = 0; index < names.Length; index++)
            Assert.Contains($"<dt>{names[index]}</dt><dd>{values[index]}</dd>", html);
        Assert.True(html.IndexOf("<dt>Weight</dt>", StringComparison.Ordinal) < html.IndexOf("<dt>Shelf life</dt>", StringComparison.Ordinal));
    }
    private sealed record Seed(Guid Id, AttributeName[] Names)
    {
        public string Path => $"/HeadOffice/Products/{Id}";
    }
    private sealed class FlowTokens(string token) : IAccessTokenSource
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult<string?>(token);
    }
}
