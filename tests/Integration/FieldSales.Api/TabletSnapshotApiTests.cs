using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Coverage;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.Quantities;
using FieldSales.ReferenceData;
using FieldSales.Snapshot.Contracts;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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

namespace FieldSales.Api.Tests;

// WI-032 increment 1: the server-side Morning Snapshot, contract v1 (scenarios 1–8 and 23's download part).
[Collection(SqlServerCollection.Name)]
public sealed class TabletSnapshotApiTests(TabletSnapshotFixture fixture) : IClassFixture<TabletSnapshotFixture>, IAsyncLifetime
{
    private readonly TabletSnapshotApplication _app = fixture.Application;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        await _app.ResetAsync();
        _initialized = true;
    }
    public Task DisposeAsync() => _initialized ? _app.ResetAsync() : Task.CompletedTask;

    [Fact]
    public async Task Should_IncludeOrderedFactsAndExcludeRestrictedFacts_When_RepSyncsAgain()
    {
        Category category = await _app.CategoryAsync("Facts");
        Guid product = await _app.ProductAsync("SUN-0342", "Sun Lotion", category.Id);
        var group = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid hidden = await _app.ProductAsync("PRIVATE", "Restricted", category.Id, group: group);
        await using (var scope = _app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var names = new[] { AttributeName.Create("Weight"), AttributeName.Create("Shelf life"), AttributeName.Create("Barcode") };
            db.AttributeNames.AddRange(names);
            var visible = await db.Products.SingleAsync(item => item.Id == product);
            foreach (var pair in names.Zip(new[] { "200 g", "24 months", "5391234567890" })) visible.AddAttribute(pair.First, pair.Second);
            var restricted = await db.Products.SingleAsync(item => item.Id == hidden);
            restricted.AddAttribute(names[0], "PRIVATE VALUE");
            await db.SaveChangesAsync();
        }
        TabletSnapshot first = await _app.SyncAsync("colm");
        Assert.Equal(["200 g", "24 months", "5391234567890"], first.ProductAttributes!.Select(row => row.Value));
        Assert.Equal([0, 1, 2], first.ProductAttributes!.Select(row => row.Position));
        Assert.All(first.ProductAttributes!, row => Assert.Equal(product, row.ProductId));
        Assert.DoesNotContain(first.Products, row => row.Id == hidden);
        await using (var scope = _app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var weight = await db.AttributeNames.SingleAsync(item => item.Name == "Weight");
            weight.Rename("Mass"); weight.Archive();
            await db.SaveChangesAsync();
        }
        TabletSnapshot next = await _app.SyncAsync("colm");
        Assert.Equal(new SnapshotProductAttribute(product, 0, "Mass", "200 g", true), next.ProductAttributes![0]);
        Assert.DoesNotContain(next.ProductAttributes!, row => row.ProductId == hidden || row.Value == "PRIVATE VALUE");
        Assert.Equal(3, next.Manifest.Counts.ProductAttributes);
    }

    [Fact]
    public async Task Should_ContainOnlyTheRepsAssignedLocations_When_BuildingTheSnapshot()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid hickeys = await _app.CustomerAsync("Hickey's Pharmacy", places.Arklow.Id);
        Guid byrnes = await _app.CustomerAsync("Byrne's Londis", places.Arklow.Id);
        Guid murphys = await _app.CustomerAsync("Murphy's Pharmacy", places.Rathdrum.Id);
        await _app.CustomerAsync("Quinn's Centra", places.Gorey.Id);
        await _app.AssignAsync("colm", TerritoryLevel.County, places.Wicklow.Id);
        await _app.AssignAsync("aoife", TerritoryLevel.Town, places.Rathdrum.Id);

        TabletSnapshot colm = await _app.SyncAsync("colm");
        Assert.Equal([byrnes, hickeys], colm.Locations.Select(location => location.Id));
        Assert.Equal("colm", colm.Manifest.RepSubject);
        Assert.Equal([murphys], (await _app.SyncAsync("aoife")).Locations.Select(location => location.Id));
        // An unassigned Location (Wexford) and another rep's carve-out never reach a snapshot.
        Assert.Empty((await _app.SyncAsync("brian")).Locations);
    }

    [Fact]
    public async Task Should_IncludeTownEircodeAndActiveMainContact_When_ALocationIsAssigned()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid hickeys = await _app.CustomerAsync("Hickey's Pharmacy", places.Arklow.Id, "Y14 X2F9", customerName: "Hickey Group");
        Guid byrnes = await _app.CustomerAsync("Byrne's Londis", places.Arklow.Id);
        Guid owner = await _app.ContactTypeAsync("Owner");
        Guid mary = await _app.ContactAsync("Mary Hickey", owner, hickeys, "+353 402 31234", "mary@hickeys.example");
        Guid sean = await _app.ContactAsync("Sean Byrne", owner, byrnes);
        await _app.RetireContactAsync(sean);
        await _app.AssignAsync("colm", TerritoryLevel.County, places.Wicklow.Id);

        TabletSnapshot snapshot = await _app.SyncAsync("colm");
        SnapshotLocation hickey = snapshot.Locations.Single(location => location.Id == hickeys);
        Assert.Equal("Hickey's Pharmacy", hickey.Name);
        Assert.Equal("Hickey Group", hickey.CustomerName);
        Assert.Equal(new SnapshotTown(places.Arklow.Id, "Arklow", "Wicklow"), hickey.Town);
        Assert.Equal("Y14 X2F9", hickey.Eircode);
        Assert.Equal(new SnapshotContact(mary, "Mary Hickey", "Owner", "+353 402 31234", "mary@hickeys.example"), hickey.MainContact);
        // A retired Main Contact is not sent; the shop itself still is.
        SnapshotLocation byrne = snapshot.Locations.Single(location => location.Id == byrnes);
        Assert.Null(byrne.MainContact);
        Assert.Null(byrne.Eircode);
    }

    [Fact]
    public async Task Should_ExcludeEveryRestrictedProduct_When_BuildingContractV1()
    {
        Category general = await _app.CategoryAsync("Cough and cold");
        Category pharmacy = await _app.CategoryAsync("Pharmacy medicines");
        Guid syrup = await _app.ProductAsync("SYR-100", "Autumn Cough Syrup 100ml", general.Id);
        RestrictionGroup pharmacyOnly = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        RestrictionGroup retired = await _app.RestrictionGroupAsync("Retired range");
        Guid permitted = await _app.ProductAsync("PH-1", "Pharmacy-only lozenges", pharmacy.Id, group: pharmacyOnly);
        Guid archived = await _app.ProductAsync("RT-1", "Retired tonic", general.Id, group: retired);
        await _app.ArchiveGroupAsync(retired.Id);
        Guid withheld = await _app.ProductAsync("PH-2", "Pharmacy-only spray", general.Id, group: pharmacyOnly);
        await _app.GrantAsync("colm", pharmacyOnly.Id);
        await _app.GrantAsync("colm", retired.Id);

        TabletSnapshot snapshot = await _app.SyncAsync("colm");
        // Version 1 carries no restricted product, even one the rep is permitted (WI-043 adds those).
        Assert.Equal([syrup], snapshot.Products.Select(product => product.Id));
        Assert.DoesNotContain(snapshot.BasePrices, price => price.ProductId == permitted || price.ProductId == archived
            || price.ProductId == withheld);
        // A category holding only restricted products is not sent either.
        Assert.Equal([general.Id], snapshot.Categories.Select(category => category.Id));
    }

    [Fact]
    public async Task Should_IncludeCategoriesUnitsAndSteps_When_ProductsAreIncluded()
    {
        Category health = await _app.CategoryAsync("Health");
        Category remedies = await _app.CategoryAsync("Remedies", health.Id);
        Category coughs = await _app.CategoryAsync("Coughs", remedies.Id);
        await _app.CategoryAsync("Empty shelf", health.Id);
        Guid loose = await _app.ProductAsync("SW-1KG", "Loose sweets", coughs.Id, UnitOfMeasure.Kilogram, step: 0.25m, minimum: 0.5m);
        Guid bottle = await _app.ProductAsync("SYR-200", "Syrup 200ml", coughs.Id);
        RestrictionGroup group = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid hidden = await _app.ProductAsync("PH-200", "Pharmacy syrup", coughs.Id, group: group);
        await _app.SetParentAsync(bottle, loose);
        await _app.SetParentAsync(loose, hidden);

        TabletSnapshot snapshot = await _app.SyncAsync("colm");
        // The full parent chain of each included product's category, and nothing else.
        Dictionary<Guid, SnapshotCategory> categories = snapshot.Categories.ToDictionary(category => category.Id);
        Assert.Equal(new[] { health.Id, remedies.Id, coughs.Id }.Order(), categories.Keys.Order());
        Assert.Equal(["Health", "Remedies", "Coughs"], Breadcrumb(categories, coughs.Id));
        SnapshotProduct sweets = snapshot.Products.Single(product => product.Id == loose);
        Assert.Equal(("kg", 0.25m, 0.5m), (sweets.Unit, sweets.QuantityStep, sweets.MinimumQuantity));
        // A parent is kept only when the parent itself is in the snapshot.
        Assert.Null(sweets.ParentProductId);
        SnapshotProduct syrup = snapshot.Products.Single(product => product.Id == bottle);
        Assert.Equal("Each", syrup.Unit);
        Assert.Equal(loose, syrup.ParentProductId);
    }

    [Fact]
    public async Task Should_IncludeTodaysAndFutureBasePrices_When_PricesAreDated()
    {
        // The clock reads 30 September 23:30 UTC: already 1 October in Dublin.
        Category category = await _app.CategoryAsync("Cough and cold");
        Guid syrup = await _app.ProductAsync("SYR-100", "Autumn Cough Syrup", category.Id, price: 9.50m, from: new(2026, 8, 1));
        await _app.AddPriceAsync(syrup, 9.99m, new(2026, 9, 1));
        await _app.AddPriceAsync(syrup, 10.49m, new(2026, 10, 1));
        await _app.AddPriceAsync(syrup, 10.99m, new(2026, 11, 1));
        await _app.AddPriceAsync(syrup, 11.49m, new(2026, 12, 1));
        Guid future = await _app.ProductAsync("NEW-1", "Winter balm", category.Id, price: 4m, from: new(2026, 10, 15));

        TabletSnapshot snapshot = await _app.SyncAsync("colm");
        Assert.Equal(new DateOnly(2026, 10, 1), snapshot.Manifest.BuildDate);
        Assert.Equal([new SnapshotBasePrice(syrup, new(2026, 10, 1), 10.49m), new SnapshotBasePrice(syrup, new(2026, 11, 1), 10.99m),
            new SnapshotBasePrice(syrup, new(2026, 12, 1), 11.49m)], snapshot.BasePrices.Where(price => price.ProductId == syrup));
        // A product priced only from a later date is still sent, with that future price.
        Assert.Equal([new SnapshotBasePrice(future, new(2026, 10, 15), 4m)], snapshot.BasePrices.Where(price => price.ProductId == future));
    }

    [Fact]
    public async Task Should_ServeOnlyTheCallersOwnSnapshot_When_StaffRequestSync()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid hickeys = await _app.CustomerAsync("Hickey's Pharmacy", places.Arklow.Id);
        Guid murphys = await _app.CustomerAsync("Murphy's Pharmacy", places.Rathdrum.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        await _app.AssignAsync("sorcha", TerritoryLevel.Town, places.Rathdrum.Id);

        // No request field can name another rep.
        TabletSnapshot colm = await _app.SyncAsync("colm", path: SnapshotContract.Path + "?rep=sorcha&repSubject=sorcha");
        Assert.Equal("colm", colm.Manifest.RepSubject);
        Assert.Equal([hickeys], colm.Locations.Select(location => location.Id));
        // A Sales Manager who also sells gets their own snapshot.
        TabletSnapshot sorcha = await _app.SyncAsync("sorcha", roles: [BusinessRoles.SalesManager, BusinessRoles.FieldSalesperson]);
        Assert.Equal("sorcha", sorcha.Manifest.RepSubject);
        Assert.Equal([murphys], sorcha.Locations.Select(location => location.Id));
    }

    [Theory]
    [InlineData(BusinessRoles.SalesManager, true, HttpStatusCode.Forbidden)]
    [InlineData(BusinessRoles.HeadOfficeUser, true, HttpStatusCode.Forbidden)]
    [InlineData(BusinessRoles.FieldSalesperson, false, HttpStatusCode.Forbidden)]
    [InlineData(null, true, HttpStatusCode.Unauthorized)]
    public async Task Should_RefuseSync_When_TheCallerIsNotACurrentFieldSalesperson(string? role, bool withScope, HttpStatusCode expected)
    {
        using HttpClient client = role is null ? _app.Anonymous() : _app.ClientAs("niamh", [role!], withScope);
        using HttpResponseMessage response = await TabletSnapshotApplication.RequestAsync(client, SnapshotContract.Path, "1");
        Assert.Equal(expected, response.StatusCode);
        Assert.NotEqual(SnapshotContract.MediaType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Should_FailClosed_When_CurrentRolesCannotBeChecked()
    {
        _app.Roles.Unavailable = true;
        using HttpClient client = _app.ClientAs("colm", [BusinessRoles.FieldSalesperson]);
        using HttpResponseMessage response = await TabletSnapshotApplication.RequestAsync(client, SnapshotContract.Path, "1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData("2", HttpStatusCode.NotAcceptable)]
    [InlineData("1,2", HttpStatusCode.OK)]
    [InlineData(" 2 , 1 ", HttpStatusCode.OK)]
    [InlineData(null, HttpStatusCode.BadRequest)]
    [InlineData("one", HttpStatusCode.BadRequest)]
    [InlineData("0", HttpStatusCode.BadRequest)]
    public async Task Should_RefuseWithUpdateMessage_When_NoContractVersionIsShared(string? majors, HttpStatusCode expected)
    {
        using HttpClient client = _app.ClientAs("colm", [BusinessRoles.FieldSalesperson]);
        using HttpResponseMessage response = await TabletSnapshotApplication.RequestAsync(client, SnapshotContract.Path, majors);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
            Assert.Equal("1.1", (await TabletSnapshotApplication.ReadAsync(response)).Manifest.Version);
        if (expected == HttpStatusCode.NotAcceptable)
            Assert.Equal(SnapshotContract.UpdateRequiredTitle, (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Should_ReflectCurrentServerData_When_SyncingAgain()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid hickeys = await _app.CustomerAsync("Hickey's Pharmacy", places.Arklow.Id);
        Guid murphys = await _app.CustomerAsync("Murphy's Pharmacy", places.Rathdrum.Id);
        TerritoryAssignment county = await _app.AssignAsync("colm", TerritoryLevel.County, places.Wicklow.Id);
        Category category = await _app.CategoryAsync("Cough and cold");
        Guid syrup = await _app.ProductAsync("SYR-100", "Autumn Cough Syrup", category.Id, price: 9.50m, from: new(2026, 8, 1));

        TabletSnapshot first = await _app.SyncAsync("colm");
        Assert.Equal([hickeys, murphys], first.Locations.Select(location => location.Id));

        await _app.AssignAsync("aoife", TerritoryLevel.Town, places.Rathdrum.Id);
        await _app.AddPriceAsync(syrup, 9.99m, new(2026, 10, 20));
        Guid balm = await _app.ProductAsync("NEW-1", "Winter balm", category.Id);
        TabletSnapshot second = await _app.SyncAsync("colm");
        Assert.Equal([hickeys], second.Locations.Select(location => location.Id));
        Assert.Contains(new SnapshotBasePrice(syrup, new(2026, 10, 20), 9.99m), second.BasePrices);
        Assert.Contains(second.Products, product => product.Id == balm);
        Assert.True(second.Manifest.BuiltUtc >= first.Manifest.BuiltUtc);

        await _app.RemoveAssignmentAsync(county.Id);
        Assert.Empty((await _app.SyncAsync("colm")).Locations);
    }

    [Fact]
    public async Task Should_UseCurrentServerAccess_When_AccessChangesBeforeTheNextSync()
    {
        Geography places = await _app.SeedGeographyAsync();
        await _app.CustomerAsync("Hickey's Pharmacy", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.County, places.Wicklow.Id);
        using HttpClient client = _app.ClientAs("colm", [BusinessRoles.FieldSalesperson]);
        using (HttpResponseMessage allowed = await TabletSnapshotApplication.RequestAsync(client, SnapshotContract.Path, "1"))
            Assert.Single((await TabletSnapshotApplication.ReadAsync(allowed)).Locations);

        // The same token, after the Field Salesperson role is removed: roles are checked on every request.
        _app.Roles.Set("colm", BusinessRoles.SalesManager);
        using HttpResponseMessage refused = await TabletSnapshotApplication.RequestAsync(client, SnapshotContract.Path, "1");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task Should_LeaveSchemaAndAccessUsable_When_ResettingSnapshotScenario()
    {
        var places = await _app.SeedGeographyAsync();
        await _app.CustomerAsync("Reset shop", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.County, places.Wicklow.Id);
        var category = await _app.CategoryAsync("Reset category");
        await _app.ProductAsync("RESET", "Reset product", category.Id);
        Assert.Single((await _app.SyncAsync("colm")).Products);
        _app.Roles.Unavailable = true;

        await _app.ResetAsync();

        Assert.False(_app.Roles.Unavailable);
        var empty = await _app.SyncAsync("colm");
        Assert.Empty(empty.Products);
        Assert.Empty(empty.Locations);
        await using var scope = _app.Api.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var catalogue = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Assert.Empty(await directory.Database.GetPendingMigrationsAsync());
        Assert.Empty(await catalogue.Database.GetPendingMigrationsAsync());
        await directory.Database.OpenConnectionAsync();
        await using var guards = directory.Database.GetDbConnection().CreateCommand();
        guards.CommandText = """
            SELECT (SELECT COUNT(*) FROM sys.triggers WHERE parent_class = 1 AND is_disabled = 1)
                 + (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1)
                 + (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled = 1 OR is_not_trusted = 1);
            """;
        Assert.Equal(0, Convert.ToInt32(await guards.ExecuteScalarAsync()));
        // Reusing a natural key confirms that the prior graph was removed.
        await _app.CategoryAsync("Reset category");
        var next = await _app.SeedGeographyAsync();
        Guid shop = await _app.CustomerAsync("Reset shop", next.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.County, next.Wicklow.Id);
        Assert.Equal(shop, Assert.Single((await _app.SyncAsync("colm")).Locations).Id);
    }

    private static string[] Breadcrumb(IReadOnlyDictionary<Guid, SnapshotCategory> categories, Guid id)
    {
        List<string> names = [];
        for (Guid? next = id; next is Guid current; next = categories[current].ParentId) names.Insert(0, categories[current].Name);
        return [.. names];
    }
}

public sealed record Geography(GeographyItem Wicklow, GeographyItem Arklow, GeographyItem Rathdrum, GeographyItem Gorey);

public sealed class TabletSnapshotFixture(SqlServerFixture sql) : IAsyncLifetime
{
    public TabletSnapshotApplication Application { get; } = new(sql.CreateConnectionString("TabletSnapshot"));
    public Task InitializeAsync() => Application.InitializeAsync();
    public Task DisposeAsync() => Application.DisposeAsync();
}

public sealed class TabletSnapshotApplication(string connectionString) : IAsyncLifetime
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));
    public SnapshotRoleLookup Roles { get; } = new();
    private WebApplicationFactory<Program>? _api;
    private SnapshotDatabaseReset? _reset;
    public WebApplicationFactory<Program> Api => _api ?? throw new InvalidOperationException("Snapshot application is not initialized.");

    public async Task InitializeAsync()
    {
        try
        {
            _api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Authority"] = Issuer,
                    ["ConnectionStrings:CatalogueDb"] = connectionString,
                    ["ConnectionStrings:DirectoryDb"] = connectionString
                }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<CatalogueDbContext>();
                    services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
                        .UseSqlServer(connectionString).Options));
                    services.RemoveAll<DirectoryDbContext>();
                    services.AddScoped(_ => new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
                        .UseSqlServer(connectionString).Options));
                    services.RemoveAll<IStaffRoleLookup>();
                    services.AddSingleton<IStaffRoleLookup>(Roles);
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new SnapshotClock());
                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        OpenIdConnectConfiguration configuration = new() { Issuer = Issuer };
                        configuration.SigningKeys.Add(SigningKey);
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                    });
                });
            });
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
            _reset = new SnapshotDatabaseReset(connectionString);
            await _reset.InitializeAsync();
        }
        catch (Exception setupError)
        {
            try { await DisposeAsync(); }
            catch (Exception cleanupError) { setupError.Data["FixtureCleanupFailure"] = cleanupError; }
            throw;
        }
    }

    public async Task ResetAsync()
    {
        await (_reset ?? throw new InvalidOperationException("Snapshot schema is not initialized.")).ResetAsync();
        Roles.Reset();
    }

    public async Task DisposeAsync()
    {
        var api = Interlocked.Exchange(ref _api, null);
        if (api is not null) await api.DisposeAsync();
    }

    public HttpClient ClientAs(string subject, string[] roles, bool withScope = true)
    {
        Roles.Set(subject, roles);
        HttpClient client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(subject, roles, withScope));
        return client;
    }

    public HttpClient Anonymous() => Api.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    public async Task<TabletSnapshot> SyncAsync(string subject, string[]? roles = null, string path = SnapshotContract.Path)
    {
        using HttpClient client = ClientAs(subject, roles ?? [BusinessRoles.FieldSalesperson]);
        using HttpResponseMessage response = await RequestAsync(client, path, "1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    public static Task<HttpResponseMessage> RequestAsync(HttpClient client, string path, string? majors)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        if (majors is not null) request.Headers.Add(SnapshotContract.AcceptedMajorsHeader, majors);
        return client.SendAsync(request);
    }

    // Verifies what the tablet will verify: both hashes, the version header and the declared counts.
    public static async Task<TabletSnapshot> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(SnapshotContract.MediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        byte[] gzip = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Header(response, SnapshotContract.GzipSha256Header), Convert.ToHexStringLower(SHA256.HashData(gzip)));
        using MemoryStream plain = new();
        using (GZipStream decompress = new(new MemoryStream(gzip), CompressionMode.Decompress)) decompress.CopyTo(plain);
        Assert.Equal(Header(response, SnapshotContract.PlainSha256Header), Convert.ToHexStringLower(SHA256.HashData(plain.ToArray())));
        TabletSnapshot snapshot = JsonSerializer.Deserialize<TabletSnapshot>(plain.ToArray(), SnapshotContract.JsonOptions)!;
        Assert.Equal(SnapshotContract.Schema, snapshot.Manifest.Schema);
        Assert.Equal(Header(response, SnapshotContract.VersionHeader), snapshot.Manifest.Version);
        Assert.Equal(new SnapshotCounts(snapshot.Locations.Count, snapshot.Categories.Count, snapshot.Products.Count,
            snapshot.BasePrices.Count, snapshot.ProductAttributes?.Count ?? 0), snapshot.Manifest.Counts);
        return snapshot;
    }

    private static string Header(HttpResponseMessage response, string name) => Assert.Single(response.Headers.GetValues(name));

    public async Task<Geography> SeedGeographyAsync()
    {
        GeographyItem leinster = await PlaceAsync("regions", "Leinster");
        GeographyItem wicklow = await PlaceAsync("counties", "Wicklow", leinster.Id);
        GeographyItem wexford = await PlaceAsync("counties", "Wexford", leinster.Id);
        return new(wicklow, await PlaceAsync("towns", "Arklow", wicklow.Id), await PlaceAsync("towns", "Rathdrum", wicklow.Id),
            await PlaceAsync("towns", "Gorey", wexford.Id));
    }

    private async Task<GeographyItem> PlaceAsync(string level, string name, Guid? parentId = null)
    {
        using HttpClient office = HeadOffice();
        using HttpResponseMessage response = await office.PostAsJsonAsync($"/directory/geography/{level}", new CreateGeographyRequest(name, parentId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }

    public async Task<Guid> CustomerAsync(string locationName, Guid townId, string? eircode = null, string? customerName = null)
    {
        using HttpClient office = HeadOffice();
        using HttpResponseMessage response = await office.PostAsJsonAsync("/directory/customers",
            new CreateCustomerRequest(customerName ?? locationName, new(locationName, townId, eircode)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.Single((await response.Content.ReadFromJsonAsync<CustomerDetails>())!.Locations).Id;
    }

    public async Task<Guid> ContactTypeAsync(string name)
    {
        using HttpClient office = HeadOffice();
        using HttpResponseMessage response = await office.PostAsJsonAsync($"/directory/reference-data/{ReferenceListKeys.ContactTypes}",
            new SaveDirectoryTypeRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().ContactTypes
            .Where(type => type.Name == name).Select(type => type.Id).SingleAsync();
    }

    // The first active contact linked to a Location becomes its Main Contact.
    public async Task<Guid> ContactAsync(string name, Guid typeId, Guid locationId, string? phone = null, string? email = null)
    {
        using HttpClient office = HeadOffice();
        using HttpResponseMessage response = await office.PostAsJsonAsync("/directory/contacts",
            new CreateContactRequest(name, typeId, [locationId], phone, email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContactDetails>())!.Id;
    }

    public async Task RetireContactAsync(Guid contactId)
    {
        using HttpClient office = HeadOffice();
        ContactDetails details = (await office.GetFromJsonAsync<ContactDetails>($"/directory/contacts/{contactId}"))!;
        using HttpResponseMessage response = await office.PostAsJsonAsync($"/directory/contacts/{contactId}/retire",
            new RetireMainContactRequest(details.Version,
                details.Locations.Select(location => new ContactLocationVersion(location.LocationId, location.LocationVersion)).ToArray()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public async Task<TerritoryAssignment> AssignAsync(string rep, TerritoryLevel level, Guid unitId)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        DirectoryDbContext db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        // Every assigned rep has a reporting line (the assignment's foreign key requires it).
        if (!await db.RepReportingLines.AnyAsync(line => line.RepSubject == rep))
            db.RepReportingLines.Add(RepReportingLine.Create(rep, "niamh"));
        TerritoryAssignment assignment = TerritoryAssignment.Create(rep, new(level, unitId));
        db.TerritoryAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }

    public async Task RemoveAssignmentAsync(Guid assignmentId)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().TerritoryAssignments
            .Where(row => row.Id == assignmentId).ExecuteDeleteAsync());
    }

    public async Task<Category> CategoryAsync(string name, Guid? parentId = null)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Category category = new CategoryTree(await db.Categories.ToArrayAsync()).Add(name, parentId);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task<RestrictionGroup> RestrictionGroupAsync(string name)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        RestrictionGroup group = RestrictionGroup.Create(name);
        db.RestrictionGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    // A group is archived after its products were assigned; new assignments to it are refused.
    public async Task ArchiveGroupAsync(Guid groupId)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        (await db.RestrictionGroups.SingleAsync(row => row.Id == groupId)).Archive();
        await db.SaveChangesAsync();
    }

    public async Task<Guid> ProductAsync(string code, string name, Guid categoryId, UnitOfMeasure unit = UnitOfMeasure.Each,
        decimal? step = null, decimal? minimum = null, RestrictionGroup? group = null, decimal price = 2.50m, DateOnly? from = null)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        QuantityRules? rules = null;
        if (step is not null || unit != UnitOfMeasure.Each)
            Assert.True(QuantityRules.TryCreate(unit.ToCode(), step, minimum, out rules, out _));
        Product product = Product.Create(code, name, categoryId, price, from ?? new DateOnly(2026, 9, 1), rules);
        if (group is not null) product.SetRestrictionGroup(await db.RestrictionGroups.SingleAsync(row => row.Id == group.Id));
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    public async Task AddPriceAsync(Guid productId, decimal amount, DateOnly from)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Product product = await db.Products.Include(row => row.BasePrices).SingleAsync(row => row.Id == productId);
        product.AddBasePrice(amount, from);
        await db.SaveChangesAsync();
    }

    // No domain operation sets a parent product yet; the column exists for minimum-product links.
    public async Task SetParentAsync(Guid productId, Guid parentId)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database
            .ExecuteSqlInterpolatedAsync($"UPDATE [Products] SET [ParentProductId] = {parentId} WHERE [Id] = {productId}");
    }

    // Permissions are granted only inside the API's writer; tests insert the stored current grant.
    public async Task GrantAsync(string rep, Guid groupId)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database
            .ExecuteSqlInterpolatedAsync($"INSERT INTO [RestrictionPermissions] ([RepSubject], [RestrictionGroupId]) VALUES ({rep}, {groupId})");
    }

    private HttpClient HeadOffice() => ClientAs("orla", [BusinessRoles.HeadOfficeUser]);

    private static string Token(string subject, string[] roles, bool withScope)
    {
        List<Claim> claims = [new("sub", subject), .. roles.Select(role => new Claim("role", role))];
        if (withScope) claims.Add(new Claim("scope", StaffApiContract.Scope));
        JwtSecurityToken token = new(issuer: Issuer, audience: StaffApiContract.Audience, claims: claims,
            notBefore: DateTime.UtcNow.AddHours(-1), expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class SnapshotRoleLookup : IStaffRoleLookup
    {
        private readonly Dictionary<string, string[]> _roles = new(StringComparer.Ordinal);
        public bool Unavailable { get; set; }
        public void Reset() { lock (_roles) { _roles.Clear(); Unavailable = false; } }
        public void Set(string subject, params string[] roles) { lock (_roles) _roles[subject] = roles; }
        public Task<StaffRoleLookupResult> GetRolesAsync(string accessToken, string subject, CancellationToken cancellationToken)
        {
            if (Unavailable) throw new HttpRequestException("Identity host unavailable");
            lock (_roles) return Task.FromResult(StaffRoleLookupResult.Found(_roles.GetValueOrDefault(subject, [])));
        }
    }

    private sealed class SnapshotClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 30, 23, 30, 0, TimeSpan.Zero);
    }
}
