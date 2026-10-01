using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed record CreateCategoryRequest(string? Name, Guid? ParentId);
public sealed record RenameCategoryRequest(string? Name);

public static class CatalogueEndpoints
{
    public static void MapCatalogueEndpoints(this WebApplication app)
    {
        RouteGroupBuilder categories = app.MapGroup("/catalogue/categories")
            .RequireAuthorization("HeadOfficeCatalogue");

        categories.MapGet("/", async (CatalogueDbContext db, CancellationToken cancellationToken) =>
        {
            List<Category> all = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            var counts = await ReadCountsAsync(db, all, cancellationToken);
            CategoryItem[] roots = all.Where(category => category.ParentId == null)
                .OrderBy(category => category.Name).ThenBy(category => category.Id)
                .Select(category => ToCountedItem(category, counts)).ToArray();
            return Results.Ok(roots);
        });

        categories.MapGet("/search", async (string? q, CatalogueDbContext db, CancellationToken cancellationToken) =>
        {
            string query = q?.Trim() ?? string.Empty;
            if (query.Length == 0) return Results.Ok(Array.Empty<CategorySearchResult>());
            List<Category> all = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            CategoryTree tree = new(all);
            return Results.Ok(all.Where(category => category.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(category => new CategorySearchResult(category.Id,
                    tree.Path(category.Id)))
                .OrderBy(result => result.Path, StringComparer.OrdinalIgnoreCase).ThenBy(result => result.Id).ToArray());
        });

        categories.MapGet("/{id:guid}", async (Guid id, CatalogueDbContext db,
            CancellationToken cancellationToken) =>
        {
            List<Category> all = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            Category? selected = all.FirstOrDefault(category => category.Id == id);
            if (selected is null) return Results.NotFound();

            CategoryTree tree = new(all);
            var counts = await ReadCountsAsync(db, all, cancellationToken);
            CategoryItem[] children = all.Where(category => category.ParentId == id)
                .OrderBy(category => category.Name).ThenBy(category => category.Id)
                .Select(category => ToCountedItem(category, counts)).ToArray();
            ProductItem[] products = await db.Products.AsNoTracking().Where(product => product.CategoryId == id)
                .OrderBy(product => product.Code).ThenBy(product => product.Id)
                .Select(product => new ProductItem(product.Id, product.Code, product.Name, product.CategoryId, product.Unit,
                    product.QuantityStep, product.MinimumQuantity))
                .ToArrayAsync(cancellationToken);
            return Results.Ok(new CategoryDetails(ToCountedItem(selected, counts), tree.Breadcrumb(id).Select(segment => new FieldSales.Catalogue.Contracts.CategoryBreadcrumbSegment(segment.Id, segment.Name)).ToArray(), children, products));
        });

        categories.MapPost("/", async (CreateCategoryRequest request, CatalogueDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!NameRules.IsValid(request.Name, Category.MaximumNameLength))
                return Results.BadRequest(new CatalogueError("Enter a category name of up to 200 characters."));

            List<Category> existing = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            CategoryTree tree = new(existing);
            Category added;
            try
            {
                added = tree.Add(request.Name, request.ParentId);
            }
            catch (ArgumentException exception) when (exception.ParamName == "parentId")
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new CatalogueError("A category with this name already exists here."));
            }

            db.Categories.Add(added);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
            {
                return Results.Conflict(new CatalogueError("A category with this name already exists here."));
            }

            return Results.Created($"/catalogue/categories/{added.Id}", ToItem(added));
        });

        categories.MapPut("/{id:guid}/name", async (Guid id, RenameCategoryRequest request,
            CatalogueDbContext db, CancellationToken cancellationToken) =>
        {
            if (!NameRules.IsValid(request.Name, Category.MaximumNameLength))
                return Results.BadRequest(new CatalogueError("Enter a category name of up to 200 characters."));

            List<Category> existing = await db.Categories.ToListAsync(cancellationToken);
            Category renamed;
            try
            {
                renamed = new CategoryTree(existing).Rename(id, request.Name);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new CatalogueError("A category with this name already exists here."));
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
            {
                return Results.Conflict(new CatalogueError("A category with this name already exists here."));
            }

            return Results.Ok(ToItem(renamed));
        });
    }

    private static CategoryItem ToItem(Category category) =>
        new(category.Id, category.ParentId, category.Name);

    private static CategoryItem ToCountedItem(Category category,
        IReadOnlyDictionary<Guid, CategoryProductCount> counts) =>
        new(category.Id, category.ParentId, category.Name, counts[category.Id].Here, counts[category.Id].Beneath);

    private static async Task<IReadOnlyDictionary<Guid, CategoryProductCount>> ReadCountsAsync(
        CatalogueDbContext db, IReadOnlyList<Category> categories, CancellationToken cancellationToken)
    {
        Dictionary<Guid, int> directCounts = await db.Products.AsNoTracking()
            .GroupBy(product => product.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.CategoryId, group => group.Count, cancellationToken);
        return CategoryProductCounts.Calculate(categories, directCounts);
    }
}
