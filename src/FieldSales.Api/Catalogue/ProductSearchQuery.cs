using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

public static class ProductSearchQuery
{
    public static async Task<IResult> SearchAsync(string? q, Guid? categoryId, Guid? brandId,
        CatalogueDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var brands = await db.Brands.AsNoTracking().OrderBy(brand => brand.Name).ThenBy(brand => brand.Id)
            .Select(brand => new ProductReferenceItem(brand.Id, brand.Name, brand.IsArchived)).ToArrayAsync(cancellationToken);
        if (categoryId is Guid category && !categories.Any(item => item.Id == category)
            || brandId is Guid brand && !brands.Any(item => item.Id == brand)) return Results.NotFound();

        IQueryable<Product> products = db.Products.AsNoTracking();
        if (categoryId is Guid selectedCategory)
        {
            var ids = Descendants(categories, selectedCategory);
            products = products.Where(product => ids.Contains(product.CategoryId));
        }
        if (brandId is Guid selectedBrand)
            products = products.Where(product => product.PrimaryBrandId == selectedBrand
                || db.ProductAlternativeBrands.Any(link => link.ProductId == product.Id && link.BrandId == selectedBrand));
        string query = q?.Trim() ?? string.Empty;
        if (query.Length != 0)
        {
            // Contains is parameterized and matches literal substrings, including SQL wildcard characters.
            products = products.Where(product => product.Code.Contains(query)
                || EF.Functions.Collate(product.Name, "Latin1_General_100_CI_AS").Contains(query)
                || db.Brands.Any(brand => brand.Name.Contains(query) && (brand.Id == product.PrimaryBrandId
                    || db.ProductAlternativeBrands.Any(link => link.ProductId == product.Id && link.BrandId == brand.Id)))
                || db.Suppliers.Any(supplier => supplier.Id == product.SupplierId && supplier.Name.Contains(query)));
        }
        var matches = await products.Include(product => product.BasePrices).OrderBy(product => product.Code)
            .ThenBy(product => product.Id).ToArrayAsync(cancellationToken);
        var tree = new CategoryTree(categories);
        var byBrand = brands.ToDictionary(brand => brand.Id);
        DateOnly today = ProductEndpoints.Today(clock);
        var items = matches.Select(product =>
        {
            var price = product.BasePriceOn(today);
            return new ProductSearchItem(product.Id, product.Code, product.Name,
                tree.Breadcrumb(product.CategoryId).Select(part => new FieldSales.Catalogue.Contracts.CategoryBreadcrumbSegment(part.Id, part.Name)).ToArray(),
                product.PrimaryBrandId is Guid primary ? byBrand[primary] : null,
                // Availability is introduced by E8; every product in the current model is Active.
                "Active", product.Unit, price is null ? null : new ProductPriceItem(price.EffectiveFrom, price.Amount));
        }).ToArray();
        var choices = categories.Select(category => new ProductCategoryChoice(category.Id, tree.Path(category.Id)))
            .OrderBy(choice => choice.Path, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.Id).ToArray();
        return Results.Ok(new ProductSearchResponse(items, choices, brands));
    }

    private static Guid[] Descendants(IReadOnlyList<Category> categories, Guid root)
    {
        var children = categories.Where(category => category.ParentId != null)
            .GroupBy(category => category.ParentId!.Value).ToDictionary(group => group.Key, group => group.Select(c => c.Id).ToArray());
        HashSet<Guid> visited = [];
        Stack<Guid> pending = new([root]);
        while (pending.TryPop(out Guid id))
        {
            if (!visited.Add(id)) continue;
            foreach (Guid child in children.GetValueOrDefault(id) ?? []) pending.Push(child);
        }
        return visited.ToArray();
    }
}
