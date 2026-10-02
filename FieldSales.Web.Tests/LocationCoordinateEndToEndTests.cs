extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using IEircodeLookup = CatalogueApi::FieldSales.Api.Directory.IEircodeLookup;
using EircodeLookupResult = CatalogueApi::FieldSales.Api.Directory.EircodeLookupResult;
using EircodeLookupStatus = CatalogueApi::FieldSales.Api.Directory.EircodeLookupStatus;
using Coordinates = CatalogueApi::FieldSales.Api.Directory.Coordinates;

namespace FieldSales.Web.Tests;

public sealed class LocationCoordinateApplication : GeographyApplication
{
    protected override bool UseTestLocationUsage => false;
    public LookupProbe Probe { get; } = new();
    protected override void ConfigureAdditionalServices(IServiceCollection services, string connectionString)
    {
        services.RemoveAll<IEircodeLookup>();
        services.AddScoped<IEircodeLookup>(provider => new RequestLookup(provider.GetRequiredService<DirectoryDbContext>(), Probe));
    }
    public async Task ResetCoordinatesAsync()
    {
        await using (var scope = Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            await db.LocationPositionHistory.ExecuteDeleteAsync();
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE [Locations] NOCHECK CONSTRAINT [FK_Locations_MainContactLink];
                DELETE FROM [LocationContacts];
                UPDATE [Locations] SET MainContactId=NULL;
                ALTER TABLE [Locations] WITH CHECK CHECK CONSTRAINT [FK_Locations_MainContactLink];
                DELETE FROM [Contacts];
                """);
        }
        await ResetAsync(); Probe.Reset();
    }
    public sealed class LookupProbe
    {
        public int Calls;
        public bool SawTransaction;
        public Func<string, CancellationToken, Task<EircodeLookupResult>> Handler { get; set; } = Unavailable;
        private static Task<EircodeLookupResult> Unavailable(string _, CancellationToken ct) => Task.FromResult(new EircodeLookupResult(EircodeLookupStatus.Unavailable));
        public void Reset() { Calls = 0; SawTransaction = false; Handler = Unavailable; }
    }
    private sealed class RequestLookup(DirectoryDbContext db, LookupProbe probe) : IEircodeLookup
    {
        public Task<EircodeLookupResult> LookupAsync(string eircode, CancellationToken ct)
        {
            Interlocked.Increment(ref probe.Calls);
            if (db.Database.CurrentTransaction is not null) probe.SawTransaction = true;
            return probe.Handler(eircode, ct);
        }
    }
}

public sealed class LocationCoordinateEndToEndTests(LocationCoordinateApplication app) : IClassFixture<LocationCoordinateApplication>
{
    [Theory]
    [InlineData(null)]
    [InlineData("D02X285")]
    public async Task Should_DefaultFirstAndAdditionalLocationsToTown_WithoutLookingUpAnEircode(string? eircode)
    {
        await app.ResetCoordinatesAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api, 52.9m, -6.3m);
        app.Probe.Handler = (_, _) => throw new InvalidOperationException("Town coordinates must have priority");
        var customer = await CreateAsync(api, town.Id, eircode);
        var first = Assert.Single(customer.Locations);
        Assert.Equal(52.9m, first.Position!.Latitude); Assert.Equal(-6.3m, first.Position.Longitude);
        Assert.Equal(LocationPositionPrecision.Town, first.Position.Precision);
        using var additional = await api.PostAsJsonAsync($"/directory/customers/{customer.Id}/locations", new CreateLocationRequest("Second shop", town.Id, eircode));
        Assert.Equal(HttpStatusCode.Created, additional.StatusCode);
        Assert.Equal(LocationPositionPrecision.Town, (await additional.Content.ReadFromJsonAsync<LocationDetails>())!.Position!.Precision);
        Assert.Equal(0, app.Probe.Calls);
        await app.RestartAsync(); using var restarted = app.CreateApiClient();
        Assert.Equal(first.Position, (await LocationAsync(restarted, first.Id)).Position);
    }

    [Theory]
    [InlineData(EircodeLookupStatus.Found)]
    [InlineData(EircodeLookupStatus.NotFound)]
    [InlineData(EircodeLookupStatus.Unavailable)]
    public async Task Should_SaveAtomicallyWithOptionalFallback_When_TownHasNoPosition(EircodeLookupStatus outcome)
    {
        await app.ResetCoordinatesAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        app.Probe.Handler = (_, _) => Task.FromResult(new EircodeLookupResult(outcome, outcome == EircodeLookupStatus.Found ? new(53.33m, -6.25m) : null));
        var customer = await CreateAsync(api, town.Id, "D02X285"); var first = Assert.Single(customer.Locations);
        if (outcome == EircodeLookupStatus.Found)
        {
            Assert.Equal(53.33m, first.Position!.Latitude);
            Assert.Equal(LocationPositionPrecision.Eircode, first.Position.Precision);
        }
        else Assert.Null(first.Position);
        Assert.Equal(1, app.Probe.Calls); Assert.False(app.Probe.SawTransaction);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(1, await db.Customers.CountAsync()); Assert.Equal(1, await db.Locations.CountAsync()); Assert.Empty(await db.LocationPositionHistory.ToArrayAsync());
    }

    [Fact]
    public async Task Should_RecalculateAndRetainPreviousSources_When_EircodeChangesOrIsCleared()
    {
        await app.ResetCoordinatesAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        app.Probe.Handler = (code, _) => Task.FromResult(new EircodeLookupResult(EircodeLookupStatus.Found,
            code == "D02X285" ? new(53, -6) : new(54, -7)));
        var customer = await CreateAsync(api, town.Id, "D02X285"); var before = await LocationAsync(api, customer.Locations[0].Id);
        var changed = await EditAsync(api, before, "A67X123");
        Assert.Equal(54m, changed.Position!.Latitude); Assert.Equal(LocationPositionPrecision.Eircode, changed.Position.Precision);
        var cleared = await EditAsync(api, changed, null);
        Assert.Null(cleared.Position); Assert.Equal(2, app.Probe.Calls);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var history = await db.LocationPositionHistory.Where(item => item.LocationId == before.Id).ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.Contains(history, item => item.SourceEircode == "D02X285" && item.Latitude == 53);
        Assert.Contains(history, item => item.SourceEircode == "A67X123" && item.Latitude == 54);
        Assert.All(history, item => Assert.Equal(town.Id, item.SourceTownId));
    }

    [Fact]
    public async Task Should_RecalculateFromTheNewTown_AndAvoidUnrelatedEditNoise()
    {
        await app.ResetCoordinatesAsync(); using var api = app.CreateApiClient(); var firstTown = await TownAsync(api, 52, -6);
        using var created = await api.PostAsJsonAsync("/directory/geography/towns", new CreateGeographyRequest("Other town", firstTown.ParentId, 54, -8));
        var other = (await created.Content.ReadFromJsonAsync<GeographyItem>())!;
        var customer = await CreateAsync(api, firstTown.Id, "D02X285"); var before = await LocationAsync(api, customer.Locations[0].Id);
        var changed = await EditAsync(api, before, before.Eircode, other.Id);
        Assert.Equal(54m, changed.Position!.Latitude); Assert.Equal(LocationPositionPrecision.Town, changed.Position.Precision);
        var named = await EditAsync(api, changed, changed.Eircode, name: "New name");
        var noOp = await EditAsync(api, named, named.Eircode);
        Assert.Equal(changed.Position, named.Position); Assert.Equal(changed.Position, noOp.Position); Assert.Equal(0, app.Probe.Calls);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var old = Assert.Single(await db.LocationPositionHistory.Where(item => item.LocationId == before.Id).ToArrayAsync());
        Assert.Equal(firstTown.Id, old.SourceTownId); Assert.Equal(52m, old.Latitude);
    }

    [Fact]
    public async Task Should_PreserveConfirmedPosition_When_TownAndEircodeChange()
    {
        await app.ResetCoordinatesAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        var customer = await CreateAsync(api, town.Id, null); Guid id = customer.Locations[0].Id;
        await ConfirmAsync(id);
        using var otherResponse = await api.PostAsJsonAsync("/directory/geography/towns", new CreateGeographyRequest("Other", town.ParentId, 54, -8));
        var other = (await otherResponse.Content.ReadFromJsonAsync<GeographyItem>())!;
        var confirmed = await LocationAsync(api, id);
        var changed = await EditAsync(api, confirmed, "D02X285", other.Id);
        Assert.Equal(confirmed.Position, changed.Position); Assert.Equal(0, app.Probe.Calls);
        Assert.Equal(LocationPositionPrecision.ConfirmedOnSite, changed.Position!.Precision);
        await using var scope = app.Api.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().LocationPositionHistory.ToArrayAsync());
    }

    [Fact]
    public async Task Should_RejectTheWholeCreation_When_TownChangesDuringLookup()
    {
        await app.ResetCoordinatesAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        var (entered, release) = DelayLookup();
        var saving = api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Customer", new("Shop", town.Id, "D02X285")));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var updated = await api.PutAsJsonAsync($"/directory/geography/towns/{town.Id}/coordinates", new SetTownCoordinatesRequest(52, -6, town.Version)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        }
        finally { release.TrySetResult(); }
        using var stale = await saving; Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.False(app.Probe.SawTransaction);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Empty(await db.Customers.ToArrayAsync()); Assert.Empty(await db.Locations.ToArrayAsync()); Assert.Empty(await db.LocationPositionHistory.ToArrayAsync());
    }

    [Theory]
    [InlineData("name")]
    [InlineData("confirmed")]
    [InlineData("main")]
    public async Task Should_RejectADelayedResult_When_LocationChangesDuringLookup(string change)
    {
        await app.ResetCoordinatesAsync(); using var api = app.CreateApiClient(); var town = await TownAsync(api);
        var customer = await CreateAsync(api, town.Id, null); var location = await LocationAsync(api, customer.Locations[0].Id);
        var (entered, release) = DelayLookup();
        var saving = api.PutAsJsonAsync("/directory/locations/" + location.Id, new EditLocationRequest(location.Name, town.Id, "D02X285", location.Version));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Guid? main = null;
        try
        {
            if (change == "confirmed") await ConfirmAsync(location.Id).WaitAsync(TimeSpan.FromSeconds(5));
            else if (change == "main") main = await AddMainAsync(location.Id).WaitAsync(TimeSpan.FromSeconds(5));
            else await EditAsync(api, location, null, name: "Other user's name").WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.TrySetResult(); }
        using var stale = await saving; Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var winner = await LocationAsync(api, location.Id); Assert.Null(winner.Eircode);
        if (change == "name") Assert.Equal("Other user's name", winner.Name);
        if (change == "confirmed") Assert.Equal(LocationPositionPrecision.ConfirmedOnSite, winner.Position!.Precision);
        else Assert.Null(winner.Position);
        Assert.False(app.Probe.SawTransaction);
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Assert.Equal(main, (await db.Locations.SingleAsync()).MainContactId); Assert.Empty(await db.LocationPositionHistory.ToArrayAsync());
    }

    private (TaskCompletionSource Entered, TaskCompletionSource Release) DelayLookup()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously), release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Probe.Handler = async (_, ct) =>
        { entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return new(EircodeLookupStatus.Found, new(53, -7)); };
        return (entered, release);
    }
    private async Task ConfirmAsync(Guid id)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Locations SET Latitude=53.1,Longitude=-6.1,PositionPrecision=2,PositionedAt={DateTimeOffset.UtcNow},PositionSourceTownId=NULL,PositionSourceEircode=NULL WHERE Id={id}");
    }
    private async Task<Guid> AddMainAsync(Guid id)
    {
        await using var scope = app.Api.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        Guid type = Guid.NewGuid(), contact = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ContactTypes (Id,Name,IsArchived) VALUES ({type},N'Buyer',0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Contacts (Id,Name,ContactTypeId,Status) VALUES ({contact},N'Mary',{type},0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO LocationContacts (LocationId,ContactId) VALUES ({id},{contact})");
        return contact;
    }
    private static async Task<GeographyItem> TownAsync(HttpClient api, decimal? latitude = null, decimal? longitude = null)
    {
        using var regionResponse = await api.PostAsJsonAsync("/directory/geography/regions", new CreateGeographyRequest("Leinster"));
        var region = (await regionResponse.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var countyResponse = await api.PostAsJsonAsync("/directory/geography/counties", new CreateGeographyRequest("Wicklow", region.Id));
        var county = (await countyResponse.Content.ReadFromJsonAsync<GeographyItem>())!;
        using var townResponse = await api.PostAsJsonAsync("/directory/geography/towns", new CreateGeographyRequest("Laragh", county.Id, latitude, longitude));
        Assert.Equal(HttpStatusCode.Created, townResponse.StatusCode); return (await townResponse.Content.ReadFromJsonAsync<GeographyItem>())!;
    }
    private static async Task<CustomerDetails> CreateAsync(HttpClient api, Guid town, string? eircode)
    {
        using var response = await api.PostAsJsonAsync("/directory/customers", new CreateCustomerRequest("Hickey's", new("Shop", town, eircode)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CustomerDetails>())!;
    }
    private static async Task<LocationDetails> LocationAsync(HttpClient api, Guid id) => (await api.GetFromJsonAsync<LocationDetails>("/directory/locations/" + id))!;
    private static async Task<LocationDetails> EditAsync(HttpClient api, LocationDetails current, string? eircode, Guid? town = null, string? name = null)
    {
        using var response = await api.PutAsJsonAsync("/directory/locations/" + current.Id,
            new EditLocationRequest(name ?? current.Name, town ?? current.Town.Id, eircode, current.Version, LocationTypeId: current.Type?.Id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<LocationDetails>())!;
    }
}
