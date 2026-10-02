extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Web.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;

namespace FieldSales.Web.Tests;

public sealed class TownCoordinatesEndToEndTests(GeographyApplication app) : IClassFixture<GeographyApplication>
{
    private const string Root = "/directory/geography";
    private const string Page = "/HeadOffice/Geography";
    private const string Header = "Region,County,Town,Latitude,Longitude\n";

    [Fact]
    public async Task Should_SaveAndClearTownCoordinates_When_HeadOfficeUsesTheForm()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var town = await SeedAsync(api);
        string url = Page + "?countyId=" + town.ParentId;
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string html = await HtmlAsync(browser, url);
        Assert.Contains("Town position needed", html);
        using var saved = await PostAsync(browser, url + "&handler=Coordinates", html, Fields(town, "52.923456789", "-6.291234567"));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        html = await HtmlAsync(browser, url);
        Assert.Contains("Town position: 52.9234568, -6.2912346", html);
        var current = (await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!;
        Assert.Equal(town.Id, current.Id); Assert.Equal(town.Name, current.Name); Assert.NotEqual(town.Version, current.Version);
        var choice = Assert.Single((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        Assert.Equal(52.9234568m, choice.Latitude); Assert.Equal(-6.2912346m, choice.Longitude);
        using var clear = await PostAsync(browser, url + "&handler=Coordinates", html, Fields(current, "", ""));
        Assert.Equal(HttpStatusCode.Redirect, clear.StatusCode);
        Assert.Contains("Town position needed", await HtmlAsync(browser, url));
        Assert.Null((await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!.Latitude);
    }

    [Theory]
    [InlineData("52", "", "both latitude and longitude")]
    [InlineData("91", "-6", "latitude between")]
    [InlineData("52", "-181", "longitude between")]
    [InlineData("52,9", "-6", "decimal number")]
    public async Task Should_RetainInvalidManualInput_WithoutChangingTownCoordinates(string latitude, string longitude, string error)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var town = await SeedAsync(api);
        string url = Page + "?countyId=" + town.ParentId;
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string html = await HtmlAsync(browser, url);
        using var rejected = await PostAsync(browser, url + "&handler=Coordinates", html, Fields(town, latitude, longitude));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        html = await rejected.Content.ReadAsStringAsync(); Assert.Contains(error, html);
        string form = Regex.Match(html, $"<li data-reference-id=\"{town.Id}\">[\\s\\S]*?<details class=\"geography-coordinates\"[\\s\\S]*?</details>").Value;
        Assert.NotEmpty(form);
        Assert.Equal(latitude, Field(form, "Latitude"));
        Assert.Equal(longitude, Field(form, "Longitude"));
        var unchanged = (await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!;
        Assert.Null(unchanged.Latitude); Assert.Equal(town.Version, unchanged.Version);
    }

    [Fact]
    public async Task Should_RejectStaleCoordinateForms_IncludingNoOpSubmissions()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var town = await SeedAsync(api);
        using var winner = await api.PutAsJsonAsync(CoordinateUrl(town.Id), new SetTownCoordinatesRequest(0, 0, town.Version));
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        var current = (await winner.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var stale = await api.PutAsJsonAsync(CoordinateUrl(town.Id), new SetTownCoordinatesRequest(0, 0, town.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var noVersion = await api.PutAsJsonAsync(CoordinateUrl(town.Id), new SetTownCoordinatesRequest(53, -6, null));
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        using var noop = await api.PutAsJsonAsync(CoordinateUrl(town.Id), new SetTownCoordinatesRequest(0, 0, current.Version));
        Assert.Equal(HttpStatusCode.OK, noop.StatusCode);
        Assert.Equal(current.Version, (await noop.Content.ReadFromJsonAsync<GeographyItem>())!.Version);
        using var missing = await api.PutAsJsonAsync(CoordinateUrl(Guid.NewGuid()), new SetTownCoordinatesRequest(0, 0, current.Version));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Should_CreateATownWithCoordinates_ThroughTheHeadOfficeForm()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var town = await SeedAsync(api);
        string url = Page + "?countyId=" + town.ParentId;
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string html = await HtmlAsync(browser, url);
        using var created = await PostAsync(browser, url + "&handler=Create", html,
            new() { ["Name"] = "New Town", ["Latitude"] = "90", ["Longitude"] = "180" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var choice = (await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!.Single(item => item.Name == "New Town");
        Assert.Equal(90m, choice.Latitude); Assert.Equal(180m, choice.Longitude);
        using var bad = await api.PostAsJsonAsync(Root + "/towns", new CreateGeographyRequest("Invalid", town.ParentId, 91, 0));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); Assert.Equal((1, 1, 2), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_UpdateCoordinatesAndKeepIds_When_NewOrLegacyCsvIsRepeated()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var town = await SeedAsync(api);
        const string values = "Leinster,Wicklow,Laragh,52.9,-6.3\nLeinster,Wicklow,Other Town,0,0\nLeinster,Wicklow,Laragh,52.9,-6.3";
        using var imported = await ImportAsync(api, Header + values);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(new GeographyImportResult(0, 0, 1, 1, 1), await imported.Content.ReadFromJsonAsync<GeographyImportResult>());
        var saved = (await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!;
        Assert.Equal(town.Name, saved.Name); Assert.Equal(52.9m, saved.Latitude);
        foreach (string csv in new[] { Header + values, "Region,County,Town\nLeinster,Wicklow,Laragh", Header + "Leinster,Wicklow,Laragh,," })
        {
            using var repeated = await ImportAsync(api, csv); Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            Assert.Equal(0, (await repeated.Content.ReadFromJsonAsync<GeographyImportResult>())!.TownCoordinatesUpdated);
            var still = (await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!;
            Assert.Equal(saved with { Usage = null }, still with { Usage = null });
        }
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        var reopened = (await restarted.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!;
        Assert.Equal(saved with { Usage = null }, reopened with { Usage = null });
    }

    [Theory]
    [InlineData("Leinster,Wicklow,Laragh,53,-7\nNew Region,New County,New Town,91,0", "Row 3")]
    [InlineData("Leinster,Wicklow,Laragh,53,-7\nLeinster,Wicklow,Laragh,54,-8", "conflicting coordinates")]
    public async Task Should_RejectTheWholeFile_When_AValidCoordinateUpdateIsFollowedByInvalidInput(string rows, string error)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var town = await SeedAsync(api);
        using var initial = await api.PutAsJsonAsync(CoordinateUrl(town.Id), new SetTownCoordinatesRequest(52, -6, town.Version));
        var before = (await initial.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var rejected = await ImportAsync(api, Header + rows);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode); Assert.Contains(error, await rejected.Content.ReadAsStringAsync());
        var after = (await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!;
        Assert.Equal(before with { Usage = null }, after with { Usage = null });
        Assert.Equal((1, 1, 1), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_KeepArchiveStateAndRollBackCoordinateUpdates_When_AFileAddsBelowArchivedGeography()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var town = await SeedAsync(api);
        var county = (await api.GetFromJsonAsync<GeographyItem>(Root + "/counties/" + town.ParentId))!;
        using var archived = await api.PostAsJsonAsync(Root + "/counties/" + county.Id + "/retire",
            new RetireGeographyRequest(FieldSales.ReferenceData.ReferenceAction.Archive, county.Version));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        using var rejected = await ImportAsync(api, Header + "Leinster,Wicklow,Laragh,52,-6\nLeinster,Wicklow,New Town,53,-7");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var unchanged = (await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!;
        Assert.Null(unchanged.Latitude); Assert.Equal(town.Version, unchanged.Version);
        using var existing = await ImportAsync(api, Header + "Leinster,Wicklow,Laragh,52,-6");
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        var reference = (await api.GetFromJsonAsync<TownChoice>(Root + "/towns/" + town.Id + "/reference"))!;
        Assert.Equal(52m, reference.Latitude); Assert.True(reference.CountyIsArchived); Assert.False(reference.IsSelectable);
        Assert.Empty((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        Assert.Equal((1, 1, 1), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_DenyCoordinateChanges_When_AccessScopeOrAntiforgeryIsMissing()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var town = await SeedAsync(api);
        var request = new SetTownCoordinatesRequest(52, -6, town.Version);
        api.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.PutAsJsonAsync(CoordinateUrl(town.Id), request)).StatusCode);
        foreach (string role in new[] { StaffRoles.FieldSalesperson, StaffRoles.SalesManager, "SysAdmin" })
        {
            app.Roles.SetRoles("niamh", role == "SysAdmin" ? [] : [role]);
            api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token(role));
            Assert.Equal(HttpStatusCode.Forbidden, (await api.PutAsJsonAsync(CoordinateUrl(town.Id), request)).StatusCode);
        }
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: "wrong.scope"));
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PutAsJsonAsync(CoordinateUrl(town.Id), request)).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string url = Page + "?countyId=" + town.ParentId;
        string html = await HtmlAsync(browser, url);
        using var noCsrf = await browser.PostAsync(url + "&handler=Coordinates", new FormUrlEncodedContent(Fields(town, "52", "-6")));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        website.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        using var revoked = await PostAsync(browser, url + "&handler=Coordinates", html, Fields(town, "52", "-6"));
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode); Assert.Contains("AccessChanged", revoked.Headers.Location!.OriginalString);
        Assert.Null((await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + town.Id))!.Latitude);
    }

    [Fact]
    public async Task Should_PreserveExistingRecordsAndRejectPartialCoordinates_When_TownSchemaIsAdded()
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var current = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var connection = new SqlConnectionStringBuilder(current.Database.GetConnectionString()) { InitialCatalog = "TownUpgrade_" + Guid.NewGuid().ToString("N") };
        await using var db = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261002152415_AddLocationPositions");
            Guid region = Guid.NewGuid(), county = Guid.NewGuid(), town = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName,IsArchived) VALUES ({region},N'Leinster',N'LEINSTER',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({county},N'Wicklow',N'WICKLOW',{region},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({town},N'Laragh',N'LARAGH',{county},1)");
            await db.Database.MigrateAsync();
            var saved = await db.Towns.SingleAsync();
            Assert.Equal(town, saved.Id); Assert.Equal(county, saved.CountyId); Assert.True(saved.IsArchived);
            Assert.Null(saved.Latitude); Assert.Null(saved.Longitude); Assert.False(db.Database.HasPendingModelChanges());
            foreach (string invalid in new[] { "partial", "range" })
            {
                var error = await Assert.ThrowsAsync<SqlException>(() => invalid == "partial"
                    ? db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Towns SET Latitude=52 WHERE Id={town}")
                    : db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Towns SET Latitude=52, Longitude=181 WHERE Id={town}"));
                Assert.Equal(547, error.Number);
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static Dictionary<string, string> Fields(GeographyItem town, string latitude, string longitude) => new()
    { ["CoordinatesId"] = town.Id.ToString(), ["Version"] = town.Version, ["Latitude"] = latitude, ["Longitude"] = longitude };
    private static string CoordinateUrl(Guid id) => Root + "/towns/" + id + "/coordinates";
    private static async Task<GeographyItem> SeedAsync(HttpClient api)
    {
        using var imported = await ImportAsync(api, "Region,County,Town\nLeinster,Wicklow,Laragh");
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var choice = Assert.Single((await api.GetFromJsonAsync<TownChoice[]>(Root + "/town-choices"))!);
        return (await api.GetFromJsonAsync<GeographyItem>(Root + "/towns/" + choice.Id))!;
    }
    private static async Task SignInAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(StaffRoles.HeadOfficeUser));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private static string Field(string html, string name) => WebUtility.HtmlDecode(
        Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static async Task<string> HtmlAsync(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string url, string html, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        return browser.PostAsync(url, new FormUrlEncodedContent(fields));
    }
    private static async Task<HttpResponseMessage> ImportAsync(HttpClient api, string csv)
    {
        using var body = new StringContent(csv, Encoding.UTF8, "text/csv"); return await api.PostAsync(Root + "/import", body);
    }
}
