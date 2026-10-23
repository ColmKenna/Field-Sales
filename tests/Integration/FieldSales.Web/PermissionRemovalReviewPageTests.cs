using System.Net;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FieldSales.Web.Tests.RestrictionPermissionHost;

namespace FieldSales.Web.Tests;

// Approved WI-031 single-rep increment. Real Razor/cookie/antiforgery, BFF, API and SQL record.
public sealed class PermissionRemovalReviewPageTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const string Page = "/Coverage/Permissions";
    private const string RepPage = Page + "?repSubject=colm";
    private const string Pharmacy = "Pharmacy-only medicines";
    private const string RemovalReason = "Training expired";
    private const string Consequence = "Colm will lose Pharmacy-only medicines at their next Sync. Any lines already on unsent orders will still send.";
    private RestrictionPermissionHost _host = null!;
    private readonly ReviewClock _clock = new();
    public Task InitializeAsync() { _host = new(app); return Task.CompletedTask; }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Should_ShowConsequenceWithoutSaving_When_ReviewingASingleRemoval()
    {
        await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        string review = await ReviewAsync(browser, "  " + RemovalReason + "  ");
        SavePreview("single-review", review);
        Assert.Contains(Consequence, Text(review));
        Assert.Contains("Reason: " + RemovalReason, Text(review));
        Assert.Contains(">Confirm removal</button>", review);
        Assert.Contains($"href=\"{RepPage}\"", review);
        Assert.DoesNotContain("lines already on In Progress", Text(review));
        Assert.Equal((1, 1), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_KeepPermissionAndRecord_When_CancellingRemoval()
    {
        await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        string review = await ReviewAsync(browser);
        string cancel = WebUtility.HtmlDecode(Regex.Match(review, "<a href=\"([^\"]+)\" class=\"button secondary\">Cancel</a>").Groups[1].Value);
        Assert.Equal(RepPage, cancel);
        string returned = await HtmlAsync(browser, cancel);
        Assert.DoesNotContain("Review removal", returned);
        Assert.Contains("Granted", Text(returned));
        Assert.Equal((1, 1), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RecordOneRemoval_When_ConfirmingASingleReview()
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser, "  " + RemovalReason + "  "));
        // Extra posted fields cannot replace the command protected in the review.
        form.Fields["Reason"] = "A different posted reason";
        form.Fields["GroupId"] = Guid.NewGuid().ToString();
        app.ClockOverride = new(2026, 10, 19, 12, 10, 0, TimeSpan.Zero);
        using var saved = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(RepPage, saved.Headers.Location?.OriginalString);
        string html = await HtmlAsync(browser, RepPage);
        SavePreview("single-removed", html);
        Assert.Contains("Pharmacy-only medicines removed from Colm.", Text(html));
        using var api = _host.ApiAs("niamh", BusinessRoles.SalesManager);
        var row = Assert.Single((await PageAsync(api, "colm")).Groups);
        Assert.Equal(group, row.RestrictionGroupId); Assert.False(row.Granted);
        Assert.Equal((RestrictionPermissionChangeKind.Removed, "niamh", RemovalReason, app.ClockOverride),
            (row.LastChange!.Change, row.LastChange.Actor.Subject, row.LastChange.Reason, (DateTimeOffset?)row.LastChange.ChangedAt));
        Assert.Equal((2, 0), await _host.CountsAsync());
        using var replay = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains("This permission changed.", await replay.Content.ReadAsStringAsync());
        Assert.Equal((2, 0), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("missing-proof")] [InlineData("altered")] [InlineData("expired")]
    [InlineData("another-actor")] [InlineData("different-rep")] [InlineData("not-confirmed")]
    public async Task Should_RejectConfirmation_When_ReviewIsMissingAlteredExpiredOrForAnotherActor(string kind)
    {
        await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser));
        switch (kind)
        {
            case "missing-proof": form.Fields.Remove("ReviewProof"); break;
            case "altered":
                string proof = form.Fields["ReviewProof"]; int middle = proof.Length / 2;
                form.Fields["ReviewProof"] = proof[..middle] + (proof[middle] == 'A' ? 'B' : 'A') + proof[(middle + 1)..]; break;
            case "expired": _clock.Now = _clock.Now.AddMinutes(30); break;
            case "another-actor":
                using (var signedIn = await browser.GetAsync("/__test/sign-in?subject=orla&roles=" + Uri.EscapeDataString(BusinessRoles.HeadOfficeUser)))
                    Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
                // Antiforgery also binds identity. Obtain a fresh actor's token so the review's own
                // actor binding, rather than a stale antiforgery token, is what rejects this command.
                form.Fields["__RequestVerificationToken"] = Field(await HtmlAsync(browser, RepPage), "__RequestVerificationToken");
                break;
            case "different-rep": form.Fields["RepSubject"] = "aoife"; break;
            case "not-confirmed": form.Fields.Remove("Confirmed"); break;
        }
        using var refused = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("This removal review is invalid or expired.", await refused.Content.ReadAsStringAsync());
        Assert.Equal((1, 1), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RequireFreshReview_When_ThePermissionChangesBeforeConfirmation()
    {
        var group = await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser));
        using (var api = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser))
        {
            var old = Assert.Single((await PageAsync(api, "colm")).Groups).LastChange!;
            var removed = await SavedAsync(ChangeAsync(api, "colm", group, RestrictionPermissionChangeKind.Removed, "Another manager", old.Id));
            await SavedAsync(ChangeAsync(api, "colm", group, RestrictionPermissionChangeKind.Granted, "Training renewed", removed.Id));
        }
        using var stale = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        string html = await stale.Content.ReadAsStringAsync();
        SavePreview("single-stale-review", html);
        Assert.Contains("This permission changed.", html);
        Assert.DoesNotContain("permission-removal-title", html);
        Assert.Equal((3, 1), await _host.CountsAsync());
        var fresh = ConfirmationForm(await ReviewAsync(browser));
        using var confirmed = await browser.PostAsync(fresh.Action, new FormUrlEncodedContent(fresh.Fields));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Equal((4, 0), await _host.CountsAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Should_DenyRemoval_When_AuthorityChangesAfterReview(bool roleChanged)
    {
        await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var form = ConfirmationForm(await ReviewAsync(browser));
        if (roleChanged) _host.SetRoles("niamh", BusinessRoles.FieldSalesperson);
        else
        {
            using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
            string version = await _host.ReportingVersionAsync("colm");
            using var moved = await office.PutAsJsonAsync("/coverage/reporting-lines/colm", new SetRepReportingLineRequest("another-manager", version));
            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        }
        // The BFF still holds the original bearer/session: the API must enforce current authority.
        using var refused = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal((1, 1), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("archived")] [InlineData("inactive")] [InlineData("both")]
    public async Task Should_AllowRemovalWithAccurateConsequence_When_GroupIsArchivedOrHolderInactive(string kind)
    {
        var group = await SeedAsync();
        bool archived = kind is "archived" or "both";
        if (archived) await _host.ArchiveAsync(group);
        if (kind is "inactive" or "both") app.Staff.Entries["colm"] = app.Staff.Entries["colm"] with { Available = false };
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        string review = await ReviewAsync(browser);
        if (archived)
        {
            SavePreview("single-archived-review", review);
            Assert.Contains("This group is archived and already grants no access.", Text(review));
            Assert.Contains("Restoring the group will not restore the permission.", Text(review));
            Assert.DoesNotContain("will lose", Text(review));
        }
        else Assert.Contains(Consequence, Text(review));
        var form = ConfirmationForm(review);
        using var confirmed = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Equal((2, 0), await _host.CountsAsync());
    }

    [Theory]
    [InlineData("identity-at-review")] [InlineData("identity-at-confirm")]
    [InlineData("csrf-at-review")] [InlineData("csrf-at-confirm")]
    public async Task Should_RecordNothing_When_IdentityFailsOrRequestsAreUnauthorized(string kind)
    {
        await SeedAsync();
        await using var parent = _host.CreateWebsite(); await using var website = WithClock(parent);
        using var browser = Browser(website); await SignInAsync(browser);
        var removal = RemovalForm(await HtmlAsync(browser, RepPage));
        FormData form = kind.EndsWith("at-confirm") ? ConfirmationForm(await ReviewAsync(browser)) : removal;
        if (kind.StartsWith("identity")) app.Staff.Unavailable = true;
        else form.Fields.Remove("__RequestVerificationToken");
        using var refused = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(kind.StartsWith("identity") ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal((1, 1), await _host.CountsAsync());
    }

    private async Task<Guid> SeedAsync()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        using var office = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        await SavedAsync(ChangeAsync(office, "colm", group, RestrictionPermissionChangeKind.Granted, "Completed training"));
        _host.SetRoles("niamh", BusinessRoles.SalesManager);
        return group;
    }
    private WebApplicationFactory<Program> WithClock(StaffWebsiteFactory parent) => parent.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock); }));
    private static HttpClient Browser(WebApplicationFactory<Program> website) => website.CreateClient(new()
        { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    private static async Task SignInAsync(HttpClient browser)
    {
        using var signedIn = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(BusinessRoles.SalesManager));
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
    }
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
    private static async Task<string> ReviewAsync(HttpClient browser, string? reason = RemovalReason)
    {
        var removal = RemovalForm(await HtmlAsync(browser, RepPage));
        removal.Fields["Reason"] = reason ?? "";
        using var response = await browser.PostAsync(removal.Action, new FormUrlEncodedContent(removal.Fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync(); Assert.Contains("Review removal", html); return html;
    }
    private static FormData RemovalForm(string html)
    {
        string row = Regex.Matches(html, "<tr>(.*?)</tr>", RegexOptions.Singleline).Select(match => match.Groups[1].Value)
            .Single(value => value.Contains("<th scope=\"row\">" + Pharmacy + "</th>", StringComparison.Ordinal));
        return ReadForm(Regex.Match(row, "<form.*?</form>", RegexOptions.Singleline).Value);
    }
    internal static FormData ConfirmationForm(string html)
    {
        string section = Regex.Match(html, "<section class=\"permission-removal-review\".*?</section>", RegexOptions.Singleline).Value;
        var form = ReadForm(Regex.Match(section, "<form.*?</form>", RegexOptions.Singleline).Value);
        form.Fields["Confirmed"] = "true";
        return form;
    }
    private static FormData ReadForm(string form)
    {
        string action = WebUtility.HtmlDecode(Regex.Match(form, "action=\"([^\"]+)\"").Groups[1].Value);
        var fields = Regex.Matches(form, "<input[^>]*>").Select(match => match.Value).ToDictionary(
            input => Regex.Match(input, "name=\"([^\"]*)\"").Groups[1].Value,
            input => WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value));
        return new(action, fields);
    }
    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ")).Trim();
    private static void SavePreview(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_REMOVAL_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output);
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "src/FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
    internal sealed record FormData(string Action, Dictionary<string, string> Fields);
    private sealed class ReviewClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
