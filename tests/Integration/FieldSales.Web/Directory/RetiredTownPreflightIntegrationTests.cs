extern alias CatalogueApi;

using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;

namespace FieldSales.Web.Tests;

// Exercise the exact preflight gap found by CI, without timing sleeps or
// weakening the existing concurrent-retirement assertions.
[Collection(DirectorySqlServerCollection.Name)]
public sealed class RetiredTownPreflightIntegrationTests(RetiredTownPreflightApplication app)
    : IClassFixture<RetiredTownPreflightApplication>
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_ReturnTownValidationAndWriteNothing_When_TownIsDeletedBeforePositionRead(bool additional)
    {
        await app.ResetAsync(); using var api = app.CreateApiClient();
        var region = await PlaceAsync(api, "regions", "Leinster"); var county = await PlaceAsync(api, "counties", "Wicklow", region.Id);
        var retiredTown = await PlaceAsync(api, "towns", "Rathdrum", county.Id);
        CustomerDetails? existing = null;
        if (additional)
        {
            var liveTown = await PlaceAsync(api, "towns", "Laragh", county.Id);
            using var seeded = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Existing customer", new("Existing shop", liveTown.Id)));
            Assert.Equal(HttpStatusCode.Created, seeded.StatusCode); existing = (await seeded.Content.ReadFromJsonAsync<CustomerDetails>())!;
        }
        app.Retirement.Arm(retiredTown.Id);
        using var response = additional
            ? await api.PostAsJsonAsync($"/directory/customers/{existing!.Id}/locations", new CreateLocationRequest("New shop", retiredTown.Id))
            : await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("New customer", new("New shop", retiredTown.Id)));
        Assert.True(app.Retirement.Deleted); Assert.False(app.Retirement.SawTransaction);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<CustomerDirectoryError>())!;
        Assert.Equal(additional ? "TownId" : "FirstLocation.TownId", error.Field); Assert.Equal("Choose a town", error.Error);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.False(await db.Towns.AnyAsync(row => row.Id == retiredTown.Id));
        Assert.Equal(additional ? 1 : 0, await db.Customers.CountAsync());
        Assert.Equal(additional ? 1 : 0, await db.Locations.CountAsync());
        Assert.Empty(await db.LocationPositionHistory.ToArrayAsync()); Assert.Empty(await db.AssignmentHistory.ToArrayAsync());
        if (existing is not null)
        {
            var saved = (await api.GetFromJsonAsync<CustomerDetails>($"/directory/customers/{existing.Id}"))!;
            Assert.Equal(existing.Id, saved.Id); Assert.Equal(existing.Locations, saved.Locations);
        }
    }

    private static async Task<GeographyItem> PlaceAsync(HttpClient api, string level, string name, Guid? parent = null)
    {
        using var response = await api.PostAsJsonAsync("/directory/geography/" + level, new CreateGeographyRequest(name, parent));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
}
