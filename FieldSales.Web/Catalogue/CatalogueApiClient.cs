using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;

namespace FieldSales.Web.Catalogue;

public sealed record CategoryItem(Guid Id, Guid? ParentId, string Name, int Here = 0, int Beneath = 0);
public sealed record CategoryBreadcrumbSegment(Guid Id, string Name);
public sealed record CategoryDetails(CategoryItem Category,
    IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb,
    IReadOnlyList<CategoryItem> Children, IReadOnlyList<ProductItem> Products);
public sealed record CategorySearchResult(Guid Id, string Path);
public enum CreateCategoryStatus { Created, Duplicate, ParentMissing, Invalid, Unavailable }
public sealed record CreateCategoryResult(CreateCategoryStatus Status, CategoryItem? Category = null);
public enum RenameCategoryStatus { Renamed, Duplicate, Missing, Invalid, Unavailable }
public sealed record RenameCategoryResult(RenameCategoryStatus Status, CategoryItem? Category = null);
public sealed record ProductCategoryChoice(Guid Id, string Path);
public sealed record ProductItem(Guid Id, string Code, string Name, Guid CategoryId, string Unit,
    decimal? QuantityStep = null, decimal? MinimumQuantity = null);
public sealed record ProductPriceItem(DateOnly EffectiveFrom, decimal Amount);
public sealed record ProductAttribute(string Name, string Value);
public sealed record ProductDetails(ProductItem Product, IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb,
    ProductPriceItem? CurrentPrice, IReadOnlyList<ProductPriceItem> PriceHistory,
    IReadOnlyList<ProductAttribute> Attributes);
public enum CreateProductStatus { Created, Invalid, Unavailable }
public sealed record CreateProductResult(CreateProductStatus Status, ProductItem? Product = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);
public enum UpdateProductUnitStatus { Saved, Invalid, Missing, Unavailable }
public sealed record UpdateProductUnitResult(UpdateProductUnitStatus Status,
    IReadOnlyDictionary<string, string[]>? Errors = null);
public enum AddProductBasePriceStatus { Created, Invalid, Missing, Unavailable }
public sealed record AddProductBasePriceResult(AddProductBasePriceStatus Status,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed class CatalogueApiClient(HttpClient client, IHttpContextAccessor contexts)
{
    public async Task<IReadOnlyList<CategoryItem>?> RootsAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            "/catalogue/categories/", null, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CategoryItem[]>(cancellationToken)
            : null;
    }

    public async Task<CategoryDetails?> DetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            $"/catalogue/categories/{id}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CategoryDetails>(cancellationToken);
    }

    public async Task<IReadOnlyList<CategorySearchResult>?> SearchCategoriesAsync(string query,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            $"/catalogue/categories/search?q={Uri.EscapeDataString(query)}", null, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CategorySearchResult[]>(cancellationToken)
            : null;
    }

    public async Task<CreateCategoryResult> CreateAsync(string name, Guid? parentId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post,
            "/catalogue/categories/", new { Name = name, ParentId = parentId }, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Created => new(CreateCategoryStatus.Created,
                await response.Content.ReadFromJsonAsync<CategoryItem>(cancellationToken)),
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
        return response.StatusCode switch
        {
            HttpStatusCode.OK => new(RenameCategoryStatus.Renamed,
                await response.Content.ReadFromJsonAsync<CategoryItem>(cancellationToken)),
            HttpStatusCode.Conflict => new(RenameCategoryStatus.Duplicate),
            HttpStatusCode.NotFound => new(RenameCategoryStatus.Missing),
            HttpStatusCode.BadRequest => new(RenameCategoryStatus.Invalid),
            _ => new(RenameCategoryStatus.Unavailable)
        };
    }

    public async Task<IReadOnlyList<ProductCategoryChoice>?> ProductCategoriesAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            "/catalogue/products/category-choices", null, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProductCategoryChoice[]>(cancellationToken)
            : null;
    }

    public async Task<ProductDetails?> ProductDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            $"/catalogue/products/{id}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDetails>(cancellationToken);
    }

    public async Task<CreateProductResult> CreateProductAsync(string code, string name, Guid? categoryId,
        decimal? basePrice, CancellationToken cancellationToken, string unit = "Each",
        decimal? quantityStep = null, decimal? minimumQuantity = null)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/catalogue/products/",
            new { Code = code, Name = name, CategoryId = categoryId, Unit = unit, BasePrice = basePrice,
                QuantityStep = quantityStep, MinimumQuantity = minimumQuantity }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created)
            return new(CreateProductStatus.Created, await response.Content.ReadFromJsonAsync<ProductItem>(cancellationToken));
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            ProductValidationErrors? body = await response.Content.ReadFromJsonAsync<ProductValidationErrors>(cancellationToken);
            return new(CreateProductStatus.Invalid, Errors: body?.Errors);
        }
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            ProductSaveError? body = await response.Content.ReadFromJsonAsync<ProductSaveError>(cancellationToken);
            return new(CreateProductStatus.Invalid, Errors: body is null ? null
                : new Dictionary<string, string[]> { [body.Field] = [body.Error] });
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
            ProductValidationErrors? body = await response.Content.ReadFromJsonAsync<ProductValidationErrors>(cancellationToken);
            return new(UpdateProductUnitStatus.Invalid, body?.Errors);
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
            ProductValidationErrors? body = await response.Content.ReadFromJsonAsync<ProductValidationErrors>(cancellationToken);
            return new(AddProductBasePriceStatus.Invalid, body?.Errors);
        }
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            ProductSaveError? body = await response.Content.ReadFromJsonAsync<ProductSaveError>(cancellationToken);
            return new(AddProductBasePriceStatus.Invalid, body is null ? null
                : new Dictionary<string, string[]> { [body.Field] = [body.Error] });
        }
        return new(AddProductBasePriceStatus.Unavailable);
    }

    private sealed record ProductValidationErrors(Dictionary<string, string[]> Errors);
    private sealed record ProductSaveError(string Field, string Error);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path,
        object? body, CancellationToken cancellationToken)
    {
        HttpContext context = contexts.HttpContext
            ?? throw new InvalidOperationException("A staff request is required.");
        string token = await context.GetTokenAsync("access_token")
            ?? throw new InvalidOperationException("The staff access token is unavailable.");
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request, cancellationToken);
    }
}
