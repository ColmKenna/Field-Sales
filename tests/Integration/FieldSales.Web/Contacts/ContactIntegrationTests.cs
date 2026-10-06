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

namespace FieldSales.Web.Tests;

public sealed class ContactApplication : GeographyApplication
{
    private string? _catalogueConnectionString;
    protected override bool UseTestLocationUsage => false;
    protected override void ConfigureAdditionalServices(IServiceCollection services, string connectionString)
    {
        _catalogueConnectionString ??= new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "ContactCatalogueTests_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        services.RemoveAll<CatalogueDbContext>();
        services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(_catalogueConnectionString).Options));
    }
    protected override async Task InitializeAdditionalAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
    }
    public async Task ResetContactsAsync()
    {
        await using (var scope = Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            // Test cleanup breaks the intentional Main/link FK cycle, then restores
            // its trusted constraint before any scenario can run.
            await db.Database.ExecuteSqlRawAsync("""
                DISABLE TRIGGER [TR_Locations_MainGuard] ON [Locations];
                UPDATE [Locations] SET RetiredMainContactId=NULL;
                ALTER TABLE [Locations] NOCHECK CONSTRAINT [FK_Locations_MainContactLink];
                DELETE FROM [LocationContacts];
                UPDATE [Locations] SET MainContactId=NULL;
                ALTER TABLE [Locations] WITH CHECK CHECK CONSTRAINT [FK_Locations_MainContactLink];
                DELETE FROM [Contacts];
                ENABLE TRIGGER [TR_Locations_MainGuard] ON [Locations];
                """);
        }
        await ResetAsync();
    }
}

public sealed class ContactIntegrationTests(ContactApplication app) : IClassFixture<ContactApplication>
{
    private const string Contacts = "/directory/contacts";
    private const string Types = "/directory/reference-data/contact-types";
    private const string CreatePage = "/HeadOffice/Contacts/Create";
    private static string ContactPage(Guid id) => "/HeadOffice/Contacts/Detail/" + id;
    private static string ShopPage(Guid id) => "/HeadOffice/Locations/Detail/" + id;
    private static string RosterUrl(Guid id) => $"/directory/locations/{id}/contacts";
    private static string MainUrl(Guid id) => $"/directory/locations/{id}/main-contact";
    private static string MainPage(Guid shop, Guid contact) => $"/HeadOffice/Contacts/Main/{shop}?ContactId={contact}";
    private sealed record Seed(Guid TownId, DirectoryTypeChoice Type, Guid Rathdrum, Guid Arklow, Guid Wicklow);

