using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FieldSales.Web.Catalogue;

public sealed partial class CatalogueApiClient
{
    private async Task<CatalogueReadResult<T>> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new(response.StatusCode switch
            {
                HttpStatusCode.NotFound => CatalogueReadStatus.Missing,
                HttpStatusCode.Unauthorized => CatalogueReadStatus.Unauthorized,
                HttpStatusCode.Forbidden => CatalogueReadStatus.Forbidden,
                _ => CatalogueReadStatus.Unavailable
            });
        T? body = await ReadBodyAsync<T>(response, cancellationToken);
        return !HasRequiredContent(body) ? new(CatalogueReadStatus.Unavailable) : new(CatalogueReadStatus.Found, body);
    }

    private static bool HasRequiredContent<T>(T? body) => body switch
    {
        null => false,
        ProductDetails details => details.Product is not null && details.Product.Name is not null
            && details.Breadcrumb is not null && details.PriceHistory is not null && details.Attributes is not null,
        CategoryDetails details => details.Category is not null && details.Category.Name is not null
            && details.Breadcrumb is not null && details.Children is not null && details.Products is not null,
        FieldSales.ReferenceData.ReferenceListViewModel list => list.Items is not null && list.SelectedList is not null && list.AvailableLists is not null,
        FieldSales.ReferenceData.ReferenceListItem item => item.Name is not null && item.Usage is not null,
        _ => true
    };

    private static async Task<T?> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try { return await response.Content.ReadFromJsonAsync<T>(cancellationToken); }
        catch (JsonException) { return default; }
        catch (NotSupportedException) { return default; }
        catch (HttpRequestException) { return default; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return default; }
    }

    private static async Task<IReadOnlyDictionary<string, string[]>?> ReadErrorsAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
            return (await ReadBodyAsync<ProductValidationErrors>(response, cancellationToken))?.Errors;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            ProductSaveError? error = await ReadBodyAsync<ProductSaveError>(response, cancellationToken);
            if (error?.Field is not null && error.Error is not null)
                return new Dictionary<string, string[]> { [error.Field] = [error.Error] };
        }
        return null;
    }
}
