using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;

namespace FieldSales.Web.Tests;

// Pins WI-018's rules before WI-020 changes the explicit retirement workflow.
[Collection(DirectorySqlServerCollection.Name)]
public sealed class MainContactCharacterisationTests(ContactApplication app) : IClassFixture<ContactApplication>
{
    [Fact]
    public async Task Should_KeepIndependentMainChoices_When_OnePersonServesSeveralShops()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient();
        var seed = await MainContactTestData.SeedAsync(api);
        var mary = await MainContactTestData.CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        foreach (var shop in seed.Shops)
            Assert.Equal(mary.Id, (await MainContactTestData.RosterAsync(api, shop)).MainContact!.Id);
        var sean = await MainContactTestData.CreateAsync(api, seed.Type, "Sean Byrne", [seed.Shops[0]]);
        var roster = await MainContactTestData.RosterAsync(api, seed.Shops[0]);
        using var question = await api.PostAsJsonAsync($"/directory/locations/{roster.LocationId}/main-contact",
            new SetMainContactRequest(sean.Id, roster.LocationVersion, mary.Id));
        Assert.Equal(HttpStatusCode.Conflict, question.StatusCode);
        using var saved = await api.PostAsJsonAsync($"/directory/locations/{roster.LocationId}/main-contact",
            new SetMainContactRequest(sean.Id, roster.LocationVersion, mary.Id, true));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(mary.Id, (await MainContactTestData.RosterAsync(api, seed.Shops[1])).MainContact!.Id);
        Assert.Contains((await MainContactTestData.RosterAsync(api, seed.Shops[0])).Contacts, c => c.Contact.Id == mary.Id);
    }

    [Fact]
    public async Task Should_RejectImplicitRetirement_When_ContactIsMainAtAnotherShop()
    {
        await app.ResetContactsAsync(); using var api = app.CreateApiClient();
        var seed = await MainContactTestData.SeedAsync(api);
        await MainContactTestData.CreateAsync(api, seed.Type, "Rathdrum Main", [seed.Shops[0]]);
        var mary = await MainContactTestData.CreateAsync(api, seed.Type, "Mary Walsh", seed.Shops);
        using var denied = await api.PutAsJsonAsync($"/directory/contacts/{mary.Id}",
            new EditContactRequest(mary.Name, mary.Type.Id, null, null, ContactStatus.Inactive, mary.Version));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal(ContactStatus.Active, (await MainContactTestData.PersonAsync(api, mary.Id)).Status);
        Assert.Equal(mary.Id, (await MainContactTestData.RosterAsync(api, seed.Shops[1])).MainContact!.Id);
    }
}
