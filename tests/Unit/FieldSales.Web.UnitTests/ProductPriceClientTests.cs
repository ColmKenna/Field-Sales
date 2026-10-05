using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FieldSales.Web.Tests;

[Trait("Category", "Unit")]

public sealed class ProductPriceClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Should_ForwardPriceAndFieldErrors_When_ApiResponds(HttpStatusCode status)
    {
        using ServiceProvider services = new ServiceCollection().AddLogging()
            .AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TokenHandler>("Test", _ => { })
            .Services.BuildServiceProvider();
        DefaultHttpContext context = new() { RequestServices = services };
        Guid id = Guid.NewGuid();
        const string duplicate = "A price already starts on 1 Nov 2026 — edit it instead";
        using ReplyHandler handler = new(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/catalogue/products/{id}/base-prices", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("server-held-access-token", request.Headers.Authorization.Parameter);
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(13.20m, body.RootElement.GetProperty("basePrice").GetDecimal());
            Assert.Equal("2026-11-01", body.RootElement.GetProperty("effectiveFrom").GetString());
            return new HttpResponseMessage(status)
            {
                Content = status == HttpStatusCode.Conflict
                    ? JsonContent.Create(new { Field = "EffectiveFrom", Error = duplicate })
                    : JsonContent.Create(new { Errors = new Dictionary<string, string[]> { ["EffectiveFrom"] = ["Enter an effective from date."] } })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://staff-api.test") };
        CatalogueApiClient client = new(http, new HttpContextAccessor { HttpContext = context });
        AddProductBasePriceResult result = await client.AddProductBasePriceAsync(id, 13.20m,
            new DateOnly(2026, 11, 1), CancellationToken.None);
        if (status == HttpStatusCode.Conflict)
        {
            Assert.Equal(AddProductBasePriceStatus.Invalid, result.Status);
            Assert.Equal(duplicate, Assert.Single(result.Errors!["EffectiveFrom"]));
        }
        else if (status == HttpStatusCode.BadRequest)
        {
            Assert.Equal(AddProductBasePriceStatus.Invalid, result.Status);
            Assert.Equal("Enter an effective from date.", Assert.Single(result.Errors!["EffectiveFrom"]));
        }
        else
        {
            Assert.Equal(status switch
            {
                HttpStatusCode.Created => AddProductBasePriceStatus.Created,
                HttpStatusCode.NotFound => AddProductBasePriceStatus.Missing,
                _ => AddProductBasePriceStatus.Unavailable
            }, result.Status);
            Assert.Null(result.Errors);
        }
    }

    private sealed class ReplyHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request);
    }

    private sealed class TokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            AuthenticationProperties properties = new();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "server-held-access-token" }]);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("Test")), properties, "Test")));
        }
    }
}
