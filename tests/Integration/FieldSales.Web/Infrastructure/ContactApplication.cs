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

public sealed class ContactApplication(DirectorySqlServerFixture sql) : GeographyApplication(sql)
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
