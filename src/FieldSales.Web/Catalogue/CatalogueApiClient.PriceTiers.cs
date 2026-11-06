using System.Net;

namespace FieldSales.Web.Catalogue;

public enum PriceTierSaveStatus { Saved, Invalid, Missing, Unavailable }
public sealed record PriceTierSaveResult(PriceTierSaveStatus Status, IReadOnlyDictionary<string, string[]>? Errors = null, Guid? Id = null);

/// <summary>Head office's percentage tiers (WI-060, H-20), through the Field Sales API's <c>/pricing/tiers</c>.</summary>
public sealed partial class CatalogueApiClient
{
    private const string TiersPath = "/pricing/tiers";

    public Task<CatalogueReadResult<IReadOnlyList<PriceTierListItem>>> ReadPriceTiersAsync(CancellationToken cancellationToken) =>
        ReadAsync<IReadOnlyList<PriceTierListItem>>(TiersPath, cancellationToken);

    public Task<CatalogueReadResult<PriceTierDetails>> ReadPriceTierAsync(Guid id, CancellationToken cancellationToken) =>
        ReadAsync<PriceTierDetails>($"{TiersPath}/{id}", cancellationToken);

    public async Task<PriceTierSaveResult> CreatePriceTierAsync(string? name, decimal? percentOff, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, TiersPath, new SavePriceTierRequest(name, percentOff),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created)
            return await ReadBodyAsync<PriceTierListItem>(response, cancellationToken) is { } created
                ? new(PriceTierSaveStatus.Saved, Id: created.Id)
                : new(PriceTierSaveStatus.Unavailable);
        return await SavedAsync(response, cancellationToken);
    }

    public async Task<PriceTierSaveResult> ChangePriceTierAsync(Guid id, string? name, decimal? percentOff,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, $"{TiersPath}/{id}",
            new SavePriceTierRequest(name, percentOff), cancellationToken);
        return await SavedAsync(response, cancellationToken);
    }

    public async Task<PriceTierSaveResult> SetPriceTierOverrideAsync(Guid id, Guid productId, decimal? percentOff, decimal? fixedPrice,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, $"{TiersPath}/{id}/overrides/{productId}",
            new SetPriceTierOverrideRequest(percentOff, fixedPrice), cancellationToken);
        return await SavedAsync(response, cancellationToken);
    }

    public async Task<PriceTierSaveResult> RemovePriceTierOverrideAsync(Guid id, Guid productId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Delete, $"{TiersPath}/{id}/overrides/{productId}", null,
            cancellationToken);
        return await SavedAsync(response, cancellationToken);
    }

    private static async Task<PriceTierSaveResult> SavedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return new(PriceTierSaveStatus.Saved);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(PriceTierSaveStatus.Missing);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            return await ReadErrorsAsync(response, cancellationToken) is { } errors
                ? new(PriceTierSaveStatus.Invalid, errors)
                : new(PriceTierSaveStatus.Unavailable);
        return new(PriceTierSaveStatus.Unavailable);
    }
}
