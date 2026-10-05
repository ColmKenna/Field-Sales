extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.Web.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using ContactTypeUsageSource = CatalogueApi::FieldSales.Api.Directory.ContactTypeUsageSource;

namespace FieldSales.Web.Tests;

public sealed class DirectoryTypeApplication : GeographyApplication
{
    protected override bool UseTestLocationUsage => false;
    public DirectoryTypeTestContacts Contacts { get; } = new();
    public string Registration { get; set; } = "valid";
    protected override void ConfigureAdditionalServices(IServiceCollection services, string connectionString)
    {
        foreach (var descriptor in services.Where(service => service.IsKeyedService
            && Equals(service.ServiceKey, "directory-types") && service.KeyedImplementationType == typeof(ContactTypeUsageSource)).ToArray())
            services.Remove(descriptor);
        if (Registration != "missing") services.AddKeyedSingleton<IReferenceUsageSource>("directory-types", Contacts);
        if (Registration == "duplicate") services.AddKeyedSingleton<IReferenceUsageSource>("directory-types", new DirectoryTypeTestContacts());
        string catalogue = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "DirectoryTypeCatalogueTests" }.ConnectionString;
        services.RemoveAll<CatalogueDbContext>();
        services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(catalogue).Options));
    }
    protected override async Task InitializeAdditionalAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
    }
}

public sealed class DirectoryTypeIntegrationTests(DirectoryTypeApplication app) : IClassFixture<DirectoryTypeApplication>
{
    private const string LocationTypes = "location-types";
    private const string ContactTypes = "contact-types";
    private const string Page = "/HeadOffice/ReferenceData";
    private const string Customers = "/directory/customers";
    private static string Root(string key) => "/directory/reference-data/" + key;
    private static string ListPage(string key) => Page + "?ListKey=" + key;
    private async Task ResetAsync() { app.Contacts.Reset(); await app.ResetAsync(); }

