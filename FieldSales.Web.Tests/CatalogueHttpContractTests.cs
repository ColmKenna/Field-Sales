using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FieldSales.Web.Tests;

public sealed class CatalogueHttpContractTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound, CatalogueReadStatus.Missing)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CatalogueReadStatus.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, CatalogueReadStatus.Unavailable)]
    [InlineData(HttpStatusCode.Unauthorized, CatalogueReadStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, CatalogueReadStatus.Forbidden)]
    public async Task ReadFailures_HaveDistinctPageOutcomes(HttpStatusCode code, CatalogueReadStatus expected)
    {
        using var services = TokenServices();
        using var http = new HttpClient(new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(code))))
            { BaseAddress = new Uri("https://api.test") };
        CatalogueApiClient client = Client(http, services);
        var result = await client.ProductDetailsAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(expected, result.Status);
        Assert.Null(result.Value);
        IActionResult page = result.FailureResult();
        switch (expected)
        {
            case CatalogueReadStatus.Missing: Assert.IsType<NotFoundResult>(page); break;
            case CatalogueReadStatus.Unauthorized: Assert.IsType<ChallengeResult>(page); break;
            case CatalogueReadStatus.Forbidden: Assert.IsType<ForbidResult>(page); break;
            default: Assert.Equal(503, Assert.IsType<StatusCodeResult>(page).StatusCode); break;
        }
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task MalformedSuccess_IsUnavailable(string body)
    {
        using var services = TokenServices();
        using var http = new HttpClient(new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") })))
            { BaseAddress = new Uri("https://api.test") };
        Assert.Equal(CatalogueReadStatus.Unavailable,
            (await Client(http, services).ProductDetailsAsync(Guid.NewGuid(), CancellationToken.None)).Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task MalformedWriteError_IsUnavailable(HttpStatusCode code)
    {
        using var services = TokenServices();
        using var http = new HttpClient(new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(code)
            { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") })))
            { BaseAddress = new Uri("https://api.test") };
        CatalogueApiClient client = Client(http, services);
        Assert.Equal(AddProductBasePriceStatus.Unavailable,
            (await client.AddProductBasePriceAsync(Guid.NewGuid(), 1m, new(2026, 1, 1), CancellationToken.None)).Status);
        Assert.Equal(CreateProductStatus.Unavailable,
            (await client.CreateProductAsync("code", "name", Guid.NewGuid(), 1m, CancellationToken.None)).Status);
        Assert.Equal(ReferenceSaveStatus.Unavailable,
            (await client.SaveReferenceNameAsync("brands", null, "name", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task NetworkFailure_IsUnavailableAndCallerCancellationPropagates()
    {
        using var services = TokenServices();
        using var http = new HttpClient(new ReplyHandler((_, _) => throw new HttpRequestException("offline")))
            { BaseAddress = new Uri("https://api.test") };
        CatalogueApiClient client = Client(http, services);
        Assert.Equal(CatalogueReadStatus.Unavailable, (await client.RootsAsync(CancellationToken.None)).Status);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RootsAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CreateAsync("name", null, canceled.Token));
    }

    [Fact]
    public async Task CancellationDuringSend_Propagates()
    {
        using var services = TokenServices();
        using CancellationTokenSource canceled = new();
        using var http = new HttpClient(new ReplyHandler(async (_, token) =>
        {
            canceled.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("https://api.test") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http, services).RootsAsync(canceled.Token));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task StaffWrite_IsSentExactlyOnceDespiteInheritedRetryPolicy(string method)
    {
        int sent = 0;
        ServiceCollection services = new();
        services.AddLogging();
        services.ConfigureHttpClientDefaults(client => client.AddStandardResilienceHandler());
        services.AddHttpClient("staff", client => client.BaseAddress = new Uri("https://api.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new ReplyHandler((_, _) =>
            {
                Interlocked.Increment(ref sent);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            })).AddSafeReadResilience();
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("staff");
        using HttpResponseMessage response = await http.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/write"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, sent);
    }

    [Fact]
    public async Task SafeRead_RetainsRetrySupport()
    {
        int sent = 0;
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHttpClient("staff", client => client.BaseAddress = new Uri("https://api.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new ReplyHandler((_, _) => Task.FromResult(
                new HttpResponseMessage(++sent == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK))))
            .AddSafeReadResilience();
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("staff");
        using HttpResponseMessage response = await http.GetAsync("/read");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, sent);
    }

    [Fact]
    public void Contracts_RoundTripOptionalAndHistoricalFields()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        ProductDetails product = new(new(Guid.NewGuid(), "TEA", "Tea", Guid.NewGuid(), "kg", 0.5m, 1m),
            [new(Guid.NewGuid(), "Category")], new(new(2026, 9, 1), 12.50m),
            [new(new(2026, 9, 1), 12.50m)], [new("colour", "green", true)],
            [new(Guid.NewGuid(), "Brand", true, true)], new(Guid.NewGuid(), "Profile", true), null);
        string json = JsonSerializer.Serialize(product, options);
        ProductDetails copy = JsonSerializer.Deserialize<ProductDetails>(json, options)!;
        Assert.Equal(product.Product, copy.Product);
        Assert.Equal(product.CurrentPrice, copy.CurrentPrice);
        Assert.Equal(product.Brands, copy.Brands);
        Assert.Equal(product.Attributes, copy.Attributes);
        Assert.Equal(product.Profile, copy.Profile);
        Assert.Null(copy.Supplier);
        ProductItem legacy = JsonSerializer.Deserialize<ProductItem>(
            """{"id":"00000000-0000-0000-0000-000000000001","code":"TEA","name":"Tea","categoryId":"00000000-0000-0000-0000-000000000002","unit":"Each"}""", options)!;
        Assert.Null(legacy.QuantityStep);
        Assert.Null(legacy.MinimumQuantity);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("2026-09-01", document.RootElement.GetProperty("currentPrice").GetProperty("effectiveFrom").GetString());
    }

    private static ServiceProvider TokenServices() => new ServiceCollection().AddLogging()
        .AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TokenHandler>("Test", _ => { })
        .Services.BuildServiceProvider();

    private static CatalogueApiClient Client(HttpClient http, ServiceProvider services) => new(http,
        new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } });

    private sealed class ReplyHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request, cancellationToken);
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
