using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldSales.Directory.Contracts;
using Microsoft.AspNetCore.Authentication;

namespace FieldSales.Web.Directory;

public sealed record DirectoryResult<T>(HttpStatusCode Status, T? Value = default, string? Error = null)
{
    public bool Success => (int)Status is >= 200 and < 300 && Value is not null;
}

public sealed class DirectoryApiClient(HttpClient client, IHttpContextAccessor contexts)
{
    private const string Root = "/directory/geography";
    public Task<DirectoryResult<GeographyPage>> PageAsync(Guid? regionId, Guid? countyId, CancellationToken ct)
    {
        List<string> parameters = [];
        if (regionId is Guid region) parameters.Add($"regionId={region}");
        if (countyId is Guid county) parameters.Add($"countyId={county}");
        string query = parameters.Count == 0 ? string.Empty : "?" + string.Join('&', parameters);
        return SendAsync<GeographyPage>(HttpMethod.Get, Root + "/" + query, null, ct);
    }
    public Task<DirectoryResult<TownChoice[]>> TownChoicesAsync(CancellationToken ct) =>
        SendAsync<TownChoice[]>(HttpMethod.Get, $"{Root}/town-choices", null, ct);
    public Task<DirectoryResult<GeographyItem>> CreateAsync(string level, string? name, Guid? parentId, CancellationToken ct) =>
        SendAsync<GeographyItem>(HttpMethod.Post, $"{Root}/{level}", JsonContent.Create(new CreateGeographyRequest(name, parentId)), ct);
    public Task<DirectoryResult<GeographyItem>> RenameAsync(string level, Guid id, string? name, string? version, CancellationToken ct) =>
        SendAsync<GeographyItem>(HttpMethod.Put, $"{Root}/{level}/{id}/name", JsonContent.Create(new RenameGeographyRequest(name, version)), ct);
    public Task<DirectoryResult<GeographyImportResult>> ImportAsync(byte[] csv, CancellationToken ct)
    {
        var content = new ByteArrayContent(csv);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return SendAsync<GeographyImportResult>(HttpMethod.Post, $"{Root}/import", content, ct);
    }

    private async Task<DirectoryResult<T>> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using HttpRequestMessage request = new(method, path) { Content = content };
        string? token = contexts.HttpContext is { } context ? await context.GetTokenAsync("access_token") : null;
        if (string.IsNullOrWhiteSpace(token)) return new(HttpStatusCode.Unauthorized);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                    return new(response.StatusCode, Error: (await response.Content.ReadFromJsonAsync<GeographyError>(ct))?.Error
                        ?? "This change could not be saved.");
                return new(response.StatusCode);
            }
            T? value = await response.Content.ReadFromJsonAsync<T>(ct);
            bool valid = value switch
            {
                null => false,
                GeographyPage page => page.Path is not null && page.Items is not null && page.Level is "regions" or "counties" or "towns"
                    && page.Items.All(item => item is not null && item.Name is not null && item.Version is not null)
                    && page.Path.All(item => item is not null && item.Name is not null),
                GeographyItem item => item.Name is not null && item.Version is not null,
                _ => true
            };
            return valid ? new(response.StatusCode, value) : new(HttpStatusCode.ServiceUnavailable);
        }
        catch (JsonException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (NotSupportedException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (HttpRequestException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (Polly.Timeout.TimeoutRejectedException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (Polly.CircuitBreaker.BrokenCircuitException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(HttpStatusCode.ServiceUnavailable); }
    }
}