    [Fact]
    public async Task Should_OfferDescribeAndSaveType_OnFirstAdditionalAndEditedLocations()
    {
        await ResetAsync(); using var api = app.CreateApiClient();
        var town = await TownAsync(api);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, ListPage(LocationTypes));
        using var saved = await PostAsync(browser, Page + "?handler=Save", form, new()
        { ["ListKey"] = LocationTypes, ["Name"] = "  Head office — no stock held  ", ["Description"] = "  Administrative office.\nNo stock stored here.  " });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var type = Assert.Single((await api.GetFromJsonAsync<ReferenceListViewModel>(Root(LocationTypes)))!.Items);
        Assert.Equal("Head office — no stock held", type.Name);
        Assert.Equal("Administrative office.\nNo stock stored here.", type.Description);
        string create = await HtmlAsync(browser, "/HeadOffice/Customers/Create");
        Assert.Contains(type.Name, create);
        using var customerSaved = await PostAsync(browser, "/HeadOffice/Customers/Create", create, new()
        { ["CustomerName"] = "Hickey's", ["Name"] = "Office", ["TownId"] = town.Id.ToString(), ["LocationTypeId"] = type.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, customerSaved.StatusCode);
        var customer = Assert.Single((await api.GetFromJsonAsync<CustomerSummary[]>(Customers))!);
        var details = (await api.GetFromJsonAsync<CustomerDetails>(Customers + "/" + customer.Id))!;
        var first = Assert.Single(details.Locations); Assert.Equal(type.Id, first.Type!.Id);
        Assert.Contains(type.Name, await HtmlAsync(browser, customerSaved.Headers.Location!.OriginalString));
        string additional = "/HeadOffice/Locations/Create/" + customer.Id;
        using var added = await PostAsync(browser, additional, await HtmlAsync(browser, additional), new()
        { ["Name"] = "Branch", ["TownId"] = town.Id.ToString(), ["LocationTypeId"] = type.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        string edit = added.Headers.Location!.OriginalString;
        string editForm = await HtmlAsync(browser, edit); Assert.Contains(type.Name, editForm);
        using var cleared = await PostAsync(browser, edit, editForm, new()
        { ["Name"] = "Branch", ["TownId"] = town.Id.ToString(), ["Version"] = Field(editForm, "Version"), ["LocationTypeId"] = "" });
        Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        details = (await api.GetFromJsonAsync<CustomerDetails>(Customers + "/" + customer.Id))!;
        Assert.Null(Assert.Single(details.Locations, location => location.Name == "Branch").Type);
        Assert.Equal("Used by 1 location", (await ItemAsync(api, LocationTypes, type.Id)).Usage.Description);
    }

    [Theory]
    [InlineData(LocationTypes)] [InlineData(ContactTypes)]
    public async Task Should_TrimEditAndPersistNameAndOptionalDescription(string key)
    {
        await ResetAsync(); using var api = app.CreateApiClient();
        var type = await CreateTypeAsync(api, key, "  Buyer  ", "  Explained\nsecond line  ");
        Assert.Equal("Buyer", type.Name); Assert.Equal("Explained\nsecond line", type.Description);
        using var updated = await api.PutAsJsonAsync(Root(key) + "/" + type.Id,
            new SaveDirectoryTypeRequest("  Purchaser  ", " ", type.Version));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var current = await ItemAsync(api, key, type.Id); Assert.Null(current.Description); Assert.Equal("Purchaser", current.Name);
        Assert.NotEqual(type.Version, current.Version);
        using var boundary = await api.PutAsJsonAsync(Root(key) + "/" + type.Id,
            new SaveDirectoryTypeRequest(new string('N', 200), new string('D', 2000), current.Version));
        Assert.Equal(HttpStatusCode.OK, boundary.StatusCode);
        var before = await ItemAsync(api, key, type.Id);
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        var after = await ItemAsync(restarted, key, type.Id);
        Assert.Equal(before.Id, after.Id); Assert.Equal(before.Name, after.Name); Assert.Equal(before.Description, after.Description); Assert.Equal(before.Version, after.Version);
    }

    [Theory]
    [InlineData(LocationTypes, "blank")] [InlineData(ContactTypes, "blank")]
    [InlineData(LocationTypes, "name-long")] [InlineData(ContactTypes, "name-long")]
    [InlineData(LocationTypes, "name-control")] [InlineData(ContactTypes, "name-control")]
    [InlineData(LocationTypes, "description-long")] [InlineData(ContactTypes, "description-long")]
    [InlineData(LocationTypes, "description-control")] [InlineData(ContactTypes, "description-control")]
    public async Task Should_RejectInvalidTypeWithoutWriting(string key, string scenario)
    {
        await ResetAsync(); using var api = app.CreateApiClient();
        string name = scenario switch { "blank" => " ", "name-long" => new string('N', 201), "name-control" => "Bad\nname", _ => "Buyer" };
        string description = scenario switch { "description-long" => new string('D', 2001), "description-control" => "Bad\0description", _ => "Description" };
        using var response = await api.PostAsJsonAsync(Root(key), new SaveDirectoryTypeRequest(name, description));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await api.GetFromJsonAsync<ReferenceListViewModel>(Root(key)))!.Items);
    }

