using System.Net;
using System.Text.RegularExpressions;
using FieldSales.StaffAccess;
using static FieldSales.Web.Tests.RestrictionPermissionCohortTests;

namespace FieldSales.Web.Tests;

// WI-030: the real Razor form, BFF, permission API and SQL record together.
public sealed class RestrictionPermissionCohortPageTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const string Page = "/Coverage/GroupPermissions";
    private const string Pharmacy = "Pharmacy-only medicines";
    private const string Training = "Completed training 15 Sep 2026";
    private RestrictionPermissionHost _host = null!;
    public Task InitializeAsync() { _host = new(app); return Task.CompletedTask; }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Should_LockOutsideTeamRows_When_TheViewerIsOnlyASalesManager()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        _host.SetRoles("niamh", BusinessRoles.SalesManager);
        await using var website = _host.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", BusinessRoles.SalesManager);
        string chooser = await HtmlAsync(browser, Page);
        Assert.Contains($"href=\"{Page}?groupId={group}\"", chooser);
        Assert.Contains($"href=\"{Page}\"", await HtmlAsync(browser, "/Coverage/Permissions"));
        string html = await HtmlAsync(browser, Page + "?groupId=" + group);
        SavePreview("group-manager", html);
        Assert.Contains("<h1>Pharmacy-only medicines</h1>", html);
        foreach (string rep in new[] { "colm", "aoife" })
        {
            string row = Row(html, rep);
            Assert.Contains("Not granted", row);
            Assert.Contains("No changes recorded", row);
            Assert.Contains("type=\"checkbox\"", row);
            Assert.Contains($"href=\"/Coverage/Permissions?repSubject={rep}\"", row);
        }
        foreach (string rep in new[] { "ciara", "brian", "sorcha" })
        {
            Assert.Contains("View only", Row(html, rep));
            Assert.DoesNotContain("type=\"checkbox\"", Row(html, rep));
        }
        Assert.Equal((0, 0), await _host.CountsAsync());

        // The browser's locked row is no substitute for server authorization.
        var forged = Form(html);
        Select(forged.Fields, "aoife", "ciara");
        using var denied = await browser.PostAsync(forged.Action, new FormUrlEncodedContent(forged.Fields));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_ShowTheSameRecord_When_OpeningEachGrantedRepsPage()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        _host.SetRoles("niamh", BusinessRoles.SalesManager, BusinessRoles.FieldSalesperson);
        _host.SetRoles("orla", BusinessRoles.HeadOfficeUser);
        app.ClockOverride = new(2026, 9, 15, 14, 2, 30, TimeSpan.Zero);
        await using var website = new StaffWebsiteFactory(_host.Api.Server.CreateHandler(), app.Token(BusinessRoles.HeadOfficeUser, subject: "orla"));
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, "orla", BusinessRoles.HeadOfficeUser);
        Assert.Contains($"href=\"{Page}?groupId={group}\"", await HtmlAsync(browser, "/HeadOffice/ReferenceData?ListKey=restriction-groups"));
        string html = await HtmlAsync(browser, Page + "?groupId=" + group);
        SavePreview("group-head-office", html);
        var form = Form(html);
        string[] reps = ["colm", "aoife", "ciara", "brian", "niamh"];
        Select(form.Fields, reps);
        form.Fields["Reason"] = Training;
        using var saved = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(Page + "?groupId=" + group, saved.Headers.Location?.OriginalString);
        string after = await HtmlAsync(browser, saved.Headers.Location!.OriginalString);
        SavePreview("group-after-grant", after);
        Assert.Contains("Permission granted to 5 reps.", after);
        string record = "Granted 15 Sep 2026 14:02 UTC by Orla Kenny — " + Training;
        foreach (string rep in reps)
        {
            Assert.Contains(record, Text(Row(after, rep)));
            Assert.Contains("Already granted", Row(after, rep));
            Assert.DoesNotContain("type=\"checkbox\"", Row(after, rep));
            string perRep = await HtmlAsync(browser, "/Coverage/Permissions?repSubject=" + rep);
            Assert.Contains(record, Text(perRep));
            Assert.Contains(">Granted", perRep);
        }
        Assert.Equal((5, 5), await _host.CountsAsync());

        // The original browser form cannot duplicate the five records.
        using var stale = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        string staleHtml = await stale.Content.ReadAsStringAsync();
        SavePreview("group-stale", staleHtml);
        Assert.Contains("This permission changed. Reload before saving.", staleHtml);
        Assert.Equal((5, 5), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RejectUnauthorizedRequests_When_UsingTheWebsiteOrApi()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        _host.SetRoles("niamh", BusinessRoles.SalesManager);
        await using var website = _host.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", BusinessRoles.SalesManager);
        var form = Form(await HtmlAsync(browser, Page + "?groupId=" + group));
        Select(form.Fields, "colm");
        Assert.True(form.Fields.Remove("__RequestVerificationToken"));
        using var noCsrf = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using var rep = _host.ApiAs("colm", BusinessRoles.FieldSalesperson);
        using (var read = await rep.GetAsync(Path(group))) Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using (var grant = await GrantAsync(rep, group, ["aoife"])) Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
        using var noScope = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        noScope.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(BusinessRoles.HeadOfficeUser, scope: "", subject: "orla"));
        using (var read = await noScope.GetAsync(Path(group))) Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using (var grant = await GrantAsync(noScope, group, ["aoife"])) Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
        using var repBrowser = website.CreateBrowser();
        await SignInAsync(repBrowser, "colm", BusinessRoles.FieldSalesperson);
        using var repRead = await repBrowser.GetAsync(Page + "?groupId=" + group);
        Assert.Equal(HttpStatusCode.Redirect, repRead.StatusCode);
        Assert.Equal("/AccessDenied", repRead.Headers.Location?.AbsolutePath);
        Assert.DoesNotContain("Grant to selected reps", await repRead.Content.ReadAsStringAsync());
        Assert.Equal((0, 0), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RecordNothing_When_SelectionIsEmptyDuplicateOrMalformed()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        _host.SetRoles("niamh", BusinessRoles.SalesManager);
        await using var website = _host.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", BusinessRoles.SalesManager);
        var form = Form(await HtmlAsync(browser, Page + "?groupId=" + group));
        using var empty = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Contains("Select at least one rep", await empty.Content.ReadAsStringAsync());
        Select(form.Fields, "colm");
        string subjectKey = form.Fields.Single(pair => pair.Key.EndsWith(".RepSubject") && pair.Value == "colm").Key;
        form.Fields[subjectKey.Replace("RepSubject", "ExpectedLastChangeId")] = "not-a-guid";
        using var malformed = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, malformed.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
    }

    [Fact]
    public async Task Should_RecordNothing_When_TheGroupIsArchivedOrMissing()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        _host.SetRoles("niamh", BusinessRoles.SalesManager);
        await using var website = _host.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", BusinessRoles.SalesManager);
        var form = Form(await HtmlAsync(browser, Page + "?groupId=" + group));
        Select(form.Fields, "colm");
        await _host.ArchiveAsync(group);
        string html = await HtmlAsync(browser, Page + "?groupId=" + group);
        SavePreview("group-archived", html);
        Assert.Contains("No new permissions can be granted.", html);
        Assert.DoesNotContain("type=\"checkbox\"", html);
        Assert.DoesNotContain("Grant to selected reps", html);
        using var stale = await browser.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Contains("Choose an active Restriction Group.", await stale.Content.ReadAsStringAsync());
        string chooser = await HtmlAsync(browser, Page);
        SavePreview("group-empty", chooser);
        Assert.Contains("No active Restriction Groups exist yet.", chooser);
        using var missing = await browser.GetAsync(Page + "?groupId=" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((0, 0), await _host.CountsAsync());
    }

    private static async Task SignInAsync(HttpClient browser, string subject, string role)
    {
        using var response = await browser.GetAsync("/__test/sign-in?subject=" + subject + "&roles=" + Uri.EscapeDataString(role));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
    private static string Row(string html, string rep) => Regex.Matches(html, "<tr data-rep-subject=\"([^\"]+)\">(.*?)</tr>", RegexOptions.Singleline)
        .Single(match => WebUtility.HtmlDecode(match.Groups[1].Value) == rep).Groups[2].Value;
    private static (string Action, Dictionary<string, string> Fields) Form(string html)
    {
        var form = Regex.Match(html, "<form[^>]*class=\"permission-cohort\"[^>]*>.*?</form>", RegexOptions.Singleline).Value;
        string action = WebUtility.HtmlDecode(Regex.Match(form, "action=\"([^\"]+)\"").Groups[1].Value);
        var fields = Regex.Matches(form, "<input[^>]*type=\"hidden\"[^>]*>").Select(match => match.Value).ToDictionary(
            input => Regex.Match(input, "name=\"([^\"]*)\"").Groups[1].Value,
            input => WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value));
        return (action, fields);
    }
    private static void Select(Dictionary<string, string> fields, params string[] reps)
    {
        foreach (string rep in reps)
        {
            string key = fields.Single(pair => pair.Key.EndsWith(".RepSubject") && pair.Value == rep).Key;
            fields[key.Replace("RepSubject", "Selected")] = "true";
        }
    }
    private static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ")).Trim();
    private static void SavePreview(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_COHORT_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output);
        string repository = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        string css = File.ReadAllText(System.IO.Path.Combine(repository, "src/FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        File.WriteAllText(System.IO.Path.Combine(output, name + ".html"), html);
    }
}
