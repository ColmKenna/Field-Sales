using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using static FieldSales.Web.Tests.RestrictionPermissionHost;

namespace FieldSales.Web.Tests;

// WI-029 increment 3: the M-14 per-rep permissions page through the real website, API and SQL. Niamh is
// signed in to the website; the people and their teams are described on RestrictionPermissionHost.
public sealed class RestrictionPermissionPageTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const RestrictionPermissionChangeKind Granted = RestrictionPermissionChangeKind.Granted;
    private const RestrictionPermissionChangeKind Removed = RestrictionPermissionChangeKind.Removed;
    private const string Page = "/Coverage/Permissions";
    private const string Pharmacy = "Pharmacy-only medicines";
    private const string HighValue = "High-value equipment";
    private const string Veterinary = "Veterinary medicines";
    private const string Training = "Completed training 15 Sep 2026";
    private static readonly DateTimeOffset TrainingDay = new(2026, 9, 15, 14, 2, 30, TimeSpan.Zero);
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
    public async Task Should_ISeeEachGroupWithGrantedNotGrantedAndTheLastChangeEntry_When_IOpenColmSPermissions()
    {
        var groups = await _host!.ResetAsync(Pharmacy, HighValue, Veterinary);
        using (var headOffice = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser))
        {
            app.ClockOverride = TrainingDay;
            await SavedAsync(ChangeAsync(headOffice, "colm", groups[0], Granted, Training));
            var highValue = await SavedAsync(ChangeAsync(headOffice, "colm", groups[1], Granted, Training));
            app.ClockOverride = new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);
            await SavedAsync(ChangeAsync(headOffice, "colm", groups[1], Removed, "Moved to the hardware team", highValue.Id));
        }
        await using var website = _host!.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, BusinessRoles.SalesManager); // Colm's own manager

        string html = await HtmlAsync(browser, Page + "?repSubject=colm");
        SavePreview("colm-manager", html);

        Assert.Contains("<h1>Colm</h1>", html);
        string[][] expected =
        [
            [HighValue, "Not granted", "Removed 02 Oct 2026 09:30 UTC by Orla Kenny — Moved to the hardware team"],
            [Pharmacy, "Granted", "Granted 15 Sep 2026 14:02 UTC by Orla Kenny — Completed training 15 Sep 2026"],
            [Veterinary, "Not granted", "No changes recorded"]
        ];
        Assert.Equal(expected, Rows(html).Select(row => row[..3]).ToArray());
        Assert.Equal(["Grant " + HighValue, "Remove " + Pharmacy, "Grant " + Veterinary], Buttons(html));
        Assert.Equal((3, 1), await _host!.CountsAsync()); // opening the page records nothing
    }

    [Fact]
    public async Task Should_ICanViewButNotChangeThem_When_IAmASalesManagerWhoDoesNotManageCiaraAndNotA()
    {
        var groups = await _host!.ResetAsync(Pharmacy, HighValue);
        app.ClockOverride = TrainingDay;
        using (var headOffice = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser))
            await SavedAsync(ChangeAsync(headOffice, "ciara", groups[0], Granted, Training));
        await using var website = _host!.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, BusinessRoles.SalesManager); // manages Colm and Aoife, not Ciara

        string chooser = await HtmlAsync(browser, Page);
        SavePreview("chooser", chooser);
        string list = Regex.Match(chooser, "<ul class=\"geography-list permission-reps\">(.*?)</ul>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(["Aoife", "Brian View only", "Ciara View only", "Colm", "Sorcha View only"],
            Regex.Matches(list, "<li>(.*?)</li>", RegexOptions.Singleline).Select(item => Text(item.Groups[1].Value)));

        string html = await HtmlAsync(browser, Page + "?repSubject=ciara");
        SavePreview("ciara-view-only", html);
        Assert.Contains("You can view Ciara's permissions but not change them.", WebUtility.HtmlDecode(html));
        string[][] expected =
        [
            [HighValue, "Not granted", "No changes recorded"],
            [Pharmacy, "Granted", "Granted 15 Sep 2026 14:02 UTC by Orla Kenny — Completed training 15 Sep 2026"]
        ];
        Assert.Equal(expected, Rows(html));
        Assert.Empty(Buttons(html));
        Assert.DoesNotContain(">Change</th>", html);

        // A change posted for Ciara anyway is refused by the API and nothing is recorded.
        var forged = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["RepSubject"] = "ciara",
            ["GroupId"] = groups[1].ToString(), ["Change"] = nameof(RestrictionPermissionChangeKind.Granted),
            ["Reason"] = Training, ["ExpectedLastChangeId"] = ""
        };
        using var refused = await browser.PostAsync(Page + "?handler=Change", new FormUrlEncodedContent(forged));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal((1, 1), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_ShowTheNewStateAndLastChange_When_IGrantOrRemoveFromThePage()
    {
        await _host!.ResetAsync(Pharmacy);
        app.ClockOverride = TrainingDay;
        await using var website = _host!.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, BusinessRoles.SalesManager);
        // The page is reached from the manager's home page and from the rep's territory page.
        Assert.Contains("href=\"/Coverage/Permissions\"", await HtmlAsync(browser, "/Manager"));
        Assert.Contains("href=\"/Coverage/Permissions?repSubject=colm\"", await HtmlAsync(browser, "/Coverage/Territory?repSubject=colm"));

        var grant = RowForm(await HtmlAsync(browser, Page + "?repSubject=colm"), Pharmacy);
        Assert.Equal(("Granted", ""), (grant.Fields["Change"], grant.Fields["ExpectedLastChangeId"]));
        grant.Fields["Reason"] = Training;
        string html = await FollowAsync(browser, await browser.PostAsync(grant.Action, new FormUrlEncodedContent(grant.Fields)));
        Assert.Contains("Pharmacy-only medicines granted to Colm. This takes effect at Colm's next Sync.", WebUtility.HtmlDecode(html));
        Assert.Equal([Pharmacy, "Granted", "Granted 15 Sep 2026 14:02 UTC by Niamh Byrne — Completed training 15 Sep 2026"],
            Assert.Single(Rows(html))[..3]);

        var removal = RowForm(html, Pharmacy);
        Assert.Equal("Removed", removal.Fields["Change"]);
        using (var review = await browser.PostAsync(removal.Action, new FormUrlEncodedContent(removal.Fields)))
        {
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
            html = await review.Content.ReadAsStringAsync();
            Assert.Contains("Review removal", html);
            Assert.Equal((1, 1), await _host!.CountsAsync());
        }
        var confirmation = PermissionRemovalReviewPageTests.ConfirmationForm(html);
        html = await FollowAsync(browser, await browser.PostAsync(confirmation.Action, new FormUrlEncodedContent(confirmation.Fields)));
        Assert.Contains("Pharmacy-only medicines removed from Colm. This takes effect at Colm's next Sync.", WebUtility.HtmlDecode(html));
        Assert.Equal([Pharmacy, "Not granted", "Removed 15 Sep 2026 14:02 UTC by Niamh Byrne"], Assert.Single(Rows(html))[..3]);

        // Submitting the earlier removal form again is refused; the page shows the current state.
        using var stale = await browser.PostAsync(removal.Action, new FormUrlEncodedContent(removal.Fields));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        html = await stale.Content.ReadAsStringAsync();
        SavePreview("stale-change", html);
        Assert.Contains("This permission changed. Reload before saving.", WebUtility.HtmlDecode(html));
        Assert.Equal("Grant " + Pharmacy, Assert.Single(Buttons(html)));
        Assert.Equal((2, 0), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_RejectThePostAndRecordNothing_When_TheAntiforgeryTokenIsMissing()
    {
        await _host!.ResetAsync(Pharmacy);
        await using var website = _host!.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, BusinessRoles.HeadOfficeUser);

        var grant = RowForm(await HtmlAsync(browser, Page + "?repSubject=colm"), Pharmacy);
        Assert.True(grant.Fields.Remove("__RequestVerificationToken"));
        using var response = await browser.PostAsync(grant.Action, new FormUrlEncodedContent(grant.Fields));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((0, 0), await _host!.CountsAsync());
    }

    [Fact]
    public async Task Should_ShowArchivedHeldGroupsWithRemoveOnlyOrAnEmptyState_When_GroupsAreArchivedOrAbsent()
    {
        await _host!.ResetAsync();
        await using var website = _host!.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, BusinessRoles.SalesManager);
        string html = await HtmlAsync(browser, Page + "?repSubject=colm");
        SavePreview("empty", html);
        Assert.Contains("No Restriction Groups exist yet.", html);
        Assert.Empty(Rows(html));

        var groups = await _host!.ResetAsync(Pharmacy, HighValue);
        app.ClockOverride = TrainingDay;
        using (var headOffice = _host!.ApiAs("orla", BusinessRoles.HeadOfficeUser))
            await SavedAsync(ChangeAsync(headOffice, "colm", groups[0], Granted, Training));
        await _host!.ArchiveAsync(groups[0]);
        await SignInAsync(browser, BusinessRoles.SalesManager);

        html = await HtmlAsync(browser, Page + "?repSubject=colm");
        SavePreview("archived", html);
        string[][] expected =
        [
            [HighValue, "Not granted", "No changes recorded"],
            [Pharmacy, "Granted Archived — no effect while archived", "Granted 15 Sep 2026 14:02 UTC by Orla Kenny — Completed training 15 Sep 2026"]
        ];
        Assert.Equal(expected, Rows(html).Select(row => row[..3]).ToArray());
        Assert.Equal(["Grant " + HighValue, "Remove " + Pharmacy], Buttons(html));
        // A rep who does not hold the archived group does not see it at all.
        Assert.Equal([HighValue], Rows(await HtmlAsync(browser, Page + "?repSubject=aoife")).Select(row => row[0]));
    }

    private async Task SignInAsync(HttpClient browser, string role)
    {
        _host!.SetRoles("niamh", role);
        using var response = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(role));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<string> FollowAsync(HttpClient browser, HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Coverage/Permissions?repSubject=colm", response.Headers.Location?.OriginalString);
            return await HtmlAsync(browser, response.Headers.Location!.OriginalString);
        }
    }

    // Each permissions row's cells as plain text, in page order.
    private static string[][] Rows(string html) => Regex.Matches(html, "<tr>(.*?)</tr>", RegexOptions.Singleline)
        .Select(match => match.Groups[1].Value).Where(row => row.Contains("<th scope=\"row\">", StringComparison.Ordinal))
        .Select(row => Regex.Matches(row, "<t[hd][^>]*>(.*?)</t[hd]>", RegexOptions.Singleline)
            .Select(cell => Text(cell.Groups[1].Value)).ToArray())
        .ToArray();

    private static string[] Buttons(string html) =>
        Regex.Matches(Regex.Match(html, "<table>(.*?)</table>", RegexOptions.Singleline).Groups[1].Value,
            "<button type=\"submit\"[^>]*>(.*?)</button>", RegexOptions.Singleline).Select(match => Text(match.Groups[1].Value)).ToArray();

    private static RowFormData RowForm(string html, string group)
    {
        string row = Regex.Matches(html, "<tr>(.*?)</tr>", RegexOptions.Singleline).Select(match => match.Groups[1].Value)
            .Single(row => row.Contains("<th scope=\"row\">" + WebUtility.HtmlEncode(group) + "</th>", StringComparison.Ordinal));
        string action = WebUtility.HtmlDecode(Regex.Match(row, "<form[^>]*action=\"([^\"]*)\"").Groups[1].Value);
        var fields = Regex.Matches(row, "<input[^>]*>").Select(match => match.Value).ToDictionary(
            input => Regex.Match(input, "name=\"([^\"]*)\"").Groups[1].Value,
            input => WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value));
        return new(action, fields);
    }

    private static string Field(string html, string name) =>
        WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

    private static string Text(string html) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ")).Trim();

    // Saves the rendered page, with the site stylesheet inlined, for the increment review screenshots.
    private static void SavePreview(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_PERMISSIONS_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output);
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "src/FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }

    private sealed record RowFormData(string Action, Dictionary<string, string> Fields);
}
