namespace FieldSales.Web.Catalogue;

public sealed partial class CatalogueApiClient
{
    public Task<CatalogueReadResult<ProductSearchResponse>> SearchProductsAsync(string query,
        Guid? categoryId, Guid? brandId, CancellationToken cancellationToken)
    {
        List<string> filters = ["q=" + Uri.EscapeDataString(query)];
        if (categoryId is Guid category) filters.Add("categoryId=" + category);
        if (brandId is Guid brand) filters.Add("brandId=" + brand);
        return ReadAsync<ProductSearchResponse>("/catalogue/products/search?" + string.Join('&', filters), cancellationToken);
    }
}
