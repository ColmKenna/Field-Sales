extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DirectoryDbContext = CatalogueApi::FieldSales.Api.Directory.DirectoryDbContext;
using TerritoryAssignment = CatalogueApi::FieldSales.Api.Coverage.TerritoryAssignment;
using RepReportingLine = CatalogueApi::FieldSales.Api.Coverage.RepReportingLine;

namespace FieldSales.Web.Tests;

public sealed class AssignmentTransferEndToEndTests(CoverageReadApplication app) : IClassFixture<CoverageReadApplication>
{
    private const string Root = "/coverage/transfers";
    private const string Page = "/Coverage/Transfer";

    [Fact]
    public async Task I1_WholeCountyMoves117LocationsPreserving23TownCarveOuts()
    {
        var seed = await SeedAsync(true); using var api = app.CreateApiClient();
        var county = await AssignAsync("colm", TerritoryLevel.County, seed.County.Id);
        var carve = await AssignAsync("aoife", TerritoryLevel.Town, seed.Towns[5].Id);
        var command = Command("niamh", new TransferSelection(county.Id, county.Target));
        var preview = await PreviewAsync(api, command); Assert.Equal(117, preview.ChangedLocations);
        Assert.Equal("117 Locations move from Colm to Niamh Byrne", Assert.Single(preview.Groups).Sentence);
        Assert.Equal("Wicklow (County)", Assert.Single(preview.TransferredAssignments!));
        Assert.Empty(preview.TransferNotices!); Assert.Equal((2,0),await CountsAsync());
        await SaveAndAssertAsync(api, command, preview);
        Assert.Equal((2,117),await CountsAsync());
        var rows = await AssignmentsAsync(); Assert.Equal("niamh",rows.Single(row=>row.Id==county.Id).RepSubject);
        Assert.Equal("aoife",rows.Single(row=>row.Id==carve.Id).RepSubject); Assert.False(app.Staff.SawTransaction);
    }

    [Fact]
    public async Task I1_PartialThenLastTownsAndFutureTownFollowTransferredCounty()
    {
        var seed = await SeedAsync(); using var api = app.CreateApiClient();
        var county = await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        await AssignAsync("aoife",TerritoryLevel.Town,seed.Towns[5].Id);
        var partial = Command("niamh",seed.Towns.Take(3).Select(t=>new TransferSelection(county.Id,new(TerritoryLevel.Town,t.Id))).ToArray());
        await SaveAndAssertAsync(api,partial,await PreviewAsync(api,partial));
        Assert.Equal("colm",(await AssignmentsAsync()).Single(row=>row.Id==county.Id).RepSubject);
        var last = Command("ciara",seed.Towns.Skip(3).Take(2).Select(t=>new TransferSelection(county.Id,new(TerritoryLevel.Town,t.Id))).ToArray());
        var preview=await PreviewAsync(api,last);
        Assert.Equal("Wicklow (County) moves to Ciara with its last Towns.",Assert.Single(preview.TransferNotices!));
        await SaveAndAssertAsync(api,last,preview); Assert.Equal((5,5),await CountsAsync());
        var future=await PlaceAsync(api,"towns","Future Town",seed.County.Id);
        using var response=await api.PostAsJsonAsync("/directory/customers",new CreateCustomerRequest("Future customer",new("Future shop",future.Id)));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var location=(await response.Content.ReadFromJsonAsync<CustomerDetails>())!.Locations[0];
        var owner=await api.GetFromJsonAsync<LocationCoverageDetails>("/coverage/locations/"+location.Id+"/owner");
        Assert.Equal("ciara",owner!.Owner!.RepSubject); Assert.Equal(county.Id,owner.Owner.Source.AssignmentId);
    }

