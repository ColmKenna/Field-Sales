extern alias CatalogueApi;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using IEircodeLookup = CatalogueApi::FieldSales.Api.Directory.IEircodeLookup;
using EircodeLookupResult = CatalogueApi::FieldSales.Api.Directory.EircodeLookupResult;
using EircodeLookupStatus = CatalogueApi::FieldSales.Api.Directory.EircodeLookupStatus;
using Coordinates = CatalogueApi::FieldSales.Api.Directory.Coordinates;

namespace FieldSales.Web.Tests;

public sealed class LocationCoordinateApplication(DirectorySqlServerFixture sql) : GeographyApplication(sql)
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
