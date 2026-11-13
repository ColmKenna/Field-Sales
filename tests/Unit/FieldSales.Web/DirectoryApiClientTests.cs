using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FieldSales.Web.Tests;

/// <summary>Characterization tests: they pin what <see cref="DirectoryApiClient"/> does today with each kind of API reply.</summary>
[Trait("Category", "Unit")]
public sealed class DirectoryApiClientTests
{
    private static readonly TownChoice Town = new(Guid.NewGuid(), "Rathdrum", Guid.NewGuid(), "Wicklow", Guid.NewGuid(), "Leinster", "Rathdrum, Wicklow");
    private static readonly DirectoryTypeChoice Role = new(Guid.NewGuid(), "Owner", null);
    private static readonly ContactChoice Contact = new(Guid.NewGuid(), "Mary Byrne", Role, default, null, null);

    [Fact]
    public async Task Should_ReturnUnauthorizedWithoutCallingTheApi_When_NoAccessTokenIsHeld()
    {
        using var handler = new ReplyHandler(_ => Json(HttpStatusCode.OK, Town));
        using var http = new HttpClient(handler) { BaseAddress = new("https://staff-api.test") };
        var client = new DirectoryApiClient(http, new HttpContextAccessor());

        DirectoryResult<TownChoice> result = await client.TownReferenceAsync(Town.Id, default);

        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
        Assert.False(result.Success);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Should_ReturnTheValueAndSendTheBearerToken_When_TheReplyIsValid()
    {
        using var handler = new ReplyHandler(_ => Json(HttpStatusCode.OK, Town));

        DirectoryResult<TownChoice> result = await Client(handler).TownReferenceAsync(Town.Id, default);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.True(result.Success);
        Assert.Equal(Town, result.Value);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal($"/directory/geography/towns/{Town.Id}/reference", handler.Path);
        Assert.Equal("server-held-access-token", handler.Token);
    }

    [Theory]
    [InlineData("empty-id")] [InlineData("null-name")] [InlineData("null-label")] [InlineData("null-county")]
    [InlineData("null-region")] [InlineData("null-json")] [InlineData("malformed-json")]
    public async Task Should_ReturnServiceUnavailableWithoutAValue_When_ATownReplyIsNotUsable(string kind)
    {
        TownChoice town = kind switch
        {
            "empty-id" => Town with { Id = Guid.Empty },
            "null-name" => Town with { Name = null! },
            "null-label" => Town with { Label = null! },
            "null-county" => Town with { CountyName = null! },
            "null-region" => Town with { RegionName = null! },
            _ => Town
        };
        using var handler = new ReplyHandler(_ => kind switch
        {
            "null-json" => Json<object?>(HttpStatusCode.OK, null),
            "malformed-json" => new(HttpStatusCode.OK) { Content = new StringContent("{invalid", Encoding.UTF8, "application/json") },
            _ => Json(HttpStatusCode.OK, town)
        });

        DirectoryResult<TownChoice> result = await Client(handler).TownReferenceAsync(Town.Id, default);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.False(result.Success);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("valid", true)] [InlineData("one-invalid", false)] [InlineData("null-entry", false)] [InlineData("empty", true)]
    public async Task Should_AcceptATownListOnlyWhenEveryTownIsUsable_When_TownChoicesAreRead(string kind, bool accepted)
    {
        TownChoice?[] towns = kind switch
        {
            "one-invalid" => [Town, Town with { Id = Guid.Empty }],
            "null-entry" => [Town, null],
            "empty" => [],
            _ => [Town]
        };
        using var handler = new ReplyHandler(_ => Json(HttpStatusCode.OK, towns));

        DirectoryResult<TownChoice[]> result = await Client(handler).TownChoicesAsync(default);

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.Equal(accepted, result.Success);
        Assert.Equal("/directory/geography/town-choices", handler.Path);
    }

    [Theory]
    [InlineData("regions", true)] [InlineData("counties", true)] [InlineData("towns", true)] [InlineData("streets", false)]
    [InlineData("null-path", false)] [InlineData("null-items", false)] [InlineData("item-without-usage", false)]
    [InlineData("item-without-version", false)] [InlineData("path-without-name", false)]
    public async Task Should_AcceptAGeographyPageOnlyWhenItsLevelAndRowsAreUsable_When_APageIsRead(string kind, bool accepted)
    {
        var item = new GeographyItem(Guid.NewGuid(), "Wicklow", null, "AAAAAAAAB9E=", Usage: new([]));
        var path = new GeographyPath(Guid.NewGuid(), "Leinster", "regions");
        var page = new GeographyPage(accepted ? kind : "regions", null, [path], [item]);
        page = kind switch
        {
            "streets" => page with { Level = "streets" },
            "null-path" => page with { Path = null! },
            "null-items" => page with { Items = null! },
            "item-without-usage" => page with { Items = [item with { Usage = null }] },
            "item-without-version" => page with { Items = [item with { Version = null! }] },
            "path-without-name" => page with { Path = [path with { Name = null! }] },
            _ => page
        };
        using var handler = new ReplyHandler(_ => Json(HttpStatusCode.OK, page));

        DirectoryResult<GeographyPage> result = await Client(handler).PageAsync(null, null, default);

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.Equal(accepted, result.Success);
    }

    [Theory]
    [InlineData(true, true)] [InlineData(false, false)]
    public async Task Should_TreatAnUnsavedMutationAsUnavailable_When_ARetireReplyIsRead(bool saved, bool accepted)
    {
        Guid id = Guid.NewGuid();
        using var handler = new ReplyHandler(_ => Json(HttpStatusCode.OK, new GeographyMutationResult(saved)));

        DirectoryResult<GeographyMutationResult> result = await Client(handler).RetireAsync("towns", id, default, "AAAAAAAAB9E=", default);

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal($"/directory/geography/towns/{id}/retire", handler.Path);
    }

    [Fact]
    public async Task Should_AcceptAReplyTypeThatHasNoChecks_When_AnImportResultIsRead()
    {
        var imported = new GeographyImportResult(1, 2, 3, 4);
        using var handler = new ReplyHandler(_ => Json(HttpStatusCode.OK, imported));

        DirectoryResult<GeographyImportResult> result = await Client(handler).ImportAsync([1, 2, 3], default);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal(imported, result.Value);
        Assert.Equal("text/csv", handler.ContentType);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)] [InlineData(HttpStatusCode.Conflict)]
    public async Task Should_CarryTheApiErrorAndField_When_TheApiRefusesAChange(HttpStatusCode status)
    {
        using var handler = new ReplyHandler(_ => Json(status, new { error = "Enter a name.", field = "Name", requiresDuplicateConfirmation = true }));

        DirectoryResult<GeographyItem> result = await Client(handler).CreateAsync("towns", null, null, default);

        Assert.Equal(status, result.Status);
        Assert.False(result.Success);
        Assert.Equal("Enter a name.", result.Error);
        Assert.Equal("Name", result.Field);
        Assert.True(result.RequiresDuplicateConfirmation);
        Assert.Null(result.Confirmation);
    }

