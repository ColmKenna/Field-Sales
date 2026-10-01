extern alias CatalogueApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FieldSales.StaffAccess;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using CategoryTree = CatalogueApi::FieldSales.Api.Catalogue.CategoryTree;
using Category = CatalogueApi::FieldSales.Api.Catalogue.Category;

namespace FieldSales.Web.Tests;

public sealed class ProductEndToEndTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    private const string CreateUrl = "/HeadOffice/Products/Create";
    private const string ProductName = "SPF30 Sun Lotion v2 200ml";

    [Fact]
    public async Task Should_OpenSavedRecord_When_MinimumProductIsCreated()
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        Assert.Contains("Create product", await PageAsync(browser, "/HeadOffice"));
        string form = await PageAsync(browser, CreateUrl);
        Assert.Contains("Health &gt; Skincare &gt; Suncare &gt; Lotions", form);
        Assert.Contains("value=\"Each\"", form);
        using HttpResponseMessage saved = await PostAsync(browser, form, Fields(category));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string record = WebUtility.HtmlDecode(await PageAsync(browser, saved.Headers.Location!.OriginalString));
        Assert.Contains($"SUN-0342 · {ProductName}", record);
        Assert.Contains("€12.50", record);
        Assert.Contains("Effective from 1 Oct 2026", record); // UTC is still September 30; Dublin is October 1.
        Assert.Contains("Unranged — orderable by all reps", record);
        foreach (string empty in new[] { "No profile yet.", "No attributes yet.", "No brands yet.",
                     "No supplier yet.", "No restriction group yet.", "No replacements yet." })
            Assert.Contains(empty, record);

        await using AsyncServiceScope scope = app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var product = await db.Products.Include(product => product.BasePrices).SingleAsync();
        Assert.Equal(category, product.CategoryId);
        Assert.Equal("Each", product.Unit);
        var price = Assert.Single(product.BasePrices);
        Assert.Equal(new DateOnly(2026, 10, 1), price.EffectiveFrom);
        Assert.Equal(12.50m, price.Amount);
        Assert.Empty(product.Attributes);
        Assert.Null(product.ParentProductId);
    }

    [Theory]
    [InlineData("SUN-0342")]
    [InlineData(" sun-0342 ")]
    public async Task Should_RejectDuplicateCode_When_CodeAlreadyExists(string duplicateCode)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CreateUrl);
        using HttpResponseMessage first = await PostAsync(browser, form, Fields(category));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Dictionary<string, string> second = Fields(category, duplicateCode);
        second["Name"] = "Unsaved product";
        using HttpResponseMessage rejected = await PostAsync(browser, form, second);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains($"Code SUN-0342 is already used by {ProductName}", html);
        Assert.Contains("value=\"Unsaved product\"", html);
        Assert.Contains("value=\"12.50\"", html);
        Assert.Equal((1, 1), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_SaveOnlyOneProduct_When_DuplicateCodesAreSubmittedConcurrently()
    {
        Guid category = await app.ResetAsync();
        using HttpClient api = app.CreateApiClient();
        var first = new { Code = "SUN-0342", Name = ProductName, CategoryId = category, Unit = "Each", BasePrice = 12.50m };
        var second = first with { Code = " sun-0342 " };
        HttpResponseMessage[] responses = await Task.WhenAll(
            api.PostAsJsonAsync("/catalogue/products/", first), api.PostAsJsonAsync("/catalogue/products/", second));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            HttpResponseMessage rejected = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains($"is already used by {ProductName}", await rejected.Content.ReadAsStringAsync());
            Assert.Equal((1, 1), await app.CountsAsync());
        }
        finally { foreach (HttpResponseMessage response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Should_RejectSave_When_CategoryIsMissingOrUnknown(string selection)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CreateUrl);
        Dictionary<string, string> fields = Fields(category);
        fields["CategoryId"] = selection;
        using HttpResponseMessage rejected = await PostAsync(browser, form, fields);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Choose a category", await rejected.Content.ReadAsStringAsync());
        Assert.Equal((0, 0), await app.CountsAsync());
    }

    [Theory]
    [InlineData("Code", "", "Code")]
    [InlineData("Name", "", "Name")]
    [InlineData("Code", "long-code", "Enter a product code of up to 100 characters.")]
    [InlineData("Name", "long-name", "Enter a product name of up to 200 characters.")]
    public async Task Should_RejectSave_When_RequiredTextIsMissingOrTooLong(string field, string value, string message)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CreateUrl);
        Dictionary<string, string> fields = Fields(category);
        fields[field] = value == "long-code" ? new string('C', 101) : value == "long-name" ? new string('N', 201) : value;
        using HttpResponseMessage rejected = await PostAsync(browser, form, fields);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains(message, await rejected.Content.ReadAsStringAsync());
        Assert.Equal((0, 0), await app.CountsAsync());
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("-1", false)]
    [InlineData("12.501", false)]
    public async Task Should_ApplyPriceValidation_When_AmountIsZeroNegativeOrOverPrecision(string amount, bool valid)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CreateUrl);
        Dictionary<string, string> fields = Fields(category);
        fields["BasePrice"] = amount;
        using HttpResponseMessage result = await PostAsync(browser, form, fields);
        Assert.Equal(valid ? HttpStatusCode.Redirect : HttpStatusCode.OK, result.StatusCode);
        if (valid)
            Assert.Contains("€0.00", WebUtility.HtmlDecode(await PageAsync(browser, result.Headers.Location!.OriginalString)));
        else
            Assert.Contains("Enter a non-negative base price with up to two decimal places", await result.Content.ReadAsStringAsync());
        Assert.Equal(valid ? (1, 1) : (0, 0), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_PreserveProductAndPrice_When_ApplicationRestarts()
    {
        Guid category = await app.ResetAsync();
        string url;
        await using (WebApplicationFactory<CatalogueDbContext> firstApi = app.NewApi())
        await using (StaffWebsiteFactory firstWebsite = app.CreateWebsite(firstApi))
        {
            using HttpClient browser = firstWebsite.CreateBrowser();
            await SignInAsync(browser);
            using HttpResponseMessage saved = await PostAsync(browser, await PageAsync(browser, CreateUrl), Fields(category));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            url = saved.Headers.Location!.OriginalString;
        }
        await using WebApplicationFactory<CatalogueDbContext> restartedApi = app.NewApi();
        await using StaffWebsiteFactory restartedWebsite = app.CreateWebsite(restartedApi);
        using HttpClient reopened = restartedWebsite.CreateBrowser();
        await SignInAsync(reopened);
        string record = WebUtility.HtmlDecode(await PageAsync(reopened, url));
        Assert.Contains($"SUN-0342 · {ProductName}", record);
        Assert.Contains("€12.50", record);
        Assert.Contains("Effective from 1 Oct 2026", record);
        Assert.Contains("Lotions", record);
        Assert.Equal((1, 1), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_RefreshProductBreadcrumb_When_CategoryIsRenamed()
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        using HttpResponseMessage saved = await PostAsync(browser, await PageAsync(browser, CreateUrl), Fields(category));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string url = saved.Headers.Location!.OriginalString;
        using HttpClient api = app.CreateApiClient();
        ProductDetails before = (await api.GetFromJsonAsync<ProductDetails>(saved.Headers.Location.OriginalString.Replace("/HeadOffice/Products", "/catalogue/products")))!;
        using HttpResponseMessage renamed = await api.PutAsJsonAsync($"/catalogue/categories/{category}/name", new { Name = "Lotions & Creams" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        string record = WebUtility.HtmlDecode(await PageAsync(browser, url));
        Assert.Contains("Lotions & Creams", record);
        Assert.DoesNotContain(">Lotions<", record);
        ProductDetails after = (await api.GetFromJsonAsync<ProductDetails>(url.Replace("/HeadOffice/Products", "/catalogue/products")))!;
        Assert.Equal(before.Product, after.Product);
        Assert.Equal(before.PriceHistory, after.PriceHistory);
        Assert.Equal(category, after.Breadcrumb.Last().Id);
    }

    [Theory]
    [InlineData(StaffRoles.FieldSalesperson)]
    [InlineData(StaffRoles.SalesManager)]
    public async Task Should_DenyProductAccess_When_StaffIsUnauthorized(string role)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser); // Obtain a real antiforgery token before changing permissions.
        string form = await PageAsync(browser, CreateUrl);
        website.Roles.SetRoles("niamh", role);
        app.Roles.SetRoles("niamh", role);
        using HttpResponseMessage read = await browser.GetAsync(CreateUrl);
        Assert.StartsWith("/AccessChanged?state=", read.Headers.Location?.OriginalString);
        using HttpResponseMessage write = await PostAsync(browser, form, Fields(category));
        Assert.StartsWith("/AccessChanged?state=", write.Headers.Location?.OriginalString);
        using HttpClient api = app.CreateApiClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/catalogue/products/category-choices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync($"/catalogue/products/{Guid.NewGuid()}")).StatusCode);
        using HttpResponseMessage directWrite = await api.PostAsJsonAsync("/catalogue/products/",
            new { Code = "SUN-0342", Name = ProductName, CategoryId = category, Unit = "Each", BasePrice = 12.50m });
        Assert.Equal(HttpStatusCode.Forbidden, directWrite.StatusCode);
        using HttpClient anonymous = app.Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/catalogue/products/category-choices")).StatusCode);
        Assert.Equal((0, 0), await app.CountsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_DenyProductAccessWithoutReplay_When_StaffLosesAccess(bool expired)
    {
        Guid category = await app.ResetAsync();
        await using StaffWebsiteFactory website = app.CreateWebsite();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, CreateUrl);
        if (expired)
        {
            await using AsyncServiceScope scope = website.Services.CreateAsyncScope();
            StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            StoredTicket ticket = await db.Tickets.SingleAsync();
            ticket.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        else
        {
            website.Roles.SetRoles("niamh");
            app.Roles.SetRoles("niamh");
        }
        using HttpResponseMessage denied = await PostAsync(browser, form, Fields(category));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        if (expired)
            Assert.StartsWith("https://localhost:7201/connect/authorize", denied.Headers.Location?.OriginalString);
        else
        {
            Assert.StartsWith("/AccessChanged?state=", denied.Headers.Location?.OriginalString);
            string message = await PageAsync(browser, denied.Headers.Location!.OriginalString);
            Assert.Contains("A change you just tried to make was not saved.", message);
        }
        Assert.Equal((0, 0), await app.CountsAsync());
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await SignInAsync(browser);
        await PageAsync(browser, CreateUrl);
        Assert.Equal((0, 0), await app.CountsAsync());
    }

    private static Dictionary<string, string> Fields(Guid category, string code = "SUN-0342") => new()
    {
        ["Code"] = code, ["Name"] = ProductName, ["CategoryId"] = category.ToString(), ["BasePrice"] = "12.50"
    };

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string form, Dictionary<string, string> fields)
    {
        Match token = Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "The product form must include an antiforgery token.");
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return browser.PostAsync(CreateUrl, new FormUrlEncodedContent(fields));
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
        return await response.Content.ReadAsStringAsync();
    }
}

