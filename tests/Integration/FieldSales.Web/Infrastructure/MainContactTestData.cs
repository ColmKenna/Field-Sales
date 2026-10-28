using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;

namespace FieldSales.Web.Tests;

internal static class MainContactTestData
{
    internal sealed record Seed(Guid Type, Guid Customer, Guid Town, Guid[] Shops);
    public static async Task<Seed> SeedAsync(HttpClient api, int shopCount = 2)
    {
        var region = await CreateReadAsync<GeographyItem>(api, "/directory/geography/regions", new CreateGeographyRequest("Leinster"));
        var county = await CreateReadAsync<GeographyItem>(api, "/directory/geography/counties", new CreateGeographyRequest("Wicklow", region.Id));
        var town = await CreateReadAsync<GeographyItem>(api, "/directory/geography/towns", new CreateGeographyRequest("Rathdrum", county.Id));
        var type = await CreateReadAsync<DirectoryTypeChoice>(api, "/directory/reference-data/contact-types", new SaveDirectoryTypeRequest("Pharmacist"));
        var customer = await CreateReadAsync<CustomerDetails>(api, "/directory/customers", new CreateCustomerRequest("Hickey's", new("Rathdrum", town.Id)));
        List<Guid> shops = [customer.Locations[0].Id];
        for (int i = 1; i < shopCount; i++)
            shops.Add((await CreateReadAsync<LocationDetails>(api, $"/directory/customers/{customer.Id}/locations",
                new CreateLocationRequest(i == 1 ? "Arklow" : "Shop " + i, town.Id))).Id);
        return new(type.Id, customer.Id, town.Id, shops.ToArray());
    }
    public static Task<ContactDetails> CreateAsync(HttpClient api, Guid type, string name, Guid[] shops) =>
        CreateReadAsync<ContactDetails>(api, "/directory/contacts", new CreateContactRequest(name, type, shops));
    public static async Task<T> CreateReadAsync<T>(HttpClient api, string url, object request)
    {
        using var response = await api.PostAsJsonAsync(url, request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    public static async Task<LocationContactsPage> RosterAsync(HttpClient api, Guid shop) =>
        (await api.GetFromJsonAsync<LocationContactsPage>($"/directory/locations/{shop}/contacts?showInactive=true"))!;
    public static async Task<ContactDetails> PersonAsync(HttpClient api, Guid person) =>
        (await api.GetFromJsonAsync<ContactDetails>($"/directory/contacts/{person}"))!;
}
