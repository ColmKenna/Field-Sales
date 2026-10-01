using System.Net;

namespace FieldSales.Web.Catalogue;

public enum SetProductClassificationStatus { Saved, Invalid, Missing, Unavailable }
public sealed record SetProductClassificationResult(SetProductClassificationStatus Status,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed partial class CatalogueApiClient
{
    public async Task<SetProductClassificationResult> SetProductClassificationAsync(Guid id,
        SetProductClassificationRequest classification, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, $"/catalogue/products/{id}/classification",
            classification, cancellationToken);
        if (response.IsSuccessStatusCode) return new(SetProductClassificationStatus.Saved);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(SetProductClassificationStatus.Missing);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(SetProductClassificationStatus.Unavailable)
                : new(SetProductClassificationStatus.Invalid, errors);
        }
        return new(SetProductClassificationStatus.Unavailable);
    }
}