public sealed class ProductApplication : IAsyncLifetime
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public TestStaffRoleLookup Roles { get; } = new();
    public WebApplicationFactory<CatalogueDbContext> Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        Api = NewApi();
        _ = Api.Server;
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _sql.DisposeAsync();
    }

    public WebApplicationFactory<CatalogueDbContext> NewApi() =>
        new WebApplicationFactory<CatalogueDbContext>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = Issuer,
                ["ConnectionStrings:CatalogueDb"] = _sql.GetConnectionString()
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CatalogueDbContext>();
                services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
                    .UseSqlServer(_sql.GetConnectionString()).Options));
                services.RemoveAll<IStaffRoleLookup>();
                services.AddSingleton<IStaffRoleLookup>(Roles);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new ProductClock());
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    OpenIdConnectConfiguration configuration = new() { Issuer = Issuer };
                    configuration.SigningKeys.Add(SigningKey);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        });

    public StaffWebsiteFactory CreateWebsite(WebApplicationFactory<CatalogueDbContext>? api = null) =>
        new((api ?? Api).Server.CreateHandler(), Token());

    public async Task RestartAsync()
    {
        await Api.DisposeAsync();
        Api = NewApi();
        _ = Api.Server;
    }

    public HttpClient CreateApiClient()
    {
        HttpClient client = Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        return client;
    }

    public async Task<Guid> ResetAsync()
    {
        Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductBasePrices.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        // Remove deepest categories first to respect the predecessor's restricted parent relationship.
        foreach (Category category in (await db.Categories.ToListAsync()).Reverse<Category>()) db.Categories.Remove(category);
        await db.SaveChangesAsync();
        CategoryTree tree = new([]);
        Guid? parent = null;
        foreach (string name in new[] { "Health", "Skincare", "Suncare", "Lotions" })
        {
            Category category = tree.Add(name, parent);
            db.Categories.Add(category);
            parent = category.Id;
        }
        await db.SaveChangesAsync();
        return parent!.Value;
    }

    public async Task<(int Products, int Prices)> CountsAsync()
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        return (await db.Products.CountAsync(), await db.ProductBasePrices.CountAsync());
    }

    private static string Token()
    {
        JwtSecurityToken token = new(issuer: Issuer, audience: "fieldsales-api",
            claims: [new Claim("sub", "niamh"), new Claim("scope", "fieldsales.api"), new Claim("role", StaffRoles.HeadOfficeUser)],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ProductClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 30, 23, 30, 0, TimeSpan.Zero);
    }
}
