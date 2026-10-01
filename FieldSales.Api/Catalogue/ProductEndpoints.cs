using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FieldSales.Quantities;
using System.Globalization;

namespace FieldSales.Api.Catalogue;

public sealed record CreateProductRequest(string? Code, string? Name, Guid? CategoryId,
    string? Unit, decimal? BasePrice, decimal? QuantityStep = null, decimal? MinimumQuantity = null);
public sealed record SetProductUnitRequest(string? Unit, decimal? QuantityStep, decimal? MinimumQuantity);
public sealed record AddProductBasePriceRequest(decimal? BasePrice, DateOnly? EffectiveFrom);

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
                    tree.Path(category.Id)))
                .OrderBy(choice => choice.Path).ToArray());
        });

        products.MapGet("/{id:guid}", async (Guid id, CatalogueDbContext db,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            Product? product = await db.Products.AsNoTracking().Include(product => product.BasePrices)
                .Include(product => product.AttributeValues).ThenInclude(value => value.AttributeName)
                .AsSplitQuery()
                .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
            if (product is null) return Results.NotFound();
            List<Category> categories = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            ProductBasePrice? price = product.BasePriceOn(Today(clock));
            var brands = await db.Brands.AsNoTracking().Where(brand => brand.Id == product.PrimaryBrandId
                || db.ProductAlternativeBrands.Any(link => link.ProductId == id && link.BrandId == brand.Id))
                .OrderBy(brand => brand.Name).Select(brand => new ProductBrandItem(brand.Id, brand.Name,
                    brand.IsArchived, brand.Id == product.PrimaryBrandId)).ToArrayAsync(cancellationToken);
            var profile = await db.ProductProfiles.AsNoTracking().Where(item => item.Id == product.ProductProfileId)
                .Select(item => new ProductReferenceItem(item.Id, item.Name, item.IsArchived)).SingleOrDefaultAsync(cancellationToken);
            var supplier = await db.Suppliers.AsNoTracking().Where(item => item.Id == product.SupplierId)
                .Select(item => new ProductReferenceItem(item.Id, item.Name, item.IsArchived)).SingleOrDefaultAsync(cancellationToken);
            return Results.Ok(new ProductDetails(ToItem(product),
                new CategoryTree(categories).Breadcrumb(product.CategoryId)
                    .Select(segment => new FieldSales.Catalogue.Contracts.CategoryBreadcrumbSegment(segment.Id, segment.Name)).ToArray(),
                price is null ? null : new ProductPriceItem(price.EffectiveFrom, price.Amount),
                product.BasePrices.OrderByDescending(entry => entry.EffectiveFrom)
                    .Select(entry => new ProductPriceItem(entry.EffectiveFrom, entry.Amount)).ToArray(),
                product.Attributes.Select(attribute => new FieldSales.Catalogue.Contracts.ProductAttribute(attribute.Name, attribute.Value, attribute.IsArchived)).ToArray(), brands, profile, supplier));
        });

        products.MapPost("/", CreateAsync);
        products.MapPut("/{id:guid}/unit", SetUnitAsync);
        products.MapPost("/{id:guid}/base-prices", AddBasePriceAsync);
    }

    private static async Task<IResult> CreateAsync(CreateProductRequest request, CatalogueDbContext db,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = [];
        if (!NameRules.IsValid(request.Code, Product.MaximumCodeLength))
            errors["Code"] = ["Enter a product code of up to 100 characters."];
        if (!NameRules.IsValid(request.Name, Product.MaximumNameLength))
            errors["Name"] = ["Enter a product name of up to 200 characters."];
        if (request.CategoryId is null || request.CategoryId == Guid.Empty
            || !await db.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
            errors["CategoryId"] = ["Choose a category"];
        QuantityRules.TryCreate(request.Unit, request.QuantityStep, request.MinimumQuantity,
            out QuantityRules? rules, out var quantityErrors);
        foreach (var error in quantityErrors) errors[error.Key] = error.Value;
        if (request.BasePrice is null) errors["BasePrice"] = ["Enter a base price."];
        else if (!ProductBasePrice.IsValidAmount(request.BasePrice.Value))
            errors["BasePrice"] = [ProductBasePrice.InvalidAmountMessage];
        if (errors.Count != 0) return Results.ValidationProblem(errors);

        string code = request.Code!.Trim();
        Product? existing = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(product => product.Code == code, cancellationToken);
        if (existing is not null) return Duplicate(existing);

        Product added = Product.Create(code, request.Name!, request.CategoryId!.Value,
            request.BasePrice!.Value, Today(clock), rules!);
        db.Products.Add(added);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        {
            // The unique index settles concurrent saves; report the persisted winner's code and name.
            existing = await db.Products.AsNoTracking()
                .SingleAsync(product => product.Code == code, cancellationToken);
            return Duplicate(existing);
        }
        return Results.Created($"/catalogue/products/{added.Id}", ToItem(added));
    }

    private static async Task<IResult> SetUnitAsync(Guid id, SetProductUnitRequest request,
        CatalogueDbContext db, CancellationToken cancellationToken)
    {
        Product? product = await db.Products.SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
        if (product is null) return Results.NotFound();
        if (!QuantityRules.TryCreate(request.Unit, request.QuantityStep, request.MinimumQuantity, out var rules, out var errors))
            return Results.ValidationProblem(errors.ToDictionary(error => error.Key, error => error.Value));
        // No order records exist in this slice. Order capture must supply actual usage here
        // before unit changes on ordered products can be offered (historical changes: WI-068).
        product.SetQuantityRules(rules, hasOrders: false);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToItem(product));
    }

    private static async Task<IResult> AddBasePriceAsync(Guid id, AddProductBasePriceRequest request,
        CatalogueDbContext db, CancellationToken cancellationToken)
    {
        Product? product = await db.Products.Include(product => product.BasePrices)
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
        if (product is null) return Results.NotFound();

        Dictionary<string, string[]> errors = [];
        if (request.BasePrice is null) errors["BasePrice"] = ["Enter a base price."];
        else if (!ProductBasePrice.IsValidAmount(request.BasePrice.Value))
            errors["BasePrice"] = [ProductBasePrice.InvalidAmountMessage];
        if (request.EffectiveFrom is null) errors["EffectiveFrom"] = ["Enter an effective from date."];
        if (errors.Count != 0) return Results.ValidationProblem(errors);

        DateOnly effectiveFrom = request.EffectiveFrom!.Value;
        if (product.BasePrices.Any(price => price.EffectiveFrom == effectiveFrom))
            return DuplicatePrice(effectiveFrom);
        product.AddBasePrice(request.BasePrice!.Value, effectiveFrom);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        {
            // A competing insert may have won after the history was loaded.
            if (!await db.ProductBasePrices.AsNoTracking().AnyAsync(price =>
                    price.ProductId == id && price.EffectiveFrom == effectiveFrom, cancellationToken))
                throw;
            return DuplicatePrice(effectiveFrom);
        }
        return Results.Created($"/catalogue/products/{id}",
            new ProductPriceItem(effectiveFrom, request.BasePrice.Value));
    }

    private static IResult DuplicatePrice(DateOnly effectiveFrom) => Results.Conflict(
        new ProductSaveError("EffectiveFrom",
            $"A price already starts on {effectiveFrom.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} — edit it instead"));

    private static IResult Duplicate(Product product) => Results.Conflict(new ProductSaveError("Code",
        $"Code {product.Code} is already used by {product.Name}"));

    private static ProductItem ToItem(Product product) =>
        new(product.Id, product.Code, product.Name, product.CategoryId, product.Unit,
            product.QuantityStep, product.MinimumQuantity);

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessTimeZone).DateTime);
}
