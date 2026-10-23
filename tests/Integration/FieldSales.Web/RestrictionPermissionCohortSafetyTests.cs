extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using static FieldSales.Web.Tests.RestrictionPermissionCohortTests;
using static FieldSales.Web.Tests.PermissionRemovalCohortTests;

namespace FieldSales.Web.Tests;

// Approved WI-030 scenarios 9 and 10: real SQL failure/rollback and current authority under concurrency.
public sealed class RestrictionPermissionCohortSafetyTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const string Pharmacy = "Pharmacy-only medicines";
    private RestrictionPermissionHost _host = null!;
    private readonly CohortSaveFailure _failure = new();
    public Task InitializeAsync() { _host = new(app, _failure); return Task.CompletedTask; }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task Should_SaveNoPartialGrants_When_AWriteFailsPartWay(int failAfter)
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        _failure.FailAfter = failAfter;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => GrantAsync(api, group, ["aoife", "brian", "colm"]));
        Assert.Equal("Injected failure after saving part of a cohort.", error.Message);
        // Both tables really contained the writes inside the transaction before the injected failure.
        Assert.Equal((failAfter, failAfter), _failure.WrittenBeforeFailure);
        Assert.Equal((0, 0), await _host.CountsAsync());
        using var retry = await GrantAsync(api, group, ["aoife", "brian", "colm"]);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_UseCurrentAuthority_When_TeamOrActorRolesChange()
    {
        var groups = await _host.ResetAsync(Pharmacy, "High-value equipment");
        using var manager = _host.ApiAs("niamh", BusinessRoles.SalesManager);
        using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        Assert.All((await GroupAsync(manager, groups[0])).Reps.Where(row => row.Rep.Subject is "aoife" or "colm"), row => Assert.True(row.CanGrant));

        // A page loaded before a team change is no authority to save afterwards.
        await MoveAsync(office, "colm", "another-manager");
        using (var staleAuthority = await GrantAsync(manager, groups[0], ["aoife", "colm"]))
            Assert.Equal(HttpStatusCode.Forbidden, staleAuthority.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
        await MoveAsync(office, "colm", "niamh");

        // A team change during an authorized cohort must wait until every grant commits.
        string version = await _host.ReportingVersionAsync("colm");
        _host.Gate.Armed = true;
        var grant = GrantAsync(manager, groups[0], ["aoife", "colm"]);
        Task<HttpResponseMessage>? move = null;
        try
        {
            await _host.Gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            move = office.PutAsJsonAsync("/coverage/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", version));
            await WaitUntilARequestIsBlockedAsync();
            Assert.False(move.IsCompleted);
        }
        finally { _host.Gate.Release.TrySetResult(); }
        using (var saved = await grant) Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using (var moved = await move!) Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal((2, 2), await _host.CountsAsync());

        Assert.False(Assert.Single((await GroupAsync(manager, groups[1])).Reps, row => row.Rep.Subject == "colm").CanGrant);
        using (var newTeam = await GrantAsync(manager, groups[1], ["aoife", "colm"]))
            Assert.Equal(HttpStatusCode.Forbidden, newTeam.StatusCode);
        // The already-issued bearer token also loses its old manager authority on the next request.
        _host.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        using (var removedRole = await GrantAsync(manager, groups[1], ["aoife"]))
            Assert.Equal(HttpStatusCode.Forbidden, removedRole.StatusCode);
        Assert.Equal((2, 2), await _host.CountsAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task Should_RollBackAllRemovals_When_APersistenceFailureOccursPartWay(int failAfter)
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        using var seed = await GrantAsync(office, group, ["aoife", "brian", "colm"]);
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);
        var held = (await seed.Content.ReadFromJsonAsync<CohortPermissionGrantResult>())!.Changes;
        _failure.FailAfter = failAfter;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RemoveAsync(office, group, held));
        Assert.Equal("Injected failure after saving part of a cohort.", error.Message);
        // Read inside the failed transaction, then from a fresh scope after rollback.
        Assert.Equal((3 + failAfter, 3 - failAfter), _failure.WrittenBeforeFailure);
        Assert.Equal((3, 3), await _host.CountsAsync());
        foreach (var old in held)
            Assert.Equal(old, Assert.Single((await RestrictionPermissionHost.PageAsync(office, old.Rep.Subject)).Groups).LastChange);
        using var retry = await RemoveAsync(office, group, held);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal((6, 0), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_KeepReportingLinesLocked_When_RemovalRacesWithATeamChange()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        using var seed = await GrantAsync(office, group, ["aoife", "colm"]);
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);
        var held = (await seed.Content.ReadFromJsonAsync<CohortPermissionGrantResult>())!.Changes;
        using var manager = _host.ApiAs("niamh", BusinessRoles.SalesManager);
        // Authority is checked before any write, including when an old review crosses a team change.
        await MoveAsync(office, "colm", "another-manager");
        using (var denied = await RemoveAsync(manager, group, held)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal((2, 2), await _host.CountsAsync());
        await MoveAsync(office, "colm", "niamh");
        string version = await _host.ReportingVersionAsync("colm");
        _host.Gate.Armed = true;
        var removal = RemoveAsync(manager, group, held);
        Task<HttpResponseMessage>? move = null;
        try
        {
            await _host.Gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            move = office.PutAsJsonAsync("/coverage/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", version));
            await WaitUntilARequestIsBlockedAsync();
            Assert.False(move.IsCompleted);
        }
        finally { _host.Gate.Release.TrySetResult(); }
        using (var saved = await removal) Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using (var moved = await move!) Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal((4, 0), await _host.CountsAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    private async Task MoveAsync(HttpClient api, string rep, string manager)
    {
        string version = await _host.ReportingVersionAsync(rep);
        using var response = await api.PutAsJsonAsync("/coverage/reporting-lines/" + rep, new SetRepReportingLineRequest(manager, version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task WaitUntilARequestIsBlockedAsync()
    {
        await using var connection = new SqlConnection(app.ConnectionString);
        await connection.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            await using var command = new SqlCommand(
                "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0 AND database_id = DB_ID()", connection);
            if ((int)(await command.ExecuteScalarAsync())! > 0) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("No team change was blocked by the cohort write.");
            await Task.Delay(50);
        }
    }

    private sealed class CohortSaveFailure : SaveChangesInterceptor
    {
        public int FailAfter { get; set; }
        public (int Changes, int Held) WrittenBeforeFailure { get; private set; }
        private int _saves;
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (FailAfter > 0 && ++_saves == FailAfter)
            {
                var db = (CatalogueDbContext)eventData.Context!;
                WrittenBeforeFailure = (await db.RestrictionPermissionChanges.CountAsync(ct), await db.RestrictionPermissions.CountAsync(ct));
                FailAfter = 0;
                throw new InvalidOperationException("Injected failure after saving part of a cohort.");
            }
            return result;
        }
    }
}
