using System.Net;
using System.Net.Http.Json;
using FieldSales.ReferenceData;

namespace FieldSales.Web.Catalogue;

public enum ReferenceSaveStatus { Saved, Invalid, Missing, Unavailable }
public sealed record ReferenceSaveResult(ReferenceSaveStatus Status, string? Error = null);

public sealed partial class CatalogueApiClient
{
    public Task<CatalogueReadResult<ReferenceListViewModel>> ReferenceListAsync(string key, bool showArchived, CancellationToken cancellationToken) =>
        ReadAsync<ReferenceListViewModel>($"/catalogue/reference-data/{Uri.EscapeDataString(key)}?showArchived={showArchived}", cancellationToken);

    public Task<CatalogueReadResult<ReferenceListItem>> ReferenceItemAsync(string key, Guid id, CancellationToken cancellationToken) =>
        ReadAsync<ReferenceListItem>($"/catalogue/reference-data/{Uri.EscapeDataString(key)}/{id}", cancellationToken);

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
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(ReferenceSaveStatus.Unavailable)
                : new(ReferenceSaveStatus.Invalid, errors.Values.SelectMany(messages => messages).FirstOrDefault());
        }
        return new(ReferenceSaveStatus.Unavailable);
    }
}
