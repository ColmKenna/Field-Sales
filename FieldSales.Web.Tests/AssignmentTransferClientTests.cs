using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FieldSales.Web.Tests;

public sealed class AssignmentTransferClientTests
{
    [Theory]
    [InlineData("valid")] [InlineData("wrong-rep")] [InlineData("negative")]
    [InlineData("duplicate")] [InlineData("foreign-parent")] [InlineData("invalid-level")] [InlineData("null-child")]
    public async Task U3_TypedReviewRejectsMalformedResponses(string kind)
    {
        var id=Guid.NewGuid(); var child=new TransferScope(new(id,new(TerritoryLevel.Town,Guid.NewGuid())),"Bray",false,1,null,[]);
        var parent=new TransferScope(new(id,new(TerritoryLevel.County,Guid.NewGuid())),"Wicklow",false,1,null,[child]);
        var review=new TransferReview(new("colm","Colm"),[new("ciara","Ciara")],[parent]);
        review=kind switch
        {
            "wrong-rep"=>review with {SourceRep=new("other","Other")},
            "negative"=>review with {Assignments=[parent with {Locations=-1}]},
            "duplicate"=>review with {Assignments=[parent,parent]},
            "foreign-parent"=>review with {Assignments=[parent with {Children=[child with {Selection=child.Selection with {AssignmentId=Guid.NewGuid()}}]}]},
            "invalid-level"=>review with {Assignments=[parent with {Selection=parent.Selection with {Target=new((TerritoryLevel)999,Guid.NewGuid())}}]},
            "null-child"=>review with {Assignments=[parent with {Children=[null!]}]}, _=>review
        };
        using var services=Services(); using var handler=new Reply(review,"/coverage/transfers/review"); using var http=new HttpClient(handler){BaseAddress=new("https://api.test")};
        var client=new CoverageApiClient(http,new HttpContextAccessor{HttpContext=new DefaultHttpContext{RequestServices=services}});
        var result=await client.TransferReviewAsync("colm",default);
        Assert.Equal(kind=="valid"?HttpStatusCode.OK:HttpStatusCode.ServiceUnavailable,result.Status);
    }

    [Theory]
    [InlineData("valid")] [InlineData("missing-notices")] [InlineData("missing-assignments")] [InlineData("incorrect-total")] [InlineData("wrong-action")]
    public async Task U3_TypedTransferPreviewRequiresCompleteImpact(string kind)
    {
        var preview=new AssignmentImpactDetails("proof","Transfer","Selected","Ciara",0,[],TransferNotices:[],TransferredAssignments:["Wicklow (County)"]);
        preview=kind switch {"missing-notices"=>preview with {TransferNotices=null},"missing-assignments"=>preview with {TransferredAssignments=null},"wrong-action"=>preview with {Action="Add"},"incorrect-total"=>preview with {ChangedLocations=1},_=>preview};
        using var services=Services(); using var handler=new Reply(preview,"/coverage/transfers/preview"); using var http=new HttpClient(handler){BaseAddress=new("https://api.test")};
        var client=new CoverageApiClient(http,new HttpContextAccessor{HttpContext=new DefaultHttpContext{RequestServices=services}});
        var result=await client.PreviewTransferAsync(new("colm","ciara",[]),default);
        Assert.Equal(kind=="valid"?HttpStatusCode.OK:HttpStatusCode.ServiceUnavailable,result.Status);
    }
    [Theory]
    [InlineData(true,1,HttpStatusCode.OK)] [InlineData(false,1,HttpStatusCode.ServiceUnavailable)] [InlineData(true,-1,HttpStatusCode.ServiceUnavailable)]
    public async Task U3_TypedSaveValidatesReceipt(bool saved,int count,HttpStatusCode expected)
    {
        using var services=Services(); using var handler=new Reply(new CoverageMutationResult(saved,count),"/coverage/transfers"); using var http=new HttpClient(handler){BaseAddress=new("https://api.test")};
        var client=new CoverageApiClient(http,new HttpContextAccessor{HttpContext=new DefaultHttpContext{RequestServices=services}});
        var result=await client.SaveTransferAsync(new("colm","ciara",[]),default); Assert.Equal(expected,result.Status);
    }
    [Theory]
    [InlineData("valid")] [InlineData("wrong-receiver")] [InlineData("self")]
    [InlineData("duplicate")] [InlineData("invalid-source")]
    public async Task Should_RejectMalformedSourceChoices_When_LoadingTakeOver(string kind)
    {
        var choices = new TransferSourceOptions(new("receiver", "Niamh"), [new("colm", "Colm")]);
        choices = kind switch
        {
            "wrong-receiver" => choices with { ReceivingRep = new("other", "Other") },
            "self" => choices with { GivingReps = [choices.ReceivingRep] },
            "duplicate" => choices with { GivingReps = [new("colm", "Colm"), new("colm", "Colm")] },
            "invalid-source" => choices with { GivingReps = [new("", "Colm")] }, _ => choices
        };
        using var services = Services(); using var handler = new Reply(choices, "/coverage/transfers/sources");
        using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
        var client = new CoverageApiClient(http, new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } });
        var result = await client.TransferSourcesAsync("receiver", default);
        Assert.Equal(kind == "valid" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
    }

    [Theory]
    [InlineData("valid")] [InlineData("empty-id")] [InlineData("invalid-rep")] [InlineData("duplicate-target")]
    public async Task Should_RejectMalformedHolderChoices_When_LoadingAssignmentOptions(string kind)
    {
        var target = new CoverageTargetChoice(new(TerritoryLevel.Town, Guid.NewGuid()), "Rathdrum", "Rathdrum — Aoife's", new(Guid.NewGuid(), new("aoife", "Aoife")));
        target = kind switch { "empty-id" => target with { Holder = target.Holder! with { AssignmentId = Guid.Empty } },
            "invalid-rep" => target with { Holder = target.Holder! with { Rep = new("", "Aoife") } }, _ => target };
        var choices = new AssignmentReviewOptions([new("receiver", "Niamh")], kind == "duplicate-target" ? [target, target] : [target], []);
        using var services = Services(); using var handler = new Reply(choices, "/coverage/assignment-options");
        using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
        var client = new CoverageApiClient(http, new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } });
        var result = await client.OptionsAsync("receiver", default);
        Assert.Equal(kind == "valid" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
    }

    private static ServiceProvider Services()=>new ServiceCollection().AddLogging().AddAuthentication("Test")
        .AddScheme<AuthenticationSchemeOptions,TokenHandler>("Test",_=>{}).Services.BuildServiceProvider();
    private sealed class Reply(object value,string path):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        { Assert.Equal(path,request.RequestUri!.AbsolutePath); Assert.Equal("server-held-access-token",request.Headers.Authorization!.Parameter); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(value)}); }
    }
    private sealed class TokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder)
        :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            AuthenticationProperties properties=new(); properties.StoreTokens([new(){Name="access_token",Value="server-held-access-token"}]);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity("Test")),properties,"Test")));
        }
    }
}