    [Fact]
    public async Task Should_ShowSameContactAtAllLocations_When_ContactIsLinkedToSeveralShops()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type.Id, "  Mary Walsh  ", [seed.Rathdrum], " +353 87 123 4567 ", " mary@example.ie ");
        Assert.Equal("Mary Walsh", mary.Name); Assert.Equal("+353 87 123 4567", mary.Phone); Assert.Equal("mary@example.ie", mary.Email);
        await LinkAsync(api, seed.Arklow, mary.Id); await LinkAsync(api, seed.Wicklow, mary.Id);
        var saved = await PersonAsync(api, mary.Id);
        Assert.Equal(3, saved.Locations.Count); Assert.Equal(2, saved.Locations.Select(location => location.CustomerId).Distinct().Count());
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        foreach (Guid shop in new[] { seed.Rathdrum, seed.Arklow, seed.Wicklow })
        {
            var person = Assert.Single((await RosterAsync(api, shop)).Contacts).Contact;
            Assert.Equal(mary.Id, person.Id); Assert.Equal(saved.Phone, person.Phone); Assert.Equal(saved.Type.Id, person.Type.Id);
            string html = await HtmlAsync(browser, ShopPage(shop)); Assert.Contains(ContactPage(mary.Id), html); Assert.Contains("Mary Walsh", html);
        }
        string detail = await HtmlAsync(browser, ContactPage(mary.Id));
        Assert.Contains("Pharmacist", detail); Assert.Contains("mary@example.ie", detail);
        foreach (var link in saved.Locations) { Assert.Contains(ShopPage(link.LocationId), detail); Assert.Contains(link.CustomerName, detail); }
    }

    [Fact]
    public async Task Should_SetMainIndependentlyAtEachShop_When_SameContactIsMainAtTwoLocations()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var wicklowMain = await CreateAsync(api, seed.Type.Id, "Wicklow Main", [seed.Wicklow]);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary Walsh", [seed.Rathdrum, seed.Arklow, seed.Wicklow]);
        Assert.Equal(2, mary.Locations.Count(location => location.IsMain));
        Assert.Equal(wicklowMain.Id, (await RosterAsync(api, seed.Wicklow)).MainContact!.Id);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        Assert.Contains("Main contact: Mary Walsh", await HtmlAsync(browser, ShopPage(seed.Rathdrum)));
        Assert.Contains("Main contact: Mary Walsh", await HtmlAsync(browser, ShopPage(seed.Arklow)));
        Assert.Contains("Main contact: Wicklow Main", await HtmlAsync(browser, ShopPage(seed.Wicklow)));
    }

    [Fact]
    public async Task Should_SetMainAndExplainIt_When_FirstActiveContactIsLinked()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        Assert.Empty((await RosterAsync(api, seed.Rathdrum)).Contacts); Assert.Null((await RosterAsync(api, seed.Rathdrum)).MainContact);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, CreatePage + "?locationId=" + seed.Rathdrum);
        Assert.Contains("checked=\"checked\"", form);
        using var response = await PostAsync(browser, CreatePage, form, new()
        { ["Name"] = "Mary Walsh", ["ContactTypeId"] = seed.Type.Id.ToString(), ["LocationIds"] = seed.Rathdrum.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("First active contact made Main automatically", await HtmlAsync(browser, response.Headers.Location!.OriginalString));
        var mary = (await RosterAsync(api, seed.Rathdrum)).MainContact!;
        string link = await HtmlAsync(browser, $"/HeadOffice/Contacts/Link?LocationId={seed.Arklow}&ContactId={mary.Id}");
        using var linked = await PostAsync(browser, "/HeadOffice/Contacts/Link", link, new()
        { ["LocationId"] = seed.Arklow.ToString(), ["ContactId"] = mary.Id.ToString(), ["LocationVersion"] = Field(link, "LocationVersion") });
        Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);
        Assert.Contains("First active contact made Main automatically", await HtmlAsync(browser, ShopPage(seed.Arklow)));
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Arklow)).MainContact!.Id);
    }

    [Fact]
    public async Task Should_KeepOutgoingContactLinked_When_MainReplacementIsConfirmed()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary Walsh", [seed.Rathdrum, seed.Arklow]);
        var sean = await CreateAsync(api, seed.Type.Id, "Sean Byrne", [seed.Rathdrum]);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string url = MainPage(seed.Rathdrum, sean.Id), confirm = await HtmlAsync(browser, url);
        Assert.Contains("Replace Mary Walsh as main contact?", confirm); Assert.Contains("Confirm replacement", confirm);
        Assert.Contains("Cancel", confirm);
        await HtmlAsync(browser, ShopPage(seed.Rathdrum)); // Follow Cancel, a read-only link.
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Rathdrum)).MainContact!.Id);
        using var notConfirmed = await PostAsync(browser, url, confirm, MainFields(confirm));
        Assert.Equal(HttpStatusCode.OK, notConfirmed.StatusCode);
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Rathdrum)).MainContact!.Id);
        using var changed = await PostAsync(browser, url + "&handler=Confirm", confirm, MainFields(confirm));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        var roster = await RosterAsync(api, seed.Rathdrum);
        Assert.Equal(sean.Id, roster.MainContact!.Id); Assert.Equal(2, roster.Contacts.Count);
        Assert.False(Assert.Single(roster.Contacts, link => link.Contact.Id == mary.Id).IsMain);
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Arklow)).MainContact!.Id);
    }

    [Fact]
    public async Task Should_HideInactiveContactsAndShowCount_When_LocationListIsOpened()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var main = await CreateAsync(api, seed.Type.Id, "Main", [seed.Rathdrum, seed.Arklow]);
        List<Guid> inactive = [];
        for (int i = 1; i <= 3; i++)
        {
            var person = await CreateAsync(api, seed.Type.Id, "Inactive " + i, [seed.Rathdrum, seed.Arklow]);
            using var saved = await EditAsync(api, person, ContactStatus.Inactive); Assert.Equal(HttpStatusCode.OK, saved.StatusCode); inactive.Add(person.Id);
        }
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        foreach (Guid shop in new[] { seed.Rathdrum, seed.Arklow })
        {
            string hidden = await HtmlAsync(browser, ShopPage(shop)); Assert.Contains("Show inactive (3)", hidden);
            foreach (Guid id in inactive) Assert.DoesNotContain("data-contact-id=\"" + id, hidden);
            string visible = await HtmlAsync(browser, ShopPage(shop) + "?ShowInactive=true");
            foreach (Guid id in inactive) Assert.Contains("data-contact-id=\"" + id, visible);
            var roster = await RosterAsync(api, shop, true); Assert.Equal(4, roster.Contacts.Count); Assert.Equal(main.Id, roster.MainContact!.Id);
        }
        Assert.Single((await api.GetFromJsonAsync<ContactChoice[]>(Contacts + "/choices"))!);
    }

    [Fact]
    public async Task Should_AvoidDuplicateLinks_When_SameContactIsLinkedAgain()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary Walsh", [seed.Rathdrum]);
        var other = await CreateAsync(api, seed.Type.Id, "Arklow Main", [seed.Arklow]);
        var roster = await RosterAsync(api, seed.Arklow); var request = new LinkContactRequest(mary.Id, roster.LocationVersion);
        var responses = await Task.WhenAll(api.PostAsJsonAsync(RosterUrl(seed.Arklow), request), api.PostAsJsonAsync(RosterUrl(seed.Arklow), request));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Single(await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<ContactMutationResult>())), result => result!.AlreadyLinked);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var replay = await api.PostAsJsonAsync(RosterUrl(seed.Arklow), request);
        Assert.True((await replay.Content.ReadFromJsonAsync<ContactMutationResult>())!.AlreadyLinked);
        var saved = await RosterAsync(api, seed.Arklow); Assert.Equal(other.Id, saved.MainContact!.Id); Assert.Equal(2, saved.Contacts.Count);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, $"/HeadOffice/Contacts/Link?LocationId={seed.Arklow}&ContactId={mary.Id}");
        using var response2 = await PostAsync(browser, "/HeadOffice/Contacts/Link", form, new()
        { ["LocationId"] = seed.Arklow.ToString(), ["ContactId"] = mary.Id.ToString(), ["LocationVersion"] = Field(form, "LocationVersion") });
        Assert.Equal(HttpStatusCode.Redirect, response2.StatusCode);
        Assert.Contains("This contact is already linked.", await HtmlAsync(browser, ShopPage(seed.Arklow)));
    }

    [Fact]
    public async Task Should_FillMainVacancy_When_FirstActiveContactJoinsInactiveOnlyLocation()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        Guid inactive = Guid.NewGuid();
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Contacts (Id,Name,ContactTypeId,Status) VALUES ({inactive},N'Legacy inactive',{seed.Type.Id},1)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO LocationContacts (LocationId,ContactId) VALUES ({seed.Rathdrum},{inactive})");
        }
        var legacy = await RosterAsync(api, seed.Rathdrum); Assert.Null(legacy.MainContact); Assert.Equal(1, legacy.InactiveCount);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary Walsh", [seed.Arklow]);
        var linked = await LinkAsync(api, seed.Rathdrum, mary.Id); Assert.True(linked.AutomaticallyMadeMain);
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Rathdrum)).MainContact!.Id);
        using var invalidLink = await api.PostAsJsonAsync(RosterUrl(seed.Wicklow), new LinkContactRequest(inactive, (await RosterAsync(api, seed.Wicklow)).LocationVersion));
        Assert.Equal(HttpStatusCode.BadRequest, invalidLink.StatusCode);
        using var invalidMain = await MainAsync(api, seed.Rathdrum, inactive, true); Assert.Equal(HttpStatusCode.BadRequest, invalidMain.StatusCode);
        // Activation of the first available Active Contact repairs a legacy-only roster too.
        await using (var scope = app.Api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO LocationContacts (LocationId,ContactId) VALUES ({seed.Wicklow},{inactive})");
        using var activated = await EditAsync(api, await PersonAsync(api, inactive), ContactStatus.Active); Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.Equal(inactive, (await RosterAsync(api, seed.Wicklow)).MainContact!.Id);
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Rathdrum)).MainContact!.Id);
    }

    [Fact]
    public async Task Should_RejectRetirementAndUnlinkOfMain_When_ReplacementFlowIsUnavailable()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var first = await CreateAsync(api, seed.Type.Id, "Rathdrum Main", [seed.Rathdrum]);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary Walsh", [seed.Rathdrum, seed.Arklow]);
        using var retired = await EditAsync(api, mary, ContactStatus.Inactive); Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
        Assert.Contains("Main at a location", await retired.Content.ReadAsStringAsync());
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string detail = await HtmlAsync(browser, ContactPage(mary.Id));
        using var blocked = await PostAsync(browser, ContactPage(mary.Id), detail, EditFields(mary, ContactStatus.Inactive));
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode); Assert.Contains("Main at a location", await blocked.Content.ReadAsStringAsync());
        foreach (string url in new[] { Contacts + "/" + mary.Id, RosterUrl(seed.Rathdrum) + "/" + mary.Id, MainUrl(seed.Arklow) })
            Assert.Contains((await api.DeleteAsync(url)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(ContactStatus.Active, (await PersonAsync(api, mary.Id)).Status);
        Assert.Equal(first.Id, (await RosterAsync(api, seed.Rathdrum)).MainContact!.Id); Assert.Equal(mary.Id, (await RosterAsync(api, seed.Arklow)).MainContact!.Id);
    }

    [Theory]
    [InlineData("name")] [InlineData("long-name")] [InlineData("phone")] [InlineData("email")]
    [InlineData("long-phone")] [InlineData("long-email")] [InlineData("type")] [InlineData("missing-type")]
    [InlineData("locations")] [InlineData("unknown-location")] [InlineData("empty-location")]
    public async Task Should_SaveNothingAndKeepInput_When_ContactDetailsOrLinksAreInvalid(string invalid)
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var request = new CreateContactRequest("  Mary Walsh  ", seed.Type.Id, [seed.Rathdrum], "+353 87 123 4567", "mary@example.ie");
        request = invalid switch
        {
            "name" => request with { Name = " " }, "long-name" => request with { Name = new string('x', 201) },
            "phone" => request with { Phone = "letters" }, "email" => request with { Email = "broken" },
            "long-phone" => request with { Phone = new string('1', 51) }, "long-email" => request with { Email = new string('a', 250) + "@mail.ie" },
            "type" => request with { ContactTypeId = Guid.NewGuid() }, "missing-type" => request with { ContactTypeId = null },
            "locations" => request with { LocationIds = [] }, "unknown-location" => request with { LocationIds = [seed.Rathdrum, Guid.NewGuid()] },
            _ => request with { LocationIds = [Guid.Empty] }
        };
        using var rejected = await api.PostAsJsonAsync(Contacts, request); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Empty((await api.GetFromJsonAsync<ContactChoice[]>(Contacts + "/choices"))!);
        Assert.Null((await RosterAsync(api, seed.Rathdrum)).MainContact);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, CreatePage);
        using var invalidForm = await PostAsync(browser, CreatePage, form, new()
        { ["Name"] = "Mary's retained input", ["Email"] = "invalid", ["ContactTypeId"] = seed.Type.Id.ToString(), ["LocationIds"] = seed.Rathdrum.ToString() });
        Assert.Equal(HttpStatusCode.OK, invalidForm.StatusCode);
        string html = WebUtility.HtmlDecode(await invalidForm.Content.ReadAsStringAsync());
        Assert.Equal("Mary's retained input", Field(html, "Name")); Assert.Equal("invalid", Field(html, "Email"));
        Assert.Contains("checked=\"checked\"", html);
        Assert.Empty((await api.GetFromJsonAsync<ContactChoice[]>(Contacts + "/choices"))!);
    }

    [Fact]
    public async Task Should_RejectStaleReplacement_When_MainChangesAfterConfirmation()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary Walsh", [seed.Rathdrum]);
        var sean = await CreateAsync(api, seed.Type.Id, "Sean", [seed.Rathdrum]);
        var ann = await CreateAsync(api, seed.Type.Id, "Ann", [seed.Rathdrum]);
        var unlinked = await CreateAsync(api, seed.Type.Id, "Other shop", [seed.Arklow]);
        var original = await RosterAsync(api, seed.Rathdrum);
        using var confirmation = await api.PostAsJsonAsync(MainUrl(seed.Rathdrum), new SetMainContactRequest(sean.Id, original.LocationVersion, mary.Id));
        Assert.Equal(HttpStatusCode.Conflict, confirmation.StatusCode);
        Assert.Equal("Replace Mary Walsh as main contact?", (await confirmation.Content.ReadFromJsonAsync<ContactDirectoryError>())!.Confirmation!.Question);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string old = await HtmlAsync(browser, MainPage(seed.Rathdrum, sean.Id));
        using var replace = await MainAsync(api, seed.Rathdrum, ann.Id, true); Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        using var stale = await api.PostAsJsonAsync(MainUrl(seed.Rathdrum), new SetMainContactRequest(sean.Id, original.LocationVersion, mary.Id, true));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var staleForm = await PostAsync(browser, MainPage(seed.Rathdrum, sean.Id) + "&handler=Confirm", old, MainFields(old));
        Assert.Equal(HttpStatusCode.OK, staleForm.StatusCode); Assert.Contains("Reload", await staleForm.Content.ReadAsStringAsync());
        var latest = await RosterAsync(api, seed.Rathdrum);
        foreach (var request in new[]
        {
            new SetMainContactRequest(sean.Id, null, ann.Id, true), new(sean.Id, "broken", ann.Id, true), new(sean.Id, "AQ==", ann.Id, true),
            new(sean.Id, latest.LocationVersion, mary.Id, true), new(unlinked.Id, latest.LocationVersion, ann.Id, true),
            new(Guid.NewGuid(), latest.LocationVersion, ann.Id, true)
        })
        { using var invalid = await api.PostAsJsonAsync(MainUrl(seed.Rathdrum), request); Assert.Contains(invalid.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict }); }
        var before = await PersonAsync(api, sean.Id);
        using var noop = await EditAsync(api, before, ContactStatus.Active); Assert.Equal(HttpStatusCode.OK, noop.StatusCode);
        using var oldEdit = await EditAsync(api, before, ContactStatus.Active); Assert.Equal(HttpStatusCode.Conflict, oldEdit.StatusCode);
        using var malformedEdit = await EditAsync(api, before with { Version = "" }, ContactStatus.Active); Assert.Equal(HttpStatusCode.BadRequest, malformedEdit.StatusCode);
        using var invalidStatus = await EditAsync(api, await PersonAsync(api, sean.Id), (ContactStatus)42); Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
        Assert.Equal(ann.Id, (await RosterAsync(api, seed.Rathdrum)).MainContact!.Id);
        using var fresh = await MainAsync(api, seed.Rathdrum, sean.Id, true); Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [Fact]
    public async Task Should_PreserveOneMain_When_LinkingAndMainChangesRace()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary", [seed.Rathdrum]);
        var sean = await CreateAsync(api, seed.Type.Id, "Sean", [seed.Rathdrum]);
        var empty = await RosterAsync(api, seed.Arklow);
        var links = await Task.WhenAll(api.PostAsJsonAsync(RosterUrl(seed.Arklow), new LinkContactRequest(mary.Id, empty.LocationVersion)),
            api.PostAsJsonAsync(RosterUrl(seed.Arklow), new LinkContactRequest(sean.Id, empty.LocationVersion)));
        try { Assert.Single(links, response => response.StatusCode == HttpStatusCode.OK); Assert.Single(links, response => response.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in links) response.Dispose(); }
        await LinkAsync(api, seed.Arklow, mary.Id); await LinkAsync(api, seed.Arklow, sean.Id);
        var ann = await CreateAsync(api, seed.Type.Id, "Ann", [seed.Arklow]);
        var current = await RosterAsync(api, seed.Arklow);
        Guid other = current.MainContact!.Id == mary.Id ? sean.Id : mary.Id;
        var replacements = await Task.WhenAll(api.PostAsJsonAsync(MainUrl(seed.Arklow), new SetMainContactRequest(other, current.LocationVersion, current.MainContact.Id, true)),
            api.PostAsJsonAsync(MainUrl(seed.Arklow), new SetMainContactRequest(ann.Id, current.LocationVersion, current.MainContact.Id, true)));
        try { Assert.Single(replacements, response => response.StatusCode == HttpStatusCode.OK); Assert.Single(replacements, response => response.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in replacements) response.Dispose(); }
        // A concurrent status edit either commits before selection or loses safely.
        var person = await PersonAsync(api, sean.Id); var shop = await RosterAsync(api, seed.Rathdrum);
        var statusRace = await Task.WhenAll(EditAsync(api, person, ContactStatus.Inactive),
            api.PostAsJsonAsync(MainUrl(seed.Rathdrum), new SetMainContactRequest(sean.Id, shop.LocationVersion, shop.MainContact!.Id, true)));
        try { Assert.All(statusRace, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Conflict })); }
        finally { foreach (var response in statusRace) response.Dispose(); }
        foreach (Guid id in new[] { seed.Rathdrum, seed.Arklow })
        {
            var roster = await RosterAsync(api, id, true); Assert.NotNull(roster.MainContact); Assert.Equal(ContactStatus.Active, roster.MainContact.Status);
            Assert.Single(roster.Contacts, link => link.IsMain);
        }
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var main = (await RosterAsync(api, seed.Arklow)).MainContact!;
        Assert.Equal(2627, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO LocationContacts (LocationId,ContactId) VALUES ({seed.Arklow},{main.Id})"))).Number);
        Assert.Equal(51002, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Locations SET MainContactId=NULL WHERE Id={seed.Arklow}"))).Number);
        Assert.Equal(51003, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Contacts SET Status=1 WHERE Id={main.Id}"))).Number);
        Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM LocationContacts WHERE LocationId={seed.Arklow} AND ContactId={main.Id}"))).Number);
        Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Locations SET MainContactId={Guid.NewGuid()} WHERE Id={seed.Arklow}"))).Number);
        Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM Contacts WHERE Id={main.Id}"))).Number);
        Assert.False(await db.LocationContacts.AnyAsync(link => !db.Contacts.Any(contact => contact.Id == link.ContactId)));
        // Direct multi-row SQL must also fill each shop's single Main slot.
        Guid active = Guid.NewGuid(), inactive = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Contacts (Id,Name,ContactTypeId,Status) VALUES ({active},N'Batch active',{seed.Type.Id},0),({inactive},N'Batch inactive',{seed.Type.Id},1)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO LocationContacts (LocationId,ContactId) VALUES ({seed.Wicklow},{active}),({seed.Wicklow},{inactive})");
        Assert.Equal(active, (await RosterAsync(api, seed.Wicklow)).MainContact!.Id);
        Assert.Equal(51001, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Locations SET MainContactId={inactive} WHERE Id={seed.Wicklow}"))).Number);
        Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Contacts SET Status=42 WHERE Id={inactive}"))).Number);
    }

    [Fact]
    public async Task Should_CountRealContactsAndKeepLabels_When_ContactTypeIsArchived()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, "Buyer");
        List<ContactDetails> people = [];
        for (int i = 0; i < 14; i++) people.Add(await CreateAsync(api, seed.Type.Id, "Buyer " + i, [seed.Rathdrum, seed.Arklow]));
        using var inactive = await EditAsync(api, people[1], ContactStatus.Inactive); Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        var type = (await api.GetFromJsonAsync<ReferenceListItem>(Types + "/" + seed.Type.Id))!;
        Assert.Equal(14, Assert.Single(type.Usage.Counts).Count); Assert.Equal("Used by 14 contacts", type.Usage.Description);
        Assert.Equal(ReferenceAction.Archive, ReferenceRetirementPolicy.Decide(type.IsArchived, type.Usage));
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string h17 = await HtmlAsync(browser, "/HeadOffice/ReferenceData?ListKey=contact-types");
        Assert.Contains("Used by 14 contacts", h17); Assert.Contains("Archive", h17);
        using var delete = await api.PostAsJsonAsync(Types + "/" + type.Id + "/retire", new RetireDirectoryTypeRequest(ReferenceAction.Delete, type.Version));
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        using var archived = await api.PostAsJsonAsync(Types + "/" + type.Id + "/retire", new RetireDirectoryTypeRequest(ReferenceAction.Archive, type.Version));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Empty((await api.GetFromJsonAsync<DirectoryTypeChoice[]>(Contacts + "/type-choices"))!);
        var saved = await PersonAsync(api, people[2].Id); Assert.True(saved.Type.IsArchived); Assert.Equal(type.Id, saved.Type.Id);
        foreach (var person in people)
        {
            var unchanged = await PersonAsync(api, person.Id);
            Assert.Equal(person.Id, unchanged.Id); Assert.Equal(type.Id, unchanged.Type.Id);
            Assert.Equal("Buyer (archived)", unchanged.Type.Label); Assert.Equal(2, unchanged.Locations.Count);
        }
        using var edit = await EditAsync(api, saved, ContactStatus.Active, name: "Unrelated edit"); Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Contains("Buyer (archived)", await HtmlAsync(browser, ContactPage(saved.Id)));
        using var newArchived = await api.PostAsJsonAsync(Contacts, new CreateContactRequest("Rejected", type.Id, [seed.Wicklow]));
        Assert.Equal(HttpStatusCode.BadRequest, newArchived.StatusCode);
        var pharmacist = await CreateTypeAsync(api, "Pharmacist");
        var active = await CreateAsync(api, pharmacist.Id, "Same Name", [seed.Wicklow]);
        var sameName = await CreateAsync(api, pharmacist.Id, "Same Name", [seed.Wicklow]); Assert.NotEqual(active.Id, sameName.Id);
        using var changedType = await api.PutAsJsonAsync(Contacts + "/" + active.Id,
            new EditContactRequest(active.Name, type.Id, null, null, ContactStatus.Active, active.Version));
        Assert.Equal(HttpStatusCode.BadRequest, changedType.StatusCode);
        var unused = await CreateTypeAsync(api, "Unused");
        var unusedItem = (await api.GetFromJsonAsync<ReferenceListItem>(Types + "/" + unused.Id))!;
        Assert.Equal(ReferenceAction.Delete, ReferenceRetirementPolicy.Decide(unusedItem.IsArchived, unusedItem.Usage));
        using var removed = await api.PostAsJsonAsync(Types + "/" + unused.Id + "/retire", new RetireDirectoryTypeRequest(ReferenceAction.Delete, unusedItem.Version));
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
    }

    [Theory]
    [InlineData(ReferenceAction.Delete)] [InlineData(ReferenceAction.Archive)]
    public async Task Should_PreserveReferences_When_ContactTypeAssignmentRacesRetirement(ReferenceAction action)
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        if (action == ReferenceAction.Archive) await CreateAsync(api, seed.Type.Id, "Existing", [seed.Rathdrum]);
        var type = (await api.GetFromJsonAsync<ReferenceListItem>(Types + "/" + seed.Type.Id))!;
        var responses = await Task.WhenAll(api.PostAsJsonAsync(Types + "/" + type.Id + "/retire", new RetireDirectoryTypeRequest(action, type.Version)),
            api.PostAsJsonAsync(Contacts, new CreateContactRequest("Concurrent", type.Id, [seed.Arklow])));
        try
        {
            Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
            if (action == ReferenceAction.Delete && responses[0].StatusCode == HttpStatusCode.OK) Assert.NotEqual(HttpStatusCode.Created, responses[1].StatusCode);
            await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            Assert.False(await db.Contacts.AnyAsync(contact => !db.ContactTypes.Any(item => item.Id == contact.ContactTypeId)));
            Assert.False(await db.Contacts.AnyAsync(contact => !db.LocationContacts.Any(link => link.ContactId == contact.Id)));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Should_DenyReadsAndWrites_When_AccessScopeRoleOrAntiforgeryIsMissing()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary", [seed.Rathdrum]);
        var sean = await CreateAsync(api, seed.Type.Id, "Sean", [seed.Rathdrum]);
        api.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync(Contacts + "/" + mary.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.PostAsJsonAsync(Contacts, new CreateContactRequest("Denied", seed.Type.Id, [seed.Rathdrum]))).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: ""));
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(RosterUrl(seed.Rathdrum))).StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(StaffRoles.FieldSalesperson)); app.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(Contacts + "/choices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(MainUrl(seed.Rathdrum), new SetMainContactRequest(sean.Id, "", mary.Id, true))).StatusCode);
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser); api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync(CreatePage)).StatusCode); await SignInAsync(browser);
        string confirm = await HtmlAsync(browser, MainPage(seed.Rathdrum, sean.Id));
        using var noCsrf = await browser.PostAsync(MainPage(seed.Rathdrum, sean.Id) + "&handler=Confirm", new FormUrlEncodedContent(MainFields(confirm)));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        website.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        using var revoked = await PostAsync(browser, MainPage(seed.Rathdrum, sean.Id) + "&handler=Confirm", confirm, MainFields(confirm));
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode); Assert.Contains("AccessChanged", revoked.Headers.Location!.OriginalString);
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Rathdrum)).MainContact!.Id);
        app.Roles.SetRoles("niamh", StaffRoles.FieldSalesperson);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(Contacts + "/" + mary.Id)).StatusCode);
    }

    [Fact]
    public async Task Should_PreserveContactsLinksTypesAndMain_When_MigrationAndRestartOccur()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type.Id, "Mary Walsh", [seed.Rathdrum, seed.Arklow], "+353 87 123 4567", "mary@example.ie");
        await app.RestartAsync(); using var restarted = app.CreateApiClient(); var saved = await PersonAsync(restarted, mary.Id);
        Assert.Equal(mary.Id, saved.Id); Assert.Equal(mary.Type, saved.Type); Assert.Equal(mary.Status, saved.Status); Assert.Equal(mary.Version, saved.Version);
        Assert.Equal(mary.Phone, saved.Phone); Assert.Equal(mary.Email, saved.Email); Assert.Equal(mary.Locations, saved.Locations);
        await using var scope = app.Api.Services.CreateAsyncScope(); var current = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        string connection = new SqlConnectionStringBuilder(current.Database.GetConnectionString()) { InitialCatalog = "ContactUpgrade_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var db = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            string previous = (await current.Database.GetAppliedMigrationsAsync()).Single(migration => migration.EndsWith("AddDirectoryTypes", StringComparison.Ordinal));
            await migrator.MigrateAsync(previous);
            Guid region = Guid.NewGuid(), county = Guid.NewGuid(), town = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid(), type = Guid.NewGuid(), locationType = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Regions (Id,Name,NormalizedName,IsArchived) VALUES ({region},N'Leinster',N'LEINSTER',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({county},N'Wicklow',N'WICKLOW',{region},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({town},N'Laragh',N'LARAGH',{county},1)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Customers (Id,Name) VALUES ({customer},N'Customer')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ContactTypes (Id,Name,IsArchived) VALUES ({type},N'Buyer',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO LocationTypes (Id,Name,IsArchived) VALUES ({locationType},N'Office',1)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Locations (Id,CustomerId,Name,NormalizedName,TownId,Eircode,LocationTypeId) VALUES ({location},{customer},N'Location',N'LOCATION',{town},N'A67 X123',{locationType})");
            await migrator.MigrateAsync();
            Assert.Equal(type, (await db.ContactTypes.SingleAsync()).Id); Assert.Equal(customer, (await db.Customers.SingleAsync()).Id);
            var shop = await db.Locations.SingleAsync(); Assert.Equal(location, shop.Id); Assert.Equal(town, shop.TownId); Assert.Null(shop.MainContactId);
            Assert.Equal(locationType, shop.LocationTypeId); Assert.True((await db.LocationTypes.SingleAsync()).IsArchived);
            Assert.Equal("A67 X123", shop.Eircode); Assert.True((await db.Towns.SingleAsync()).IsArchived);
            Assert.Empty(await db.Contacts.ToArrayAsync()); Assert.Empty(await db.LocationContacts.ToArrayAsync()); Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task<Seed> SeedAsync(HttpClient api, string typeName = "Pharmacist")
    {
        using var region = await api.PostAsJsonAsync("/directory/geography/regions", new CreateGeographyRequest("Leinster"));
        var r = (await region.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var county = await api.PostAsJsonAsync("/directory/geography/counties", new CreateGeographyRequest("Wicklow", r.Id));
        var c = (await county.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var town = await api.PostAsJsonAsync("/directory/geography/towns", new CreateGeographyRequest("Rathdrum", c.Id));
        var t = (await town.Content.ReadFromJsonAsync<GeographyItem>())!;
        var type = await CreateTypeAsync(api, typeName);
        using var customer = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Hickey's", new("Rathdrum", t.Id)));
        Assert.Equal(HttpStatusCode.Created, customer.StatusCode); var first = (await customer.Content.ReadFromJsonAsync<CustomerDetails>())!;
        using var arklow = await api.PostAsJsonAsync($"/directory/customers/{first.Id}/locations", new CreateLocationRequest("Arklow", t.Id));
        var a = (await arklow.Content.ReadFromJsonAsync<LocationDetails>())!;
        using var other = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Other Customer", new("Wicklow Town", t.Id)));
        var second = (await other.Content.ReadFromJsonAsync<CustomerDetails>())!;
        return new(t.Id, type, first.Locations[0].Id, a.Id, second.Locations[0].Id);
    }
    private static async Task<DirectoryTypeChoice> CreateTypeAsync(HttpClient api, string name)
    {
        using var response = await api.PostAsJsonAsync(Types, new SaveDirectoryTypeRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<DirectoryTypeChoice>())!;
    }
    private static async Task<ContactDetails> CreateAsync(HttpClient api, Guid typeId, string name, Guid[] locations, string? phone = null, string? email = null)
    {
        using var response = await api.PostAsJsonAsync(Contacts, new CreateContactRequest(name, typeId, locations, phone, email));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ContactDetails>())!;
    }
    private static Task<ContactDetails> PersonAsync(HttpClient api, Guid id) => ReadAsync<ContactDetails>(api, Contacts + "/" + id);
    private static Task<LocationContactsPage> RosterAsync(HttpClient api, Guid id, bool showInactive = false) => ReadAsync<LocationContactsPage>(api, RosterUrl(id) + "?showInactive=" + showInactive);
    private static async Task<T> ReadAsync<T>(HttpClient api, string url) => (await api.GetFromJsonAsync<T>(url))!;
    private static async Task<ContactMutationResult> LinkAsync(HttpClient api, Guid shop, Guid person)
    {
        using var response = await api.PostAsJsonAsync(RosterUrl(shop), new LinkContactRequest(person, (await RosterAsync(api, shop)).LocationVersion));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ContactMutationResult>())!;
    }
    private static Task<HttpResponseMessage> EditAsync(HttpClient api, ContactDetails contact, ContactStatus status, string? name = null) =>
        api.PutAsJsonAsync(Contacts + "/" + contact.Id, new EditContactRequest(name ?? contact.Name, contact.Type.Id, contact.Phone, contact.Email, status, contact.Version));
    private static async Task<HttpResponseMessage> MainAsync(HttpClient api, Guid shop, Guid contact, bool confirm)
    {
        var roster = await RosterAsync(api, shop); return await api.PostAsJsonAsync(MainUrl(shop), new SetMainContactRequest(contact, roster.LocationVersion, roster.MainContact?.Id, confirm));
    }
    private static Dictionary<string, string> EditFields(ContactDetails contact, ContactStatus status) => new()
    { ["Name"] = contact.Name, ["ContactTypeId"] = contact.Type.Id.ToString(), ["Phone"] = contact.Phone ?? "", ["Email"] = contact.Email ?? "", ["Status"] = status.ToString(), ["Version"] = contact.Version };
    private static Dictionary<string, string> MainFields(string html) => new()
    { ["ContactId"] = Field(html, "ContactId"), ["LocationVersion"] = Field(html, "LocationVersion"), ["ExpectedMainContactId"] = Field(html, "ExpectedMainContactId") };
    private static async Task SignInAsync(HttpClient browser) => Assert.Equal(HttpStatusCode.NoContent,
        (await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(StaffRoles.HeadOfficeUser))).StatusCode);
    private static async Task<string> HtmlAsync(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string url, string html, Dictionary<string, string> fields) =>
        browser.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>(fields)
        { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") }));
}
