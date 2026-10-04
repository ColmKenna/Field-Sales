using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

public sealed class AssignmentPullPageTests
{
    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task Should_AskTransferAndKeepReceiverThroughReview_When_AddOpensAHeldArea(string role)
    {
        using var handler = new Reply(); await using var app = new StaffWebsiteFactory(handler); using var browser = app.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=manager&roles=" + Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var menu = await browser.GetAsync("/Coverage/Assignments?repSubject=receiver&returnToTerritory=true"); Assert.Equal(HttpStatusCode.OK, menu.StatusCode);
        string html = await menu.Content.ReadAsStringAsync(); Assert.Contains("Rathdrum — Aoife's", WebUtility.HtmlDecode(html));
        AssignmentTransferEndToEndTests.SaveRender("pull-add", html);
        var fields = AssignmentTransferEndToEndTests.Inputs(html); fields.Add(new("TargetKey", $"Town:{handler.Unit:D}:{handler.Assignment:D}:aoife"));
        using var start = await browser.PostAsync("/Coverage/Assignments?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        using var question = await browser.GetAsync(start.Headers.Location); Assert.Equal(HttpStatusCode.OK, question.StatusCode);
        html = await question.Content.ReadAsStringAsync(); Assert.Contains("Transfer Rathdrum from Aoife to Niamh?", html);
        fields = AssignmentTransferEndToEndTests.Inputs(html); Assert.Equal("receiver", fields.Single(row => row.Key == "ReceivingRepSubject").Value);
        Assert.Single(fields, row => row.Key == "Selected"); Assert.DoesNotContain("Choose receiving rep", html);
        AssignmentTransferEndToEndTests.SaveRender("pull-question", html);
        using var preview = await browser.PostAsync("/Coverage/Transfer?handler=Preview", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        html = await preview.Content.ReadAsStringAsync(); Assert.Contains("Confirm transfer", html); Assert.Contains("repSubject=receiver\">Cancel", html);
        Assert.DoesNotContain("test-access-token", html); Assert.Equal(1, handler.Previews); Assert.Equal(0, handler.Saves);
        AssignmentTransferEndToEndTests.SaveRender("pull-impact", html);
        fields = AssignmentTransferEndToEndTests.Inputs(html); fields.Add(new("Confirmed", "true"));
        using var saved = await browser.PostAsync("/Coverage/Transfer?handler=Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode); Assert.Equal("/Coverage/Territory?repSubject=receiver", saved.Headers.Location!.OriginalString); Assert.Equal(1, handler.Saves);
    }

    [Fact]
    public async Task Should_OpenWholeBookPrefilled_When_TakeOverSelectsGivingRep()
    {
        using var handler = new Reply(); await using var app = new StaffWebsiteFactory(handler); using var browser = app.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=manager&roles=" + Uri.EscapeDataString(BusinessRoles.SalesManager));
        using var page = await browser.GetAsync("/Coverage/TakeOver?repSubject=receiver"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync(); AssignmentTransferEndToEndTests.SaveRender("takeover", html);
        var fields = AssignmentTransferEndToEndTests.Inputs(html); fields.Add(new("GivingRepSubject", "aoife"));
        using var start = await browser.PostAsync("/Coverage/TakeOver", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        using var review = await browser.GetAsync(start.Headers.Location); Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        html = await review.Content.ReadAsStringAsync(); Assert.Contains("Receiving rep: <strong>Niamh</strong>", html);
        Assert.Equal(2, AssignmentTransferEndToEndTests.Inputs(html).Count(row => row.Key == "Selected")); Assert.Equal(0, handler.Previews); Assert.Equal(0, handler.Saves);
        AssignmentTransferEndToEndTests.SaveRender("takeover-review", html);
        fields.RemoveAll(row => row.Key == "GivingRepSubject"); fields.Add(new("GivingRepSubject", "receiver"));
        using var forged = await browser.PostAsync("/Coverage/TakeOver", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, forged.StatusCode);
        Assert.Contains("Choose a rep whose assignments", await forged.Content.ReadAsStringAsync());
    }

    private sealed class Reply : HttpMessageHandler
    {
        public Guid Assignment { get; } = Guid.NewGuid(); public Guid Unit { get; } = Guid.NewGuid();
        public int Previews { get; private set; } public int Saves { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter);
            string path = request.RequestUri!.AbsolutePath;
            var receiver = new StaffChoice("receiver", "Niamh"); var source = new StaffChoice("aoife", "Aoife");
            var target = new TerritoryTarget(TerritoryLevel.Town, Unit);
            object value;
            switch (path)
            {
                case "/coverage/assignment-options": value = new AssignmentReviewOptions([receiver], [new(target, "Rathdrum", "Rathdrum — Aoife's · Town", new(Assignment, source))], []); break;
                case "/coverage/transfers/sources": value = new TransferSourceOptions(receiver, [source]); break;
                case "/coverage/transfers/review": value = new TransferReview(source, [receiver], [new(new(Assignment, target), "Rathdrum", false, 0, null, [], "Wicklow"), new(new(Guid.NewGuid(), new(TerritoryLevel.Location, Guid.NewGuid())), "Other shop", false, 1, null, [], "Wexford")]); break;
                case "/coverage/transfers/preview":
                    var command = await request.Content!.ReadFromJsonAsync<TransferAssignmentsRequest>(ct); Assert.Equal("aoife", command!.SourceRepSubject); Assert.Equal("receiver", command.ReceivingRepSubject);
                    Assert.Equal(new TransferSelection(Assignment, target), Assert.Single(command.Selections!)); Previews++;
                    value = new AssignmentImpactDetails("proof", "Transfer", "Selected assignments", "Niamh", 0, [], TransferNotices: [], TransferredAssignments: ["Rathdrum (Town)"]); break;
                case "/coverage/transfers": var save = await request.Content!.ReadFromJsonAsync<TransferAssignmentsRequest>(ct); Assert.True(save!.Confirmed); Assert.Equal("proof", save.PreviewProof); Saves++; value = new CoverageMutationResult(true, 0); break;
                default: throw new InvalidOperationException("Unexpected path " + path);
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        }
    }
}
