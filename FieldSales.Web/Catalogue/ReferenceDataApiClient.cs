using System.Net;
using System.Net.Http.Json;
using FieldSales.ReferenceData;

namespace FieldSales.Web.Catalogue;

public enum ReferenceSaveStatus { Saved, Invalid, Missing, Unavailable }
public sealed record ReferenceSaveResult(ReferenceSaveStatus Status, string? Error = null);

public sealed partial class CatalogueApiClient
{
    public async Task<ReferenceListViewModel?> ReferenceListAsync(string key, bool showArchived,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"/catalogue/reference-data/{Uri.EscapeDataString(key)}?showArchived={showArchived}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReferenceListViewModel>(cancellationToken);
    }

    public async Task<ReferenceListItem?> ReferenceItemAsync(string key, Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"/catalogue/reference-data/{Uri.EscapeDataString(key)}/{id}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReferenceListItem>(cancellationToken);
    }

    public async Task<ReferenceSaveResult> SaveReferenceNameAsync(string key, Guid? id, string name,
        CancellationToken cancellationToken)
    {
        string path = $"/catalogue/reference-data/{Uri.EscapeDataString(key)}";
        using var response = await SendAsync(id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? path : $"{path}/{id}/name", new { Name = name }, cancellationToken);
        return await ReferenceResultAsync(response, cancellationToken);
    }

    public async Task<ReferenceSaveResult> RetireReferenceAsync(string key, Guid id, ReferenceAction action,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"/catalogue/reference-data/{Uri.EscapeDataString(key)}/{id}/retire", new { Action = action }, cancellationToken);
        return await ReferenceResultAsync(response, cancellationToken);
    }

    private static async Task<ReferenceSaveResult> ReferenceResultAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return new(ReferenceSaveStatus.Saved);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(ReferenceSaveStatus.Missing);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadFromJsonAsync<ReferenceError>(cancellationToken);
            return new(ReferenceSaveStatus.Invalid, body?.Error);
        }
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadFromJsonAsync<ProductValidationErrors>(cancellationToken);
            return new(ReferenceSaveStatus.Invalid, body?.Errors.Values.SelectMany(errors => errors).FirstOrDefault());
        }
        return new(ReferenceSaveStatus.Unavailable);
    }
    private sealed record ReferenceError(string Field, string Error);
}
