extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using static FieldSales.Web.Tests.MainContactTestData;

namespace FieldSales.Web.Tests;

public sealed class MainContactReplacementTests(ContactApplication app) : IClassFixture<ContactApplication>
{
    [Theory]
    [InlineData("existing")] [InlineData("new")] [InlineData("none")]
    public async Task Should_ApplyChosenReplacementAtomically_When_MainIsUnlinkedThroughInlineQuestion(string choice)
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var sean = await CreateAsync(api, seed.Type, "Sean Byrne", [seed.Shops[0]]);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string question = await HtmlAsync(browser, Page(mary.Id) + "?UnlinkLocationId=" + seed.Shops[0]);
        Assert.Contains("Who is Rathdrum's main contact now?", question);
        Assert.Contains("Add a new contact", question); Assert.Contains("No replacement yet", question);
        Assert.Contains("Sean Byrne", question); Assert.Contains("Arklow", question);
        // Opening/cancelling the question is read-only.
        _ = await HtmlAsync(browser, Page(mary.Id));
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Shops[0])).MainContact!.Id);
        var fields = HiddenFields(question);
        fields["ReplacementChoice"] = choice; fields["ReplacementContactId"] = sean.Id.ToString();
        fields["NewName"] = "Ann Murphy"; fields["NewContactTypeId"] = seed.Type.ToString();
        using var response = await browser.PostAsync(Page(mary.Id) + "?handler=Remove", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var shop = await RosterAsync(api, seed.Shops[0]);
        var person = await PersonAsync(api, mary.Id);
        if (choice == "none")
        {
            Assert.Equal(ContactStatus.Inactive, person.Status); Assert.Equal(2, person.Locations.Count);
            foreach (var id in seed.Shops)
            {
                var flagged = await RosterAsync(api, id); Assert.True(flagged.ReplacementNeeded);
                Assert.Null(flagged.MainContact); Assert.Equal(mary.Id, flagged.RetiredMainContact!.Id);
                string html = await HtmlAsync(browser, "/HeadOffice/Locations/Detail/" + id);
                Assert.Contains("Main contact inactive — replacement needed", html);
                Assert.Contains("Mary Walsh (inactive) — replacement needed", html);
            }
        }
        else
        {
            Assert.Equal(choice == "existing" ? "Sean Byrne" : "Ann Murphy", shop.MainContact!.Name);
            Assert.DoesNotContain(shop.Contacts, link => link.Contact.Id == mary.Id);
            Assert.False(shop.ReplacementNeeded); Assert.Equal(ContactStatus.Active, person.Status);
            Assert.Equal(seed.Shops[1], Assert.Single(person.Locations).LocationId);
            Assert.Equal(mary.Id, (await RosterAsync(api, seed.Shops[1])).MainContact!.Id);
        }
    }

    [Theory]
    [InlineData(2)] [InlineData(5)]
    public async Task Should_FlagEveryMainShopAndRetainLinks_When_ContactIsRetiredGlobally(int count)
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api, count);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var other = await CreateReadAsync<LocationDetails>(api, $"/directory/customers/{seed.Customer}/locations", new CreateLocationRequest("Other shop", seed.Town));
        var main = await CreateAsync(api, seed.Type, "Other Main", [other.Id]);
        var otherRoster = await RosterAsync(api, other.Id);
        using var linked = await api.PostAsJsonAsync($"/directory/locations/{other.Id}/contacts", new LinkContactRequest(mary.Id, otherRoster.LocationVersion));
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        using var response = await RetireAsync(api, mary.Id); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var retired = (await response.Content.ReadFromJsonAsync<MainContactRetirementResult>())!;
        Assert.Equal(count, retired.FlaggedLocations.Count);
        Assert.Equal(count + 1, (await PersonAsync(api, mary.Id)).Locations.Count);
        foreach (var id in seed.Shops) { var roster = await RosterAsync(api, id); Assert.True(roster.ReplacementNeeded); Assert.Equal(mary.Id, roster.RetiredMainContact!.Id); }
        var untouched = await RosterAsync(api, other.Id); Assert.False(untouched.ReplacementNeeded); Assert.Equal(main.Id, untouched.MainContact!.Id);
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(count, await db.Locations.CountAsync(location => location.RetiredMainContactId != null));
    }

    [Fact]
    public async Task Should_FlagAffectedShops_When_GlobalRetirementIsConfirmedOnContactRecord()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string question = await HtmlAsync(browser, Page(mary.Id) + "?Retire=true");
        Assert.Contains("Mark Mary Walsh inactive at every linked location?", question);
        Assert.Contains("Rathdrum", question); Assert.Contains("Arklow", question);
        using var response = await browser.PostAsync(Page(mary.Id) + "?handler=Retire", new FormUrlEncodedContent(HiddenFields(question)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        foreach (var id in seed.Shops) Assert.True((await RosterAsync(api, id)).ReplacementNeeded);
        Assert.Equal(ContactStatus.Inactive, (await PersonAsync(api, mary.Id)).Status);
    }

    [Fact]
    public async Task Should_RollBackNewContactAndMain_When_UnlinkCannotBeSaved()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var request = (await RequestAsync(api, seed.Shops[0], mary.Id)) with { NewContact = new("Ann Murphy", seed.Type) };
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        // A failure after the new Contact/link and Main saves must undo all three.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER [TR_WI020_TestRejectUnlink] ON [LocationContacts] AFTER DELETE AS
            BEGIN
                IF EXISTS (SELECT 1 FROM deleted) THROW 51004, 'Test unlink failure.', 1;
            END;
            """);
        try
        {
            using var response = await api.PostAsJsonAsync(RemoveUrl(seed.Shops[0], mary.Id), request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(1, await db.Contacts.CountAsync());
            var roster = await RosterAsync(api, seed.Shops[0]);
            Assert.Equal(mary.Id, roster.MainContact!.Id); Assert.Single(roster.Contacts);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS [TR_WI020_TestRejectUnlink]"); }
    }

    [Fact]
    public async Task Should_ClearOnlyChosenGapAndKeepRetiredContactInactive_When_ActiveMainIsNamed()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var sean = await CreateAsync(api, seed.Type, "Sean Byrne", seed.Shops);
        using var retired = await RetireAsync(api, mary.Id); Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        await SetMainAsync(api, seed.Shops[0], sean.Id);
        Assert.False((await RosterAsync(api, seed.Shops[0])).ReplacementNeeded);
        Assert.True((await RosterAsync(api, seed.Shops[1])).ReplacementNeeded);
        Assert.Equal(ContactStatus.Inactive, (await PersonAsync(api, mary.Id)).Status);
    }

    [Fact]
    public async Task Should_KeepGapUntilExplicitMainChoice_When_RetiredPersonReactivatesOrActivePersonIsLinked()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        using var retired = await RetireAsync(api, mary.Id); Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var current = await PersonAsync(api, mary.Id);
        using var active = await api.PutAsJsonAsync($"/directory/contacts/{mary.Id}",
            new EditContactRequest(current.Name, seed.Type, null, null, ContactStatus.Active, current.Version));
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        _ = await CreateAsync(api, seed.Type, "Sean Byrne", seed.Shops);
        foreach (var id in seed.Shops)
        {
            var roster = await RosterAsync(api, id); Assert.True(roster.ReplacementNeeded); Assert.Null(roster.MainContact);
            Assert.All(roster.Contacts, link => Assert.False(link.IsMain));
        }
        // Reactivated Mary is eligible only after an explicit choice.
        await SetMainAsync(api, seed.Shops[0], mary.Id);
        Assert.False((await RosterAsync(api, seed.Shops[0])).ReplacementNeeded);
        Assert.True((await RosterAsync(api, seed.Shops[1])).ReplacementNeeded);
    }

    [Fact]
    public async Task Should_PreserveMainAndContactRecord_When_NonMainIsUnlinked()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var sean = await CreateAsync(api, seed.Type, "Sean Byrne", [seed.Shops[0]]);
        using var response = await RemoveAsync(api, seed.Shops[0], sean.Id); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await PersonAsync(api, sean.Id)).Locations);
        var roster = await RosterAsync(api, seed.Shops[0]); Assert.Equal(mary.Id, roster.MainContact!.Id); Assert.Single(roster.Contacts);
        using var deletion = await api.DeleteAsync($"/directory/contacts/{sean.Id}");
        Assert.Contains(deletion.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
    }

    [Theory]
    [InlineData("inactive")] [InlineData("unlinked")] [InlineData("self")] [InlineData("missing")]
    [InlineData("ambiguous")] [InlineData("invalid-new")] [InlineData("archived-type")]
    public async Task Should_SaveNothing_When_ReplacementChoiceIsInvalid(string invalid)
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var sean = await CreateAsync(api, seed.Type, "Sean Byrne", [seed.Shops[0]]);
        if (invalid == "inactive")
        {
            using var inactive = await api.PutAsJsonAsync($"/directory/contacts/{sean.Id}", new EditContactRequest(sean.Name, seed.Type, null, null, ContactStatus.Inactive, sean.Version));
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        }
        if (invalid == "archived-type")
        {
            await using var scope = app.Api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE ContactTypes SET IsArchived=1 WHERE Id={seed.Type}");
        }
        var request = await RequestAsync(api, seed.Shops[0], mary.Id);
        request = invalid switch
        {
            "self" => request with { ReplacementContactId = mary.Id },
            "unlinked" => request with { ReplacementContactId = Guid.NewGuid() },
            "missing" => request,
            "ambiguous" => request with { ReplacementContactId = sean.Id, NoReplacementYet = true },
            "invalid-new" => request with { NewContact = new("", seed.Type) },
            "archived-type" => request with { NewContact = new("Ann Murphy", seed.Type) },
            _ => request with { ReplacementContactId = sean.Id }
        };
        using var rejected = await api.PostAsJsonAsync(RemoveUrl(seed.Shops[0], mary.Id), request);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var roster = await RosterAsync(api, seed.Shops[0]); Assert.Equal(mary.Id, roster.MainContact!.Id);
        Assert.False(roster.ReplacementNeeded); Assert.Equal(2, roster.Contacts.Count);
        Assert.Equal(ContactStatus.Active, (await PersonAsync(api, mary.Id)).Status);
        await using var verify = app.Api.Services.CreateAsyncScope();
        Assert.Equal(2, await verify.ServiceProvider.GetRequiredService<DirectoryDbContext>().Contacts.CountAsync());
    }

    [Fact]
    public async Task Should_KeepEnteredNewContactAndSaveNothing_When_InlineFormIsInvalid()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        string form = await HtmlAsync(browser, Page(mary.Id) + "?UnlinkLocationId=" + seed.Shops[0]);
        var fields = HiddenFields(form); fields["ReplacementChoice"] = "new";
        fields["NewName"] = "Ann's retained name"; fields["NewContactTypeId"] = seed.Type.ToString(); fields["NewEmail"] = "invalid";
        using var response = await browser.PostAsync(Page(mary.Id) + "?handler=Remove", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Ann's retained name", html); Assert.Contains("value=\"invalid\"", html);
        Assert.Equal(mary.Id, (await RosterAsync(api, seed.Shops[0])).MainContact!.Id);
        Assert.Single((await RosterAsync(api, seed.Shops[0])).Contacts);
    }

    [Theory]
    [InlineData("contact")] [InlineData("other-shop")] [InlineData("links")] [InlineData("malformed")]
    public async Task Should_RejectWholeRetirement_When_ConfirmedImpactIsStaleOrIncomplete(string change)
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var request = await RetirementRequestAsync(api, mary.Id);
        if (change == "contact")
        {
            using var edit = await api.PutAsJsonAsync($"/directory/contacts/{mary.Id}", new EditContactRequest("Changed Mary", seed.Type, null, null, ContactStatus.Active, mary.Version));
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        }
        if (change == "other-shop")
        {
            var sean = await CreateAsync(api, seed.Type, "Sean Byrne", [seed.Shops[1]]);
            await SetMainAsync(api, seed.Shops[1], sean.Id);
        }
        if (change == "links")
        {
            var other = await CreateReadAsync<LocationDetails>(api, $"/directory/customers/{seed.Customer}/locations", new CreateLocationRequest("Other", seed.Town));
            using var link = await api.PostAsJsonAsync($"/directory/locations/{other.Id}/contacts", new LinkContactRequest(mary.Id, other.Version));
            Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        }
        if (change == "malformed") request = request with { AffectedLocations = [] };
        using var response = await api.PostAsJsonAsync($"/directory/contacts/{mary.Id}/retire", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ContactStatus.Active, (await PersonAsync(api, mary.Id)).Status);
        Assert.False((await RosterAsync(api, seed.Shops[0])).ReplacementNeeded);
    }

    [Fact]
    public async Task Should_PreserveOneConsistentOutcome_When_ReplacementAndRetirementRace()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var sean = await CreateAsync(api, seed.Type, "Sean Byrne", [seed.Shops[0]]);
        var remove = (await RequestAsync(api, seed.Shops[0], mary.Id)) with { ReplacementContactId = sean.Id };
        var retirement = await RetirementRequestAsync(api, mary.Id);
        var responses = await Task.WhenAll(api.PostAsJsonAsync(RemoveUrl(seed.Shops[0], mary.Id), remove),
            api.PostAsJsonAsync($"/directory/contacts/{mary.Id}/retire", retirement));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            foreach (var id in seed.Shops)
            {
                var roster = await RosterAsync(api, id);
                Assert.True(roster.ReplacementNeeded || roster.MainContact is { Status: ContactStatus.Active });
                Assert.True(roster.Contacts.Count(link => link.IsMain) <= 1);
            }
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Should_RejectSilentGapClearAndRetainedLinkRemoval_When_DatabaseIsWrittenDirectly()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        using var retired = await RetireAsync(api, mary.Id); Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(51004, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Locations SET RetiredMainContactId=NULL WHERE Id={seed.Shops[0]}"))).Number);
        Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM LocationContacts WHERE LocationId={seed.Shops[0]} AND ContactId={mary.Id}"))).Number);
        Assert.True((await RosterAsync(api, seed.Shops[0])).ReplacementNeeded);
    }

    [Fact]
    public async Task Should_DenyMutation_When_ScopeRoleAntiforgeryOrCurrentAccessIsMissing()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        var request = (await RequestAsync(api, seed.Shops[0], mary.Id)) with { NoReplacementYet = true };
        api.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await api.PostAsJsonAsync(RemoveUrl(seed.Shops[0], mary.Id), request); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope: "other"));
        using var wrongScope = await api.PostAsJsonAsync(RemoveUrl(seed.Shops[0], mary.Id), request); Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        await using var website = app.CreateWebsite(); using var browser = website.CreateBrowser(); await SignInAsync(browser);
        var fields = HiddenFields(await HtmlAsync(browser, Page(mary.Id) + "?UnlinkLocationId=" + seed.Shops[0]));
        fields["ReplacementChoice"] = "none"; string token = fields["__RequestVerificationToken"]; fields.Remove("__RequestVerificationToken");
        using var csrf = await browser.PostAsync(Page(mary.Id) + "?handler=Remove", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        fields["__RequestVerificationToken"] = token; app.Roles.SetRoles("niamh", StaffRoles.SalesManager);
        using var revoked = await browser.PostAsync(Page(mary.Id) + "?handler=Remove", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using var role = await api.PostAsJsonAsync($"/directory/contacts/{mary.Id}/retire", new RetireMainContactRequest(request.ContactVersion, request.AffectedLocations));
        Assert.Equal(HttpStatusCode.Forbidden, role.StatusCode);
        app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        Assert.Equal(ContactStatus.Active, (await PersonAsync(api, mary.Id)).Status);
    }

    [Fact]
    public async Task Should_PreserveGapAndOutgoingIdentity_When_ApplicationRestarts()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient(); var seed = await SeedAsync(api);
        var mary = await CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        using var response = await RetireAsync(api, mary.Id); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        foreach (var id in seed.Shops)
        {
            var roster = await RosterAsync(restarted, id); Assert.True(roster.ReplacementNeeded);
            Assert.Equal(mary.Id, roster.RetiredMainContact!.Id); Assert.Equal(ContactStatus.Inactive, roster.RetiredMainContact.Status);
        }
    }

    [Fact]
    public async Task Should_PreserveExistingMainAndLinks_When_PredecessorDatabaseIsUpgraded()
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var current = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        string connection = new SqlConnectionStringBuilder(current.Database.GetConnectionString()) { InitialCatalog = "WI020Upgrade_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var db = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseSqlServer(connection).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            string previous = (await current.Database.GetAppliedMigrationsAsync()).Single(m => m.EndsWith("AddTownCoordinates", StringComparison.Ordinal));
            await migrator.MigrateAsync(previous);
            Guid region = Guid.NewGuid(), county = Guid.NewGuid(), town = Guid.NewGuid(), customer = Guid.NewGuid(), shop = Guid.NewGuid(), type = Guid.NewGuid(), mary = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Regions (Id,Name,NormalizedName,IsArchived) VALUES ({region},N'Region',N'REGION',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Counties (Id,Name,NormalizedName,RegionId,IsArchived) VALUES ({county},N'County',N'COUNTY',{region},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Towns (Id,Name,NormalizedName,CountyId,IsArchived) VALUES ({town},N'Town',N'TOWN',{county},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Customers (Id,Name) VALUES ({customer},N'Customer')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT ContactTypes (Id,Name,IsArchived) VALUES ({type},N'Buyer',0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Locations (Id,CustomerId,Name,NormalizedName,TownId) VALUES ({shop},{customer},N'Shop',N'SHOP',{town})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Contacts (Id,Name,ContactTypeId,Status) VALUES ({mary},N'Mary',{type},0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT LocationContacts (LocationId,ContactId) VALUES ({shop},{mary})");
            await migrator.MigrateAsync();
            var location = await db.Locations.SingleAsync(); Assert.Equal(mary, location.MainContactId); Assert.Null(location.RetiredMainContactId);
            Assert.Equal(mary, (await db.Contacts.SingleAsync()).Id); Assert.Single(await db.LocationContacts.ToArrayAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static string Page(Guid id) => "/HeadOffice/Contacts/Detail/" + id;
    private static string RemoveUrl(Guid shop, Guid person) => $"/directory/locations/{shop}/contacts/{person}/remove";
    private static async Task<RemoveLocationContactRequest> RequestAsync(HttpClient api, Guid shop, Guid person)
    {
        var contact = await PersonAsync(api, person); var location = await RosterAsync(api, shop);
        return new(contact.Version, location.LocationVersion, AffectedLocations: Versions(contact));
    }
    private static ContactLocationVersion[] Versions(ContactDetails contact) =>
        contact.Locations.Select(location => new ContactLocationVersion(location.LocationId, location.LocationVersion)).ToArray();
    private static async Task<RetireMainContactRequest> RetirementRequestAsync(HttpClient api, Guid person)
    { var contact = await PersonAsync(api, person); return new(contact.Version, Versions(contact)); }
    private static async Task<HttpResponseMessage> RetireAsync(HttpClient api, Guid person) =>
        await api.PostAsJsonAsync($"/directory/contacts/{person}/retire", await RetirementRequestAsync(api, person));
    private static async Task<HttpResponseMessage> RemoveAsync(HttpClient api, Guid shop, Guid person) =>
        await api.PostAsJsonAsync(RemoveUrl(shop, person), await RequestAsync(api, shop, person));
    private static async Task SetMainAsync(HttpClient api, Guid shop, Guid person)
    {
        var location = await RosterAsync(api, shop);
        using var result = await api.PostAsJsonAsync($"/directory/locations/{shop}/main-contact", new SetMainContactRequest(person, location.LocationVersion, location.MainContact?.Id, true));
        Assert.True(result.StatusCode == HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
    }
    private static async Task SignInAsync(HttpClient browser) => Assert.Equal(HttpStatusCode.NoContent,
        (await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(StaffRoles.HeadOfficeUser))).StatusCode);
    private static async Task<string> HtmlAsync(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static Dictionary<string, string> HiddenFields(string html) => Regex.Matches(html, "<input\\b[^>]*>")
        .Cast<Match>().Select(match => match.Value).Where(input => input.Contains("type=\"hidden\"", StringComparison.Ordinal))
        .Select(input => new
        {
            Name = Regex.Match(input, "name=\"([^\"]*)\"").Groups[1].Value,
            Value = Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value
        }).Where(field => field.Name.Length > 0).GroupBy(field => field.Name)
        .ToDictionary(group => group.Key, group => WebUtility.HtmlDecode(group.First().Value));
}
