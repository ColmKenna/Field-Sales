using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed record ProductCategoryChoice(Guid Id, string Path);
public sealed record CreateProductRequest(string? Code, string? Name, Guid? CategoryId,
    string? Unit, decimal? BasePrice);
public sealed record ProductItem(Guid Id, string Code, string Name, Guid CategoryId, string Unit);
public sealed record ProductPriceItem(DateOnly EffectiveFrom, decimal Amount);
public sealed record ProductDetails(ProductItem Product, IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb,
    ProductPriceItem? CurrentPrice, IReadOnlyList<ProductPriceItem> PriceHistory,
    IReadOnlyList<ProductAttribute> Attributes);
public sealed record ProductSaveError(string Field, string Error);

public static class ProductEndpoints
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin");

    public static void MapProductEndpoints(this WebApplication app)
    {
        RouteGroupBuilder products = app.MapGroup("/catalogue/products")
            .RequireAuthorization("HeadOfficeCatalogue");

        products.MapGet("/category-choices", async (CatalogueDbContext db, CancellationToken cancellationToken) =>
        {
            List<Category> all = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            CategoryTree tree = new(all);
            return Results.Ok(all.Select(category => new ProductCategoryChoice(category.Id,
                    string.Join(" > ", tree.Breadcrumb(category.Id).Select(segment => segment.Name))))
                .OrderBy(choice => choice.Path).ToArray());
        });

        products.MapGet("/{id:guid}", async (Guid id, CatalogueDbContext db,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            Product? product = await db.Products.AsNoTracking().Include(product => product.BasePrices)
                .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
            if (product is null) return Results.NotFound();
            List<Category> categories = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            ProductBasePrice? price = product.BasePriceOn(Today(clock));
            return Results.Ok(new ProductDetails(ToItem(product),
                new CategoryTree(categories).Breadcrumb(product.CategoryId),
                price is null ? null : new ProductPriceItem(price.EffectiveFrom, price.Amount),
                product.BasePrices.OrderByDescending(entry => entry.EffectiveFrom)
                    .Select(entry => new ProductPriceItem(entry.EffectiveFrom, entry.Amount)).ToArray(),
                product.Attributes));
        });

        products.MapPost("/", CreateAsync);
    }

    private static async Task<IResult> CreateAsync(CreateProductRequest request, CatalogueDbContext db,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = [];
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > Product.MaximumCodeLength)
            errors["Code"] = ["Enter a product code of up to 100 characters."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > Product.MaximumNameLength)
            errors["Name"] = ["Enter a product name of up to 200 characters."];
        if (request.CategoryId is null || request.CategoryId == Guid.Empty
            || !await db.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
            errors["CategoryId"] = ["Choose a category"];
        if (request.Unit != "Each") errors["Unit"] = ["Choose Each as the unit."];
        if (request.BasePrice is null) errors["BasePrice"] = ["Enter a base price."];
        else if (request.BasePrice < 0 || request.BasePrice > ProductBasePrice.MaximumAmount
                 || decimal.Round(request.BasePrice.Value, 2) != request.BasePrice.Value)
            errors["BasePrice"] = ["Enter a non-negative base price with up to two decimal places within the supported amount."];
        if (errors.Count != 0) return Results.ValidationProblem(errors);

        string code = request.Code!.Trim();
        Product? existing = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(product => product.Code == code, cancellationToken);
        if (existing is not null) return Duplicate(existing);

        Product added = Product.Create(code, request.Name!, request.CategoryId!.Value,
            request.BasePrice!.Value, Today(clock));
        db.Products.Add(added);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique index settles concurrent saves; report the persisted winner's code and name.
            existing = await db.Products.AsNoTracking()
                .SingleAsync(product => product.Code == code, cancellationToken);
            return Duplicate(existing);
        }
        return Results.Created($"/catalogue/products/{added.Id}", ToItem(added));
    }

    private static IResult Duplicate(Product product) => Results.Conflict(new ProductSaveError("Code",
        $"Code {product.Code} is already used by {product.Name}"));

    private static ProductItem ToItem(Product product) =>
        new(product.Id, product.Code, product.Name, product.CategoryId, product.Unit);

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessTimeZone).DateTime);
}
