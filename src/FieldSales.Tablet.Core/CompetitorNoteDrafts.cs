using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>Writing Competitor Notes on T-06 (T-5.8.1): a new note, switching its link type, ticking products and choosing a
/// category, and the lists the pickers offer.</summary>
public static class CompetitorNoteDrafts
{
    /// <summary>Range has nothing to pick until E9 brings Ranges to the snapshot (WI-057 D4).</summary>
    public const string NoRanges = "No ranges on this tablet yet";

    /// <summary>A new, empty note linked to nothing.</summary>
    public static CallCompetitorNote New() => new(Guid.NewGuid(), "", CompetitorLinkType.None);

    /// <summary>What switching away from the note's link type would remove (D5): "Switching will remove 2 linked products", or
    /// null when nothing is picked, so it switches straight away.</summary>
    public static string? SwitchWarning(CallCompetitorNote note) => note.LinkTo switch
    {
        CompetitorLinkType.Products when note.Products is { Count: > 0 } products =>
            $"Switching will remove {products.Count} linked {(products.Count == 1 ? "product" : "products")}",
        CompetitorLinkType.Category when note.Category is not null => "Switching will remove the linked category",
        CompetitorLinkType.Range when note.Range is not null => "Switching will remove the linked range",
        _ => null
    };

    /// <summary>The note on another link type, with nothing picked (one type per note).</summary>
    public static CallCompetitorNote Switch(CallCompetitorNote note, CompetitorLinkType to) =>
        note with { LinkTo = to, Products = to == CompetitorLinkType.Products ? [] : null, Range = null, Category = null };

    /// <summary>The product ticked or unticked on a Products note; it keeps the order products were ticked in.</summary>
    public static CallCompetitorNote Toggle(CallCompetitorNote note, ProductSearchResult product)
    {
        IReadOnlyList<CompetitorLinkedProduct> linked = note.Products ?? [];
        return note with
        {
            Products = linked.Any(each => each.ProductId == product.Id)
                ? [.. linked.Where(each => each.ProductId != product.Id)]
                : [.. linked, new CompetitorLinkedProduct(product.Id, product.Code, product.Name)]
        };
    }

    /// <summary>The products the picker lists first (D4): this Call's products with a count or a Low mark, in the Call's order.</summary>
    public static IReadOnlyList<ProductSearchResult> FromThisCall(IEnumerable<StockLine> lines) =>
        [.. lines.Where(line => line.Counted || line.Low).Select(line => line.Product)];

    /// <summary>The categories the Category picker offers: every downloaded category with products beneath it, by breadcrumb,
    /// narrowed by every word typed, with how many match in all; at most <paramref name="limit"/> are shown.</summary>
    public static (IReadOnlyList<OrderPadCategory> Shown, int Total) Categories(OrderPad pad, string? filter, int limit = 20)
    {
        string[] terms = ProductSearchIndex.Terms(filter);
        OrderPadCategory[] matching = [.. Flatten(pad.Categories)
            .Where(category => terms.All(LocationSearchIndex.Normalize(category.Breadcrumb).Contains))];
        return ([.. matching.Take(limit)], matching.Length);
    }

    private static IEnumerable<OrderPadCategory> Flatten(IEnumerable<OrderPadCategory> categories) =>
        categories.SelectMany(category => Flatten(category.Subcategories).Prepend(category));
}
