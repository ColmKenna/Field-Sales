using System.Net;

namespace FieldSales.Web.Catalogue;

public enum AddProductAttributeStatus { Saved, Invalid, Missing, Unavailable }
public sealed record AddProductAttributeResult(AddProductAttributeStatus Status,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed partial class CatalogueApiClient
{
    public async Task<AddProductAttributeResult> AddProductAttributeAsync(Guid id,
        AddProductAttributeRequest attribute, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"/catalogue/products/{id}/attributes",
            attribute, cancellationToken);
        if (response.IsSuccessStatusCode) return new(AddProductAttributeStatus.Saved);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(AddProductAttributeStatus.Missing);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(AddProductAttributeStatus.Unavailable)
                : new(AddProductAttributeStatus.Invalid, errors);
        }
        return new(AddProductAttributeStatus.Unavailable);
    }
}
