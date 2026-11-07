using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>A downloaded product as Order entry lists it: its breadcrumb and its price today (null when nothing prices it, so it
/// cannot be ordered). <paramref name="CategoryId"/> places it on the Order Pad.</summary>
/// <param name="Price">The base price in effect today; for a shop, the price its customer gets (WI-064, <see cref="LocationPrices.Apply"/>).</param>
/// <param name="ListPrice">The base price in effect today, kept when <paramref name="Price"/> is a shop's price; null means it is
/// <paramref name="Price"/>.</param>
public sealed record ProductSearchResult(Guid Id, string Code, string Name, string Breadcrumb, string Unit, decimal? Price,
    Guid? CategoryId = null, decimal? QuantityStep = null, decimal? MinimumQuantity = null, decimal? ListPrice = null);

/// <summary>The first <see cref="ProductSearchIndex.MaximumResults"/> matches, and how many matched in all.</summary>
public sealed record ProductSearchOutcome(IReadOnlyList<ProductSearchResult> Results, int Total);

/// <summary>An immutable, rep-local projection of one active snapshot's products. No network or persisted schema.</summary>
/// <remarks>Matching ignores case and accents. Every word typed must appear in the product's name or code.</remarks>
public sealed class ProductSearchIndex
{
    public const int MaximumResults = 50;
    private readonly (ProductSearchResult Product, string Name, string Code)[] rows;
    private readonly Dictionary<Guid, ProductSearchResult> products;
    private readonly IReadOnlyList<PadCategory> categories;
    private OrderPad? pad;

    /// <summary>The same index with each product changed by <paramref name="price"/>: a shop's prices (WI-064).</summary>
    public ProductSearchIndex WithPrices(Func<ProductSearchResult, ProductSearchResult> price) =>
        new(Generation, rows.Select(row => price(row.Product)), categories);

    public ProductSearchIndex(string? generation, IEnumerable<ProductSearchResult> products, IEnumerable<PadCategory>? categories = null)
    {
        this.categories = categories?.ToArray() ?? [];
        Generation = generation;
        rows = products.Select(product => (Product: product, Name: LocationSearchIndex.Normalize(product.Name),
                Code: LocationSearchIndex.Normalize(product.Code)))
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ThenBy(row => LocationSearchIndex.Normalize(row.Product.Breadcrumb), StringComparer.Ordinal)
            .ThenBy(row => row.Code, StringComparer.Ordinal)
            .ThenBy(row => row.Product.Id).ToArray();
        this.products = rows.ToDictionary(row => row.Product.Id, row => row.Product);
    }

    public string? Generation { get; }
    public int Count => rows.Length;
    public bool Contains(Guid productId) => products.ContainsKey(productId);

    /// <summary>The downloaded product with this id, or null when it isn't on the current snapshot.</summary>
    public ProductSearchResult? Find(Guid productId) => products.GetValueOrDefault(productId);

    /// <summary>The same products by category, built on first use. Like the index, it is never changed afterwards.</summary>
    public OrderPad Pad => pad ??= OrderPad.Build(categories, rows.Select(row => row.Product));

    public ProductSearchOutcome Search(string? query)
    {
        string[] terms = Terms(query);
        if (terms.Length == 0) return new([], 0);
        List<ProductSearchResult> matches = [];
        int total = 0;
        foreach ((ProductSearchResult product, string name, string code) in rows)
        {
            if (!terms.All(term => name.Contains(term, StringComparison.Ordinal) || code.Contains(term, StringComparison.Ordinal))) continue;
            total++;
            if (matches.Count < MaximumResults) matches.Add(product);
        }
        return new(matches, total);
    }

    /// <summary>The words typed, ignoring case and accents; empty when nothing is typed.</summary>
    public static string[] Terms(string? query) => string.IsNullOrWhiteSpace(query) ? []
        : LocationSearchIndex.Normalize(query.Trim()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Whether this product matches as <see cref="Search"/> would match it: every word in its name or code. No words
    /// match everything.</summary>
    public static bool Matches(ProductSearchResult product, IReadOnlyCollection<string> terms)
    {
        if (terms.Count == 0) return true;
        string name = LocationSearchIndex.Normalize(product.Name), code = LocationSearchIndex.Normalize(product.Code);
        return terms.All(term => name.Contains(term, StringComparison.Ordinal) || code.Contains(term, StringComparison.Ordinal));
    }
}

public sealed partial class SnapshotStore
{
    /// <summary>Reads the generation, categories, products and today's prices in one transaction, so a
    /// concurrent Sync cannot mix copies. The price is the newest one on or before <paramref name="today"/> (the business day), or
    /// on or before the snapshot's build day when the tablet's clock is set before it (WI-059 D2).</summary>
    public ProductSearchIndex ReadProductIndex(DateOnly today)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        DateOnly day = PriceDay(command, today);
        command.CommandText = "SELECT generation FROM active";
        string? generation = command.ExecuteScalar() as string;
        List<ProductSearchResult> products = [];
        if (generation is not null)
        {
            Dictionary<Guid, (Guid? Parent, string Name)> categories = [];
            List<PadCategory> padCategories = [];
            command.CommandText = "SELECT id, parent_id, name FROM categories WHERE generation=$g";
            command.Parameters.AddWithValue("$g", generation);
            using (SqliteDataReader reader = command.ExecuteReader())
                while (reader.Read())
                {
                    PadCategory category = new(Guid.Parse(reader.GetString(0)), reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)), reader.GetString(2));
                    categories[category.Id] = (category.ParentId, category.Name);
                    padCategories.Add(category);
                }

            Dictionary<Guid, string> paths = [];
            string Path(Guid category)
            {
                if (paths.TryGetValue(category, out string? known)) return known;
                List<string> names = [];
                HashSet<Guid> visited = [];
                for (Guid? current = category; current is Guid id && visited.Add(id) && categories.TryGetValue(id, out var node); current = node.Parent)
                    names.Insert(0, node.Name);
                return paths[category] = string.Join(" > ", names);
            }

            command.CommandText = """
                SELECT p.id, p.code, p.name, p.category_id, p.unit,
                       (SELECT b.amount FROM base_prices b WHERE b.generation=p.generation AND b.product_id=p.id AND b.effective_from <= $today
                        ORDER BY b.effective_from DESC LIMIT 1), p.quantity_step, p.minimum_quantity
                FROM products p WHERE p.generation=$g
                """;
            command.Parameters.AddWithValue("$today", day.ToString("O", CultureInfo.InvariantCulture));
            using SqliteDataReader rows = command.ExecuteReader();
            while (rows.Read())
            {
                Guid category = Guid.Parse(rows.GetString(3));
                products.Add(new(Guid.Parse(rows.GetString(0)), rows.GetString(1), rows.GetString(2), Path(category),
                    rows.GetString(4), Number(rows, 5), category, Number(rows, 6), Number(rows, 7)));
            }
            transaction.Commit();
            return new(generation, products, padCategories);
        }
        transaction.Commit();
        return new(generation, products);
    }
}
