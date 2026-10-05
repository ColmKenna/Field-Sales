using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

public sealed class LocationCoverageActionPageTests
{
    [Theory]
    [InlineData("short")] [InlineData("long")] [InlineData("markup")] [InlineData("unassigned")]
    public async Task Should_EncodeLabelsAndKeepAllControlsLocal_When_LocationAndPreviewRender(string labels)
    {
        using var handler = new Reply(labels); await using var web = new StaffWebsiteFactory(handler); using var browser = web.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(BusinessRoles.SalesManager)); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        string page = $"/Coverage/Location/{handler.Id}"; string html = await GetHtml(browser, page); Capture(labels + "-actions", html);
        Assert.Contains("Specialists:</strong><span></span>", html); Assert.DoesNotContain("Visit Dues", html); Assert.DoesNotContain("Last Call", html);
        var link = Regex.Matches(html, "<a[^>]*href=\"([^\"]*LocationChange[^\"]*)\"[^>]*>([^<]*)</a>")
            .Single(row => row.Groups[2].Value.Contains("just this shop", StringComparison.Ordinal));
        string entry = WebUtility.HtmlDecode(link.Groups[1].Value); Assert.StartsWith("/Coverage/LocationChange/" + handler.Id, entry);
        html = await GetHtml(browser, entry); Capture(labels + "-choose", html);
        Assert.Contains("<label for=\"RepSubject\">Receiving rep</label>", html); Assert.Contains("<label for=\"Reason\">Reason (optional)</label>", html);
        Assert.Contains("href=\"" + page + "\">Cancel", html);
        var fields = AssignmentTransferEndToEndTests.Inputs(html); fields.RemoveAll(row => row.Key is "RepSubject" or "Reason");
        fields.Add(new("RepSubject", "colm")); fields.Add(new("Reason", labels == "markup" ? "<img onerror=alert(1)>" : "Agreed coverage"));
        using var response = await browser.PostAsync($"/Coverage/LocationChange/{handler.Id}?handler=Preview", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); html = await response.Content.ReadAsStringAsync(); Capture(labels + "-preview", html);
        Assert.Contains("Confirm assignment", html); Assert.Contains("<summary>", html); Assert.Contains("href=\"" + page + "\">Cancel", html);
        Assert.Contains(handler.RepName, WebUtility.HtmlDecode(html)); Assert.DoesNotContain("test-access-token", html);
        if (labels == "markup")
        {
            Assert.Contains("&lt;script&gt;", html); Assert.Contains("&lt;img onerror=alert(1)&gt;", html);
            Assert.DoesNotContain("<script>Colm", html); Assert.DoesNotContain("<img onerror", html);
        }
        Assert.Equal(1, handler.Previews); Assert.Equal(0, handler.Saves);
    }
    private static async Task<string> GetHtml(HttpClient browser, string path)
    { using var r = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return await r.Content.ReadAsStringAsync(); }
    private static void Capture(string name, string html)
    {
        string? output = Environment.GetEnvironmentVariable("FIELD_SALES_LOCATION_ACTION_ARTIFACT_DIR"); if (string.IsNullOrEmpty(output)) return;
        System.IO.Directory.CreateDirectory(output); string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string css = File.ReadAllText(Path.Combine(repository, "FieldSales.Web/wwwroot/css/site.css"));
        html = Regex.Replace(html, "<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>", "<style>" + css + "</style>");
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
    }
    private sealed class Reply(string labels) : HttpMessageHandler
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid Town { get; } = Guid.NewGuid();
        public Guid Assignment { get; } = Guid.NewGuid();
        public int Previews { get; private set; }
        public int Saves { get; private set; }
        public string RepName => labels switch { "long" => new('R', 512), "markup" => "<script>Colm</script>", _ => "Colm" };
        private string Name => labels switch { "long" => new('L', 200), "markup" => "<script>Shop</script>", _ => "Walsh's Shop" };
        private string TownName => labels switch { "long" => new('T', 200), "markup" => "<img>Town", _ => "Laragh" };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter);
            var owner = labels == "unassigned" ? null : new ImpactOwnerDetails(new("aoife", "Aoife"), new(TerritoryLevel.Town, Town), TownName);
            var page = new LocationCoveragePage(Id, Name, Town, TownName, owner, 0);
            object value;
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/actions", StringComparison.Ordinal)) value = new LocationCoverageActions(page, owner is null ? null : Assignment, owner is null ? 0 : 23, owner is null, true, owner is not null);
            else if (path == "/coverage/assignment-options") value = new AssignmentReviewOptions([new("colm", RepName)],
                [new(new(TerritoryLevel.Location, Id), Name, Name), new(new(TerritoryLevel.Town, Town), TownName, TownName)], []);
            else if (path == "/coverage/assignments/preview")
            {
                var command = (await request.Content!.ReadFromJsonAsync<AddTerritoryAssignmentRequest>(ct))!;
                Assert.Equal(new TerritoryTarget(TerritoryLevel.Location, Id), command.Target); Assert.Equal("colm", command.RepSubject); Previews++;
                var next = new ImpactOwnerDetails(new("colm", RepName), command.Target!, Name);
                value = new AssignmentImpactDetails("preview-proof", "Add", Name, RepName, 1,
                    [new(owner?.Rep.Subject, "colm", "1 Location moves to " + RepName, [new(Id, Name, owner, next)])]);
            }
            else { Saves++; throw new InvalidOperationException("Unexpected save or route " + path); }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        }
    }
}