    [Fact]
    public async Task Should_UseTheDefaultMessage_When_ARefusalHasNoBody()
    {
        using var handler = new ReplyHandler(_ => Json<object?>(HttpStatusCode.Conflict, null));

        DirectoryResult<GeographyItem> result = await Client(handler).CreateAsync("towns", "Rathdrum", null, default);

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("This change could not be saved.", result.Error);
        Assert.Null(result.Field);
        Assert.False(result.RequiresDuplicateConfirmation);
    }

    [Theory]
    [InlineData("valid", true)] [InlineData("outgoing-without-id", false)] [InlineData("proposed-without-type", false)]
    [InlineData("no-location-version", false)]
    public async Task Should_KeepAMainContactConfirmationOnlyWhenItIsComplete_When_TheApiAsksForOne(string kind, bool kept)
    {
        var confirmation = new MainContactConfirmation(Contact, Contact with { Id = Guid.NewGuid(), Name = "Sean Doyle" }, "AAAAAAAAB9E=");
        confirmation = kind switch
        {
            "outgoing-without-id" => confirmation with { Outgoing = Contact with { Id = Guid.Empty } },
            "proposed-without-type" => confirmation with { Proposed = Contact with { Type = null! } },
            "no-location-version" => confirmation with { LocationVersion = null! },
            _ => confirmation
        };
        Guid location = Guid.NewGuid();
        using var handler = new ReplyHandler(_ => Json(HttpStatusCode.Conflict, new { error = "Confirm the replacement.", confirmation }));

        DirectoryResult<ContactMutationResult> result = await Client(handler)
            .SetMainContactAsync(location, new SetMainContactRequest(Contact.Id, "AAAAAAAAB9E=", null), default);

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("Confirm the replacement.", result.Error);
        Assert.Equal(kept, result.Confirmation is not null);
        Assert.Equal($"/directory/locations/{location}/main-contact", handler.Path);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)] [InlineData(HttpStatusCode.Forbidden)] [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Should_ReturnOnlyTheStatus_When_TheApiFailsWithoutARefusal(HttpStatusCode status)
    {
        using var handler = new ReplyHandler(_ => Json(status, new { error = "ignored" }));

        DirectoryResult<TownChoice> result = await Client(handler).TownReferenceAsync(Town.Id, default);

        Assert.Equal(status, result.Status);
        Assert.Null(result.Error);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("outage")] [InlineData("timeout")] [InlineData("malformed-refusal")]
    public async Task Should_ReturnServiceUnavailable_When_TheApiCannotBeReachedOrRead(string kind)
    {
        using var handler = new ReplyHandler(_ => kind switch
        {
            "outage" => throw new HttpRequestException("Unavailable"),
            "timeout" => throw new TaskCanceledException("Timed out"),
            _ => new(HttpStatusCode.BadRequest) { Content = new StringContent("{invalid", Encoding.UTF8, "application/json") }
        });

        DirectoryResult<TownChoice> result = await Client(handler).TownReferenceAsync(Town.Id, default);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_TheCallerCancels()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        using var handler = new ReplyHandler(_ => throw new OperationCanceledException(cancelled.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).TownReferenceAsync(Town.Id, cancelled.Token));
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) => new(status) { Content = JsonContent.Create(value) };

    private static DirectoryApiClient Client(ReplyHandler handler)
    {
        ServiceProvider services = new ServiceCollection().AddLogging().AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TokenHandler>("Test", _ => { }).Services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new("https://staff-api.test") };
        return new(http, new HttpContextAccessor { HttpContext = context });
    }

    private sealed class ReplyHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Token { get; private set; }
        public string? ContentType { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Token = request.Headers.Authorization?.Parameter;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            return Task.FromResult(reply(request));
        }
    }

    private sealed class TokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            AuthenticationProperties properties = new();
            properties.StoreTokens([new() { Name = "access_token", Value = "server-held-access-token" }]);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity("Test")), properties, "Test")));
        }
    }
}
