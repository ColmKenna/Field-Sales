extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using GeographyStore = CatalogueApi::FieldSales.Api.Directory.GeographyStore;
using GeographyValidationException = CatalogueApi::FieldSales.Api.Directory.GeographyValidationException;

namespace FieldSales.Web.Tests;

public sealed class GeographyRetirementEndToEndTests(GeographyApplication app) : IClassFixture<GeographyApplication>
{
    private const string Root = "/directory/geography";
    private const string Page = "/HeadOffice/Geography";

    [Fact]
    public async Task Should_OfferArchiveWithFourLocations_When_LaraghIsUsed()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (region, county, town) = await HierarchyAsync(api);
        app.Locations.Add(town.Id, 4);
        var before = app.Locations.Records.ToArray();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string url = Page + "?countyId=" + county.Id;
        string html = await HtmlAsync(browser, url);
        string row = Row(html, town.Id);
        Assert.Contains("Used by 4 locations", row);
        Assert.Contains(">Archive</a>", row);
        Assert.DoesNotContain(">Delete</a>", row);
        string confirm = await HtmlAsync(browser, Link(row, "Archive"));
        Assert.Contains("Used by 4 locations", confirm);
        Assert.Contains("It stays on existing records", confirm);
        Assert.Contains(">Cancel</a>", confirm);
        // Cancel is a read and keeps every reference and the archive state.
        await HtmlAsync(browser, Link(confirm, "Cancel"));
        Assert.False((await ReadAsync(api, "towns", town.Id)).IsArchived);
        using var saved = await ConfirmAsync(browser, confirm);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var archived = await ReadAsync(api, "towns", town.Id);
        Assert.True(archived.IsArchived);
        Assert.Equal("Used by 4 locations", archived.Usage!.Description);
        Assert.Equal(before, app.Locations.Records);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
        Assert.Empty((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        TownChoice reference = (await api.GetFromJsonAsync<TownChoice>($"{Root}/towns/{town.Id}/reference"))!;
        Assert.Equal(town.Id, reference.Id); Assert.Equal(county.Id, reference.CountyId); Assert.Equal(region.Id, reference.RegionId);
        Assert.Equal("Laragh (archived) — Wicklow, Leinster", reference.Label);
        Assert.False(reference.IsSelectable);
        await using var scope = app.Api.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<GeographyStore>();
        Assert.Equal(reference, await store.TownForAssignmentAsync(town.Id, town.Id, default));
        await Assert.ThrowsAsync<GeographyValidationException>(() => store.TownForAssignmentAsync(town.Id, null, default));
        await Assert.ThrowsAsync<GeographyValidationException>(() => store.TownForAssignmentAsync(Guid.NewGuid(), town.Id, default));
        string hidden = await HtmlAsync(browser, url);
        Assert.Contains("Show archived (1)", hidden);
        Assert.DoesNotContain("data-reference-id=\"" + town.Id, hidden);
        string shown = await HtmlAsync(browser, url + "&showArchived=true");
        Assert.Contains("Laragh (archived)", Row(shown, town.Id));
        Assert.Contains(">Un-archive</a>", Row(shown, town.Id));
        string restore = await HtmlAsync(browser, Link(Row(shown, town.Id), "Un-archive"));
        using var restored = await ConfirmAsync(browser, restore);
        Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        Assert.True(Assert.Single((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!).IsSelectable);
        Assert.Equal(before, app.Locations.Records);
    }

    [Theory]
    [InlineData("towns")]
    [InlineData("counties")]
    [InlineData("regions")]
    public async Task Should_OfferOnlyDelete_When_PlaceIsUnused(string level)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var region = await CreateAsync(api, "regions", "Leinster");
        GeographyItem item = region;
        if (level is "counties" or "towns") item = await CreateAsync(api, "counties", "Wicklow", region.Id);
        if (level == "towns") item = await CreateAsync(api, "towns", "Unused", item.Id);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string url = Page + (level == "towns" ? "?countyId=" + item.ParentId : level == "counties" ? "?regionId=" + item.ParentId : "");
        string row = Row(await HtmlAsync(browser, url), item.Id);
        Assert.Contains("Not used yet", row); Assert.Contains(">Delete</a>", row); Assert.DoesNotContain(">Archive</a>", row);
        using var invalid = await RetireAsync(api, level, item, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        string confirm = await HtmlAsync(browser, Link(row, "Delete"));
        await HtmlAsync(browser, Link(confirm, "Cancel"));
        Assert.False((await ReadAsync(api, level, item.Id)).IsArchived);
        using var deleted = await ConfirmAsync(browser, confirm);
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync($"{Root}/{level}/{item.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("counties", "Used by 1 town")]
    [InlineData("regions", "Used by 1 county")]
    public async Task Should_PreserveDescendantsAndFilterChoices_When_ParentIsArchived(string level, string description)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (region, county, town) = await HierarchyAsync(api);
        GeographyItem parent = level == "regions" ? region : county;
        var current = await ReadAsync(api, level, parent.Id);
        Assert.Equal(description, current.Usage!.Description);
        using var delete = await RetireAsync(api, level, current, ReferenceAction.Delete);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string url = Page + (level == "counties" ? "?regionId=" + region.Id : "");
        string row = Row(await HtmlAsync(browser, url), parent.Id);
        Assert.Contains(description, row); Assert.Contains(">Archive</a>", row); Assert.DoesNotContain(">Delete</a>", row);
        using var saved = await ConfirmAsync(browser, await HtmlAsync(browser, Link(row, "Archive")));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.False((await ReadAsync(api, "towns", town.Id)).IsArchived);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
        Assert.Empty((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        TownChoice reference = (await api.GetFromJsonAsync<TownChoice>($"{Root}/towns/{town.Id}/reference"))!;
        Assert.Contains("(archived)", reference.Label);
        string children = await HtmlAsync(browser, Page + "?countyId=" + county.Id);
        Assert.Contains("Un-archive the parent geography", children);
        Assert.DoesNotContain(">Add town</button>", children);
        using var rejectedTown = await api.PostAsJsonAsync(Root + "/towns", new CreateGeographyRequest("New", county.Id));
        Assert.Equal(HttpStatusCode.BadRequest, rejectedTown.StatusCode);
        if (level == "regions")
        {
            using var rejectedCounty = await api.PostAsJsonAsync(Root + "/counties", new CreateGeographyRequest("New", region.Id));
            Assert.Equal(HttpStatusCode.BadRequest, rejectedCounty.StatusCode);
        }
        var archived = await ReadAsync(api, level, parent.Id);
        Assert.Equal(description, archived.Usage!.Description);
        using var restored = await RetireAsync(api, level, archived, ReferenceAction.Unarchive);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Single((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
    }

    [Fact]
    public async Task Should_CountArchivedChildrenAndKeepChoicesHidden_When_OnlyChildIsRestored()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (region, county, town) = await HierarchyAsync(api);
        app.Locations.Add(town.Id, 1);
        using var townArchive = await RetireAsync(api, "towns", town, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.OK, townArchive.StatusCode);
        var countedCounty = await ReadAsync(api, "counties", county.Id);
        Assert.Equal("Used by 1 town", countedCounty.Usage!.Description);
        using var countyArchive = await RetireAsync(api, "counties", countedCounty, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.OK, countyArchive.StatusCode);
        Assert.Equal("Used by 1 county", (await ReadAsync(api, "regions", region.Id)).Usage!.Description);
        using var restoreTown = await RetireAsync(api, "towns", await ReadAsync(api, "towns", town.Id), ReferenceAction.Unarchive);
        Assert.Equal(HttpStatusCode.OK, restoreTown.StatusCode);
        Assert.Empty((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
    }

    [Theory]
    [InlineData("towns")]
    [InlineData("counties")]
    [InlineData("regions")]
    public async Task Should_KeepArchiveAndIdentity_When_ExistingCsvPathIsImportedAgain(string level)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (region, county, town) = await HierarchyAsync(api);
        app.Locations.Add(town.Id, 4);
        var item = level == "towns" ? town : level == "counties" ? county : region;
        using var archived = await RetireAsync(api, level, item, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        using var repeat = await ImportAsync(api, "Region,County,Town\nleinster,wicklow,laragh");
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(new GeographyImportResult(0, 0, 0, 1), await repeat.Content.ReadFromJsonAsync<GeographyImportResult>());
        Assert.True((await ReadAsync(api, level, item.Id)).IsArchived);
        var reference = (await api.GetFromJsonAsync<TownChoice>($"{Root}/towns/{town.Id}/reference"))!;
        Assert.Equal("Laragh", reference.Name); Assert.Equal(county.Id, reference.CountyId); Assert.Equal(region.Id, reference.RegionId);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
        using var duplicate = await api.PostAsJsonAsync(Root + "/" + level, new CreateGeographyRequest(item.Name.ToLowerInvariant(), item.ParentId));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("counties", "Leinster,Wicklow,New town")]
    [InlineData("regions", "Leinster,New county,New town")]
    [InlineData("regions", "Leinster,Wicklow,New town")]
    public async Task Should_RollBackWholeImport_When_LaterRowAddsBelowArchivedParent(string level, string invalidRow)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (region, county, _) = await HierarchyAsync(api);
        using var archived = await RetireAsync(api, level, level == "regions" ? region : county, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await HtmlAsync(browser, Page);
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(Field(html, "__RequestVerificationToken")), "__RequestVerificationToken");
        body.Add(new StringContent("Region,County,Town\nConnacht,Galway,New valid town\n" + invalidRow, Encoding.UTF8, "text/csv"), "Upload", "geography.csv");
        using var rejected = await browser.PostAsync(Page + "?handler=Import", body);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("No changes were saved", await rejected.Content.ReadAsStringAsync());
        Assert.Equal((1, 1, 1), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_RejectStaleOrNowUsedDelete_When_ConfirmationWasOpenedEarlier()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (_, county, town) = await HierarchyAsync(api);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string row = Row(await HtmlAsync(browser, Page + "?countyId=" + county.Id), town.Id);
        string confirm = await HtmlAsync(browser, Link(row, "Delete"));
        app.Locations.Add(town.Id, 1);
        using var nowUsed = await ConfirmAsync(browser, confirm);
        Assert.Equal(HttpStatusCode.OK, nowUsed.StatusCode);
        Assert.Contains("now in use", await nowUsed.Content.ReadAsStringAsync());
        Assert.False((await ReadAsync(api, "towns", town.Id)).IsArchived);
        using var renamed = await api.PutAsJsonAsync($"{Root}/towns/{town.Id}/name", new RenameGeographyRequest("New Laragh", town.Version));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        using var staleArchive = await RetireAsync(api, "towns", town, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.Conflict, staleArchive.StatusCode);
        var fresh = await ReadAsync(api, "towns", town.Id);
        using var invalidAction = await api.PostAsJsonAsync($"{Root}/towns/{town.Id}/retire", new { Action = 99, fresh.Version });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAction.StatusCode);
        using var malformedVersion = await RetireAsync(api, "towns", fresh with { Version = "bad" }, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.BadRequest, malformedVersion.StatusCode);
        using var missing = await RetireAsync(api, "towns", fresh with { Id = Guid.NewGuid() }, ReferenceAction.Archive);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("negative")]
    [InlineData("incomplete")]
    [InlineData("unavailable")]
    public async Task Should_RefuseReadsAndRetirement_When_UsageCannotBeTrusted(string failure)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (_, county, town) = await HierarchyAsync(api);
        app.Locations.Failure = failure;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await api.GetAsync(Root + "/?countyId=" + county.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await api.GetAsync($"{Root}/towns/{town.Id}")).StatusCode);
        using var mutation = await RetireAsync(api, "towns", town, ReferenceAction.Delete);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mutation.StatusCode);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var page = await browser.GetAsync(Page + "?countyId=" + county.Id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
        app.Locations.Failure = null;
    }

    [Fact]
    public async Task Should_DenyRetirement_When_AccessOrAntiforgeryIsMissing()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (_, county, town) = await HierarchyAsync(api);
        api.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await RetireAsync(api, "towns", town, ReferenceAction.Delete);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        foreach (string role in new[] { StaffRoles.FieldSalesperson, StaffRoles.SalesManager, "SysAdmin" })
        {
            app.Roles.SetRoles("niamh", role == "SysAdmin" ? [] : [role]);
            using var denied = await RetireAsync(api, "towns", town, ReferenceAction.Delete);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string url = Page + "?countyId=" + county.Id + "&handler=Retire";
        string html = await HtmlAsync(browser, Page + "?countyId=" + county.Id);
        using var noCsrf = await browser.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Id"] = town.Id.ToString(), ["Action"] = "0", ["Version"] = town.Version }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        website.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        using var revoked = await browser.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Id"] = town.Id.ToString(), ["Action"] = "0", ["Version"] = town.Version,
                ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") }));
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.Contains("AccessChanged", revoked.Headers.Location!.OriginalString);
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_PersistArchiveAndUsage_When_ApplicationRestarts()
    {
        await app.ResetAsync();
        Guid townId;
        using (var api = app.CreateApiClient())
        {
            var (_, _, town) = await HierarchyAsync(api); townId = town.Id;
            app.Locations.Add(town.Id, 4);
            using var archived = await RetireAsync(api, "towns", town, ReferenceAction.Archive);
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        }
        await app.RestartAsync();
        using var restarted = app.CreateApiClient();
        var item = await ReadAsync(restarted, "towns", townId);
        Assert.True(item.IsArchived); Assert.Equal("Used by 4 locations", item.Usage!.Description);
        Assert.Empty((await restarted.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
    }

    [Fact]
    public async Task Should_PreserveExistingHierarchy_When_PredecessorDatabaseIsUpgraded()
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var current = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(current.Database.GetConnectionString())
            { InitialCatalog = "GeographyUpgrade_" + Guid.NewGuid().ToString("N") };
        await using var db = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261002091702_InitialDirectory");
            Guid region = Guid.NewGuid(), county = Guid.NewGuid(), town = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName) VALUES ({region},N'Leinster',N'LEINSTER')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId) VALUES ({county},N'Wicklow',N'WICKLOW',{region})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId) VALUES ({town},N'Laragh',N'LARAGH',{county})");
            await migrator.MigrateAsync();
            Assert.False((await db.Regions.SingleAsync()).IsArchived);
            Assert.Equal(region, (await db.Counties.SingleAsync()).RegionId);
            var upgraded = await db.Towns.SingleAsync();
            Assert.Equal(town, upgraded.Id); Assert.Equal(county, upgraded.CountyId); Assert.Equal("Laragh", upgraded.Name); Assert.False(upgraded.IsArchived);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("archive")]
    public async Task Should_PreserveReferentialIntegrity_When_ChildCreationRacesRetirement(string action)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var region = await CreateAsync(api, "regions", "Leinster");
        if (action == "archive") await CreateAsync(api, "counties", "Existing", region.Id);
        var results = await Task.WhenAll(RetireAsync(api, "regions", region, action == "archive" ? ReferenceAction.Archive : ReferenceAction.Delete),
            api.PostAsJsonAsync(Root + "/counties", new CreateGeographyRequest("Concurrent", region.Id)));
        try
        {
            Assert.All(results, result => Assert.Contains(result.StatusCode,
                new[] { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
            await using var scope = app.Api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            Assert.False(await db.Counties.AnyAsync(county => !db.Regions.Any(region => region.Id == county.RegionId)));
            var savedRegion = await db.Regions.SingleOrDefaultAsync();
            if (savedRegion is null) Assert.Empty(await db.Counties.ToArrayAsync());
            if (savedRegion?.IsArchived == true) Assert.Empty((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    private static async Task<(GeographyItem Region, GeographyItem County, GeographyItem Town)> HierarchyAsync(HttpClient api)
    {
        var region = await CreateAsync(api, "regions", "Leinster");
        var county = await CreateAsync(api, "counties", "Wicklow", region.Id);
        return (region, county, await CreateAsync(api, "towns", "Laragh", county.Id));
    }
    private static async Task<GeographyItem> CreateAsync(HttpClient api, string level, string name, Guid? parent = null)
    {
        using var response = await api.PostAsJsonAsync(Root + "/" + level, new CreateGeographyRequest(name, parent));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
    private static async Task<GeographyItem> ReadAsync(HttpClient api, string level, Guid id) =>
        (await api.GetFromJsonAsync<GeographyItem>($"{Root}/{level}/{id}"))!;
    private static Task<HttpResponseMessage> RetireAsync(HttpClient api, string level, GeographyItem item, ReferenceAction action) =>
        api.PostAsJsonAsync($"{Root}/{level}/{item.Id}/retire", new RetireGeographyRequest(action, item.Version));
    private static async Task<HttpResponseMessage> ImportAsync(HttpClient api, string csv)
    { using var body = new StringContent(csv, Encoding.UTF8, "text/csv"); return await api.PostAsync(Root + "/import", body); }
    private static async Task SignInAsync(HttpClient browser) => Assert.Equal(HttpStatusCode.NoContent,
        (await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(StaffRoles.HeadOfficeUser))).StatusCode);
    private static async Task<string> HtmlAsync(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static string Row(string html, Guid id) => Regex.Match(html,
        "<li data-reference-id=\"" + id + "\">.*?</li>", RegexOptions.Singleline).Value;
    private static string Link(string html, string label) => Regex.Match(html,
        "<a[^>]*href=\"([^\"]+)\"[^>]*>" + Regex.Escape(label) + "</a>").Groups[1].Value;
    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient browser, string html)
    {
        html = Regex.Match(html, "<section class=\"reference-confirmation\".*?</section>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(html);
        string url = Regex.Match(html, "<form[^>]*action=\"([^\"]+)\"").Groups[1].Value;
        return browser.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Id"] = Field(html, "Id"), ["Version"] = Field(html, "Version"), ["Action"] = Field(html, "Action"),
                ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") }));
    }
}
