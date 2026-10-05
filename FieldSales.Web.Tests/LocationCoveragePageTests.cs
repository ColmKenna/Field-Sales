using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

public sealed class LocationCoveragePageTests
{
    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task Should_EncodeLabelsAndOpenSeparateHistory_When_ManagerOrHeadOfficeReadsShop(string role)
    {
        Guid id = Guid.NewGuid(), town = Guid.NewGuid();
        var owner = new ImpactOwnerDetails(new("aoife", "Aoife <script>"), new(TerritoryLevel.Town, town), "Rathdrum <img>");
        var page = new LocationCoveragePage(id, "Murphy's <script>Pharmacy</script>", town, "Rathdrum <img>", owner, 0);
        var entry = LocationCoverageClientTests.Entry(id, 1) with { Actor = new("niamh", "M. <script>Byrne</script>"), Reason = "<img onerror=alert(1)>" };
        var history = new LocationCoverageHistoryPage(id, page.Name, page.TownName, [entry]);
        using var handler = new Reply(page, history); await using var app = new StaffWebsiteFactory(handler); using var browser = app.CreateBrowser();
        await SignInAsync(browser, role); string html = await HtmlAsync(browser, $"/Coverage/Location/{id}");
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>Pharmacy", html); Assert.DoesNotContain("<img>", html);
        Assert.Contains($"href=\"/Coverage/LocationHistory/{id}\"", html); Assert.DoesNotContain("M. &lt;script&gt;", html);
        Assert.DoesNotContain("test-access-token", html); Assert.DoesNotContain("Visit Dues", html); Assert.DoesNotContain("Last Call", html);
        html = await HtmlAsync(browser, $"/Coverage/LocationHistory/{id}");
        Assert.Contains("M. &lt;script&gt;Byrne&lt;/script&gt;", html); Assert.Contains("&lt;img onerror=alert(1)&gt;", html);
        Assert.Contains("<time datetime=", html); Assert.Contains($"href=\"/Coverage/Location/{id}\"", html);
    }

    [Theory]
    [InlineData("inherited")] [InlineData("direct")] [InlineData("unassigned")] [InlineData("long")]
    public async Task Should_RenderActualM07AndHistory_ForBrowserVerification(string state)
    {
        Guid id = Guid.Parse("11111111-1111-1111-1111-111111111111"), town = Guid.NewGuid();
        string name = state switch { "direct" => "Byrne's Chemist", "unassigned" => "Walsh's Shop", _ => "Murphy's Pharmacy" };
        var owner = state == "unassigned" ? null : new ImpactOwnerDetails(new(state == "direct" ? "colm" : "aoife", state == "direct" ? "Colm" : "Aoife"),
            new(state == "direct" ? TerritoryLevel.Location : TerritoryLevel.Town, state == "direct" ? id : town), "Rathdrum");
        var page = new LocationCoveragePage(id, name, town, "Rathdrum", owner, state == "unassigned" ? 4 : 0);
        var older = LocationCoverageClientTests.Entry(id, 1);
        older = older with { PreviousOwner = null, NewOwner = older.PreviousOwner, ChangedAt = older.ChangedAt.AddMinutes(-1) };
        var history = new LocationCoverageHistoryPage(id, name, "Rathdrum", state == "unassigned" ? [] : [LocationCoverageClientTests.Entry(id, 2), older]);
        if (state == "long")
        {
            page = page with { Name = new string('M', 200), TownName = new string('T', 200),
                Owner = owner! with { Rep = new("aoife", new string('A', 512)), SourceName = new string('T', 200) } };
            history = history with { Name = page.Name, TownName = page.TownName,
                Entries = [history.Entries[0] with { Actor = new("niamh", new string('B', 512)), Reason = new string('R', 1000) }] };
        }
        using var handler = new Reply(page, history); await using var app = new StaffWebsiteFactory(handler); using var browser = app.CreateBrowser();
        await SignInAsync(browser, BusinessRoles.SalesManager);
        string html = await HtmlAsync(browser, $"/Coverage/Location/{id}"); SaveRender(state, html);
        string decoded = WebUtility.HtmlDecode(html);
        Assert.Contains(state switch { "direct" => "Colm (assigned directly)", "unassigned" => "Unassigned", "long" => new string('A', 512), _ => "Aoife (via Rathdrum)" }, decoded);
        Assert.Contains("Specialists:</strong><span></span>", html); Assert.DoesNotContain("Visit Dues", html); Assert.DoesNotContain("Last Call", html);
        html = await HtmlAsync(browser, $"/Coverage/LocationHistory/{id}"); SaveRender(state + "-history", html);
        Assert.Contains(state == "unassigned" ? "No assignment history yet." : "via Rathdrum assignment", html);
    }

    [Theory]
    [InlineData(BusinessRoles.FieldSalesperson)] [InlineData("SysAdmin")]
    public async Task Should_DenyProtectedContent_When_CurrentCookieRoleDoesNotAllowCoverage(string role)
    {
        using var handler = new NeverRead(); await using var app = new StaffWebsiteFactory(handler); using var browser = app.CreateBrowser(); await SignInAsync(browser, role);
        if (!BusinessRoles.Contains(role)) app.Roles.SetRoles("niamh");
        foreach (string page in new[] { "Location", "LocationHistory" })
        { using var denied = await browser.GetAsync($"/Coverage/{page}/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); Assert.DoesNotContain("Primary:", await denied.Content.ReadAsStringAsync()); }
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)] [InlineData(HttpStatusCode.ServiceUnavailable)] [InlineData(HttpStatusCode.Forbidden)]
    public async Task Should_ReturnFailedReadWithoutProtectedContent_When_ApiCannotSupplyLocation(HttpStatusCode status)
    {
        using var handler = new FailedRead(status); await using var app = new StaffWebsiteFactory(handler); using var browser = app.CreateBrowser(); await SignInAsync(browser, BusinessRoles.SalesManager);
        foreach (string page in new[] { "Location", "LocationHistory" })
        { using var response = await browser.GetAsync($"/Coverage/{page}/{Guid.NewGuid()}"); Assert.Equal(status, response.StatusCode); Assert.DoesNotContain("Unassigned", await response.Content.ReadAsStringAsync()); }
    }

    private static async Task SignInAsync(HttpClient browser, string role)
    { using var login = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode); }
    private static async Task<string> HtmlAsync(HttpClient browser, string path)
    { using var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadAsStringAsync(); }
    private static void SaveRender(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_LOCATION_ARTIFACT_DIR"); if (string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output); string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        html = html.Replace("/Coverage/LocationHistory/11111111-1111-1111-1111-111111111111", name + "-history.html", StringComparison.Ordinal)
            .Replace("/Coverage/Location/11111111-1111-1111-1111-111111111111", name.Replace("-history", "", StringComparison.Ordinal) + ".html", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
    private sealed class Reply(LocationCoveragePage page, LocationCoverageHistoryPage history) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<object>(request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal) ? history : request.RequestUri.AbsolutePath.EndsWith("/actions", StringComparison.Ordinal) ? new LocationCoverageActions(page, page.Owner is null ? null : Guid.NewGuid(), page.Owner is null ? 0 : page.Owner.Source.Level == TerritoryLevel.Location ? 1 : 23, page.Owner is null, true, page.Owner is not null && page.Owner.Source.Level != TerritoryLevel.Location) : page) }); }
    }
    private sealed class NeverRead : HttpMessageHandler
    { public int Calls { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; throw new InvalidOperationException("Protected API should not be reached"); } }
    private sealed class FailedRead(HttpStatusCode status) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(status)); }
}
