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

public sealed class RetiredTownPreflightApplication(DirectorySqlServerFixture sql) : GeographyApplication(sql)
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
