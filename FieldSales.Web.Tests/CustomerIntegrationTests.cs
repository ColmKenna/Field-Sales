extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
using FieldSales.Web.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;

namespace FieldSales.Web.Tests;

public sealed class CustomerApplication : GeographyApplication
{
    protected override bool UseTestLocationUsage => false;
}

public sealed class CustomerIntegrationTests(CustomerApplication app) : IClassFixture<CustomerApplication>
{
    private const string Customers = "/directory/customers";
    private const string Locations = "/directory/locations";
    private const string CreatePage = "/HeadOffice/Customers/Create";
    private const string DuplicateMessage = "This customer already has a location with that name";

    [Fact]
    public async Task Should_SaveCustomerAndFirstLocationAndListThem_When_MinimumRecordIsEntered()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        Assert.Contains("Manage customers and locations", await HtmlAsync(browser, "/HeadOffice"));
        Assert.Contains("No customers yet", await HtmlAsync(browser, "/HeadOffice/Customers"));
        string form = await HtmlAsync(browser, CreatePage);
        Assert.Contains("Rathdrum — Wicklow, Leinster", form);
        Assert.DoesNotContain("access_token", form);
        using var saved = await PostAsync(browser, CreatePage, form, new()
        {
            ["CustomerName"] = "  Hickey's Pharmacies  ", ["Name"] = "  Hickey's Rathdrum  ",
            ["TownId"] = town.Id.ToString(), ["Eircode"] = " A67 X123 "
        });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string record = await HtmlAsync(browser, saved.Headers.Location!.OriginalString);
        Assert.Contains("Hickey's Pharmacies", record);
        Assert.Contains("Hickey's Rathdrum", record);
        Assert.Contains("<th scope=\"col\">Type</th>", record);
        Assert.Contains("<th scope=\"col\">Master</th>", record);
        Assert.Contains("<th scope=\"col\">Closure</th>", record);
        Assert.Contains("<td>Rathdrum — Wicklow, Leinster</td><td></td><td></td><td></td>", record);
        CustomerSummary summary = Assert.Single((await api.GetFromJsonAsync<CustomerSummary[]>(Customers))!);
        Assert.Equal("Hickey's Pharmacies", summary.Name); Assert.Equal(1, summary.LocationCount);
        var customer = (await api.GetFromJsonAsync<CustomerDetails>(Customers + "/" + summary.Id))!;
        var first = Assert.Single(customer.Locations);
        Assert.Equal("Hickey's Rathdrum", first.Name); Assert.Equal("A67 X123", first.Eircode); Assert.Equal(town.Id, first.Town.Id);
        var location = await LocationAsync(api, first.Id);
        Assert.Equal(customer.Id, location.CustomerId); Assert.Equal(customer.Name, location.CustomerName);
        Assert.Equal(8, Convert.FromBase64String(location.Version).Length);
        string detail = await HtmlAsync(browser, "/HeadOffice/Locations/Detail/" + first.Id);
        Assert.Equal("Hickey's Rathdrum", Field(detail, "Name")); Assert.Equal("A67 X123", Field(detail, "Eircode"));
        Assert.DoesNotContain("Coordinates", detail);
        Assert.DoesNotContain("name=\"CustomerId\"", detail);
        Assert.Contains("Hickey's Pharmacies", await HtmlAsync(browser, "/HeadOffice/Customers"));
        Assert.Equal((1, 1), await CountsAsync());
    }

    [Theory]
    [InlineData("missing-first", "FirstLocation", "Add the first location")]
    [InlineData("customer-name", "Name", "Enter a customer name")]
    [InlineData("long-customer-name", "Name", "Enter a customer name")]
    [InlineData("location-name", "FirstLocation.Name", "Enter a location name")]
    [InlineData("long-location-name", "FirstLocation.Name", "Enter a location name")]
    [InlineData("control-name", "FirstLocation.Name", "Enter a location name")]
    [InlineData("missing-town", "FirstLocation.TownId", "Choose a town")]
    [InlineData("empty-town", "FirstLocation.TownId", "Choose a town")]
    [InlineData("unknown-town", "FirstLocation.TownId", "Choose a town")]
    [InlineData("long-eircode", "FirstLocation.Eircode", "Enter an Eircode")]
    [InlineData("control-eircode", "FirstLocation.Eircode", "Enter an Eircode")]
    public async Task Should_SaveNeitherRecord_When_FirstLocationOrCustomerIsInvalid(string scenario, string field, string message)
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api);
        CreateCustomerRequest request = new("Customer", new("Location", town.Id));
        request = scenario switch
        {
            "missing-first" => request with { FirstLocation = null },
            "customer-name" => request with { Name = " " },
            "long-customer-name" => request with { Name = new string('x', 201) },
            "location-name" => request with { FirstLocation = request.FirstLocation! with { Name = " " } },
            "long-location-name" => request with { FirstLocation = request.FirstLocation! with { Name = new string('x', 201) } },
            "control-name" => request with { FirstLocation = request.FirstLocation! with { Name = "Name\nSecond" } },
            "missing-town" => request with { FirstLocation = request.FirstLocation! with { TownId = null } },
            "empty-town" => request with { FirstLocation = request.FirstLocation! with { TownId = Guid.Empty } },
            "unknown-town" => request with { FirstLocation = request.FirstLocation! with { TownId = Guid.NewGuid() } },
            "long-eircode" => request with { FirstLocation = request.FirstLocation! with { Eircode = new string('x', 21) } },
            _ => request with { FirstLocation = request.FirstLocation! with { Eircode = "A67\nX123" } }
        };
        using var response = await api.PostAsJsonAsync(Customers, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<CustomerDirectoryError>())!;
        Assert.Equal(field, error.Field); Assert.Contains(message, error.Error); Assert.False(error.RequiresDuplicateConfirmation);
        Assert.Equal((0, 0), await CountsAsync()); Assert.Equal((1, 1, 1), await app.CountsAsync());
    }

    [Fact]
    public async Task Should_RetainInputAndOfferGeography_When_CustomerFormCannotBeSaved()
    {
        await app.ResetAsync();
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await HtmlAsync(browser, CreatePage);
        Assert.Contains("Add a town before saving", form); Assert.Contains("Manage geography", form);
        using var rejected = await PostAsync(browser, CreatePage, form, new()
            { ["CustomerName"] = "Hickey's Pharmacies", ["Name"] = "Hickey's Rathdrum", ["Eircode"] = "A67 X123" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("Choose a town", html);
        Assert.Equal("Hickey's Pharmacies", Field(html, "CustomerName")); Assert.Equal("Hickey's Rathdrum", Field(html, "Name"));
        Assert.Equal("A67 X123", Field(html, "Eircode")); Assert.Equal((0, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_WarnThenAllowCancelRenameOrSaveAnyway_When_LocationNameMatchesWithinCustomer()
    {
        await app.ResetAsync();
        using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api);
        var customer = await CreateAsync(api, town.Id);
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string page = "/HeadOffice/Locations/Create/" + customer.Id;
        string form = await HtmlAsync(browser, page);
        Dictionary<string, string> fields = new() { ["Name"] = "  hickey's rathdrum  ", ["TownId"] = town.Id.ToString(), ["Eircode"] = "A67 X124" };
        using var warning = await PostAsync(browser, page, form, fields);
        Assert.Equal(HttpStatusCode.OK, warning.StatusCode);
        string html = await warning.Content.ReadAsStringAsync();
        Assert.Contains(DuplicateMessage, WebUtility.HtmlDecode(html)); Assert.Contains("Save anyway", html);
        Assert.Equal(fields["Name"], Field(html, "Name")); Assert.Equal("A67 X124", Field(html, "Eircode"));
        Assert.Equal((1, 1), await CountsAsync());
        await HtmlAsync(browser, "/HeadOffice/Customers/Detail/" + customer.Id); // Cancel changes no data.
        Assert.Equal((1, 1), await CountsAsync());
        using var confirmed = await PostAsync(browser, page + "?handler=Confirm", html, fields);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode); Assert.Equal((1, 2), await CountsAsync());
        fields["Name"] = "Hickey's New Location";
        using var renamed = await PostAsync(browser, page, html, fields);
        Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode); Assert.Equal((1, 3), await CountsAsync());
    }

    [Fact]
    public async Task Should_RecheckDuplicateOnRenameAndPreserveOwnership_When_LocationIsEdited()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api);
        var customer = await CreateAsync(api, town.Id);
        var otherCustomer = await CreateAsync(api, town.Id); // Customer names may repeat; Location names under another Customer are allowed.
        var second = await AddAsync(api, customer.Id, town.Id, "Other location");
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string page = "/HeadOffice/Locations/Detail/" + second.Id;
        string form = await HtmlAsync(browser, page);
        Dictionary<string, string> fields = new() { ["Name"] = "Hickey's Rathdrum", ["TownId"] = town.Id.ToString(),
            ["Eircode"] = "A67 X123", ["Version"] = Field(form, "Version"), ["CustomerId"] = otherCustomer.Id.ToString() };
        using var warning = await PostAsync(browser, page, form, fields);
        Assert.Equal(HttpStatusCode.OK, warning.StatusCode);
        string html = await warning.Content.ReadAsStringAsync(); Assert.Contains(DuplicateMessage, html);
        Assert.Equal("Other location", (await LocationAsync(api, second.Id)).Name);
        await HtmlAsync(browser, page); // Cancel/reload does not rename.
        Assert.Equal("Other location", (await LocationAsync(api, second.Id)).Name);
        using var confirmed = await PostAsync(browser, page + "?handler=Confirm", html, fields);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        var saved = await LocationAsync(api, second.Id);
        Assert.Equal(customer.Id, saved.CustomerId); Assert.Equal("Hickey's Rathdrum", saved.Name);
        Assert.Equal("A67 X123", saved.Eircode); Assert.NotEqual(second.Version, saved.Version);
        using var attemptedMove = await api.PutAsJsonAsync(Locations + "/" + second.Id,
            new { saved.Name, TownId = town.Id, Eircode = "", saved.Version, CustomerId = otherCustomer.Id, ConfirmDuplicateName = true });
        Assert.Equal(HttpStatusCode.OK, attemptedMove.StatusCode);
        Assert.Equal(customer.Id, (await LocationAsync(api, second.Id)).CustomerId);
        Assert.Null((await LocationAsync(api, second.Id)).Eircode);
        Assert.Single((await api.GetFromJsonAsync<CustomerDetails>(Customers + "/" + otherCustomer.Id))!.Locations);
    }

    [Fact]
    public async Task Should_ExcludeEditedLocationFromDuplicateCheck_When_NameStaysTheSame()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api); var customer = await CreateAsync(api, town.Id);
        var first = await LocationAsync(api, customer.Locations[0].Id);
        using var response = await EditAsync(api, first, first.Name, first.Town.Id, "A67 X999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("A67 X999", (await LocationAsync(api, first.Id)).Eircode);
        using var duplicate = await api.PostAsJsonAsync(Customers + "/" + customer.Id + "/locations", new CreateLocationRequest(" HICKEY'S RATHDRUM ", town.Id));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var error = (await duplicate.Content.ReadFromJsonAsync<CustomerDirectoryError>())!;
        Assert.True(error.RequiresDuplicateConfirmation); Assert.Equal(DuplicateMessage, error.Error);
        Assert.Equal((1, 1), await CountsAsync());
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("unchanged")]
    [InlineData("duplicate-confirmation")]
    public async Task Should_RejectStaleEditsWithoutChangingData_When_AnotherSaveOccurred(string scenario)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api); var customer = await CreateAsync(api, town.Id);
        var first = await LocationAsync(api, customer.Locations[0].Id);
        using var updated = await EditAsync(api, first, "New name", town.Id, "Updated"); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var current = await LocationAsync(api, first.Id);
        using var stale = await api.PutAsJsonAsync(Locations + "/" + first.Id,
            new EditLocationRequest(scenario == "unchanged" ? current.Name : first.Name, town.Id,
                scenario == "unchanged" ? current.Eircode : first.Eircode, first.Version, scenario == "duplicate-confirmation"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.False((await stale.Content.ReadFromJsonAsync<CustomerDirectoryError>())!.RequiresDuplicateConfirmation);
        Assert.Equal(current, await LocationAsync(api, first.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-base64")]
    [InlineData("AQ==")]
    public async Task Should_RejectInvalidVersions_When_EditingLocation(string? version)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api); var customer = await CreateAsync(api, town.Id);
        var first = await LocationAsync(api, customer.Locations[0].Id);
        using var response = await api.PutAsJsonAsync(Locations + "/" + first.Id, new EditLocationRequest("Invalid", town.Id, null, version));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(first, await LocationAsync(api, first.Id));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("town")]
    [InlineData("eircode")]
    public async Task Should_RejectInvalidAdditionalLocationOrEdit_When_FieldsAreInvalid(string scenario)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api); var customer = await CreateAsync(api, town.Id);
        var first = await LocationAsync(api, customer.Locations[0].Id);
        string name = scenario == "name" ? " " : "Changed";
        Guid? townId = scenario == "town" ? null : town.Id;
        string? eircode = scenario == "eircode" ? new string('x', 21) : null;
        using var add = await api.PostAsJsonAsync(Customers + "/" + customer.Id + "/locations", new CreateLocationRequest(name, townId, eircode));
        using var edit = await api.PutAsJsonAsync(Locations + "/" + first.Id, new EditLocationRequest(name, townId, eircode, first.Version));
        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode); Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Equal(first, await LocationAsync(api, first.Id)); Assert.Equal((1, 1), await CountsAsync());
    }

    [Theory]
    [InlineData("towns")]
    [InlineData("counties")]
    [InlineData("regions")]
    public async Task Should_KeepExistingTownButRejectNewAssignment_When_GeographyIsArchived(string level)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (region, county, town) = await HierarchyAsync(api); var customer = await CreateAsync(api, town.Id);
        var first = await LocationAsync(api, customer.Locations[0].Id);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, CreatePage); // Town was active when the form opened.
        var item = level == "towns" ? town : level == "counties" ? county : region;
        using var retired = await RetireAsync(api, level, item, ReferenceAction.Archive); Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        Assert.Empty((await api.GetFromJsonAsync<TownChoice[]>("/directory/geography/town-choices"))!);
        using var rejected = await PostAsync(browser, CreatePage, form, new()
            { ["CustomerName"] = "Rejected customer", ["Name"] = "Rejected location", ["TownId"] = town.Id.ToString(), ["Eircode"] = "A67 X123" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string retained = await rejected.Content.ReadAsStringAsync(); Assert.Contains("Choose an active town", retained);
        Assert.Equal("Rejected customer", Field(retained, "CustomerName")); Assert.Equal("Rejected location", Field(retained, "Name"));
        using var add = await api.PostAsJsonAsync(Customers + "/" + customer.Id + "/locations", new CreateLocationRequest("Rejected", town.Id));
        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode); Assert.Equal((1, 1), await CountsAsync());
        using var edit = await EditAsync(api, first, "Updated existing", town.Id, "A67 X999"); Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var current = await LocationAsync(api, first.Id);
        Assert.Contains("(archived)", current.Town.Label); Assert.False(current.Town.IsSelectable); Assert.Equal(town.Id, current.Town.Id);
        string detail = await HtmlAsync(browser, "/HeadOffice/Locations/Detail/" + first.Id);
        Assert.Contains("(archived)", detail);
        Assert.Contains("value=\"" + town.Id + "\" selected=\"selected\"", detail);
        Assert.Contains("(archived)", await HtmlAsync(browser, "/HeadOffice/Customers/Detail/" + customer.Id));
        using var bffSaved = await PostAsync(browser, "/HeadOffice/Locations/Detail/" + first.Id, detail,
            new() { ["Name"] = "BFF archived edit", ["TownId"] = town.Id.ToString(), ["Eircode"] = "", ["Version"] = Field(detail, "Version") });
        Assert.Equal(HttpStatusCode.Redirect, bffSaved.StatusCode);
        Assert.Equal(town.Id, (await LocationAsync(api, first.Id)).Town.Id);
    }

    [Fact]
    public async Task Should_RejectChangingToAnotherArchivedTown_When_ExistingTownIsActive()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, county, town) = await HierarchyAsync(api); var customer = await CreateAsync(api, town.Id);
        var archivedTown = await PlaceAsync(api, "towns", "Archived", county.Id);
        var other = await CreateAsync(api, archivedTown.Id);
        using var retired = await RetireAsync(api, "towns", archivedTown, ReferenceAction.Archive); Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var first = await LocationAsync(api, customer.Locations[0].Id);
        using var edit = await EditAsync(api, first, first.Name, archivedTown.Id);
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode); Assert.Equal(first, await LocationAsync(api, first.Id));
        Assert.Contains("(archived)", (await LocationAsync(api, other.Locations[0].Id)).Town.Label);
    }

    [Fact]
    public async Task Should_CountActualLocationsAndOfferArchive_When_LaraghHasFourReferences()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, county, town) = await HierarchyAsync(api, "Laragh"); var customer = await CreateAsync(api, town.Id);
        for (int i = 2; i <= 4; i++) await AddAsync(api, customer.Id, town.Id, "Location " + i);
        Assert.Empty(app.Locations.Records); // Production counting, not the older geography fixture's substitute.
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string geography = await HtmlAsync(browser, "/HeadOffice/Geography?countyId=" + county.Id);
        string row = Regex.Match(geography, "<li data-reference-id=\"" + town.Id + "\">.*?</li>", RegexOptions.Singleline).Value;
        Assert.Contains("Used by 4 locations", row); Assert.Contains(">Archive</a>", row); Assert.DoesNotContain(">Delete</a>", row);
        using var forbiddenDelete = await RetireAsync(api, "towns", town, ReferenceAction.Delete); Assert.Equal(HttpStatusCode.Conflict, forbiddenDelete.StatusCode);
        var before = (await api.GetFromJsonAsync<CustomerDetails>(Customers + "/" + customer.Id))!.Locations;
        using var archived = await RetireAsync(api, "towns", town, ReferenceAction.Archive); Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        var after = (await api.GetFromJsonAsync<CustomerDetails>(Customers + "/" + customer.Id))!.Locations;
        Assert.Equal(before.Select(item => item.Id), after.Select(item => item.Id)); Assert.Equal((1, 4), await CountsAsync());
        Assert.All(after, item => Assert.Contains("Laragh (archived)", item.Town.Label));
        var reference = (await api.GetFromJsonAsync<GeographyItem>("/directory/geography/towns/" + town.Id))!;
        Assert.Equal("Used by 4 locations", reference.Usage!.Description);
        using var restored = await RetireAsync(api, "towns", reference, ReferenceAction.Unarchive); Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.True(Assert.Single((await api.GetFromJsonAsync<TownChoice[]>("/directory/geography/town-choices"))!).IsSelectable);
    }

    [Theory]
    [InlineData(ReferenceAction.Delete)]
    [InlineData(ReferenceAction.Archive)]
    public async Task Should_PreserveReferences_When_LocationCreationRacesTownRetirement(ReferenceAction action)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api);
        if (action == ReferenceAction.Archive) await CreateAsync(api, town.Id);
        var responses = await Task.WhenAll(RetireAsync(api, "towns", town, action),
            api.PostAsJsonAsync(Customers, new CreateCustomerRequest("Concurrent customer", new("Concurrent location", town.Id))));
        try
        {
            Assert.All(responses, response => Assert.Contains(response.StatusCode,
                new[] { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
            await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            Assert.False(await db.Locations.AnyAsync(location => !db.Towns.Any(town => town.Id == location.TownId)
                || !db.Customers.Any(customer => customer.Id == location.CustomerId)));
            Assert.False(await db.Customers.AnyAsync(customer => !customer.Locations.Any()));
            if (responses[0].StatusCode == HttpStatusCode.OK && action == ReferenceAction.Delete)
            { Assert.Equal(HttpStatusCode.BadRequest, responses[1].StatusCode); Assert.Equal((0, 0), await CountsAsync()); }
            if (responses[1].StatusCode == HttpStatusCode.Created)
                Assert.Equal(action == ReferenceAction.Archive ? (2, 2) : (1, 1), await CountsAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Should_RequireExplicitDuplicateConfirmation_When_TwoAddsRace()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, _, town) = await HierarchyAsync(api); var customer = await CreateAsync(api, town.Id);
        var request = new CreateLocationRequest("Concurrent duplicate", town.Id);
        var responses = await Task.WhenAll(api.PostAsJsonAsync(Customers + "/" + customer.Id + "/locations", request),
            api.PostAsJsonAsync(Customers + "/" + customer.Id + "/locations", request));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal((1, 2), await CountsAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var retry = await api.PostAsJsonAsync(Customers + "/" + customer.Id + "/locations", request);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        Assert.True((await retry.Content.ReadFromJsonAsync<CustomerDirectoryError>())!.RequiresDuplicateConfirmation);
    }

    [Fact]
    public async Task Should_SaveOptionalEircodeAndChangeTown_When_AnotherActiveTownIsSelected()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var (_, county, town) = await HierarchyAsync(api);
        using var created = await api.PostAsJsonAsync(Customers, new CreateCustomerRequest("Customer", new("Location", town.Id)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerDetails>())!;
        var first = await LocationAsync(api, customer.Locations[0].Id);
        Assert.Null(first.Eircode);
        var nextTown = await PlaceAsync(api, "towns", "Avoca", county.Id);
        using var edited = await EditAsync(api, first, "Renamed location", nextTown.Id);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var saved = await LocationAsync(api, first.Id);
        Assert.Equal(first.Id, saved.Id); Assert.Equal(customer.Id, saved.CustomerId);
        Assert.Equal("Avoca — Wicklow, Leinster", saved.Town.Label); Assert.Null(saved.Eircode);
        var originalTown = (await api.GetFromJsonAsync<GeographyItem>("/directory/geography/towns/" + town.Id))!;
        var selectedTown = (await api.GetFromJsonAsync<GeographyItem>("/directory/geography/towns/" + nextTown.Id))!;
        Assert.Equal(0, Assert.Single(originalTown.Usage!.Counts).Count); Assert.Equal("Used by 1 location", selectedTown.Usage!.Description);
        Assert.Equal(nextTown.Id, Assert.Single((await api.GetFromJsonAsync<CustomerDetails>(Customers + "/" + customer.Id))!.Locations).Town.Id);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithoutWrites_When_RecordDoesNotExist()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var (_, _, town) = await HierarchyAsync(api);
        Guid missing = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync(Customers + "/" + missing)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync(Locations + "/" + missing)).StatusCode);
        using var add = await api.PostAsJsonAsync(Customers + "/" + missing + "/locations", new CreateLocationRequest("Missing", town.Id));
        using var edit = await api.PutAsJsonAsync(Locations + "/" + missing, new EditLocationRequest("Missing", town.Id, null, "AQ=="));
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode); Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        foreach (string url in new[] { "/HeadOffice/Customers/Detail/", "/HeadOffice/Locations/Detail/", "/HeadOffice/Locations/Create/" })
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(url + missing)).StatusCode);
        Assert.Equal((0, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_DenyReadsAndWrites_When_AccessScopeRoleOrAntiforgeryIsMissing()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var (_, _, town) = await HierarchyAsync(api);
        CreateCustomerRequest request = new("Denied", new("Denied", town.Id));
        api.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync(Customers)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.PostAsJsonAsync(Customers, request)).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: ""));
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(Customers, request)).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(StaffRoles.FieldSalesperson)); app.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(Customers)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(Customers, request)).StatusCode);
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser); api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync(CreatePage)).StatusCode);
        await SignInAsync(browser); string form = await HtmlAsync(browser, CreatePage);
        Dictionary<string, string> fields = new() { ["CustomerName"] = "Denied", ["Name"] = "Denied", ["TownId"] = town.Id.ToString() };
        using var noCsrf = await browser.PostAsync(CreatePage, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        website.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        using var revoked = await PostAsync(browser, CreatePage, form, fields); Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.Contains("AccessChanged", revoked.Headers.Location!.OriginalString);
        app.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(Customers, request)).StatusCode);
        Assert.Equal((0, 0), await CountsAsync());
    }

    [Fact]
    public async Task Should_PreserveCustomersLocationsAndCounts_When_ApiRestarts()
    {
        await app.ResetAsync(); CustomerDetails customer; LocationDetails location;
        using (var api = app.CreateApiClient())
        {
            var (_, _, town) = await HierarchyAsync(api); customer = await CreateAsync(api, town.Id);
            location = await AddAsync(api, customer.Id, town.Id, "Second location");
        }
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        Assert.Equal(location, await LocationAsync(restarted, location.Id));
        var saved = (await restarted.GetFromJsonAsync<CustomerDetails>(Customers + "/" + customer.Id))!;
        Assert.Equal(customer.Id, saved.Id); Assert.Equal(customer.Name, saved.Name); Assert.Equal(2, saved.Locations.Count);
        Assert.Equal(customer.Locations[0], saved.Locations.Single(item => item.Id == customer.Locations[0].Id));
        Assert.Equal("Used by 2 locations", (await restarted.GetFromJsonAsync<GeographyItem>("/directory/geography/towns/" + location.Town.Id))!.Usage!.Description);
    }

    [Fact]
    public async Task Should_PreservePreviousGeography_When_LocationMigrationIsApplied()
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var current = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var connection = new SqlConnectionStringBuilder(current.Database.GetConnectionString()) { InitialCatalog = "CustomerUpgrade_" + Guid.NewGuid().ToString("N") };
        await using var db = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261002102647_AddGeographyArchive");
            Guid region = Guid.NewGuid(), county = Guid.NewGuid(), town = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName,IsArchived) VALUES ({region},N'Leinster',N'LEINSTER',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({county},N'Wicklow',N'WICKLOW',{region},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({town},N'Laragh',N'LARAGH',{county},1)");
            await migrator.MigrateAsync();
            Assert.Equal(region, (await db.Regions.SingleAsync()).Id); Assert.Equal(region, (await db.Counties.SingleAsync()).RegionId);
            var saved = await db.Towns.SingleAsync(); Assert.Equal(town, saved.Id); Assert.Equal(county, saved.CountyId); Assert.True(saved.IsArchived);
            Assert.Empty(await db.Customers.ToArrayAsync()); Assert.Empty(await db.Locations.ToArrayAsync()); Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task Should_RejectMissingOwnershipAndDeletionOfReferencedRows_When_DatabaseConstraintsAreUsed()
    {
        await app.ResetAsync(); using var api = app.CreateApiClient(); var (_, _, town) = await HierarchyAsync(api);
        var customer = await CreateAsync(api, town.Id);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        foreach (Guid? invalidCustomer in new Guid?[] { null, Guid.NewGuid() })
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Locations (Id,CustomerId,Name,NormalizedName,TownId) VALUES ({Guid.NewGuid()},{invalidCustomer},N'Invalid',N'INVALID',{town.Id})"));
            Assert.Contains(error.Number, new[] { 515, 547 });
        }
        Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Customers.Where(item => item.Id == customer.Id).ExecuteDeleteAsync())).Number);
        Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Towns.Where(item => item.Id == town.Id).ExecuteDeleteAsync())).Number);
        Assert.Equal((1, 1), await CountsAsync());
    }

    private async Task<(int Customers, int Locations)> CountsAsync()
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        return (await db.Customers.CountAsync(), await db.Locations.CountAsync());
    }
    private static async Task<CustomerDetails> CreateAsync(HttpClient api, Guid townId)
    {
        using var response = await api.PostAsJsonAsync(Customers, new CreateCustomerRequest("Hickey's Pharmacies", new("Hickey's Rathdrum", townId, "A67 X123")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CustomerDetails>())!;
    }
    private static async Task<LocationDetails> AddAsync(HttpClient api, Guid customerId, Guid townId, string name)
    {
        using var response = await api.PostAsJsonAsync(Customers + "/" + customerId + "/locations", new CreateLocationRequest(name, townId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<LocationDetails>())!;
    }
    private static async Task<LocationDetails> LocationAsync(HttpClient api, Guid id) => (await api.GetFromJsonAsync<LocationDetails>(Locations + "/" + id))!;
    private static Task<HttpResponseMessage> EditAsync(HttpClient api, LocationDetails location, string name, Guid townId, string? eircode = null) =>
        api.PutAsJsonAsync(Locations + "/" + location.Id, new EditLocationRequest(name, townId, eircode, location.Version));
    private static Task<HttpResponseMessage> RetireAsync(HttpClient api, string level, GeographyItem item, ReferenceAction action) =>
        api.PostAsJsonAsync($"/directory/geography/{level}/{item.Id}/retire", new RetireGeographyRequest(action, item.Version));
    private static async Task<(GeographyItem, GeographyItem, GeographyItem)> HierarchyAsync(HttpClient api, string townName = "Rathdrum")
    {
        var region = await PlaceAsync(api, "regions", "Leinster"); var county = await PlaceAsync(api, "counties", "Wicklow", region.Id);
        return (region, county, await PlaceAsync(api, "towns", townName, county.Id));
    }
    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parentId = null)
    {
        using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parentId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
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
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string url, string html, Dictionary<string, string> fields)
    {
        Dictionary<string, string> body = new(fields) { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") };
        return browser.PostAsync(url, new FormUrlEncodedContent(body));
    }
}
