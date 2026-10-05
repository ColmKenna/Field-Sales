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

public sealed class RetiredTownPreflightApplication : GeographyApplication
{
    protected override bool UseTestLocationUsage => false;
    public RetirementBeforePositionRead Retirement { get; } = new();

    protected override void ConfigureAdditionalServices(IServiceCollection services, string connectionString)
    {
        Retirement.ConnectionString = connectionString;
        services.RemoveAll<DirectoryDbContext>();
        services.AddScoped(_ => new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(Retirement).Options));
    }

    public sealed class RetirementBeforePositionRead : DbCommandInterceptor
    {
        public string ConnectionString { get; set; } = "";
        private Guid? target;
        public bool Deleted { get; private set; }
        public bool SawTransaction { get; private set; }
        public void Arm(Guid id) { target = id; Deleted = false; SawTransaction = false; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            // The position preflight materializes a single Town. Earlier
            // eligibility checks use EXISTS or join its County/Region.
            if (target is { } id && command.CommandText.Contains("SELECT TOP(2)", StringComparison.Ordinal)
                && command.CommandText.Contains("FROM [Towns] AS [t]", StringComparison.Ordinal)
                && command.CommandText.Contains("[t].[Latitude]", StringComparison.Ordinal)
                && !command.CommandText.Contains("JOIN", StringComparison.Ordinal))
            {
                target = null; SawTransaction = command.Transaction is not null;
                await using var retirement = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
                    .UseSqlServer(ConnectionString).Options);
                Deleted = await retirement.Towns.Where(town => town.Id == id).ExecuteDeleteAsync(ct) == 1;
            }
            return result;
        }
    }
}
