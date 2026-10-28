using System.Data;
using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using static FieldSales.Web.Tests.RestrictionPermissionHost;

namespace FieldSales.Web.Tests;

// WI-029 increment 2: who may grant or remove a rep's Restriction Permission, through the real API and SQL.
// The people and their teams are described on RestrictionPermissionHost.
public sealed class RestrictionPermissionIntegrationTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const RestrictionPermissionChangeKind Granted = RestrictionPermissionChangeKind.Granted;
    private const RestrictionPermissionChangeKind Removed = RestrictionPermissionChangeKind.Removed;
    private const string Pharmacy = "Pharmacy-only medicines";
    private const string HighValue = "High-value equipment";
    private const string Training = "Completed training 15 Sep 2026";
    private RestrictionPermissionHost? _host;

    public Task InitializeAsync()
    {
        _host = new(app);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        var host = Interlocked.Exchange(ref _host, null);
        if (host is not null) await host.DisposeAsync();
    }

    [Fact]
    public async Task Should_TheRecordShowsDateMyNameAndTheReason_When_IGrantColmPharmacyOnlyMedicinesWithReasonCompletedTraining15Sep2026()
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        app.ClockOverride = new DateTimeOffset(2026, 9, 15, 14, 2, 30, TimeSpan.Zero);
        using var api = _host!.ApiAs("niamh", BusinessRoles.SalesManager); // Colm's own manager, not Head Office

        var entry = await SavedAsync(ChangeAsync(api, "colm", group, Granted, Training));

        Assert.Equal("Granted 15 Sep 2026 14:02 UTC by Niamh Byrne — Completed training 15 Sep 2026", entry.Display);
        Assert.Equal((Granted, new HistoryIdentityDetails("niamh", "Niamh Byrne"), Training), (entry.Change, entry.Actor, entry.Reason));
        Assert.Equal(new DateTimeOffset(2026, 9, 15, 14, 2, 30, TimeSpan.Zero), entry.ChangedAt);
        var row = Assert.Single((await PageAsync(api, "colm")).Groups);
        Assert.True(row.Granted);
        Assert.Equal(entry, row.LastChange);
        Assert.Equal((1, 1), await _host!.CountsAsync());
        // Identity is read before any transaction; the reporting line is read under the serializable lock.
        Assert.False(app.Staff.SawTransaction);
        Assert.NotEmpty(app.Reads.Commands);
        Assert.All(app.Reads.Commands, isolation => Assert.Equal(IsolationLevel.Serializable, isolation));
    }

    [Fact]
    public async Task Should_ForbidAndRecordNothing_When_AManagerOutsideTheTeamPostsAChange()
    {
        var groups = await _host!.ResetAsync(Pharmacy, HighValue);
        using (var headOffice = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser))
            await SavedAsync(ChangeAsync(headOffice, "ciara", groups[0], Granted, Training));
        using var api = _host!.ApiAs("niamh", BusinessRoles.SalesManager);

        var page = await PageAsync(api, "ciara");
        Assert.False(page.CanChange);
        Assert.Equal([HighValue, Pharmacy], page.Groups.Select(row => row.Name));
        Assert.All(page.Groups, row => Assert.False(row.CanGrant || row.CanRemove));
        var held = Assert.Single(page.Groups, row => row.Granted).LastChange!;
        Assert.EndsWith("by Orla Kenny — Completed training 15 Sep 2026", held.Display);
        using (var grant = await ChangeAsync(api, "ciara", groups[1], Granted, Training))
            Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
        using (var removal = await ChangeAsync(api, "ciara", groups[0], Removed, null, held.Id))
            Assert.Equal(HttpStatusCode.Forbidden, removal.StatusCode);
        Assert.Equal((1, 1), await _host!.CountsAsync());

        var reps = (await api.GetFromJsonAsync<PermissionRepChoice[]>("/coverage/permissions/reps"))!;
        Assert.Equal(["aoife", "brian", "ciara", "colm", "sorcha"], reps.Select(choice => choice.Rep.Subject));
        Assert.Equal(["aoife", "colm"], reps.Where(choice => choice.CanChange).Select(choice => choice.Rep.Subject));
        using var notRep = await api.GetAsync("/coverage/reps/orla/permissions");
        Assert.Equal(HttpStatusCode.NotFound, notRep.StatusCode);
    }

    [Fact]
    public async Task Should_LetHeadOfficeChangeAnyRep_When_TheRepIsOutsideTheirTeamOrIsASellingManager()
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        app.Staff.Entries["dara"] = new("dara", "Dara", [BusinessRoles.FieldSalesperson], true); // no reporting line
        using var headOffice = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser);

        foreach (string rep in new[] { "ciara", "sorcha", "colm", "dara" })
        {
            Assert.True((await PageAsync(headOffice, rep)).CanChange);
            await SavedAsync(ChangeAsync(headOffice, rep, group, Granted, Training));
        }
        // The selling manager's own manager can change her permissions too.
        using var manager = _host!.ApiAs("another-manager", BusinessRoles.SalesManager);
        var sorcha = Assert.Single((await PageAsync(manager, "sorcha")).Groups);
        Assert.True(sorcha.CanRemove);
        await SavedAsync(ChangeAsync(manager, "sorcha", group, Removed, "Moved to the hardware team", sorcha.LastChange!.Id));

        Assert.Equal((5, 3), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_RefuseAndRecordNothing_When_ActorChangesTheirOwnPermissions()
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        using var seller = _host!.ApiAs("sorcha", BusinessRoles.SalesManager, BusinessRoles.FieldSalesperson);
        var own = await PageAsync(seller, "sorcha");
        Assert.False(own.CanChange);
        Assert.False(Assert.Single(own.Groups).CanGrant);
        using (var refused = await ChangeAsync(seller, "sorcha", group, Granted, Training))
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        // A Head Office User who also sells cannot change their own permissions either.
        using var headOffice = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser, BusinessRoles.FieldSalesperson);
        Assert.False((await PageAsync(headOffice, "orla")).CanChange);
        using (var refused = await ChangeAsync(headOffice, "orla", group, Granted, Training))
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal((0, 0), await _host!.CountsAsync());

        // Someone else may change them.
        await SavedAsync(ChangeAsync(headOffice, "sorcha", group, Granted, Training));
        Assert.Equal((1, 1), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_UseTheCurrentTeamAndRoles_When_ReportingLineOrRoleChangesBetweenRequests()
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        using var manager = _host!.ApiAs("niamh", BusinessRoles.SalesManager);
        using var headOffice = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        string version = await _host!.ReportingVersionAsync("colm");

        // Niamh's grant waits just before its catalogue commit while Orla moves Colm to another team.
        _host!.Gate.Armed = true;
        var grant = ChangeAsync(manager, "colm", group, Granted, Training);
        await _host!.Gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var move = headOffice.PutAsJsonAsync("/coverage/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", version));
        await WaitUntilARequestIsBlockedAsync();
        Assert.False(move.IsCompleted);
        _host!.Gate.Release.SetResult();
        var granted = await SavedAsync(grant);
        using (var moved = await move) Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        // The next request uses Colm's new team.
        Assert.False((await PageAsync(manager, "colm")).CanChange);
        using (var denied = await ChangeAsync(manager, "colm", group, Removed, null, granted.Id))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var newManager = _host!.ApiAs("another-manager", BusinessRoles.SalesManager);
        var removed = await SavedAsync(ChangeAsync(newManager, "colm", group, Removed, null, granted.Id));
        // A removed role applies on the next request too.
        _host!.SetRoles("another-manager", BusinessRoles.FieldSalesperson);
        using (var denied = await ChangeAsync(newManager, "colm", group, Granted, Training, removed.Id))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal((2, 0), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_FailClosedAndRecordNothing_When_IdentityIsUnavailableOrRepIsNotAnActiveFieldSalesperson()
    {
        var groups = await _host!.ResetAsync(Pharmacy, HighValue);
        using var api = _host!.ApiAs("niamh", BusinessRoles.HeadOfficeUser);
        app.Staff.Unavailable = true;
        using (var unavailable = await ChangeAsync(api, "colm", groups[0], Granted, Training))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        app.Staff.Unavailable = false;
        using (var unknown = await ChangeAsync(api, "nobody", groups[0], Granted, Training))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unknown.StatusCode);
        using (var notRep = await ChangeAsync(api, "another-manager", groups[0], Granted, Training))
        {
            Assert.Equal(HttpStatusCode.BadRequest, notRep.StatusCode);
            Assert.Equal("RepSubject", (await notRep.Content.ReadFromJsonAsync<CoverageError>())!.Field);
        }
        Assert.Equal((0, 0), await _host!.CountsAsync());

        // Once Brian is no longer active he cannot be granted, but what he holds can still be removed.
        var granted = await SavedAsync(ChangeAsync(api, "brian", groups[0], Granted, Training));
        app.Staff.Entries["brian"] = app.Staff.Entries["brian"] with { Available = false };
        var page = await PageAsync(api, "brian");
        Assert.False(page.Active);
        Assert.True(Assert.Single(page.Groups, row => row.Granted).CanRemove);
        Assert.All(page.Groups, row => Assert.False(row.CanGrant));
        Assert.False(Assert.Single((await api.GetFromJsonAsync<PermissionRepChoice[]>("/coverage/permissions/reps"))!,
            choice => choice.Rep.Subject == "brian").Active);
        using (var inactive = await ChangeAsync(api, "brian", groups[1], Granted, Training))
            Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        await SavedAsync(ChangeAsync(api, "brian", groups[0], Removed, "Account suspended", granted.Id));
        Assert.DoesNotContain((await api.GetFromJsonAsync<PermissionRepChoice[]>("/coverage/permissions/reps"))!,
            choice => choice.Rep.Subject == "brian");
        Assert.Equal((2, 0), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_RefuseAndRecordNothing_When_ThePermissionChangedSinceThePageWasLoaded()
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        using var api = _host!.ApiAs("niamh", BusinessRoles.HeadOfficeUser);
        var first = await SavedAsync(ChangeAsync(api, "colm", group, Granted, Training));

        // Submitting the same page again: it still expects no change, so nothing more is recorded.
        using (var repeat = await ChangeAsync(api, "colm", group, Granted, Training))
        {
            Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
            Assert.Equal("This permission changed. Reload before saving.", (await repeat.Content.ReadFromJsonAsync<CoverageError>())!.Error);
        }
        using (var wrongPage = await ChangeAsync(api, "colm", group, Removed, null, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.Conflict, wrongPage.StatusCode);
        using (var noChange = await ChangeAsync(api, "colm", group, Granted, null, first.Id))
            Assert.Equal(HttpStatusCode.Conflict, noChange.StatusCode);
        var removed = await SavedAsync(ChangeAsync(api, "colm", group, Removed, null, first.Id));
        using (var doubleSubmit = await ChangeAsync(api, "colm", group, Removed, null, first.Id))
            Assert.Equal(HttpStatusCode.Conflict, doubleSubmit.StatusCode);
        Assert.Equal((2, 0), await _host!.CountsAsync());

        // Two grants from the same page at once: one is saved, the other is told to reload.
        using var other = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        var racing = await Task.WhenAll(ChangeAsync(api, "colm", group, Granted, "First", removed.Id),
            ChangeAsync(other, "colm", group, Granted, "Second", removed.Id));
        Assert.Single(racing, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(racing, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in racing) response.Dispose();
        Assert.Equal((3, 1), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_RefuseGrantButRecordRemoval_When_TheGroupIsArchived()
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        using var api = _host!.ApiAs("niamh", BusinessRoles.HeadOfficeUser);
        var granted = await SavedAsync(ChangeAsync(api, "colm", group, Granted, Training));
        await _host!.ArchiveAsync(group);

        using (var refused = await ChangeAsync(api, "aoife", group, Granted, Training))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("RestrictionGroupId", (await refused.Content.ReadFromJsonAsync<CoverageError>())!.Field);
        }
        // An archived group appears only for reps who still hold it, and can only be removed.
        Assert.Empty((await PageAsync(api, "aoife")).Groups);
        var held = Assert.Single((await PageAsync(api, "colm")).Groups);
        Assert.Equal((true, true, true, false), (held.Archived, held.Granted, held.CanRemove, held.CanGrant));
        var removed = await SavedAsync(ChangeAsync(api, "colm", group, Removed, "Left the pharmacy team", granted.Id));
        Assert.Equal(RestrictionPermissionChangeKind.Removed, removed.Change);
        Assert.Empty((await PageAsync(api, "colm")).Groups);
        using (var missing = await ChangeAsync(api, "colm", Guid.NewGuid(), Granted, Training))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((2, 0), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_DenyFieldSalespeople_When_TheyReadOrChangePermissions()
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        using var rep = _host!.ApiAs("colm", BusinessRoles.FieldSalesperson);
        foreach (string path in new[] { "/coverage/permissions/reps", "/coverage/reps/colm/permissions", "/coverage/reps/aoife/permissions" })
        {
            using var read = await rep.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        }
        using (var change = await ChangeAsync(rep, "aoife", group, Granted, Training))
            Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
        // A Head Office token without the Field Sales API scope is refused too.
        using var unscoped = _host!.Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        unscoped.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(BusinessRoles.HeadOfficeUser, scope: "openid"));
        using (var read = await unscoped.GetAsync("/coverage/reps/colm/permissions"))
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using (var change = await ChangeAsync(unscoped, "colm", group, Granted, Training))
            Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
        Assert.Equal((0, 0), await _host!.CountsAsync());
    }

    // Proves a request is waiting on a SQL lock in this database, rather than inferring it from timing.
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
            if (DateTime.UtcNow > deadline) throw new TimeoutException("No request was blocked by the permission write.");
            await Task.Delay(50);
        }
    }
}
