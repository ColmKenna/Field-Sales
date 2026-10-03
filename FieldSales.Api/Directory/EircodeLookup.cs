using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenTelemetry;

namespace FieldSales.Api.Directory;

public enum EircodeLookupStatus { Found, NotFound, Unavailable }
public sealed record EircodeLookupResult(EircodeLookupStatus Status, Coordinates? Coordinates = null);
public interface IEircodeLookup
{
    Task<EircodeLookupResult> LookupAsync(string eircode, CancellationToken ct);
}

public sealed class PostcoderOptions
{
    public const string Section = "LocationCoordinates:Postcoder";
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class PostcoderEircodeLookup(HttpClient client, IOptions<PostcoderOptions> options) : IEircodeLookup
{
    public const string ClientName = "postcoder-position";
    private const int MaximumResponseBytes = 64 * 1024;
    public async Task<EircodeLookupResult> LookupAsync(string eircode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(options.Value.ApiKey))
            return new(EircodeLookupStatus.Unavailable);
        string code = string.Concat(eircode.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
        if (code.Length != 7 || code.Any(character => !char.IsAsciiLetterOrDigit(character)))
            return new(EircodeLookupStatus.NotFound);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        // Postcoder puts the secret in the URL path. This client has no request
        // logging or retries; suppress automatic tracing for the whole operation.
        using var suppression = SuppressInstrumentationScope.Begin();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://ws.postcoder.com/pcw/{Uri.EscapeDataString(options.Value.ApiKey)}/position/ie/{Uri.EscapeDataString(code)}?format=json");
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(EircodeLookupStatus.NotFound);
            if (response.StatusCode != HttpStatusCode.OK) return new(EircodeLookupStatus.Unavailable);
            if (response.Content.Headers.ContentLength > MaximumResponseBytes) return new(EircodeLookupStatus.Unavailable);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            byte[] bytes = new byte[MaximumResponseBytes + 1];
            int count = 0;
            while (count < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes.AsMemory(count), timeout.Token);
                if (read == 0) break;
                count += read;
            }
            if (count > MaximumResponseBytes) return new(EircodeLookupStatus.Unavailable);
            using var json = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 16 });
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() != 1)
                return new(EircodeLookupStatus.NotFound);
            var item = json.RootElement[0];
            if (item.ValueKind != JsonValueKind.Object || !Number(item, "latitude", out var latitude)
                || !Number(item, "longitude", out var longitude)) return new(EircodeLookupStatus.NotFound);
            try { return new(EircodeLookupStatus.Found, new(latitude, longitude)); }
            catch (CustomerDirectoryValidationException) { return new(EircodeLookupStatus.NotFound); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(EircodeLookupStatus.Unavailable); }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        { return new(EircodeLookupStatus.Unavailable); }
    }

    private static bool Number(JsonElement item, string name, out decimal number)
    {
        number = 0;
        if (!item.TryGetProperty(name, out var value)) return false;
        return value.ValueKind == JsonValueKind.Number ? value.TryGetDecimal(out number)
            : value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number);
    }
}

public static class LocationCoordinateServices
{
    public static IServiceCollection AddLocationCoordinates(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostcoderOptions>(configuration.GetSection(PostcoderOptions.Section));
        services.AddScoped<LocationPositionResolver>();
        services.AddHttpClient<IEircodeLookup, PostcoderEircodeLookup>(PostcoderEircodeLookup.ClientName)
            .RemoveAllLoggers()
            // Fixed external endpoint: remove the host's default retry/discovery
            // handlers so a paid lookup has exactly one bounded request.
            .ConfigureAdditionalHttpMessageHandlers((handlers, _) => handlers.Clear())
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
        return services;
    }
}
