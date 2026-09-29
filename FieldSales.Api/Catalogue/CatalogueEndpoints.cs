using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public sealed record CategoryItem(Guid Id, Guid? ParentId, string Name);
public sealed record CategoryDetails(CategoryItem Category,
    IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb,
    IReadOnlyList<CategoryItem> Children);
public sealed record CreateCategoryRequest(string? Name, Guid? ParentId);

public static class CatalogueEndpoints
{
    public static void MapCatalogueEndpoints(this WebApplication app)
    {
        RouteGroupBuilder categories = app.MapGroup("/catalogue/categories")
            .RequireAuthorization("HeadOfficeCatalogue");

        categories.MapGet("/", async (CatalogueDbContext db, CancellationToken cancellationToken) =>
        {
            CategoryItem[] roots = await db.Categories.AsNoTracking()
                .Where(category => category.ParentId == null)
                .OrderBy(category => category.Name)
                .Select(category => new CategoryItem(category.Id, category.ParentId, category.Name))
                .ToArrayAsync(cancellationToken);
            return Results.Ok(roots);
        });

        categories.MapGet("/{id:guid}", async (Guid id, CatalogueDbContext db,
            CancellationToken cancellationToken) =>
        {
            List<Category> all = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
            Category? selected = all.FirstOrDefault(category => category.Id == id);
            if (selected is null) return Results.NotFound();

            CategoryTree tree = new(all);
            CategoryItem[] children = all.Where(category => category.ParentId == id)
                .OrderBy(category => category.Name)
                .Select(ToItem).ToArray();
            return Results.Ok(new CategoryDetails(ToItem(selected), tree.Breadcrumb(id), children));
        });

        categories.MapPost("/", async (CreateCategoryRequest request, CatalogueDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || request.Name.Trim().Length > Category.MaximumNameLength)
                return Results.BadRequest(new { Error = "Enter a category name of up to 200 characters." });

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
                return Results.Conflict(new { Error = "A category with this name already exists here." });
            }

            db.Categories.Add(added);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException
                       { Number: 2601 or 2627 })
            {
                return Results.Conflict(new { Error = "A category with this name already exists here." });
            }

            return Results.Created($"/catalogue/categories/{added.Id}", ToItem(added));
        });
    }

    private static CategoryItem ToItem(Category category) =>
        new(category.Id, category.ParentId, category.Name);
}
