using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using static FieldSales.Web.Tests.RestrictionPermissionHost;
using static FieldSales.Web.Tests.RestrictionPermissionCohortTests;

namespace FieldSales.Web.Tests;

// WI-031 scenarios 8–14/17: the existing rule/writer must remove a whole cohort or nothing.
[Collection(DirectorySqlServerCollection.Name)]
public sealed class PermissionRemovalCohortTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const string Pharmacy = "Pharmacy-only medicines";
    private RestrictionPermissionHost? _host;
    public Task InitializeAsync() { _host = new(app); return Task.CompletedTask; }
    public async Task DisposeAsync()
    {
        var host = Interlocked.Exchange(ref _host, null);
        if (host is not null) await host.DisposeAsync();
    }

    [Fact]
    public async Task Should_RemoveAllSelectedAndKeepUnselected_When_ConfirmingACohort()
    {
        var (group, held) = await SeedAsync("colm", "aoife", "ciara");
        using var manager = _host!.ApiAs("niamh", BusinessRoles.SalesManager);
        var page = await GroupAsync(manager, group);
        Assert.All(page.Reps.Where(row => row.Rep.Subject is "colm" or "aoife"), row => Assert.True(row.CanRemove));
        Assert.False(Assert.Single(page.Reps, row => row.Rep.Subject == "ciara").CanRemove);
        Assert.All(page.Reps.Where(row => !row.Granted), row => Assert.False(row.CanRemove));
        app.ClockOverride = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        using var response = await RemoveAsync(manager, group, [held["colm"], held["aoife"]], "  Training expired  ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<CohortPermissionRemovalResult>())!;
        Assert.Equal(group, saved.RestrictionGroupId);
        Assert.Equal(["aoife", "colm"], saved.Changes.Select(change => change.Rep.Subject));
        Assert.Equal(2, saved.Changes.Select(change => change.Id).Distinct().Count());
        Assert.All(saved.Changes, change =>
        {
            Assert.Equal((RestrictionPermissionChangeKind.Removed, "niamh", "Niamh Byrne", "Training expired", app.ClockOverride),
                (change.Change, change.Actor.Subject, change.Actor.DisplayName, change.Reason, (DateTimeOffset?)change.ChangedAt));
        });
        Assert.Equal((5, 1), await _host!.CountsAsync());
        var after = await GroupAsync(manager, group);
        foreach (var change in saved.Changes)
        {
            var row = Assert.Single(after.Reps, row => row.Rep.Subject == change.Rep.Subject);
            Assert.False(row.Granted); Assert.False(row.CanRemove); Assert.True(row.CanGrant);
            Assert.Equal(change, row.LastChange);
            var perRep = Assert.Single((await PageAsync(manager, change.Rep.Subject)).Groups);
            Assert.False(perRep.Granted); Assert.Equal(change, perRep.LastChange);
        }
        var unselected = Assert.Single(after.Reps, row => row.Rep.Subject == "ciara");
        Assert.True(unselected.Granted); Assert.Equal(held["ciara"], unselected.LastChange);
        using var replay = await RemoveAsync(manager, group, [held["colm"], held["aoife"]]);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal((5, 1), await _host!.CountsAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    [Theory]
    [InlineData("empty")] [InlineData("duplicate")] [InlineData("stale")] [InlineData("not-held")]
    [InlineData("blank")] [InlineData("control")] [InlineData("null-list")] [InlineData("null-row")]
    public async Task Should_RejectTheWholeRemoval_When_SelectionIsEmptyDuplicateStaleOrNotHeld(string kind)
    {
        var (group, held) = await SeedAsync("aoife", "colm");
        using var office = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        var aoife = new CohortPermissionSelection("aoife", held["aoife"].Id);
        IReadOnlyList<CohortPermissionSelection> selection = kind switch
        {
            "empty" => [], "duplicate" => [aoife, aoife],
            // Aoife is saved first; the later failure must roll her removal and history back.
            "stale" => [aoife, new("colm", Guid.NewGuid())], "not-held" => [aoife, new("brian", null)],
            "blank" => [aoife, new(" ", null)], "control" => [aoife, new("co\nlm", null)],
            "null-list" => null!, _ => [aoife, null!]
        };
        using var response = await office.PostAsJsonAsync(Path(group) + "/removals", new RemoveCohortPermissionRequest(selection, null));
        Assert.Equal(kind is "stale" or "not-held" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((2, 2), await _host!.CountsAsync());
        foreach (string rep in new[] { "aoife", "colm" })
        {
            var row = Assert.Single((await PageAsync(office, rep)).Groups);
            Assert.True(row.Granted); Assert.Equal(held[rep], row.LastChange);
        }
    }

    [Theory]
    [InlineData("outside-team")] [InlineData("manager-self")] [InlineData("office-self")]
    public async Task Should_RejectTheWholeRemoval_When_SelectionIncludesAnOutsideTeamRepOrTheActor(string kind)
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        _host!.SetRoles("niamh", BusinessRoles.SalesManager, BusinessRoles.FieldSalesperson);
        _host!.SetRoles("orla", BusinessRoles.HeadOfficeUser, BusinessRoles.FieldSalesperson);
        using var seeder = _host!.ApiAs("another-manager", BusinessRoles.HeadOfficeUser);
        string forbidden = kind switch { "outside-team" => "ciara", "manager-self" => "niamh", _ => "orla" };
        using var seeded = await GrantAsync(seeder, group, ["aoife", forbidden]);
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var held = (await seeded.Content.ReadFromJsonAsync<CohortPermissionGrantResult>())!.Changes;
        using var actor = kind == "office-self" ? _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser, BusinessRoles.FieldSalesperson)
            : _host!.ApiAs("niamh", BusinessRoles.SalesManager, BusinessRoles.FieldSalesperson);
        Assert.False(Assert.Single((await GroupAsync(actor, group)).Reps, row => row.Rep.Subject == forbidden).CanRemove);
        using var denied = await RemoveAsync(actor, group, held);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal((2, 2), await _host!.CountsAsync());
    }

    [Theory]
    [InlineData("archived")] [InlineData("inactive")] [InlineData("former-rep")] [InlineData("both")]
    public async Task Should_AllowRemovalWithAccurateConsequence_When_GroupIsArchivedOrHolderInactive(string kind)
    {
        var (group, held) = await SeedAsync("aoife", "colm");
        using var manager = _host!.ApiAs("niamh", BusinessRoles.SalesManager);
        bool archived = kind is "archived" or "both";
        if (archived) await _host!.ArchiveAsync(group);
        if (kind is "inactive" or "both") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        if (kind == "former-rep") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Roles = [] };
        var page = await GroupAsync(manager, group);
        Assert.All(page.Reps.Where(row => row.Granted), row => Assert.True(row.CanRemove));
        if (archived)
        {
            Assert.Empty((await manager.GetFromJsonAsync<PermissionGroupChoice[]>("/coverage/permissions/groups"))!);
            var chooser = (await manager.GetFromJsonAsync<PermissionGroupChoice[]>("/coverage/permissions/groups?includeArchived=true"))!;
            Assert.True(Assert.Single(chooser).Archived);
        }
        using var removed = await RemoveAsync(manager, group, held.Values.ToArray());
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal((4, 0), await _host!.CountsAsync());
        if (archived)
            Assert.Empty((await manager.GetFromJsonAsync<PermissionGroupChoice[]>("/coverage/permissions/groups?includeArchived=true"))!);
    }

    [Theory]
    [InlineData("identity")] [InlineData("unknown")] [InlineData("rep-role")]
    [InlineData("missing-scope")] [InlineData("anonymous")]
    public async Task Should_RecordNothing_When_IdentityFailsOrRequestsAreUnauthorized(string kind)
    {
        var (group, held) = await SeedAsync("aoife", "colm");
        using var actor = _host!.ApiAs(kind == "rep-role" ? "brian" : "niamh",
            kind == "rep-role" ? BusinessRoles.FieldSalesperson : BusinessRoles.SalesManager);
        if (kind == "identity") app.Staff.Unavailable = true;
        if (kind == "unknown") app.Staff.Entries.Remove("colm");
        if (kind == "missing-scope") actor.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(BusinessRoles.SalesManager, scope: "", subject: "niamh"));
        if (kind == "anonymous") actor.DefaultRequestHeaders.Authorization = null;
        using var denied = await RemoveAsync(actor, group, held.Values.ToArray());
        Assert.Equal(kind is "identity" or "unknown" ? HttpStatusCode.ServiceUnavailable : kind == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden,
            denied.StatusCode);
        Assert.Equal((2, 2), await _host!.CountsAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    [Theory]
    [InlineData(null, true)] [InlineData("   ", true)] [InlineData("  Training expired  ", true)]
    [InlineData("limit", true)] [InlineData("over-limit", false)] [InlineData("Reason\nwith control", false)]
    public async Task Should_PreserveReasonRules_When_ReviewingAndConfirmingRemoval(string? reason, bool accepted)
    {
        var (group, held) = await SeedAsync("aoife", "colm");
        using var manager = _host!.ApiAs("niamh", BusinessRoles.SalesManager);
        reason = reason switch { "limit" => new string('r', 1000), "over-limit" => new string('r', 1001), _ => reason };
        using var response = await RemoveAsync(manager, group, held.Values.ToArray(), reason);
        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(accepted ? (4, 0) : (2, 2), await _host!.CountsAsync());
        if (accepted)
            Assert.All((await response.Content.ReadFromJsonAsync<CohortPermissionRemovalResult>())!.Changes,
                change => Assert.Equal(string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), change.Reason));
    }

    private async Task<(Guid Group, Dictionary<string, RestrictionPermissionChangeDetails> Held)> SeedAsync(params string[] reps)
    {
        var group = (await _host!.ResetAsync(Pharmacy))[0];
        using var office = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        using var seeded = await GrantAsync(office, group, reps);
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        return (group, (await seeded.Content.ReadFromJsonAsync<CohortPermissionGrantResult>())!.Changes.ToDictionary(change => change.Rep.Subject));
    }

    internal static Task<HttpResponseMessage> RemoveAsync(HttpClient api, Guid group,
        IReadOnlyList<RestrictionPermissionChangeDetails> held, string? reason = "Training expired") =>
        api.PostAsJsonAsync(Path(group) + "/removals", new RemoveCohortPermissionRequest(
            held.Select(change => new CohortPermissionSelection(change.Rep.Subject, change.Id)).ToArray(), reason));
}
