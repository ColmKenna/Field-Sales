using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;

namespace FieldSales.Web.Catalogue;

public enum CreateCategoryStatus { Created, Duplicate, ParentMissing, Invalid, Unavailable }
public sealed record CreateCategoryResult(CreateCategoryStatus Status, CategoryItem? Category = null);
public enum RenameCategoryStatus { Renamed, Duplicate, Missing, Invalid, Unavailable }
public sealed record RenameCategoryResult(RenameCategoryStatus Status, CategoryItem? Category = null);
public enum CreateProductStatus { Created, Invalid, Unavailable }
public sealed record CreateProductResult(CreateProductStatus Status, ProductItem? Product = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);
public enum UpdateProductUnitStatus { Saved, Invalid, Missing, Unavailable }
public sealed record UpdateProductUnitResult(UpdateProductUnitStatus Status,
    IReadOnlyDictionary<string, string[]>? Errors = null);
public enum AddProductBasePriceStatus { Created, Invalid, Missing, Unavailable }
public sealed record AddProductBasePriceResult(AddProductBasePriceStatus Status,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed partial class CatalogueApiClient(HttpClient client, IHttpContextAccessor contexts)
{
    public Task<CatalogueReadResult<IReadOnlyList<CategoryItem>>> RootsAsync(CancellationToken cancellationToken) =>
        ReadAsync<IReadOnlyList<CategoryItem>>("/catalogue/categories/", cancellationToken);

    public Task<CatalogueReadResult<CategoryDetails>> DetailsAsync(Guid id, CancellationToken cancellationToken) =>
        ReadAsync<CategoryDetails>($"/catalogue/categories/{id}", cancellationToken);

    public Task<CatalogueReadResult<IReadOnlyList<CategorySearchResult>>> SearchCategoriesAsync(string query, CancellationToken cancellationToken) =>
        ReadAsync<IReadOnlyList<CategorySearchResult>>($"/catalogue/categories/search?q={Uri.EscapeDataString(query)}", cancellationToken);

    public async Task<CreateCategoryResult> CreateAsync(string name, Guid? parentId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post,
            "/catalogue/categories/", new { Name = name, ParentId = parentId }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            CategoryItem? body = await ReadBodyAsync<CategoryItem>(response, cancellationToken);
            return body is null ? new(CreateCategoryStatus.Unavailable) : new(CreateCategoryStatus.Created, body);
        }
        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new(CreateCategoryStatus.Duplicate),
            HttpStatusCode.NotFound => new(CreateCategoryStatus.ParentMissing),
            HttpStatusCode.BadRequest => new(CreateCategoryStatus.Invalid),
            _ => new(CreateCategoryStatus.Unavailable)
        };
    }

    public async Task<RenameCategoryResult> RenameAsync(Guid id, string name,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put,
            $"/catalogue/categories/{id}/name", new { Name = name }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            CategoryItem? body = await ReadBodyAsync<CategoryItem>(response, cancellationToken);
            return body is null ? new(RenameCategoryStatus.Unavailable) : new(RenameCategoryStatus.Renamed, body);
        }
        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new(RenameCategoryStatus.Duplicate),
            HttpStatusCode.NotFound => new(RenameCategoryStatus.Missing),
            HttpStatusCode.BadRequest => new(RenameCategoryStatus.Invalid),
            _ => new(RenameCategoryStatus.Unavailable)
        };
    }

    public Task<CatalogueReadResult<IReadOnlyList<ProductCategoryChoice>>> ProductCategoriesAsync(CancellationToken cancellationToken) =>
        ReadAsync<IReadOnlyList<ProductCategoryChoice>>("/catalogue/products/category-choices", cancellationToken);

    public Task<CatalogueReadResult<ProductDetails>> ProductDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        ReadAsync<ProductDetails>($"/catalogue/products/{id}", cancellationToken);

    public async Task<CreateProductResult> CreateProductAsync(string code, string name, Guid? categoryId,
        decimal? basePrice, CancellationToken cancellationToken, string unit = "Each",
        decimal? quantityStep = null, decimal? minimumQuantity = null)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/catalogue/products/",
            new { Code = code, Name = name, CategoryId = categoryId, Unit = unit, BasePrice = basePrice,
                QuantityStep = quantityStep, MinimumQuantity = minimumQuantity }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            ProductItem? body = await ReadBodyAsync<ProductItem>(response, cancellationToken);
            return body is null ? new(CreateProductStatus.Unavailable) : new(CreateProductStatus.Created, body);
        }
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(CreateProductStatus.Unavailable) : new(CreateProductStatus.Invalid, Errors: errors);
        }
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(CreateProductStatus.Unavailable) : new(CreateProductStatus.Invalid, Errors: errors);
        }
        return new(CreateProductStatus.Unavailable);
    }

    public async Task<UpdateProductUnitResult> UpdateProductUnitAsync(Guid id, string unit,
        decimal? quantityStep, decimal? minimumQuantity, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, $"/catalogue/products/{id}/unit",
            new { Unit = unit, QuantityStep = quantityStep, MinimumQuantity = minimumQuantity }, cancellationToken);
        if (response.IsSuccessStatusCode) return new(UpdateProductUnitStatus.Saved);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateProductUnitStatus.Missing);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(UpdateProductUnitStatus.Unavailable) : new(UpdateProductUnitStatus.Invalid, errors);
        }
        return new(UpdateProductUnitStatus.Unavailable);
    }

    public async Task<AddProductBasePriceResult> AddProductBasePriceAsync(Guid id, decimal? basePrice,
        DateOnly? effectiveFrom, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post,
            $"/catalogue/products/{id}/base-prices",
            new { BasePrice = basePrice, EffectiveFrom = effectiveFrom }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created) return new(AddProductBasePriceStatus.Created);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(AddProductBasePriceStatus.Missing);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(AddProductBasePriceStatus.Unavailable) : new(AddProductBasePriceStatus.Invalid, errors);
        }
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var errors = await ReadErrorsAsync(response, cancellationToken);
            return errors is null ? new(AddProductBasePriceStatus.Unavailable) : new(AddProductBasePriceStatus.Invalid, errors);
        }
        return new(AddProductBasePriceStatus.Unavailable);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path,
        object? body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpContext context = contexts.HttpContext
            ?? throw new InvalidOperationException("A staff request is required.");
        string? token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) return new(HttpStatusCode.Unauthorized);
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        try { return await client.SendAsync(request, cancellationToken); }
        catch (Polly.Timeout.TimeoutRejectedException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (Polly.CircuitBreaker.BrokenCircuitException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (HttpRequestException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(HttpStatusCode.ServiceUnavailable); }
    }
}
