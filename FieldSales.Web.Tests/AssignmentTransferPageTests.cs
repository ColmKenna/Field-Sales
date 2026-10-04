using System.Net;
using System.Net.Http.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

public sealed class AssignmentTransferPageTests
{
    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task U3_AuthenticatedTransferPagesRenderSelectionAndImpact(string role)
    {
        var id=Guid.NewGuid();
        TransferScope Child(string name,int count,string? owner=null)=>new(new(id,new(TerritoryLevel.Town,Guid.NewGuid())),name,false,count,owner is null?null:new(owner,owner),[]);
        var review=new TransferReview(new("colm","Colm"),[new("ciara","Ciara")],
            [new(new(id,new(TerritoryLevel.County,Guid.NewGuid())),"Wicklow",false,140,null,
                [Child("Bray",31),Child("Greystones",18),Child("Wicklow Town",37),Child("Arklow",22),Child("Aughrim",9),Child("Rathdrum",23,"Aoife")],"South East")]);
        using var handler=new Reply(review); await using var app=new StaffWebsiteFactory(handler); using var browser=app.CreateBrowser();
        using var login=await browser.GetAsync("/__test/sign-in?subject=niamh&roles="+Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent,login.StatusCode);
        using var page=await browser.GetAsync("/Coverage/Transfer?repSubject=colm&filter=Wicklow"); Assert.Equal(HttpStatusCode.OK,page.StatusCode);
        string html=await page.Content.ReadAsStringAsync(); Assert.Contains("assigned separately to Aoife",html); Assert.Contains("assignment-transfer.js",html);
        AssignmentTransferEndToEndTests.SaveRender("selection-"+role,html);
        var fields=AssignmentTransferEndToEndTests.Inputs(html);
        Assert.Equal(6,fields.Count(pair=>pair.Key=="Selected"));
        using var choose=await browser.PostAsync("/Coverage/Transfer?handler=Choose",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK,choose.StatusCode);
        html=await choose.Content.ReadAsStringAsync(); Assert.Contains("Choose the receiving rep",html);
        fields=AssignmentTransferEndToEndTests.Inputs(html); fields.Add(new("ReceivingRepSubject","ciara"));
        using var preview=await browser.PostAsync("/Coverage/Transfer?handler=Preview",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK,preview.StatusCode);
        html=await preview.Content.ReadAsStringAsync(); Assert.Contains("Confirm transfer",html); Assert.Contains("Wicklow (County) moves to Ciara with its last Towns.",html);
        Assert.Contains("Future matching Locations follow",html); Assert.DoesNotContain("test-access-token",html);
        AssignmentTransferEndToEndTests.SaveRender("empty-impact",html);
    }
    private sealed class Reply(TransferReview review):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("test-access-token",request.Headers.Authorization!.Parameter);
            object value=request.RequestUri!.AbsolutePath.EndsWith("/preview",StringComparison.Ordinal)
                ?new AssignmentImpactDetails("proof","Transfer","Selected","Ciara",0,[],TransferNotices:["Wicklow (County) moves to Ciara with its last Towns."],TransferredAssignments:["Wicklow (County)"]):review;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(value)});
        }
    }
}
