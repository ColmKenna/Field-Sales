using System.Net;
using System.Text;
using FieldSales.Api.Directory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]

public sealed class EircodeLookupTests
{
    [Theory]
    [InlineData("[{\"latitude\":\"53.332067\",\"longitude\":\"-6.255492\"}]")]
    [InlineData("[{\"latitude\":53.332067,\"longitude\":-6.255492}]")]
    public async Task Should_ReadAnUnambiguousPosition_AndEncodeTheIrishRequest(string body)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Json(body)));
        using var http = new HttpClient(handler);
        var result = await Lookup(http).LookupAsync(" d02 x285 ", default);
        Assert.Equal(new(EircodeLookupStatus.Found, new(53.332067m, -6.255492m)), result);
        Assert.Equal("https://ws.postcoder.com/pcw/test-key/position/ie/D02X285?format=json", handler.LastUri!.AbsoluteUri);
        Assert.Equal(1, handler.Count);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{}]")]
    [InlineData("[{\"latitude\":53}]")]
    [InlineData("[{\"latitude\":91,\"longitude\":-6}]")]
    [InlineData("[{\"latitude\":53,\"longitude\":181}]")]
    [InlineData("[{\"latitude\":\"NaN\",\"longitude\":\"-6\"}]")]
    [InlineData("[{\"latitude\":53,\"longitude\":-6},{\"latitude\":54,\"longitude\":-7}]")]
    public async Task Should_ReturnNotFound_When_ResponseHasNoSingleValidCoordinatePair(string body)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Json(body)));
        using var http = new HttpClient(handler);
        Assert.Equal(new(EircodeLookupStatus.NotFound), await Lookup(http).LookupAsync("D02X285", default));
    }

    [Theory]
    [InlineData(false, "test-key", "D02X285", EircodeLookupStatus.Unavailable)]
    [InlineData(true, null, "D02X285", EircodeLookupStatus.Unavailable)]
    [InlineData(true, "test-key", "bad", EircodeLookupStatus.NotFound)]
    [InlineData(true, "test-key", "D02/285", EircodeLookupStatus.NotFound)]
    public async Task Should_MakeNoRequest_When_DisabledUnconfiguredOrInputIsInvalid(bool enabled, string? key, string eircode, EircodeLookupStatus expected)
    {
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("Unexpected external request"));
        using var http = new HttpClient(handler);
        var lookup = new PostcoderEircodeLookup(http, Options.Create(new PostcoderOptions { Enabled = enabled, ApiKey = key }));
        Assert.Equal(expected, (await lookup.LookupAsync(eircode, default)).Status); Assert.Equal(0, handler.Count);
    }

    [Theory]
    [InlineData(404, EircodeLookupStatus.NotFound)]
    [InlineData(401, EircodeLookupStatus.Unavailable)]
    [InlineData(429, EircodeLookupStatus.Unavailable)]
    [InlineData(503, EircodeLookupStatus.Unavailable)]
    public async Task Should_ReturnTypedFailure_When_ProviderRejectsTheRequest(int code, EircodeLookupStatus expected)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)));
        using var http = new HttpClient(handler);
        Assert.Equal(expected, (await Lookup(http).LookupAsync("D02X285", default)).Status);
    }

    [Fact]
    public async Task Should_ReturnUnavailable_When_TransportOrPayloadIsUnusable()
    {
        foreach (string mode in new[] { "network", "invalid-json", "oversized" })
        {
            using var handler = new StubHandler((_, _) => mode == "network" ? throw new HttpRequestException("private provider detail")
                : Task.FromResult(Json(mode == "invalid-json" ? "<bad response>" : new string('x', 65537))));
            using var http = new HttpClient(handler);
            Assert.Equal(new(EircodeLookupStatus.Unavailable), await Lookup(http).LookupAsync("D02X285", default));
        }
    }

    [Fact]
    public async Task Should_BoundTheLookup_AndPreserveCallerCancellation()
    {
        using var handler = new StubHandler(async (_, ct) =>
        { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Json("[]"); });
        using var http = new HttpClient(handler);
        Assert.Equal(EircodeLookupStatus.Unavailable, (await Lookup(http).LookupAsync("D02X285", default).WaitAsync(TimeSpan.FromSeconds(10))).Status);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Lookup(http).LookupAsync("D02X285", cancellation.Token));
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Should_AvoidRetriesAndSecretLogging_InTheRegisteredClient()
    {
        const string secret = "private-test-key";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [PostcoderOptions.Section + ":Enabled"] = "true", [PostcoderOptions.Section + ":ApiKey"] = secret }).Build();
        var logs = new CaptureLogs();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.ConfigureHttpClientDefaults(client => client.AddStandardResilienceHandler());
        services.AddLocationCoordinates(configuration);
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        services.AddHttpClient(PostcoderEircodeLookup.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IEircodeLookup>().LookupAsync("D02X285", default);
        Assert.Equal(EircodeLookupStatus.Unavailable, result.Status); Assert.Equal(1, handler.Count);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(secret) || message.Contains("D02X285"));
    }

    private static PostcoderEircodeLookup Lookup(HttpClient http) => new(http, Options.Create(new PostcoderOptions { Enabled = true, ApiKey = "test-key" }));
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Count++; LastUri = request.RequestUri; return send(request, ct); }
    }
    private sealed class CaptureLogs : ILoggerProvider, ILogger
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { lock (Messages) Messages.Add(formatter(state, exception)); }
    }
}
