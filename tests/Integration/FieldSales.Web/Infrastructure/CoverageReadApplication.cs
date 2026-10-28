extern alias CatalogueApi;

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using RepReportingLine = CatalogueApi::FieldSales.Api.Coverage.RepReportingLine;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;
using AssignmentHistory = CatalogueApi::FieldSales.Api.Coverage.AssignmentHistory;

namespace FieldSales.Web.Tests;

public sealed class CoverageReadApplication(DirectorySqlServerFixture sql) : GeographyApplication(sql)
{
    protected override bool UseTestLocationUsage => false;
    public ReadProbe Reads { get; } = new();
    public DateTimeOffset? ClockOverride { get; set; }
    private sealed class CoverageClock(CoverageReadApplication app) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => app.ClockOverride ?? DateTimeOffset.UtcNow; }

    protected override void ConfigureAdditionalServices(IServiceCollection services, string connectionString)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new CoverageClock(this));
        services.RemoveAll<DirectoryDbContext>();
        services.AddScoped(_ => new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(Reads, Failures).Options));
        services.RemoveAll<IStaffDirectory>();
        services.AddScoped<IStaffDirectory>(provider => new CoverageStaffDirectory(
            provider.GetRequiredService<DirectoryDbContext>(), Staff));
    }

    public async Task ResetCoverageAsync()
    {
        Roles.Unavailable = false; ClockOverride = null;
        await using (var scope = Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            // Privileged test-database reset; DML append-only guards stay enabled.
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [AssignmentHistory]");
            await db.TerritoryAssignments.ExecuteDeleteAsync();
            await db.RepReportingLines.ExecuteDeleteAsync();
            await db.LocationPositionHistory.ExecuteDeleteAsync();
        }
        await ResetAsync();
        Staff.Reset();
        Failures.FailAfterHistorySave = false; Failures.DidFailAfterSave = false;
        await using var seedScope = Api.Services.CreateAsyncScope();
        var seed = seedScope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        seed.RepReportingLines.AddRange(RepReportingLine.Create("colm", "niamh"),
            RepReportingLine.Create("aoife", "niamh"), RepReportingLine.Create("brian", "another-manager"));
        await seed.SaveChangesAsync();
        Reads.Commands.Clear();
    }

    public CoverageStaffState Staff { get; } = new();
    public HistoryFailureProbe Failures { get; } = new();

    public sealed class HistoryFailureProbe : SaveChangesInterceptor
    {
        public bool FailAfterHistorySave { get; set; }
        public bool DidFailAfterSave { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (FailAfterHistorySave && eventData.Context!.ChangeTracker.Entries<AssignmentHistory>().Any())
            {
                FailAfterHistorySave = false; DidFailAfterSave = true;
                throw new InvalidOperationException("Injected failure after history save.");
            }
            return base.SavedChangesAsync(eventData, result, ct);
        }
    }

    public sealed class CoverageStaffState
    {
        public Dictionary<string, StaffDirectoryEntry> Entries { get; } = new(StringComparer.Ordinal);
        public bool Unavailable { get; set; }
        public bool SawTransaction { get; set; }
        public int Calls { get; set; }
        public void Reset()
        {
            Entries.Clear(); Unavailable = false; SawTransaction = false; Calls = 0;
            Entries.Add("niamh", new("niamh", "Niamh Byrne", [BusinessRoles.HeadOfficeUser], true));
            Entries.Add("colm", new("colm", "Colm", [BusinessRoles.FieldSalesperson], true));
            Entries.Add("aoife", new("aoife", "Aoife", [BusinessRoles.FieldSalesperson], true));
            Entries.Add("brian", new("brian", "Brian", [BusinessRoles.FieldSalesperson], true));
            Entries.Add("another-manager", new("another-manager", "Other Manager", [BusinessRoles.SalesManager], true));
        }
    }

    private sealed class CoverageStaffDirectory(DirectoryDbContext db, CoverageStaffState staff) : IStaffDirectory
    {
        public Task<IReadOnlyList<StaffDirectoryEntry>> ListAsync(string accessToken, CancellationToken ct) =>
            LookupAsync(accessToken, staff.Entries.Keys.ToArray(), ct);
        public Task<IReadOnlyList<StaffDirectoryEntry>> LookupAsync(string accessToken, IReadOnlyCollection<string> subjects, CancellationToken ct)
        {
            staff.Calls++; staff.SawTransaction |= db.Database.CurrentTransaction is not null;
            if (staff.Unavailable) throw new HttpRequestException("Test staff directory unavailable.");
            return Task.FromResult<IReadOnlyList<StaffDirectoryEntry>>(subjects.Where(staff.Entries.ContainsKey)
                .Select(subject => staff.Entries[subject]).ToArray());
        }
    }

    public sealed class ReadProbe : DbCommandInterceptor
    {
        public ConcurrentQueue<IsolationLevel?> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.Transaction?.IsolationLevel);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

// Real SQL, real API JWT/current-role boundary; assignment writes are setup only.
// Production add/remove/history operations belong to later approved increments.
