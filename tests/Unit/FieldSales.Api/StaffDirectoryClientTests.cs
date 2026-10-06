using System.Net;
using System.Net.Http.Json;
using FieldSales.StaffAccess;
using Microsoft.Extensions.Configuration;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]

public sealed class StaffDirectoryClientTests
{
    [Fact]
    public async Task Should_ReturnTrustedLabelsAndAvailability_When_ReadOnlyLookupSucceeds()
    {
        using var client = Client(async request =>
        {
            Assert.Equal("https://identity.test/staff/directory/lookup", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            var body = await request.Content!.ReadFromJsonAsync<StaffDirectoryLookupRequest>();
            Assert.Equal(new[] { "rep", "actor" }, body!.Subjects);
            return Response([new("rep", "Colm", [BusinessRoles.FieldSalesperson], true),
                new("actor", "Niamh", [BusinessRoles.HeadOfficeUser], true)]);
        });
        var lookup = Lookup(client);
        var rows = await lookup.LookupAsync("test-token", ["rep", "actor", "rep"], default);
        Assert.Equal(2, rows.Count); Assert.Equal("Colm", rows[0].DisplayName);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("case")]
    [InlineData("blank-name")]
    [InlineData("unknown-role")]
    [InlineData("control")]
    [InlineData("null-roles")]
    public async Task Should_RejectUntrustedResponse_When_StaffLookupShapeIsInvalid(string scenario)
    {
        StaffDirectoryEntry valid = new("rep", "Colm", [BusinessRoles.FieldSalesperson], true);
        using var client = Client(_ => Task.FromResult(scenario switch
        {
            "missing" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<object?>(null) },
            "duplicate" => Response([valid, valid]),
            "foreign" => Response([valid with { Subject = "someone-else" }]),
            "case" => Response([valid with { Subject = "Rep" }]),
            "blank-name" => Response([valid with { DisplayName = " " }]),
            "unknown-role" => Response([valid with { Roles = ["SysAdmin"] }]),
            "control" => Response([valid with { DisplayName = "Colm\nAdmin" }]),
            "null-roles" => Response([valid with { Roles = null! }]),
            _ => throw new InvalidOperationException()
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => Lookup(client).LookupAsync("test-token", ["rep"], default));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Should_FailClosed_When_IdentityLookupIsRejectedOrUnavailable(HttpStatusCode status)
    {
        using var client = Client(_ => Task.FromResult(new HttpResponseMessage(status)));
        await Assert.ThrowsAsync<HttpRequestException>(() => Lookup(client).LookupAsync("test-token", ["rep"], default));
    }

    private static HttpResponseMessage Response(StaffDirectoryEntry[] rows) => new(HttpStatusCode.OK) { Content = JsonContent.Create(rows) };
    private static HttpClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) => new(new Handler(send));
    private static HttpStaffDirectory Lookup(HttpClient client) => new(client, new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Authentication:Authority"] = "https://identity.test/" }).Build());
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