    [Fact]
    public async Task I1_PartialRegionThenLastCountyMovesRegion()
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var region=await AssignAsync("colm",TerritoryLevel.Region,seed.Region.Id);
        await using (var website=app.CreateWebsite())
        {
            using var browser=website.CreateBrowser();
            using var signedIn=await browser.GetAsync("/__test/sign-in?subject=niamh&roles="+Uri.EscapeDataString(BusinessRoles.HeadOfficeUser));
            string territory=await HtmlAsync(browser,"/Coverage/Territory?repSubject=colm&filter=Wexford");
            Assert.Contains("Matching Counties: Wexford",territory); Assert.DoesNotContain("No assignments match",territory);
            string reviewHtml=await HtmlAsync(browser,Page+"?repSubject=colm&filter=Wexford");
            var selected=Assert.Single(Inputs(reviewHtml),pair=>pair.Key=="Selected");
            Assert.Contains(seed.OtherCounty.Id.ToString(),selected.Value); SaveRender("region-review",reviewHtml);
        }
        var first=Command("ciara",new TransferSelection(region.Id,new(TerritoryLevel.County,seed.County.Id)));
        await SaveAndAssertAsync(api,first,await PreviewAsync(api,first));
        var last=Command("ciara",new TransferSelection(region.Id,new(TerritoryLevel.County,seed.OtherCounty.Id)));
        var preview=await PreviewAsync(api,last); Assert.Equal("South East (Region) moves to Ciara with its last Counties.",Assert.Single(preview.TransferNotices!));
        await SaveAndAssertAsync(api,last,preview); Assert.Equal(2,(await AssignmentsAsync()).Length);
        var review=await api.GetFromJsonAsync<TransferReview>(Root+"/review?sourceRepSubject=ciara");
        Assert.Equal(2,review!.Assignments.Single(row=>row.Selection.Target.Level==TerritoryLevel.Region).Children.Count);
    }

    [Fact]
    public async Task I1_ExclusionsAndMixedDirectAssignmentHistoryUseOneOperation()
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        var direct=await AssignAsync("colm",TerritoryLevel.Location,seed.Locations[0]);
        var foreign=await AssignAsync("aoife",TerritoryLevel.Town,seed.Towns[5].Id);
        var command=Command("ciara",new TransferSelection(county.Id,new(TerritoryLevel.Town,seed.Towns[0].Id)),new TransferSelection(direct.Id,direct.Target));
        var preview=await PreviewAsync(api,command); Assert.Single(preview.Groups.SelectMany(g=>g.Locations));
        await SaveAndAssertAsync(api,command,preview);
        var rows=await AssignmentsAsync(); Assert.Equal("colm",rows.Single(row=>row.Id==county.Id).RepSubject); Assert.Equal("aoife",rows.Single(row=>row.Id==foreign.Id).RepSubject);
        // A second mixed transfer changes a direct Location and inherited territory together.
        var mixed=Command("niamh",new TransferSelection(county.Id,county.Target),new TransferSelection(direct.Id,direct.Target)) with { SourceRepSubject="colm" };
        // The direct row now belongs to Ciara; foreign-row spoofing must be rejected, with no partial County save.
        using var denied=await api.PostAsJsonAsync(Root+"/preview",mixed); Assert.Equal(HttpStatusCode.BadRequest,denied.StatusCode);
        Assert.Equal("colm",(await AssignmentsAsync()).Single(row=>row.Id==county.Id).RepSubject);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task I1_WholeAndOptionalDirectTransferRecordsOnlyChangedOwners(bool moveDirect)
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        var direct=await AssignAsync("colm",TerritoryLevel.Location,seed.Locations[0]);
        var command=Command("ciara",moveDirect ? [new(county.Id,county.Target),new(direct.Id,direct.Target)] : [new(county.Id,county.Target)]);
        await SaveAndAssertAsync(api,command,await PreviewAsync(api,command));
        await using var scope=app.Api.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var entries=await db.AssignmentHistory.ToArrayAsync(); Assert.Single(entries.Select(row=>row.OperationId).Distinct());
        Assert.Equal(moveDirect,entries.Any(row=>row.Cause==OwnershipChangeCause.DirectLocationAssignment));
        Assert.Contains(entries,row=>row.Cause==OwnershipChangeCause.TerritoryAssignment);
        Assert.Equal(moveDirect ? "ciara" : "colm",(await AssignmentsAsync()).Single(row=>row.Id==direct.Id).RepSubject);
    }

    [Fact]
    public async Task I2_HistoryFailureRollsBackEveryAssignment()
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        var command=Command("ciara",new TransferSelection(county.Id,new(TerritoryLevel.Town,seed.Towns[0].Id)),new TransferSelection(county.Id,new(TerritoryLevel.Town,seed.Towns[1].Id)));
        var preview=await PreviewAsync(api,command); app.Failures.FailAfterHistorySave=true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.PostAsJsonAsync(Root,command with { PreviewProof=preview.Proof,Confirmed=true }));
        Assert.True(app.Failures.DidFailAfterSave);
        Assert.Equal((1,0),await CountsAsync()); Assert.Equal("colm",Assert.Single(await AssignmentsAsync()).RepSubject);
    }

    [Theory]
    [InlineData("geography")] [InlineData("location")] [InlineData("assignment")] [InlineData("reporting")]
    [InlineData("identity")] [InlineData("new-town")] [InlineData("moved-location")] [InlineData("expired")]
    public async Task I3_ChangedSnapshotRequiresExplicitNewConfirmation(string change)
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        var command=Command("ciara",new TransferSelection(county.Id,county.Target)); var preview=await PreviewAsync(api,command);
        await using(var scope=app.Api.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
            switch(change)
            {
                case "geography": await db.Counties.Where(row=>row.Id==seed.County.Id).ExecuteUpdateAsync(set=>set.SetProperty(row=>row.Name,"Renamed")); break;
                case "location": await db.Locations.Where(row=>row.Id==seed.Locations[0]).ExecuteUpdateAsync(set=>set.SetProperty(row=>row.Name,"Renamed shop")); break;
                case "assignment": db.TerritoryAssignments.Add(TerritoryAssignment.Create("aoife",new(TerritoryLevel.Town,seed.Towns[0].Id))); await db.SaveChangesAsync(); break;
                case "reporting": await db.RepReportingLines.Where(row=>row.RepSubject=="ciara").ExecuteUpdateAsync(set=>set.SetProperty(row=>row.ManagerSubject,"another-manager")); break;
                case "identity": app.Staff.Entries["ciara"]=app.Staff.Entries["ciara"] with {DisplayName="Ciara New"}; break;
                case "new-town": break;
                case "expired": app.ClockOverride=DateTimeOffset.UtcNow.AddMinutes(31); break;
                case "moved-location": await db.Locations.Where(row=>row.Id==seed.Locations[0]).ExecuteUpdateAsync(set=>set.SetProperty(row=>row.TownId,seed.OtherTown.Id)); break;
            }
        }
        if(change=="new-town") await PlaceAsync(api,"towns","New empty Town",seed.County.Id);
        using var stale=await api.PostAsJsonAsync(Root,command with { PreviewProof=preview.Proof,Confirmed=true });
        Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode); var refreshed=(await stale.Content.ReadFromJsonAsync<CoverageError>())!.Preview!;
        Assert.NotEqual(preview.Proof,refreshed.Proof); Assert.Equal(0,(await CountsAsync()).Item2);
        using var unconfirmed=await api.PostAsJsonAsync(Root,command with {PreviewProof=refreshed.Proof}); Assert.Equal(HttpStatusCode.BadRequest,unconfirmed.StatusCode);
        await SaveAndAssertAsync(api,command,refreshed);
    }

    [Theory]
    [InlineData("recipient")] [InlineData("selection")] [InlineData("reason")] [InlineData("proof")] [InlineData("unconfirmed")] [InlineData("actor")]
    public async Task I3_ChangedCommandOrForgedProofCannotSave(string change)
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        var command=Command("ciara",new TransferSelection(county.Id,county.Target)); var preview=await PreviewAsync(api,command);
        var submitted=command with {PreviewProof=preview.Proof,Confirmed=true};
        submitted=change switch {"recipient"=>submitted with {ReceivingRepSubject="aoife"},"selection"=>submitted with {Selections=[new TransferSelection(county.Id,new(TerritoryLevel.Town,seed.Towns[0].Id))]},"reason"=>submitted with {Reason="changed"},"proof"=>submitted with {PreviewProof="forged"},"unconfirmed"=>submitted with {Confirmed=false},_=>submitted};
        if(change=="actor")
        {
            app.Roles.SetRoles("another-manager",BusinessRoles.HeadOfficeUser);
            app.Staff.Entries["another-manager"]=app.Staff.Entries["another-manager"] with {Roles=[BusinessRoles.HeadOfficeUser]};
            api.DefaultRequestHeaders.Authorization=new("Bearer",app.Token(subject:"another-manager"));
        }
        using var response=await api.PostAsJsonAsync(Root,submitted); Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Equal((1,0),await CountsAsync());
    }

    [Theory]
    [InlineData("inactive",HttpStatusCode.BadRequest)] [InlineData("non-rep",HttpStatusCode.BadRequest)]
    [InlineData("missing",HttpStatusCode.ServiceUnavailable)] [InlineData("identity-unavailable",HttpStatusCode.ServiceUnavailable)]
    [InlineData("foreign-team",HttpStatusCode.Forbidden)] [InlineData("role-revoked",HttpStatusCode.Forbidden)] [InlineData("missing-line",HttpStatusCode.BadRequest)]
    public async Task I4_InvalidRecipientOrAuthorityCannotTransfer(string kind,HttpStatusCode expected)
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        var command=Command(kind=="foreign-team"?"brian":"ciara",new TransferSelection(county.Id,county.Target));
        var preview=await PreviewAsync(api,command);
        switch(kind)
        {
            case "inactive": app.Staff.Entries["ciara"]=app.Staff.Entries["ciara"] with {Available=false}; break;
            case "non-rep": app.Staff.Entries["ciara"]=app.Staff.Entries["ciara"] with {Roles=[BusinessRoles.SalesManager]}; break;
            case "missing": app.Staff.Entries.Remove("ciara"); break;
            case "missing-line":
                await using(var scope=app.Api.Services.CreateAsyncScope())
                    await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().RepReportingLines.Where(row=>row.RepSubject=="ciara").ExecuteDeleteAsync();
                break;
            case "identity-unavailable": app.Staff.Unavailable=true; break;
            case "foreign-team": SetManager(); break;
            case "role-revoked": app.Roles.SetRoles("niamh",BusinessRoles.FieldSalesperson); break;
        }
        using var response=await api.PostAsJsonAsync(Root,command with {PreviewProof=preview.Proof,Confirmed=true}); Assert.Equal(expected,response.StatusCode);
        Assert.Equal((1,0),await CountsAsync());
    }

    [Fact]
    public async Task I4_ArchivedCoverageAndInactiveGivingRepCanTransferToEligibleRep()
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        app.Staff.Entries["colm"]=app.Staff.Entries["colm"] with {Available=false};
        await using(var scope=app.Api.Services.CreateAsyncScope())
        { var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); await db.Towns.Where(row=>row.Id==seed.Towns[0].Id).ExecuteUpdateAsync(set=>set.SetProperty(row=>row.IsArchived,true)); }
        SetManager(); var review=await api.GetFromJsonAsync<TransferReview>(Root+"/review?sourceRepSubject=colm");
        Assert.DoesNotContain(review!.ReceivingReps,row=>row.Subject=="brian"); Assert.True(review.Assignments[0].Children.Single(row=>row.Selection.Target.UnitId==seed.Towns[0].Id).Archived);
        var command=Command("ciara",new TransferSelection(county.Id,new(TerritoryLevel.Town,seed.Towns[0].Id)));
        await SaveAndAssertAsync(api,command,await PreviewAsync(api,command));
    }

    [Theory]
    [InlineData(BusinessRoles.SalesManager)] [InlineData(BusinessRoles.HeadOfficeUser)]
    public async Task U1_U3_FilterExclusionsPreviewAndFreshConfirmationWorkThroughBff(string role)
    {
        var seed=await SeedAsync(); using var api=app.CreateApiClient(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        await AssignAsync("aoife",TerritoryLevel.Town,seed.Towns[5].Id);
        if(role==BusinessRoles.SalesManager) SetManager();
        await using var website=app.CreateWebsite(); using var browser=website.CreateBrowser();
        using var signedIn=await browser.GetAsync("/__test/sign-in?subject=niamh&roles="+Uri.EscapeDataString(role)); Assert.Equal(HttpStatusCode.NoContent,signedIn.StatusCode);
        string html=await HtmlAsync(browser,Page+"?repSubject=colm&filter=Bray");
        var townFields=Inputs(html); Assert.Single(townFields,pair=>pair.Key=="Selected");
        Assert.Contains(seed.Towns[0].Id.ToString(),townFields.Single(pair=>pair.Key=="Selected").Value);
        html=await HtmlAsync(browser,Page+"?repSubject=colm&filter=Wicklow"); SaveRender("review-"+role,html);
        Assert.Contains("assigned separately to Aoife",html); Assert.DoesNotContain("server-held",html);
        var fields=Inputs(html); Assert.Equal(6,fields.Count(pair=>pair.Key=="Selected"));
        fields.RemoveAll(pair=>pair.Key=="Selected"&&pair.Value.Contains(seed.Towns[4].Id.ToString(),StringComparison.Ordinal));
        using var choose=await browser.PostAsync(Page+"?handler=Choose",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK,choose.StatusCode);
        html=await choose.Content.ReadAsStringAsync(); Assert.Contains("Choose the receiving rep",html); fields=Inputs(html);
        fields.Add(new("ReceivingRepSubject","ciara")); fields.Add(new("Reason","Permanent transfer"));
        using var reviewed=await browser.PostAsync(Page+"?handler=Preview",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK,reviewed.StatusCode);
        html=await reviewed.Content.ReadAsStringAsync(); SaveRender("preview",html);
        Assert.Contains("4 Locations move from Colm to Ciara",html); Assert.DoesNotContain("with its last Towns",html);
        Assert.Equal((2,0),await CountsAsync()); fields=Inputs(html); fields.Add(new("Confirmed","true"));
        string oldProof=fields.Single(pair=>pair.Key=="PreviewProof").Value;
        await using(var scope=app.Api.Services.CreateAsyncScope())
        { var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); await db.Locations.Where(row=>row.Id==seed.Locations[0]).ExecuteUpdateAsync(set=>set.SetProperty(row=>row.Name,"Changed shop")); }
        using var stale=await browser.PostAsync(Page+"?handler=Save",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK,stale.StatusCode);
        html=await stale.Content.ReadAsStringAsync(); Assert.Contains("Nothing was saved",html); Assert.Equal((2,0),await CountsAsync());
        fields=Inputs(html); Assert.NotEqual(oldProof,fields.Single(pair=>pair.Key=="PreviewProof").Value); Assert.DoesNotContain(fields,pair=>pair.Key=="Confirmed");
        fields.Add(new("Confirmed","true"));
        using var saved=await browser.PostAsync(Page+"?handler=Save",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect,saved.StatusCode);
        Assert.Equal("/Coverage/Territory?repSubject=colm",saved.Headers.Location!.OriginalString); Assert.Equal((6,4),await CountsAsync());
        Assert.Equal("colm",(await AssignmentsAsync()).Single(row=>row.Id==county.Id).RepSubject);
    }

    [Fact]
    public async Task U2_EmptySelectionCancellationAndMissingCsrfNeverWrite()
    {
        var seed=await SeedAsync(); var county=await AssignAsync("colm",TerritoryLevel.County,seed.County.Id);
        await using var website=app.CreateWebsite(); using var browser=website.CreateBrowser();
        using var signedIn=await browser.GetAsync("/__test/sign-in?subject=niamh&roles="+Uri.EscapeDataString(BusinessRoles.HeadOfficeUser));
        string html=await HtmlAsync(browser,Page+"?repSubject=colm&filter=missing"); Assert.Contains("No assignments match",html);
        html=await HtmlAsync(browser,Page+"?repSubject=colm"); var fields=Inputs(html);
        using var noCsrf=await browser.PostAsync(Page+"?handler=Choose",new FormUrlEncodedContent(fields.Where(pair=>pair.Key!="__RequestVerificationToken")));
        Assert.Equal(HttpStatusCode.BadRequest,noCsrf.StatusCode);
        fields.RemoveAll(pair=>pair.Key=="Selected");
        using var empty=await browser.PostAsync(Page+"?handler=Choose",new FormUrlEncodedContent(fields)); Assert.Contains("Select at least one",await empty.Content.ReadAsStringAsync());
        await HtmlAsync(browser,"/Coverage/Territory?repSubject=colm"); Assert.Equal((1,0),await CountsAsync());
        using var api=app.CreateApiClient();
        foreach(string recipient in new[]{"", "colm"})
        { using var rejected=await api.PostAsJsonAsync(Root+"/preview",Command(recipient,new TransferSelection(county.Id,county.Target))); Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode); }
        Assert.Equal((1,0),await CountsAsync());
    }

    private static async Task<string> HtmlAsync(HttpClient browser,string path)
    { using var response=await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK,response.StatusCode); return await response.Content.ReadAsStringAsync(); }
    internal static List<KeyValuePair<string,string>> Inputs(string html)
    {
        List<KeyValuePair<string,string>> result=[];
        foreach(Match input in Regex.Matches(html,"<input[^>]*>"))
        {
            string Attr(string name)=>WebUtility.HtmlDecode(Regex.Match(input.Value,name+"=\"([^\"]*)\"").Groups[1].Value);
            if(Attr("type")=="checkbox"&&!Regex.IsMatch(input.Value,@"\bchecked\b")) continue;
            if(Attr("name") is {Length:>0} name)
            {
                if(name is not ("Selected" or "ReviewedSelections") && result.Any(pair=>pair.Key==name)) continue;
                result.Add(new(name,Attr("value")));
            }
        }
        return result;
    }
    internal static void SaveRender(string name,string html)
    {
        string? output=Environment.GetEnvironmentVariable("FIELD_SALES_TRANSFER_ARTIFACT_DIR"); if(string.IsNullOrWhiteSpace(output)) return;
        System.IO.Directory.CreateDirectory(output); string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../.."));
        string css=File.ReadAllText(Path.Combine(root,"FieldSales.Web/wwwroot/css/site.css"));
        string js=File.ReadAllText(Path.Combine(root,"FieldSales.Web/wwwroot/js/assignment-transfer.js"));
        html=Regex.Replace(html,"<link[^>]*href=\"/css/site.css[^\"]*\"[^>]*>","<style>"+css+"</style>");
        html=Regex.Replace(html,"<script[^>]*src=\"/js/assignment-transfer.js[^\"]*\"[^>]*></script>","<script>"+js+"</script>");
        File.WriteAllText(Path.Combine(output,name.Replace(' ','-')+".html"),html);
    }

    private async Task<Seed> SeedAsync(bool large=false)
    {
        await app.ResetCoverageAsync();
        app.Staff.Entries["niamh"]=app.Staff.Entries["niamh"] with {Roles=[BusinessRoles.HeadOfficeUser,BusinessRoles.FieldSalesperson]};
        app.Staff.Entries["ciara"]=new("ciara","Ciara",[BusinessRoles.FieldSalesperson],true);
        using var api=app.CreateApiClient(); var region=await PlaceAsync(api,"regions","South East");
        var county=await PlaceAsync(api,"counties","Wicklow",region.Id); var otherCounty=await PlaceAsync(api,"counties","Wexford",region.Id);
        List<GeographyItem> towns=[];
        foreach(var name in new[]{"Bray","Greystones","Wicklow Town","Arklow","Aughrim","Rathdrum"}) towns.Add(await PlaceAsync(api,"towns",name,county.Id));
        var otherTown=await PlaceAsync(api,"towns","Enniscorthy",otherCounty.Id);
        using var response=await api.PostAsJsonAsync("/directory/customers",new CreateCustomerRequest("Customer",new("Shop 0",towns[0].Id)));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode); var customer=(await response.Content.ReadFromJsonAsync<CustomerDetails>())!;
        List<Guid> locations=[customer.Locations[0].Id]; var counts=large?new[]{31,18,22,9,37,23}:new[]{1,1,1,1,1,1};
        await using var scope=app.Api.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        db.RepReportingLines.AddRange(RepReportingLine.Create("ciara","niamh"), RepReportingLine.Create("niamh","another-manager")); await db.SaveChangesAsync();
        for(int t=0;t<7;t++) for(int i=t==0?1:0;i<(t==6?1:counts[t]);i++)
        {
            Guid id=Guid.NewGuid(),town=t==6?otherTown.Id:towns[t].Id; string name="Shop "+locations.Count,normalized=name.ToUpperInvariant();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Locations] ([Id],[CustomerId],[TownId],[Name],[NormalizedName]) VALUES ({id},{customer.Id},{town},{name},{normalized})"); locations.Add(id);
        }
        return new(region,county,otherCounty,towns.ToArray(),otherTown,locations.ToArray());
    }
    private async Task<TerritoryAssignment> AssignAsync(string rep,TerritoryLevel level,Guid id)
    {
        await using var scope=app.Api.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var row=TerritoryAssignment.Create(rep,new(level,id)); db.TerritoryAssignments.Add(row); await db.SaveChangesAsync(); return row;
    }
    private static TransferAssignmentsRequest Command(string recipient,params TransferSelection[] selections)=>new("colm",recipient,selections,"Permanent transfer");
    private static async Task<AssignmentImpactDetails> PreviewAsync(HttpClient api,TransferAssignmentsRequest request)
    { using var response=await api.PostAsJsonAsync(Root+"/preview",request); Assert.Equal(HttpStatusCode.OK,response.StatusCode); return (await response.Content.ReadFromJsonAsync<AssignmentImpactDetails>())!; }
    private async Task SaveAndAssertAsync(HttpClient api,TransferAssignmentsRequest command,AssignmentImpactDetails preview)
    {
        long before; await using(var scope=app.Api.Services.CreateAsyncScope())
        { var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); before=await db.AssignmentHistory.Select(row=>(long?)row.Sequence).MaxAsync()??0; }
        using var saved=await api.PostAsJsonAsync(Root,command with {PreviewProof=preview.Proof,Confirmed=true}); Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        await using var verify=app.Api.Services.CreateAsyncScope(); var database=verify.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var entries=await database.AssignmentHistory.Where(row=>row.Sequence>before).ToArrayAsync();
        var changes=preview.Groups.SelectMany(row=>row.Locations).ToArray(); Assert.Equal(changes.Length,entries.Length);
        foreach(var change in changes)
        {
            var entry=Assert.Single(entries,row=>row.LocationId==change.LocationId); Assert.Equal(change.PreviousOwner!.Rep.Subject,entry.PreviousRepSubject);
            Assert.Equal(change.NewOwner!.Rep.Subject,entry.NewRepSubject); Assert.Equal("niamh",entry.ActorSubject); Assert.Equal(command.Reason,entry.Reason);
            var owner=await api.GetFromJsonAsync<LocationCoverageDetails>("/coverage/locations/"+change.LocationId+"/owner");
            Assert.Equal(change.NewOwner.Rep.Subject,owner!.Owner!.RepSubject); Assert.Equal(change.NewOwner.Source,owner.Owner.Source.Target); Assert.Equal(change.NewOwner.SourceName,owner.Owner.Source.Name);
            Assert.Equal(change.NewOwner.Source.Level==TerritoryLevel.Location?OwnershipChangeCause.DirectLocationAssignment:OwnershipChangeCause.TerritoryAssignment,entry.Cause);
        }
        if(entries.Length>0) Assert.Single(entries.Select(row=>row.OperationId).Distinct());
    }
    private async Task<TerritoryAssignment[]> AssignmentsAsync()
    { await using var scope=app.Api.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<DirectoryDbContext>().TerritoryAssignments.AsNoTracking().ToArrayAsync(); }
    private async Task<(int,int)> CountsAsync()
    { await using var scope=app.Api.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<DirectoryDbContext>(); return(await db.TerritoryAssignments.CountAsync(),await db.AssignmentHistory.CountAsync()); }
    private static async Task<GeographyItem> PlaceAsync(HttpClient api,string level,string name,Guid? parent=null)
    { using var response=await api.PostAsJsonAsync("/directory/geography/"+level,new CreateGeographyRequest(name,parent)); Assert.Equal(HttpStatusCode.Created,response.StatusCode); return(await response.Content.ReadFromJsonAsync<GeographyItem>())!; }
    private void SetManager()
    { app.Roles.SetRoles("niamh",BusinessRoles.SalesManager); app.Staff.Entries["niamh"]=app.Staff.Entries["niamh"] with {Roles=[BusinessRoles.SalesManager]}; }
    private sealed record Seed(GeographyItem Region,GeographyItem County,GeographyItem OtherCounty,GeographyItem[] Towns,GeographyItem OtherTown,Guid[] Locations);
}
