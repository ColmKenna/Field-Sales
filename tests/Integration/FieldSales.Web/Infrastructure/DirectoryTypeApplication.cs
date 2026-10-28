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
using ContactTypeUsageSource = CatalogueApi::FieldSales.Api.Directory.ContactTypeUsageSource;

namespace FieldSales.Web.Tests;

public sealed class DirectoryTypeApplication(DirectorySqlServerFixture sql) : GeographyApplication(sql)
{
    protected override bool UseTestLocationUsage => false;
    public DirectoryTypeTestContacts Contacts { get; } = new();
    public string Registration { get; set; } = "valid";
    private string? _catalogueConnectionString;
    protected override void ConfigureAdditionalServices(IServiceCollection services, string connectionString)
    {
        foreach (var descriptor in services.Where(service => service.IsKeyedService
            && Equals(service.ServiceKey, "directory-types") && service.KeyedImplementationType == typeof(ContactTypeUsageSource)).ToArray())
            services.Remove(descriptor);
        if (Registration != "missing") services.AddKeyedSingleton<IReferenceUsageSource>("directory-types", Contacts);
        if (Registration == "duplicate") services.AddKeyedSingleton<IReferenceUsageSource>("directory-types", new DirectoryTypeTestContacts());
        _catalogueConnectionString ??= new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "DirectoryTypeCatalogueTests_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        services.RemoveAll<CatalogueDbContext>();
        services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>().UseSqlServer(_catalogueConnectionString).Options));
    }
    protected override async Task InitializeAdditionalAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
    }
}
