extern alias CatalogueApi;

using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using RepReportingLine = CatalogueApi::FieldSales.Api.Coverage.RepReportingLine;
using RestrictionGroup = CatalogueApi::FieldSales.Api.Catalogue.RestrictionGroup;

namespace FieldSales.Web.Tests;

// One coverage API host per WI-029 test. The API's Aspire registration reads ConnectionStrings:CatalogueDb
// before test configuration applies, so the host points the catalogue at the fixture's database the way
// the catalogue fixtures do. Niamh manages Colm and Aoife. Ciara and Sorcha (a selling manager) report to
// another manager. Orla is a Head Office User with no team.
public sealed class RestrictionPermissionHost : IAsyncDisposable
{
    private readonly CoverageReadApplication _app;

    public RestrictionPermissionHost(CoverageReadApplication app)
    {
        _app = app;
        Api = app.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CatalogueDbContext>();
            services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlServer(app.ConnectionString).AddInterceptors(Gate).Options));
        }));
    }

    public WebApplicationFactory<DirectoryDbContext> Api { get; }
    public CommitGate Gate { get; } = new();

    public async Task<Guid[]> ResetAsync(params string[] groupNames)
    {
        await _app.ResetCoverageAsync();
        _app.Staff.Entries["ciara"] = new("ciara", "Ciara", [BusinessRoles.FieldSalesperson], true);
        _app.Staff.Entries["sorcha"] = new("sorcha", "Sorcha", [BusinessRoles.SalesManager, BusinessRoles.FieldSalesperson], true);
        _app.Staff.Entries["orla"] = new("orla", "Orla Kenny", [BusinessRoles.HeadOfficeUser], true);
        await using var scope = Api.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        directory.RepReportingLines.AddRange(RepReportingLine.Create("ciara", "another-manager"),
            RepReportingLine.Create("sorcha", "another-manager"));
        await directory.SaveChangesAsync();
        var catalogue = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await catalogue.Database.MigrateAsync();
        // Privileged test-database reset; the record's DML append-only guards stay enabled.
        await catalogue.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [RestrictionPermissionChanges]");
        await catalogue.RestrictionPermissions.ExecuteDeleteAsync();
        await catalogue.ProductBasePrices.ExecuteDeleteAsync();
        await catalogue.Products.ExecuteDeleteAsync();
        await catalogue.Categories.ExecuteDeleteAsync(); // the tests only add top-level categories
        await catalogue.RestrictionGroups.ExecuteDeleteAsync();
        var groups = groupNames.Select(RestrictionGroup.Create).ToArray();
        catalogue.RestrictionGroups.AddRange(groups);
        await catalogue.SaveChangesAsync();
        _app.Reads.Commands.Clear();
        return groups.Select(group => group.Id).ToArray();
    }

    public void SetRoles(string subject, params string[] roles)
    {
        _app.Roles.SetRoles(subject, roles);
        _app.Staff.Entries[subject] = _app.Staff.Entries[subject] with { Roles = roles };
    }

    public HttpClient ApiAs(string subject, params string[] roles)
    {
        SetRoles(subject, roles);
        var client = Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", _app.Token(roles[0], subject: subject));
        return client;
    }

    // The website signs Niamh in and calls this host's API with her access token.
    public StaffWebsiteFactory CreateWebsite() => new(Api.Server.CreateHandler(), _app.Token());

    public async Task<(int Changes, int Held)> CountsAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        return (await db.RestrictionPermissionChanges.CountAsync(), await db.RestrictionPermissions.CountAsync());
    }

    public async Task ArchiveAsync(Guid group, bool archived = true)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().RestrictionGroups.Where(row => row.Id == group)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.IsArchived, archived));
    }

    public async Task<string> ReportingVersionAsync(string rep)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var line = await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().RepReportingLines.AsNoTracking()
            .SingleAsync(row => row.RepSubject == rep);
        return Convert.ToBase64String(line.Version);
    }

    public static Task<HttpResponseMessage> ChangeAsync(HttpClient api, string rep, Guid group,
        RestrictionPermissionChangeKind change, string? reason = null, Guid? expectedLastChangeId = null) =>
        api.PostAsJsonAsync($"/coverage/reps/{Uri.EscapeDataString(rep)}/permissions/{group}",
            new ChangeRestrictionPermissionRequest(change, reason, expectedLastChangeId));

    public static async Task<RestrictionPermissionChangeDetails> SavedAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestrictionPermissionChangeDetails>())!;
    }

    public static async Task<RepPermissionsPage> PageAsync(HttpClient api, string rep) =>
        (await api.GetFromJsonAsync<RepPermissionsPage>($"/coverage/reps/{Uri.EscapeDataString(rep)}/permissions"))!;

    public ValueTask DisposeAsync() => Api.DisposeAsync();
}

// Pauses the next catalogue commit until released, so a test can act while a permission write holds its locks.
public sealed class CommitGate : DbTransactionInterceptor
{
    private int _armed;
    public bool Armed { set => Volatile.Write(ref _armed, value ? 1 : 0); }
    public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        return result;
    }
}
