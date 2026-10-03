extern alias CatalogueApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
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
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using LocationTownUsageSource = CatalogueApi::FieldSales.Api.Directory.LocationTownUsageSource;

namespace FieldSales.Web.Tests;

public sealed class GeographyEndToEndTests(GeographyApplication app) : IClassFixture<GeographyApplication>
{
    private const string PageUrl = "/HeadOffice/Geography";
    private const string Root = "/directory/geography";

    [Fact]
    public async Task Should_OfferTown_When_HierarchyIsCreated()
    {
        await app.ResetAsync();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        Assert.Contains("Manage geography", await HtmlAsync(browser, "/HeadOffice"));
        string page = await HtmlAsync(browser, PageUrl);
        SavePreview("empty", page);
        Assert.Contains("No regions yet.", page);
        using var createdRegion = await PostAsync(browser, PageUrl + "?handler=Create", page, new() { ["Name"] = "Leinster" });
        Assert.Equal(HttpStatusCode.Redirect, createdRegion.StatusCode);
        using var api = app.CreateApiClient();
        var region = Assert.Single((await api.GetFromJsonAsync<GeographyPage>(Root + "/"))!.Items);
        string countyUrl = PageUrl + "?regionId=" + region.Id;
        page = await HtmlAsync(browser, countyUrl);
        Assert.Contains("Leinster", page);
        using var createdCounty = await PostAsync(browser, countyUrl + "&handler=Create", page, new() { ["Name"] = "Wicklow" });
        Assert.Equal(HttpStatusCode.Redirect, createdCounty.StatusCode);
        var county = Assert.Single((await api.GetFromJsonAsync<GeographyPage>(Root + "/?regionId=" + region.Id))!.Items);
        Assert.Equal(region.Id, county.ParentId);
        string townUrl = PageUrl + "?countyId=" + county.Id;
        page = await HtmlAsync(browser, townUrl);
        using var createdTown = await PostAsync(browser, townUrl + "&handler=Create", page, new() { ["Name"] = "Rathdrum" });
        Assert.Equal(HttpStatusCode.Redirect, createdTown.StatusCode);
        string townsPage = await HtmlAsync(browser, townUrl);
        SavePreview("towns", townsPage);
        Assert.Contains("Rathdrum — Wicklow", WebUtility.HtmlDecode(townsPage));
        var choice = Assert.Single((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        Assert.Equal("Rathdrum — Wicklow, Leinster", choice.Label);
        Assert.Equal((county.Id, region.Id), (choice.CountyId, choice.RegionId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Should_RejectTown_When_CountyMissingOrUnknown(string? value)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        using var rejected = await api.PostAsJsonAsync(Root + "/towns", new CreateGeographyRequest("Rathdrum", value is null ? null : Guid.Parse(value)));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("Choose a county", (await rejected.Content.ReadFromJsonAsync<GeographyError>())!.Error);
        Assert.Equal((0, 0, 0), await app.CountsAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Should_RejectCounty_When_RegionMissingOrUnknown(string? value)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        using var rejected = await api.PostAsJsonAsync(Root + "/counties", new CreateGeographyRequest("Wicklow", value is null ? null : Guid.Parse(value)));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("Choose a region", (await rejected.Content.ReadFromJsonAsync<GeographyError>())!.Error);
        Assert.Equal((0, 0, 0), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_DisambiguateTowns_When_NamesMatchAcrossCounties()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var region = await CreateAsync(api, "regions", "Leinster");
        var wicklow = await CreateAsync(api, "counties", "Wicklow", region.Id);
        var wexford = await CreateAsync(api, "counties", "Wexford", region.Id);
        await CreateAsync(api, "towns", "Newtown", wicklow.Id);
        await CreateAsync(api, "towns", "Newtown", wexford.Id);
        var choices = (await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!;
        Assert.Equal(2, choices.Length);
        Assert.Equal(2, choices.Select(item => item.Id).Distinct().Count());
        Assert.Contains(choices, item => item.Label == "Newtown — Wicklow, Leinster");
        Assert.Contains(choices, item => item.Label == "Newtown — Wexford, Leinster");
    }

    [Fact]
    public async Task Should_PreserveIdentity_When_GeographyIsRenamed()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var region = await CreateAsync(api, "regions", "Leinster");
        var county = await CreateAsync(api, "counties", "Wicklow", region.Id);
        var town = await CreateAsync(api, "towns", "Rathdrum", county.Id);
        foreach (var (level, entity, name) in new[] { ("regions", region, "East"), ("counties", county, "County Wicklow"), ("towns", town, "Rathdrum Village") })
        {
            using var changed = await api.PutAsJsonAsync($"{Root}/{level}/{entity.Id}/name", new RenameGeographyRequest(name, entity.Version));
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var renamed = (await changed.Content.ReadFromJsonAsync<GeographyItem>())!;
            Assert.Equal((entity.Id, entity.ParentId), (renamed.Id, renamed.ParentId));
            Assert.NotEqual(entity.Version, renamed.Version);
            using var stale = await api.PutAsJsonAsync($"{Root}/{level}/{entity.Id}/name", new RenameGeographyRequest("Lost change", entity.Version));
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        var choice = Assert.Single((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        Assert.Equal(town.Id, choice.Id);
        Assert.Equal("Rathdrum Village — County Wicklow, East", choice.Label);
        var hierarchy = (await api.GetFromJsonAsync<GeographyPage>(Root + "/?countyId=" + county.Id))!;
        Assert.Equal(new[] { "East", "County Wicklow" }, hierarchy.Path.Select(item => item.Name));
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string url = PageUrl + "?countyId=" + county.Id;
        string html = await HtmlAsync(browser, url);
        var current = Assert.Single(hierarchy.Items);
        Dictionary<string, string> fields = new() { ["RenameId"] = town.Id.ToString(), ["RenameName"] = "Rathdrum", ["Version"] = current.Version };
        using var saved = await PostAsync(browser, url + "&handler=Rename", html, fields);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        fields["RenameName"] = "Stale submitted name";
        using var stalePage = await PostAsync(browser, url + "&handler=Rename", html, fields);
        Assert.Equal(HttpStatusCode.OK, stalePage.StatusCode);
        string error = await stalePage.Content.ReadAsStringAsync();
        Assert.Contains("Reload before renaming", error);
        Assert.Contains("value=\"Stale submitted name\"", error);
        Assert.Equal("Rathdrum", Assert.Single((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!).Name);
    }

    [Fact]
    public async Task Should_RejectDuplicateName_When_SiblingAlreadyExists()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var region = await CreateAsync(api, "regions", " Leinster ");
        var county = await CreateAsync(api, "counties", "Wicklow", region.Id);
        var town = await CreateAsync(api, "towns", "Rathdrum", county.Id);
        foreach (var (level, entity) in new[] { ("regions", region), ("counties", county), ("towns", town) })
        {
            using var duplicate = await api.PostAsJsonAsync(Root + "/" + level, new CreateGeographyRequest(entity.Name.ToLowerInvariant(), entity.ParentId));
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            var other = await CreateAsync(api, level, "Other", entity.ParentId);
            using var rename = await api.PutAsJsonAsync($"{Root}/{level}/{other.Id}/name", new RenameGeographyRequest(entity.Name.ToUpperInvariant(), other.Version));
            Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        }
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        var page = await HtmlAsync(browser, PageUrl);
        using var rejected = await PostAsync(browser, PageUrl + "?handler=Create", page, new() { ["Name"] = " leinster " });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("already exists", html);
        Assert.Contains("value=\" leinster \"", html);
        Assert.Equal((2, 2, 2), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_RejectDuplicateName_When_SiblingIsCreatedConcurrently()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var responses = await Task.WhenAll(api.PostAsJsonAsync(Root + "/regions", new CreateGeographyRequest("Leinster")),
            api.PostAsJsonAsync(Root + "/regions", new CreateGeographyRequest(" leinster ")));
        try
        {
            Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal((1, 0, 0), await app.CountsAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("long")]
    public async Task Should_RejectDuplicateName_When_RequiredNameIsInvalid(string input)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        using var response = await api.PostAsJsonAsync(Root + "/regions", new CreateGeographyRequest(input == "long" ? new string('L', 201) : input));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((0, 0, 0), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_LoadHierarchy_When_FileIsValid()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var region = await CreateAsync(api, "regions", "Leinster");
        var county = await CreateAsync(api, "counties", "Wicklow", region.Id);
        var town = await CreateAsync(api, "towns", "Rathdrum", county.Id);
        const string csv = "Region,County,Town\r\nleinster,WICKLOW,rathdrum\r\nLeinster,Wicklow,Arklow\r\nConnacht,Galway,\"Béal, Átha\"\r\nLeinster,Wicklow,Arklow\r\n";
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string page = await HtmlAsync(browser, PageUrl);
        using var imported = await UploadAsync(browser, page, csv);
        Assert.Equal(HttpStatusCode.Redirect, imported.StatusCode);
        Assert.Contains("Added 1 region, 1 county and 2 towns", await HtmlAsync(browser, imported.Headers.Location!.OriginalString));
        using var again = await ImportAsync(api, csv);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(new GeographyImportResult(0, 0, 0, 3), await again.Content.ReadFromJsonAsync<GeographyImportResult>());
        var choices = (await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!;
        Assert.Contains(choices, item => item.Id == town.Id && item.Name == "Rathdrum");
        Assert.Equal((2, 2, 3), await app.CountsAsync());
    }

    [Theory]
    [InlineData("Region,County,Town\nLeinster,Wicklow,Rathdrum\nConnacht,,Galway", "Row 3")]
    [InlineData("Region,County,Town\nLeinster,Wicklow,Rathdrum\nLeinster,Wicklow,\"Unclosed", "Row 3")]
    public async Task Should_SaveNothing_When_ImportContainsInvalidRows(string csv, string error)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        await CreateAsync(api, "regions", "Existing");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var rejected = await UploadAsync(browser, await HtmlAsync(browser, PageUrl), csv);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = await rejected.Content.ReadAsStringAsync();
        Assert.Contains(error, html); Assert.Contains("Existing", html);
        Assert.Equal((1, 0, 0), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_SaveNothing_When_ImportIsOversizedOrNotUtf8()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        using var large = await ImportAsync(api, new string('A', GeographyImportLimits.MaximumBytes + 1));
        Assert.Equal(HttpStatusCode.BadRequest, large.StatusCode);
        using var bytes = new ByteArrayContent([0xff, 0xfe, 0xff]);
        bytes.Headers.ContentType = new("text/csv");
        using var invalidEncoding = await api.PostAsync(Root + "/import", bytes);
        Assert.Equal(HttpStatusCode.BadRequest, invalidEncoding.StatusCode);
        Assert.Equal((0, 0, 0), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_DenyChanges_When_HeadOfficeAccessIsMissing()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        api.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync(Root + "/")).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        foreach (string role in new[] { StaffRoles.FieldSalesperson, StaffRoles.SalesManager, "SysAdmin" })
        {
            app.Roles.SetRoles("niamh", BusinessRoles.Contains(role) ? [role] : []);
            api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(role));
            foreach (string level in new[] { "regions", "counties", "towns" })
                Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(Root + "/" + level, new CreateGeographyRequest("Denied"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await ImportAsync(api, "Region,County,Town\nLeinster,Wicklow,Rathdrum")).StatusCode);
        }
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string page = await HtmlAsync(browser, PageUrl);
        using var noCsrf = await browser.PostAsync(PageUrl + "?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Denied" }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        website.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        using var revokedPage = await browser.GetAsync(PageUrl);
        Assert.Equal(HttpStatusCode.Redirect, revokedPage.StatusCode);
        Assert.Contains("AccessChanged", revokedPage.Headers.Location!.OriginalString);
        using var revokedWrite = await PostAsync(browser, PageUrl + "?handler=Create", page, new() { ["Name"] = "Denied" });
        Assert.Equal(HttpStatusCode.Redirect, revokedWrite.StatusCode);
        app.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(Root + "/town-choices")).StatusCode);
        Assert.Equal((0, 0, 0), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_PreserveHierarchy_When_ApplicationRestarts()
    {
        await app.ResetAsync();
        TownChoice[] before;
        using (var api = app.CreateApiClient())
        {
            using var imported = await ImportAsync(api, "Region,County,Town\nLeinster,Wicklow,Rathdrum");
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
            before = (await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!;
        }
        await app.RestartAsync();
        using var restarted = app.CreateApiClient();
        Assert.Equal(before, (await restarted.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
    }

    private static async Task<GeographyItem> CreateAsync(HttpClient api, string level, string name, Guid? parent = null)
    {
        using var response = await api.PostAsJsonAsync(Root + "/" + level, new CreateGeographyRequest(name, parent));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
    private static async Task<HttpResponseMessage> ImportAsync(HttpClient api, string csv)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await api.PostAsync(Root + "/import", content);
    }
    private static async Task SignInAsync(HttpClient browser) => Assert.Equal(HttpStatusCode.NoContent,
        (await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(StaffRoles.HeadOfficeUser))).StatusCode);
    private static async Task<string> HtmlAsync(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
    private static string Csrf(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string url, string html, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = Csrf(html);
        return browser.PostAsync(url, new FormUrlEncodedContent(fields));
    }
    private static async Task<HttpResponseMessage> UploadAsync(HttpClient browser, string html, string csv)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(Csrf(html)), "__RequestVerificationToken");
        body.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "Upload", "geography.csv");
        return await browser.PostAsync(PageUrl + "?handler=Import", body);
    }
    private static void SavePreview(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_GEOGRAPHY_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output);
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string stylesheet = new Uri(Path.Combine(repository, "FieldSales.Web/wwwroot/css/site.css")).AbsoluteUri;
        html = Regex.Replace(html, "href=\"/css/site.css[^\"]*\"", $"href=\"{stylesheet}\"");
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
}

public class GeographyApplication : IAsyncLifetime
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public TestStaffRoleLookup Roles { get; } = new();
    public TestLocationUsageSource Locations { get; } = new();
    protected virtual bool UseTestLocationUsage => true;
    public WebApplicationFactory<DirectoryDbContext> Api { get; private set; } = null!;
    private string ConnectionString => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "DirectoryTests" }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        Api = NewApi();
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Database.MigrateAsync();
        await InitializeAdditionalAsync();
    }
    protected virtual Task InitializeAdditionalAsync() => Task.CompletedTask;
    protected virtual void ConfigureAdditionalServices(IServiceCollection services, string connectionString) { }
    public async Task DisposeAsync() { await Api.DisposeAsync(); await _sql.DisposeAsync(); }
    private WebApplicationFactory<DirectoryDbContext> NewApi() => new WebApplicationFactory<DirectoryDbContext>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = Issuer,
            ["ConnectionStrings:DirectoryDb"] = ConnectionString,
            ["ConnectionStrings:CatalogueDb"] = _sql.GetConnectionString()
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DirectoryDbContext>();
            services.AddScoped(_ => new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(ConnectionString).Options));
            services.RemoveAll<IStaffRoleLookup>(); services.AddSingleton<IStaffRoleLookup>(Roles);
            if (UseTestLocationUsage)
            {
                foreach (var descriptor in services.Where(service => service.IsKeyedService
                    && Equals(service.ServiceKey, "directory") && service.KeyedImplementationType == typeof(LocationTownUsageSource)).ToArray())
                    services.Remove(descriptor);
                services.AddKeyedSingleton<IReferenceUsageSource>("directory", Locations);
            }
            ConfigureAdditionalServices(services, ConnectionString);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer }; configuration.SigningKeys.Add(Key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    });
    public StaffWebsiteFactory CreateWebsite() => new(Api.Server.CreateHandler(), Token());
    public HttpClient CreateApiClient()
    {
        var client = Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token()); return client;
    }
    public async Task ResetAsync()
    {
        Locations.Reset();
        Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        await db.Locations.ExecuteDeleteAsync(); await db.Customers.ExecuteDeleteAsync();
        await db.LocationTypes.ExecuteDeleteAsync(); await db.ContactTypes.ExecuteDeleteAsync();
        await db.Towns.ExecuteDeleteAsync(); await db.Counties.ExecuteDeleteAsync(); await db.Regions.ExecuteDeleteAsync();
    }
    public async Task<(int, int, int)> CountsAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        return (await db.Regions.CountAsync(), await db.Counties.CountAsync(), await db.Towns.CountAsync());
    }
    public async Task RestartAsync() { await Api.DisposeAsync(); Api = NewApi(); _ = Api.Server; }
    public string Token(string role = StaffRoles.HeadOfficeUser, string scope = "fieldsales.api", string subject = "niamh")
    {
        var token = new JwtSecurityToken(Issuer, "fieldsales-api",
            [new("sub", subject), new("scope", scope), new("role", role)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
