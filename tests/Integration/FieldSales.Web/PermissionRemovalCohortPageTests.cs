using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FieldSales.Web.Tests.RestrictionPermissionHost;
using static FieldSales.Web.Tests.RestrictionPermissionCohortTests;
using static FieldSales.Web.Tests.PermissionRemovalReviewPageTests;
using IOPath = System.IO.Path;

namespace FieldSales.Web.Tests;

// WI-031 cohort checkpoint: real cookies, Razor, antiforgery, BFF, API and SQL persistence.
public sealed class PermissionRemovalCohortPageTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const string Page = "/Coverage/GroupPermissions";
    private const string Pharmacy = "Pharmacy-only medicines";
    private const string Reason = "Training expired";
    private RestrictionPermissionHost _host = null!;
    private readonly ReviewClock _clock = new();
    public Task InitializeAsync() { _host = new(app); return Task.CompletedTask; }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task Should_ShowOneConsequenceWithRepCount_When_ReviewingACohort(int count)
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        string selection = await HtmlAsync(browser, Url(group));
        SavePreview("cohort-selection", selection);
        Assert.DoesNotContain("type=\"checkbox\"", Row(selection, "ciara"));
        Assert.Contains("View only", Row(selection, "ciara"));
        string[] reps = count == 1 ? ["colm"] : ["aoife", "colm"];
        string review = await ReviewAsync(browser, group, reps);
        SavePreview("cohort-review-" + count, review);
        string consequence = $"{count} {(count == 1 ? "rep" : "reps")} will lose Pharmacy-only medicines at their next Sync. Any lines already on their unsent orders will still send.";
        Assert.Single(Regex.Matches(Text(review), Regex.Escape(consequence)));
        Assert.Contains("<li>Colm</li>", review);
        Assert.Equal(count == 2, review.Contains("<li>Aoife</li>", StringComparison.Ordinal));
        Assert.DoesNotContain("<li>Ciara</li>", review);
        Assert.Contains("Reason: " + Reason, Text(review));
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_KeepPermissionAndRecord_When_CancellingRemoval()
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        string review = await ReviewAsync(browser, group, ["aoife", "colm"]);
        string cancel = WebUtility.HtmlDecode(Regex.Match(review, "<a href=\"([^\"]+)\" class=\"button secondary\">Cancel</a>").Groups[1].Value);
        Assert.Equal(Url(group), cancel);
        string returned = await HtmlAsync(browser, cancel);
        Assert.DoesNotContain("permission-removal-title", returned);
        Assert.Contains("Granted", Row(returned, "colm"));
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RemoveAllSelectedAndKeepUnselected_When_ConfirmingACohort()
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser, group, ["aoife", "colm"]));
        form.Fields["Reason"] = "Unreviewed reason";
        form.Fields["Reps[0].RepSubject"] = "ciara";
        form.Fields["Reps[0].Selected"] = "true";
        app.ClockOverride = new(2026, 10, 20, 12, 10, 0, TimeSpan.Zero);
        using var response = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(Url(group), response.Headers.Location?.OriginalString);
        string html = await HtmlAsync(browser, Url(group));
        SavePreview("cohort-removed", html);
        Assert.Contains("Permission removed from 2 reps.", Text(html));
        foreach (string rep in new[] { "aoife", "colm" })
        {
            Assert.Contains("Not granted", Row(html, rep));
            Assert.Contains("Removed 20 Oct 2026 12:10 UTC by Niamh Byrne", Text(Row(html, rep)));
            Assert.Contains(Reason, Text(Row(html, rep)));
            Assert.DoesNotContain("Unreviewed reason", Row(html, rep));
            string perRep = await HtmlAsync(browser, "/Coverage/Permissions?repSubject=" + rep);
            Assert.Contains("Removed 20 Oct 2026 12:10 UTC by Niamh Byrne", Text(perRep));
        }
        Assert.Contains("Granted", Row(html, "ciara"));
        Assert.Equal((5, 1), await _host.CountsAsync());
        using var replay = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains("This permission changed.", await replay.Content.ReadAsStringAsync());
        Assert.Equal((5, 1), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("missing")] [InlineData("altered")] [InlineData("expired")]
    [InlineData("foreign-actor")] [InlineData("wrong-group")] [InlineData("not-confirmed")]
    [InlineData("single-review")]
    public async Task Should_RejectConfirmation_When_ReviewIsMissingAlteredExpiredOrForAnotherActor(string kind)
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser, group, ["aoife", "colm"]));
        switch (kind)
        {
            case "missing": form.Fields.Remove("ReviewProof"); break;
            case "altered":
                string proof = form.Fields["ReviewProof"]; int middle = proof.Length / 2;
                form.Fields["ReviewProof"] = proof[..middle] + (proof[middle] == 'A' ? 'B' : 'A') + proof[(middle + 1)..]; break;
            case "expired": _clock.Now = _clock.Now.AddMinutes(30); break;
            case "foreign-actor":
                await SignInAsync(browser, "orla", BusinessRoles.HeadOfficeUser);
                form.Fields["__RequestVerificationToken"] = Form(await HtmlAsync(browser, Url(group))).Fields["__RequestVerificationToken"];
                break;
            case "wrong-group": form.Fields["GroupId"] = (await AddOtherGroupAsync()).ToString(); break;
            case "not-confirmed": form.Fields.Remove("Confirmed"); break;
            case "single-review":
                using (var api = _host.ApiAs("niamh", BusinessRoles.SalesManager))
                {
                    var held = Assert.Single((await PageAsync(api, "colm")).Groups, row => row.RestrictionGroupId == group).LastChange!;
                    form.Fields["ReviewProof"] = website.Services.GetRequiredService<PermissionRemovalReviews>()
                        .Create("niamh", group, Pharmacy, false, [new("colm", "Colm", held.Id)], Reason).Proof;
                }
                break;
        }
        using var response = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("This removal review is invalid or expired.", await response.Content.ReadAsStringAsync());
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RejectConfirmation_When_ACohortReviewIsPostedToTheSingleRepPage()
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser, group, ["colm"]));
        form.Fields["RepSubject"] = "colm";
        using var response = await browser.PostAsync("/Coverage/Permissions?handler=ConfirmRemoval", new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RequireFreshReview_When_ThePermissionChangesBeforeConfirmation()
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser, group, ["aoife", "colm"]));
        using (var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser))
        {
            var held = Assert.Single((await PageAsync(office, "colm")).Groups, row => row.RestrictionGroupId == group).LastChange!;
            var removed = await SavedAsync(ChangeAsync(office, "colm", group, RestrictionPermissionChangeKind.Removed, null, held.Id));
            await SavedAsync(ChangeAsync(office, "colm", group, RestrictionPermissionChangeKind.Granted, null, removed.Id));
        }
        using var stale = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        string html = await stale.Content.ReadAsStringAsync(); SavePreview("cohort-stale", html);
        Assert.Contains("This permission changed.", html);
        Assert.DoesNotContain("permission-removal-title", html);
        Assert.Equal((5, 3), await _host.CountsAsync());
        var fresh = ConfirmationForm(await ReviewAsync(browser, group, ["aoife", "colm"]));
        using var saved = await browser.PostAsync(fresh.Action, new FormUrlEncodedContent(fresh.Fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal((7, 1), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("team")] [InlineData("role")]
    public async Task Should_DenyRemoval_When_AuthorityChangesAfterReview(string kind)
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser, group, ["aoife", "colm"]));
        if (kind == "role") _host.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        else
        {
            using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
            using var moved = await office.PutAsJsonAsync("/coverage/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", await _host.ReportingVersionAsync("colm")));
            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        }
        using var denied = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("archived")] [InlineData("inactive")] [InlineData("both")]
    public async Task Should_AllowRemovalWithAccurateConsequence_When_GroupIsArchivedOrHolderInactive(string kind)
    {
        var group = await SeedAsync();
        if (kind is "archived" or "both") await _host.ArchiveAsync(group);
        if (kind is "inactive" or "both") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        if (kind is "archived" or "both")
        {
            string chooser = await HtmlAsync(browser, Page);
            Assert.Contains($"href=\"{Page}?groupId={group}\"", chooser);
            Assert.Contains("Archived", chooser);
        }
        string review = await ReviewAsync(browser, group, ["aoife", "colm"]);
        if (kind is "archived" or "both")
        {
            SavePreview("cohort-archived-review", review);
            Assert.Contains("Permissions for Pharmacy-only medicines will be removed from 2 reps.", Text(review));
            Assert.Contains("already grants no access", Text(review));
            Assert.Contains("Restoring the group will not restore these permissions.", Text(review));
            Assert.DoesNotContain("will lose", Text(review));
        }
        var form = ConfirmationForm(review);
        using var saved = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal((5, 1), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("identity-review")] [InlineData("identity-confirm")]
    [InlineData("csrf-review")] [InlineData("csrf-confirm")]
    public async Task Should_RecordNothing_When_IdentityFailsOrRequestsAreUnauthorized(string kind)
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = kind.EndsWith("confirm", StringComparison.Ordinal) ? ConfirmationForm(await ReviewAsync(browser, group, ["aoife", "colm"]))
            : Select(Form(await HtmlAsync(browser, Url(group))), "aoife", "colm");
        if (kind.StartsWith("identity", StringComparison.Ordinal)) app.Staff.Unavailable = true;
        else form.Fields.Remove("__RequestVerificationToken");
        using var denied = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(kind.StartsWith("identity", StringComparison.Ordinal) ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("empty")] [InlineData("duplicate")] [InlineData("stale")]
    [InlineData("not-held")] [InlineData("outside-team")]
    public async Task Should_RejectTheWholeRemoval_When_TheReviewSelectionIsInvalid(string kind)
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = Form(await HtmlAsync(browser, Url(group)));
        if (kind != "empty") Select(form, "aoife");
        if (kind == "duplicate")
        {
            form.Fields["Reps[5].RepSubject"] = "aoife";
            form.Fields["Reps[5].Selected"] = "true";
        }
        if (kind == "stale")
        {
            Select(form, "colm");
            string key = form.Fields.Single(pair => pair.Key.EndsWith(".RepSubject") && pair.Value == "colm").Key;
            form.Fields[key.Replace("RepSubject", "ExpectedLastChangeId")] = Guid.NewGuid().ToString();
        }
        if (kind == "not-held") Select(form, "brian");
        if (kind == "outside-team") Select(form, "ciara");
        using var response = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(kind == "outside-team" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("permission-removal-title", await response.Content.ReadAsStringAsync());
        Assert.Equal((3, 3), await _host.CountsAsync());
    }

    [Theory]
    [InlineData(null, true)] [InlineData("   ", true)] [InlineData("  Training expired  ", true)]
    [InlineData("limit", true)] [InlineData("over-limit", false)] [InlineData("Reason\nwith control", false)]
    public async Task Should_PreserveReasonRules_When_ReviewingAndConfirmingRemoval(string? reason, bool accepted)
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = Select(Form(await HtmlAsync(browser, Url(group))), "aoife", "colm");
        reason = reason switch { "limit" => new string('r', 1000), "over-limit" => new string('r', 1001), _ => reason };
        form.Fields["Reason"] = reason ?? "";
        using var response = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync();
        Assert.Equal((3, 3), await _host.CountsAsync());
        if (!accepted) { Assert.Contains("Enter a reason of up to 1000 characters.", html); return; }
        var confirm = ConfirmationForm(html);
        using var saved = await browser.PostAsync(confirm.Action, new FormUrlEncodedContent(confirm.Fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var manager = _host.ApiAs("niamh", BusinessRoles.SalesManager);
        var changes = (await GroupAsync(manager, group)).Reps.Where(row => row.Rep.Subject is "aoife" or "colm");
        Assert.All(changes, row => Assert.Equal(string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), row.LastChange!.Reason));
        Assert.Equal((5, 1), await _host.CountsAsync());
    }

    private async Task<Guid> SeedAsync()
    {
        var group = (await _host.ResetAsync(Pharmacy, "High-value equipment"))[0];
        using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        using var seed = await GrantAsync(office, group, ["aoife", "colm", "ciara"]);
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);
        _host.SetRoles("niamh", BusinessRoles.SalesManager);
        return group;
    }
    private async Task<Guid> AddOtherGroupAsync()
    {
        using var api = _host.ApiAs("niamh", BusinessRoles.SalesManager);
        return (await api.GetFromJsonAsync<PermissionGroupChoice[]>("/coverage/permissions/groups"))!.Single(group => group.Name != Pharmacy).RestrictionGroupId;
    }
    private WebApplicationFactory<Program> WithClock(StaffWebsiteFactory parent) => parent.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock); }));
    private static HttpClient Browser(WebApplicationFactory<Program> website) => website.CreateClient(new()
        { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    private static async Task SignInAsync(HttpClient browser, string subject = "niamh", string role = BusinessRoles.SalesManager)
    {
        using var signedIn = await browser.GetAsync("/__test/sign-in?subject=" + subject + "&roles=" + Uri.EscapeDataString(role));
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
    }
    private static string Url(Guid group) => Page + "?groupId=" + group + "&remove=True";
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
    private static async Task<string> ReviewAsync(HttpClient browser, Guid group, string[] reps)
    {
        var form = Select(Form(await HtmlAsync(browser, Url(group))), reps); form.Fields["Reason"] = Reason;
        using var response = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync(); Assert.Contains("permission-removal-title", html); return html;
    }
    private static FormData Form(string html)
    {
        string form = Regex.Match(html, "<form[^>]*class=\"permission-cohort\"[^>]*>.*?</form>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(form);
        string action = WebUtility.HtmlDecode(Regex.Match(form, "action=\"([^\"]+)\"").Groups[1].Value);
        var fields = Regex.Matches(form, "<input[^>]*type=\"hidden\"[^>]*>").Select(match => match.Value).ToDictionary(
            input => Regex.Match(input, "name=\"([^\"]*)\"").Groups[1].Value,
            input => WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value));
        return new(action, fields);
    }
    private static FormData Select(FormData form, params string[] reps)
    {
        foreach (string rep in reps)
        {
            string key = form.Fields.Single(pair => pair.Key.EndsWith(".RepSubject") && pair.Value == rep).Key;
            form.Fields[key.Replace("RepSubject", "Selected")] = "true";
        }
        return form;
    }
    private static string Row(string html, string rep) => Regex.Matches(html, "<tr data-rep-subject=\"([^\"]+)\">(.*?)</tr>", RegexOptions.Singleline)
        .Single(match => WebUtility.HtmlDecode(match.Groups[1].Value) == rep).Groups[2].Value;
    private static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ")).Trim();
    private static void SavePreview(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_REMOVAL_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output);
        string repository = IOPath.GetFullPath(IOPath.Combine(AppContext.BaseDirectory, "../../../../../.."));
        string css = File.ReadAllText(IOPath.Combine(repository, "src/FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        File.WriteAllText(IOPath.Combine(output, name + ".html"), html);
    }
    private sealed class ReviewClock : TimeProvider
    {
        // The shared sign-in fixture gives cookies a real-time one-hour expiry. Freeze near sign-in
        // so advancing this clock by the review's 30 minutes tests the proof, not cookie expiration.
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
