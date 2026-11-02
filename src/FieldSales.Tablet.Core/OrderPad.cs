namespace FieldSales.Tablet.Core;

/// <summary>A downloaded category as the Order Pad needs it.</summary>
public sealed record PadCategory(Guid Id, Guid? ParentId, string Name);

/// <summary>One category header on the Order Pad: its full breadcrumb, how many products sit beneath it at every depth,
/// its subcategories, then the products directly in it (shown as "In {Name} ({Here})").</summary>
public sealed record OrderPadCategory(Guid Id, string Name, string Breadcrumb, int Beneath,
    IReadOnlyList<OrderPadCategory> Subcategories, IReadOnlyList<ProductSearchResult> Products)
{
    public int Here => Products.Count;
}

/// <summary>The Order Pad's category tree (T-07, T7.3), built once from one snapshot's categories and products.</summary>
/// <remarks>Categories with nothing beneath them are left out, so a category holding only products the rep can't see leaves
/// no trace (T7.5). Subcategories sort by name; products by name, then code (both ignoring case and accents). Built without
/// recursion, so a deep tree can't exhaust the stack; a category in a parent cycle is never reached from a top-level one.</remarks>
public sealed class OrderPad
{
    private OrderPad(IReadOnlyList<OrderPadCategory> categories) => Categories = categories;

    public static OrderPad Empty { get; } = new([]);

    /// <summary>The top-level categories.</summary>
    public IReadOnlyList<OrderPadCategory> Categories { get; }

    public static OrderPad Build(IEnumerable<PadCategory> categories, IEnumerable<ProductSearchResult> products)
    {
        Dictionary<Guid, PadCategory> byId = [];
        foreach (PadCategory category in categories) byId[category.Id] = category;

        Dictionary<Guid, List<PadCategory>> children = [];
        List<PadCategory> roots = [];
        foreach (PadCategory category in byId.Values)
        {
            if (category.ParentId is Guid parent && parent != category.Id && byId.ContainsKey(parent))
                (children.TryGetValue(parent, out var list) ? list : children[parent] = []).Add(category);
            else roots.Add(category);
        }

        Dictionary<Guid, List<ProductSearchResult>> direct = [];
        foreach (ProductSearchResult product in products)
            if (product.CategoryId is Guid category && byId.ContainsKey(category))
                (direct.TryGetValue(category, out var list) ? list : direct[category] = []).Add(product);

        // Parents before children, each visited once, with the breadcrumb built on the way down.
        List<PadCategory> order = [];
        Dictionary<Guid, string> breadcrumbs = [];
        HashSet<Guid> visited = [];
        Stack<PadCategory> pending = new(roots);
        while (pending.Count > 0)
        {
            PadCategory category = pending.Pop();
            if (!visited.Add(category.Id)) continue;
            breadcrumbs[category.Id] = category.ParentId is Guid parent && breadcrumbs.TryGetValue(parent, out string? above)
                ? $"{above} > {category.Name}" : category.Name;
            order.Add(category);
            if (children.TryGetValue(category.Id, out var below))
                foreach (PadCategory child in below) pending.Push(child);
        }

        // Children before parents, so each header is assembled from finished subcategories.
        Dictionary<Guid, OrderPadCategory> built = [];
        for (int i = order.Count - 1; i >= 0; i--)
        {
            PadCategory category = order[i];
            OrderPadCategory[] subcategories = children.TryGetValue(category.Id, out var below)
                ? below.Where(child => built.ContainsKey(child.Id)).Select(child => built[child.Id])
                    .OrderBy(child => LocationSearchIndex.Normalize(child.Name), StringComparer.Ordinal).ThenBy(child => child.Id).ToArray()
                : [];
            ProductSearchResult[] here = direct.TryGetValue(category.Id, out var mine)
                ? mine.OrderBy(product => LocationSearchIndex.Normalize(product.Name), StringComparer.Ordinal)
                    .ThenBy(product => LocationSearchIndex.Normalize(product.Code), StringComparer.Ordinal).ThenBy(product => product.Id).ToArray()
                : [];
            int beneath = here.Length + subcategories.Sum(child => child.Beneath);
            if (beneath > 0) built[category.Id] = new(category.Id, category.Name, breadcrumbs[category.Id], beneath, subcategories, here);
        }

        return new(roots.Where(root => built.ContainsKey(root.Id)).Select(root => built[root.Id])
            .OrderBy(root => LocationSearchIndex.Normalize(root.Name), StringComparer.Ordinal).ThenBy(root => root.Id).ToArray());
    }
}
