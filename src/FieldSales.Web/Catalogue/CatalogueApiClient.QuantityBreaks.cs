using System.Net;

namespace FieldSales.Web.Catalogue;

public enum SaveQuantityBreaksStatus { Saved, Invalid, Missing, Unavailable }
public sealed record SaveQuantityBreaksResult(SaveQuantityBreaksStatus Status, IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>A product's quantity breaks (WI-062, H-22), through the Field Sales API.</summary>
public sealed partial class CatalogueApiClient
{
    /// <summary>Replaces the product's breaks with <paramref name="breaks"/>; refusals come back keyed <c>Breaks[i].FromQuantity</c>
    /// and <c>Breaks[i].Price</c>, by position in <paramref name="breaks"/>.</summary>
    public async Task<SaveQuantityBreaksResult> SetProductQuantityBreaksAsync(Guid id, IReadOnlyList<QuantityBreakRequestItem> breaks,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, $"/catalogue/products/{id}/quantity-breaks",
            new SetProductQuantityBreaksRequest(breaks), cancellationToken);
        if (response.IsSuccessStatusCode) return new(SaveQuantityBreaksStatus.Saved);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(SaveQuantityBreaksStatus.Missing);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            return await ReadErrorsAsync(response, cancellationToken) is { } errors
                ? new(SaveQuantityBreaksStatus.Invalid, errors)
                : new(SaveQuantityBreaksStatus.Unavailable);
        return new(SaveQuantityBreaksStatus.Unavailable);
    }
}