    [Fact]
    public async Task Should_KeepArchivedNamesUniqueWithinEachListAndRetainRejectedFormInput()
    {
        await ResetAsync(); using var api = app.CreateApiClient();
        var buyer = await CreateTypeAsync(api, ContactTypes, "Buyer"); app.Contacts.Add(buyer.Id, 1);
        Assert.Equal(HttpStatusCode.OK, (await RetireAsync(api, ContactTypes, buyer, ReferenceAction.Archive)).StatusCode);
        using var duplicate = await api.PostAsJsonAsync(Root(ContactTypes), new SaveDirectoryTypeRequest(" buyer "));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await CreateTypeAsync(api, LocationTypes, "Buyer"); // Names are independent between the two lists.
        var other = await CreateTypeAsync(api, ContactTypes, "Other", "Original");
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, ListPage(ContactTypes));
        using var rejected = await PostAsync(browser, Page + "?handler=Save", form, new()
        { ["ListKey"] = ContactTypes, ["Id"] = other.Id.ToString(), ["Name"] = "Buyer", ["Description"] = "My retained description", ["Version"] = other.Version! });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("My retained description", html); Assert.Contains("already exists", html);
        Assert.Equal("Other", (await ItemAsync(api, ContactTypes, other.Id)).Name);
    }

    [Fact]
    public async Task Should_ArchiveBuyerWithFourteenDistinctContactReferencesAndKeepTheirLabels()
    {
        await ResetAsync(); using var api = app.CreateApiClient();
        var buyer = await CreateTypeAsync(api, ContactTypes, "Buyer"); app.Contacts.Add(buyer.Id, 14);
        var ids = app.Contacts.Records.Select(record => record.Id).ToArray();
        Assert.Equal(14, ids.Distinct().Count()); Assert.Contains(app.Contacts.Records, record => record.Inactive);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string list = await HtmlAsync(browser, ListPage(ContactTypes));
        string row = Row(list, buyer.Id); Assert.Contains("Used by 14 contacts", row);
        Assert.Contains(">Archive<", row); Assert.DoesNotContain(">Delete<", row);
        string confirmation = await HtmlAsync(browser, ListPage(ContactTypes) + "&handler=Confirm&id=" + buyer.Id);
        Assert.Contains("14 contacts", confirmation); Assert.Contains("Cancel", confirmation);
        await HtmlAsync(browser, ListPage(ContactTypes)); Assert.False((await ItemAsync(api, ContactTypes, buyer.Id)).IsArchived);
        using var archived = await ConfirmAsync(browser, confirmation, ContactTypes, buyer.Id);
        Assert.Equal(HttpStatusCode.Redirect, archived.StatusCode);
        Assert.Empty((await api.GetFromJsonAsync<ReferenceListViewModel>(Root(ContactTypes)))!.Items);
        var stored = await ItemAsync(api, ContactTypes, buyer.Id); Assert.True(stored.IsArchived); Assert.Equal("Used by 14 contacts", stored.Usage.Description);
        foreach (var record in app.Contacts.Records)
        {
            var link = (await api.GetFromJsonAsync<DirectoryTypeChoice>(Root(ContactTypes) + "/" + record.TypeId + "/reference"))!;
            Assert.Equal(buyer.Id, link.Id); Assert.Equal("Buyer (archived)", link.Label);
        }
        Assert.Equal(ids, app.Contacts.Records.Select(record => record.Id));
        Assert.Contains("Buyer (archived)", await HtmlAsync(browser, ListPage(ContactTypes) + "&ShowArchived=true"));
    }

    [Theory]
    [InlineData(LocationTypes)] [InlineData(ContactTypes)]
    public async Task Should_DeleteUnusedTypeOnlyAfterPlainConfirmation(string key)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var type = await CreateTypeAsync(api, key, "Unused");
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string list = await HtmlAsync(browser, ListPage(key)); Assert.Contains(">Delete<", Row(list, type.Id));
        string confirmation = await HtmlAsync(browser, ListPage(key) + "&handler=Confirm&id=" + type.Id);
        Assert.Contains("Delete", confirmation); Assert.Contains("Unused", confirmation); Assert.Contains("Cancel", confirmation);
        await HtmlAsync(browser, ListPage(key)); Assert.Equal(type.Id, (await ItemAsync(api, key, type.Id)).Id);
        using var deleted = await ConfirmAsync(browser, confirmation, key, type.Id); Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync(Root(key) + "/" + type.Id)).StatusCode);
    }

    [Fact]
    public async Task Should_KeepExistingArchivedTypeButRejectNewAssignmentsAndRestoreChoicesOnUnarchive()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        var type = await CreateTypeAsync(api, LocationTypes, "Office");
        var customer = await CustomerAsync(api, town.Id, type.Id); var location = Assert.Single(customer.Locations);
        Assert.Equal("Used by 1 location", (await ItemAsync(api, LocationTypes, type.Id)).Usage.Description);
        Assert.Equal(HttpStatusCode.Conflict, (await RetireAsync(api, LocationTypes, type, ReferenceAction.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RetireAsync(api, LocationTypes, type, ReferenceAction.Archive)).StatusCode);
        Assert.Empty((await api.GetFromJsonAsync<DirectoryTypeChoice[]>(Root(LocationTypes) + "/choices"))!);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string page = "/HeadOffice/Locations/Detail/" + location.Id;
        string form = await HtmlAsync(browser, page); Assert.Contains("Office (archived)", form);
        Assert.DoesNotContain("value=\"" + type.Id + "\"", await HtmlAsync(browser, "/HeadOffice/Locations/Create/" + customer.Id));
        var detail = (await api.GetFromJsonAsync<LocationDetails>("/directory/locations/" + location.Id))!;
        using var edit = await api.PutAsJsonAsync("/directory/locations/" + location.Id,
            new EditLocationRequest("Renamed", town.Id, null, detail.Version, LocationTypeId: type.Id));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("Office (archived)", (await api.GetFromJsonAsync<LocationDetails>("/directory/locations/" + location.Id))!.Type!.Label);
        using var newAssignment = await api.PostAsJsonAsync(Customers + "/" + customer.Id + "/locations", new CreateLocationRequest("New", town.Id, LocationTypeId: type.Id));
        Assert.Equal(HttpStatusCode.BadRequest, newAssignment.StatusCode);
        detail = (await api.GetFromJsonAsync<LocationDetails>("/directory/locations/" + location.Id))!;
        using var cleared = await api.PutAsJsonAsync("/directory/locations/" + location.Id,
            new EditLocationRequest(detail.Name, town.Id, detail.Eircode, detail.Version));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null((await api.GetFromJsonAsync<LocationDetails>("/directory/locations/" + location.Id))!.Type);
        var archived = await ItemAsync(api, LocationTypes, type.Id);
        Assert.Equal(HttpStatusCode.OK, (await RetireAsync(api, LocationTypes, archived, ReferenceAction.Unarchive)).StatusCode);
        Assert.Equal(type.Id, Assert.Single((await api.GetFromJsonAsync<DirectoryTypeChoice[]>(Root(LocationTypes) + "/choices"))!).Id);
    }

    [Fact]
    public async Task Should_OfferAllSevenListsFromEverySwitcherWithoutChangingCatalogueResponses()
    {
        await ResetAsync();
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        foreach (var definition in ReferenceListKeys.SwitcherLists)
        {
            string html = await HtmlAsync(browser, ListPage(definition.Key));
            foreach (var choice in ReferenceListKeys.SwitcherLists) Assert.Contains(choice.PluralLabel, html);
        }
        using var api = app.CreateApiClient();
        string catalogue = await api.GetStringAsync("/catalogue/reference-data/brands");
        Assert.DoesNotContain("location-types", catalogue); Assert.DoesNotContain("contact-types", catalogue);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync(Root("brands"))).StatusCode);
    }

    [Fact]
    public async Task Should_RejectMissingTypesAndStaleVersionsIncludingNoOpEdits()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var type = await CreateTypeAsync(api, LocationTypes, "Office");
        using var changed = await api.PutAsJsonAsync(Root(LocationTypes) + "/" + type.Id, new SaveDirectoryTypeRequest("Office", null, type.Version));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode); Assert.NotEqual(type.Version, (await ItemAsync(api, LocationTypes, type.Id)).Version);
        foreach (string? version in new[] { type.Version, null, "bad", Convert.ToBase64String(new byte[7]) })
        {
            var expected = version == type.Version ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest;
            using var stale = await api.PutAsJsonAsync(Root(LocationTypes) + "/" + type.Id, new SaveDirectoryTypeRequest("Overwritten", null, version));
            Assert.Equal(expected, stale.StatusCode);
            Assert.Equal(expected, (await api.PostAsJsonAsync(Root(LocationTypes) + "/" + type.Id + "/retire", new RetireDirectoryTypeRequest(ReferenceAction.Delete, version))).StatusCode);
        }
        Assert.Equal("Office", (await ItemAsync(api, LocationTypes, type.Id)).Name);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync(Root(LocationTypes) + "/" + Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PutAsJsonAsync(Root(LocationTypes) + "/" + Guid.NewGuid(), new SaveDirectoryTypeRequest("Missing"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsJsonAsync(Root("unknown"), new SaveDirectoryTypeRequest("Missing"))).StatusCode);
        var current = await ItemAsync(api, LocationTypes, type.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await RetireAsync(api, LocationTypes, current, (ReferenceAction)123)).StatusCode);
    }

    [Fact]
    public async Task Should_RecheckUsageAfterDeleteConfirmationAndRejectInvalidFirstTypeAtomically()
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        var type = await CreateTypeAsync(api, LocationTypes, "Office");
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string confirmation = await HtmlAsync(browser, ListPage(LocationTypes) + "&handler=Confirm&id=" + type.Id);
        await CustomerAsync(api, town.Id, type.Id);
        using var rejected = await ConfirmAsync(browser, confirmation, LocationTypes, type.Id);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode); Assert.Contains("now in use", await rejected.Content.ReadAsStringAsync());
        Assert.False((await ItemAsync(api, LocationTypes, type.Id)).IsArchived);
        using var invalid = await api.PostAsJsonAsync(Customers, new CreateCustomerRequest("Must not save", new("Location", town.Id, LocationTypeId: Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var error = (await invalid.Content.ReadFromJsonAsync<CustomerDirectoryError>())!; Assert.Equal("FirstLocation.LocationTypeId", error.Field);
        Assert.Single((await api.GetFromJsonAsync<CustomerSummary[]>(Customers))!);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(1, await db.Locations.CountAsync());
        var foreignKey = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM [LocationTypes]"));
        Assert.Equal(547, foreignKey.Number);
    }

    [Theory]
    [InlineData("unavailable")] [InlineData("wrong-key")] [InlineData("negative")] [InlineData("incomplete")]
    public async Task Should_FailClosedWithoutRetiring_When_ContactUsageCannotBeTrusted(string mode)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var type = await CreateTypeAsync(api, ContactTypes, "Buyer");
        app.Contacts.Mode = mode;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await api.GetAsync(Root(ContactTypes))).StatusCode);
        if (mode != "incomplete") Assert.Equal(HttpStatusCode.ServiceUnavailable, (await RetireAsync(api, ContactTypes, type, ReferenceAction.Delete)).StatusCode);
        app.Contacts.Mode = "valid"; Assert.False((await ItemAsync(api, ContactTypes, type.Id)).IsArchived);
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")]
    public async Task Should_FailClosed_When_RequiredContactUsageRegistrationIsInvalid(string registration)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var type = await CreateTypeAsync(api, ContactTypes, "Buyer");
        try
        {
            app.Registration = registration; await app.RestartAsync(); using var invalid = app.CreateApiClient();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await invalid.GetAsync(Root(ContactTypes))).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await RetireAsync(invalid, ContactTypes, type, ReferenceAction.Delete)).StatusCode);
        }
        finally { app.Registration = "valid"; await app.RestartAsync(); }
        using var restored = app.CreateApiClient(); Assert.Equal(type.Id, (await ItemAsync(restored, ContactTypes, type.Id)).Id);
    }

    [Fact]
    public async Task Should_DenyTypeChangesWithoutCurrentHeadOfficeAccessScopeAndCsrf()
    {
        await ResetAsync(); using var api = app.CreateApiClient();
        api.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync(Root(LocationTypes))).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: "other.api"));
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(Root(LocationTypes), new SaveDirectoryTypeRequest("Denied"))).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, ListPage(LocationTypes));
        using var noCsrf = await browser.PostAsync(Page + "?handler=Save", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["ListKey"] = LocationTypes, ["Name"] = "Denied" }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        website.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        using var revoked = await PostAsync(browser, Page + "?handler=Save", form, new() { ["ListKey"] = LocationTypes, ["Name"] = "Denied" });
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode); Assert.Contains("AccessChanged", revoked.Headers.Location!.OriginalString);
        app.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(Root(ContactTypes), new SaveDirectoryTypeRequest("Denied"))).StatusCode);
    }

    [Theory]
    [InlineData(ReferenceAction.Delete)] [InlineData(ReferenceAction.Archive)]
    public async Task Should_PreserveReferences_When_TypeAssignmentRacesRetirement(ReferenceAction action)
    {
        await ResetAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        var type = await CreateTypeAsync(api, LocationTypes, "Office");
        if (action == ReferenceAction.Archive) await CustomerAsync(api, town.Id, type.Id);
        var responses = await Task.WhenAll(RetireAsync(api, LocationTypes, type, action),
            api.PostAsJsonAsync(Customers, new CreateCustomerRequest("Concurrent", new("Concurrent location", town.Id, LocationTypeId: type.Id))));
        try
        {
            Assert.All(responses, response => Assert.Contains(response.StatusCode,
                new[] { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
            await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            Assert.False(await db.Locations.AnyAsync(location => location.LocationTypeId != null
                && !db.LocationTypes.Any(type => type.Id == location.LocationTypeId)));
            Assert.False(await db.Customers.AnyAsync(customer => !customer.Locations.Any()));
            if (action == ReferenceAction.Delete && responses[0].StatusCode == HttpStatusCode.OK)
            { Assert.NotEqual(HttpStatusCode.Created, responses[1].StatusCode); Assert.Empty(await db.Locations.ToArrayAsync()); }
            if (responses[1].StatusCode == HttpStatusCode.Created)
                Assert.Equal(action == ReferenceAction.Archive ? 2 : 1, await db.Locations.CountAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Should_PreserveUntypedCustomerLocationAndGeography_When_PredecessorDatabaseIsUpgraded()
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var current = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var connection = new SqlConnectionStringBuilder(current.Database.GetConnectionString()) { InitialCatalog = "TypeUpgrade_" + Guid.NewGuid().ToString("N") };
        await using var db = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            string previous = (await current.Database.GetAppliedMigrationsAsync()).Last(migration => !migration.EndsWith("AddDirectoryTypes", StringComparison.Ordinal));
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(previous);
            Guid region = Guid.NewGuid(), county = Guid.NewGuid(), town = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName,IsArchived) VALUES ({region},N'Leinster',N'LEINSTER',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({county},N'Wicklow',N'WICKLOW',{region},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({town},N'Laragh',N'LARAGH',{county},1)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Customers (Id,Name) VALUES ({customer},N'Customer')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Locations (Id,CustomerId,Name,NormalizedName,TownId,Eircode) VALUES ({location},{customer},N'Location',N'LOCATION',{town},N'A67 X123')");
            await migrator.MigrateAsync();
            Assert.Equal(customer, (await db.Customers.SingleAsync()).Id);
            var saved = await db.Locations.SingleAsync(); Assert.Equal(location, saved.Id); Assert.Equal(customer, saved.CustomerId);
            Assert.Equal(town, saved.TownId); Assert.Null(saved.LocationTypeId); Assert.Equal("A67 X123", saved.Eircode);
            Assert.True((await db.Towns.SingleAsync()).IsArchived); Assert.Equal(region, (await db.Counties.SingleAsync()).RegionId);
            Assert.Empty(await db.LocationTypes.ToArrayAsync()); Assert.Empty(await db.ContactTypes.ToArrayAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task<ReferenceListItem> CreateTypeAsync(HttpClient api, string key, string name, string? description = null)
    {
        using var response = await api.PostAsJsonAsync(Root(key), new SaveDirectoryTypeRequest(name, description));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var type = (await response.Content.ReadFromJsonAsync<DirectoryTypeChoice>())!;
        return await ItemAsync(api, key, type.Id);
    }
    private static async Task<ReferenceListItem> ItemAsync(HttpClient api, string key, Guid id) =>
        (await api.GetFromJsonAsync<ReferenceListItem>(Root(key) + "/" + id))!;
    private static Task<HttpResponseMessage> RetireAsync(HttpClient api, string key, ReferenceListItem type, ReferenceAction action) =>
        api.PostAsJsonAsync(Root(key) + "/" + type.Id + "/retire", new RetireDirectoryTypeRequest(action, type.Version));
    private static async Task<TownChoice> TownAsync(HttpClient api)
    {
        using var region = await api.PostAsJsonAsync("/directory/geography/regions", new CreateGeographyRequest("Leinster"));
        var r = (await region.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var county = await api.PostAsJsonAsync("/directory/geography/counties", new CreateGeographyRequest("Wicklow", r.Id));
        var c = (await county.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var town = await api.PostAsJsonAsync("/directory/geography/towns", new CreateGeographyRequest("Rathdrum", c.Id));
        Assert.Equal(HttpStatusCode.Created, town.StatusCode);
        return Assert.Single((await api.GetFromJsonAsync<TownChoice[]>("/directory/geography/town-choices"))!);
    }
    private static async Task<CustomerDetails> CustomerAsync(HttpClient api, Guid town, Guid? type = null)
    {
        using var response = await api.PostAsJsonAsync(Customers, new CreateCustomerRequest("Customer", new("Location", town, LocationTypeId: type)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CustomerDetails>())!;
    }
    private static async Task SignInAsync(HttpClient browser) => Assert.Equal(HttpStatusCode.NoContent,
        (await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(StaffRoles.HeadOfficeUser))).StatusCode);
    private static async Task<string> HtmlAsync(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static string Row(string html, Guid id) => Regex.Match(html,
        "<section class=\"reference-item\" data-reference-id=\"" + id + "\">.*?</section>", RegexOptions.Singleline).Value;
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string url, string html, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken");
        return browser.PostAsync(url, new FormUrlEncodedContent(fields));
    }
    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient browser, string html, string key, Guid id) =>
        PostAsync(browser, Page + "?handler=Retire", html, new()
        { ["ListKey"] = key, ["Id"] = id.ToString(), ["Action"] = Field(html, "Action"), ["Version"] = Field(html, "Version") });
}
