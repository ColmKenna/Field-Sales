using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using static FieldSales.Web.Tests.RestrictionPermissionHost;

namespace FieldSales.Web.Tests;

// WI-030: one action must either grant every selected rep or record nothing.
public sealed class RestrictionPermissionCohortTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const string Pharmacy = "Pharmacy-only medicines";
    private const string Training = "Completed training 15 Sep 2026";
    private static readonly DateTimeOffset TrainingDay = new(2026, 9, 15, 14, 2, 30, TimeSpan.Zero);
    private RestrictionPermissionHost _host = null!;
    public Task InitializeAsync() { _host = new(app); return Task.CompletedTask; }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Should_RecordFiveSeparateGrants_When_GrantingToFiveSelectedReps()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        _host.SetRoles("niamh", BusinessRoles.SalesManager, BusinessRoles.FieldSalesperson);
        app.ClockOverride = TrainingDay;
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        string[] reps = ["colm", "aoife", "ciara", "brian", "niamh"];
        using var response = await GrantAsync(api, group, reps);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<CohortPermissionGrantResult>())!;
        Assert.Equal(group, saved.RestrictionGroupId);
        Assert.Equal(reps.Order(), saved.Changes.Select(row => row.Rep.Subject).Order());
        Assert.Equal(5, saved.Changes.Select(row => row.Id).Distinct().Count());
        Assert.All(saved.Changes, row =>
        {
            Assert.Equal(TrainingDay, row.ChangedAt);
            Assert.Equal(("orla", "Orla Kenny", Training), (row.Actor.Subject, row.Actor.DisplayName, row.Reason));
            Assert.Equal((group, Pharmacy, RestrictionPermissionChangeKind.Granted), (row.RestrictionGroupId, row.RestrictionGroupName, row.Change));
        });
        Assert.Equal((5, 5), await _host.CountsAsync());
        Assert.False(app.Staff.SawTransaction);

        // CV009-C: both views return the actual same entry, not a second audit record.
        foreach (var change in saved.Changes)
        {
            var repPage = await PageAsync(api, change.Rep.Subject);
            var row = Assert.Single(repPage.Groups);
            Assert.True(row.Granted);
            Assert.Equal(change, row.LastChange);
        }
        var groupPage = await GroupAsync(api, group);
        foreach (var change in saved.Changes)
            Assert.Equal(change, Assert.Single(groupPage.Reps, row => row.Rep.Subject == change.Rep.Subject).LastChange);
        Assert.Equal((5, 5), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_ShowStatesAndLastChanges_When_OpeningTheGroupPage()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        var granted = await SavedAsync(ChangeAsync(office, "colm", group, RestrictionPermissionChangeKind.Granted, Training));
        var first = await SavedAsync(ChangeAsync(office, "aoife", group, RestrictionPermissionChangeKind.Granted));
        var removed = await SavedAsync(ChangeAsync(office, "aoife", group, RestrictionPermissionChangeKind.Removed, "Training expired", first.Id));
        using var manager = _host.ApiAs("niamh", BusinessRoles.SalesManager);
        var page = await GroupAsync(manager, group);
        Assert.Equal(["Aoife", "Brian", "Ciara", "Colm", "Sorcha"], page.Reps.Select(row => row.Rep.Name));
        var colm = Assert.Single(page.Reps, row => row.Rep.Subject == "colm");
        Assert.True(colm.Granted); Assert.False(colm.CanGrant); Assert.Equal(granted, colm.LastChange);
        var aoife = Assert.Single(page.Reps, row => row.Rep.Subject == "aoife");
        Assert.False(aoife.Granted); Assert.True(aoife.CanGrant); Assert.Equal(removed, aoife.LastChange);
        Assert.All(page.Reps.Where(row => row.Rep.Subject is "ciara" or "brian" or "sorcha"), row => Assert.False(row.CanGrant));
        Assert.Null(Assert.Single(page.Reps, row => row.Rep.Subject == "ciara").LastChange);
        Assert.Equal((3, 1), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RecordNothing_When_AForgedSelectionIncludesAnOutsideTeamRep()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("niamh", BusinessRoles.SalesManager);
        using var forged = await GrantAsync(api, group, ["aoife", "ciara"]);
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
        using var allowed = await GrantAsync(api, group, ["colm", "aoife"]);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal((2, 2), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_AllowCrossTeamGrantsButRejectSelfChange_When_ActingAsHeadOffice()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser, BusinessRoles.FieldSalesperson);
        using var self = await GrantAsync(api, group, ["aoife", "orla"]);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
        Assert.False(Assert.Single((await GroupAsync(api, group)).Reps, row => row.Rep.Subject == "orla").CanGrant);
        using var others = await GrantAsync(api, group, ["colm", "ciara", "sorcha"]);
        Assert.Equal(HttpStatusCode.OK, others.StatusCode);
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("empty")] [InlineData("duplicate")] [InlineData("blank")] [InlineData("control")]
    [InlineData("null-list")] [InlineData("null-row")]
    public async Task Should_RecordNothing_When_SelectionIsEmptyDuplicateOrMalformed(string kind)
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        IReadOnlyList<CohortPermissionSelection> rows = kind switch
        {
            "empty" => [], "duplicate" => [new("colm", null), new("colm", null)],
            "blank" => [new(" ", null)], "control" => [new("co\nlm", null)],
            "null-list" => null!, _ => [null!]
        };
        using var response = await api.PostAsJsonAsync(Path(group) + "/grants", new GrantCohortPermissionRequest(rows, Training));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RejectTheWholeCohort_When_AnySelectedPermissionIsAlreadyGrantedOrStale()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        var first = await SavedAsync(ChangeAsync(api, "colm", group, RestrictionPermissionChangeKind.Granted, Training));
        // Aoife is written first. Colm's stale/unchanged grant must roll Aoife and her record back.
        foreach (Guid? expected in new Guid?[] { null, first.Id })
        {
            using var stale = await api.PostAsJsonAsync(Path(group) + "/grants", new GrantCohortPermissionRequest(
                [new("aoife", null), new("colm", expected)], Training));
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal((1, 1), await _host.CountsAsync());
            Assert.False(Assert.Single((await PageAsync(api, "aoife")).Groups).Granted);
        }
        var removed = await SavedAsync(ChangeAsync(api, "colm", group, RestrictionPermissionChangeKind.Removed, null, first.Id));
        using var oldForm = await GrantAsync(api, group, ["aoife", "colm"]);
        Assert.Equal(HttpStatusCode.Conflict, oldForm.StatusCode);
        Assert.Equal((2, 0), await _host.CountsAsync());
        using var current = await api.PostAsJsonAsync(Path(group) + "/grants", new GrantCohortPermissionRequest(
            [new("aoife", null), new("colm", removed.Id)], Training));
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal((4, 2), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RecordNothing_When_TheGroupIsArchivedOrMissing()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        await SavedAsync(ChangeAsync(api, "colm", group, RestrictionPermissionChangeKind.Granted));
        app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        await _host.ArchiveAsync(group);
        var page = await GroupAsync(api, group);
        Assert.True(page.Group.Archived);
        Assert.All(page.Reps, row => Assert.False(row.CanGrant));
        var inactive = Assert.Single(page.Reps, row => row.Rep.Subject == "colm");
        Assert.False(inactive.Active); Assert.True(inactive.Granted);
        Assert.Empty((await api.GetFromJsonAsync<PermissionGroupChoice[]>("/coverage/permissions/groups"))!);
        using var archived = await GrantAsync(api, group, ["aoife", "ciara"]);
        Assert.Equal(HttpStatusCode.BadRequest, archived.StatusCode);
        using var missing = await GrantAsync(api, Guid.NewGuid(), ["aoife", "ciara"]);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((1, 1), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RecordNothing_When_IdentityFailsOrASelectedRepIsIneligible()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        app.Staff.Unavailable = true;
        using (var failed = await GrantAsync(api, group, ["aoife", "colm"])) Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        app.Staff.Unavailable = false;
        using (var unknown = await GrantAsync(api, group, ["aoife", "missing"])) Assert.Equal(HttpStatusCode.ServiceUnavailable, unknown.StatusCode);
        app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        using (var inactive = await GrantAsync(api, group, ["aoife", "colm"])) Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        using (var nonrep = await GrantAsync(api, group, ["aoife", "another-manager"])) Assert.Equal(HttpStatusCode.BadRequest, nonrep.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
        Assert.False(app.Staff.SawTransaction);
    }

    [Theory]
    [InlineData(null, true)] [InlineData("   ", true)] [InlineData("  Training completed  ", true)]
    [InlineData("limit", true)] [InlineData("over-limit", false)] [InlineData("Training\ncompleted", false)]
    public async Task Should_ValidateTheSharedReason_When_GrantingACohort(string? reason, bool accepted)
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        reason = reason switch { "limit" => new string('a', 1000), "over-limit" => new string('a', 1001), _ => reason };
        using var response = await GrantAsync(api, group, ["aoife", "colm"], reason);
        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(accepted ? (2, 2) : (0, 0), await _host.CountsAsync());
        if (accepted)
            Assert.All((await response.Content.ReadFromJsonAsync<CohortPermissionGrantResult>())!.Changes,
                row => Assert.Equal(string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), row.Reason));
    }

    internal static string Path(Guid group) => "/coverage/permissions/groups/" + group;
    internal static Task<GroupPermissionsPage> GroupAsync(HttpClient api, Guid group) => ReadGroupAsync(api, group);
    private static async Task<GroupPermissionsPage> ReadGroupAsync(HttpClient api, Guid group) =>
        (await api.GetFromJsonAsync<GroupPermissionsPage>(Path(group)))!;
    internal static Task<HttpResponseMessage> GrantAsync(HttpClient api, Guid group, string[] reps, string? reason = Training) =>
        api.PostAsJsonAsync(Path(group) + "/grants", new GrantCohortPermissionRequest(reps.Select(rep => new CohortPermissionSelection(rep, null)).ToArray(), reason));
}
