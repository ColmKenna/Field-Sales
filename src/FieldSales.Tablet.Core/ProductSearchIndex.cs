using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>A downloaded product as Order entry lists it: its breadcrumb and the base price in effect today
/// (null when none is, so it cannot be ordered).</summary>
public sealed record ProductSearchResult(Guid Id, string Code, string Name, string Breadcrumb, string Unit, decimal? Price);

/// <summary>The first <see cref="ProductSearchIndex.MaximumResults"/> matches, and how many matched in all.</summary>
public sealed record ProductSearchOutcome(IReadOnlyList<ProductSearchResult> Results, int Total);

/// <summary>An immutable, rep-local projection of one active snapshot's products. No network or persisted schema.</summary>
/// <remarks>Matching ignores case and accents. Every word typed must appear in the product's name or code.</remarks>
public sealed class ProductSearchIndex
{
    public const int MaximumResults = 50;
    private readonly (ProductSearchResult Product, string Name, string Code)[] rows;

    public ProductSearchIndex(string? generation, IEnumerable<ProductSearchResult> products)
    {
        Generation = generation;
        rows = products.Select(product => (Product: product, Name: LocationSearchIndex.Normalize(product.Name),
                Code: LocationSearchIndex.Normalize(product.Code)))
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ThenBy(row => LocationSearchIndex.Normalize(row.Product.Breadcrumb), StringComparer.Ordinal)
            .ThenBy(row => row.Code, StringComparer.Ordinal)
            .ThenBy(row => row.Product.Id).ToArray();
    }

    public string? Generation { get; }
    public int Count => rows.Length;

    public ProductSearchOutcome Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new([], 0);
        string[] terms = LocationSearchIndex.Normalize(query.Trim()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
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
}

public sealed partial class SnapshotStore
{
    /// <summary>Reads the generation, categories, products and today's prices in one transaction, so a
    /// concurrent Sync cannot mix copies. The price is the newest one on or before <paramref name="today"/>.</summary>
    public ProductSearchIndex ReadProductIndex(DateOnly today)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT generation FROM active";
        string? generation = command.ExecuteScalar() as string;
        List<ProductSearchResult> products = [];
        if (generation is not null)
        {
            Dictionary<Guid, (Guid? Parent, string Name)> categories = [];
            command.CommandText = "SELECT id, parent_id, name FROM categories WHERE generation=$g";
            command.Parameters.AddWithValue("$g", generation);
            using (SqliteDataReader reader = command.ExecuteReader())
                while (reader.Read())
                    categories[Guid.Parse(reader.GetString(0))] = (reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)), reader.GetString(2));

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
                        ORDER BY b.effective_from DESC LIMIT 1)
                FROM products p WHERE p.generation=$g
                """;
            command.Parameters.AddWithValue("$today", today.ToString("O", CultureInfo.InvariantCulture));
            using SqliteDataReader rows = command.ExecuteReader();
            while (rows.Read())
                products.Add(new(Guid.Parse(rows.GetString(0)), rows.GetString(1), rows.GetString(2), Path(Guid.Parse(rows.GetString(3))),
                    rows.GetString(4), Number(rows, 5)));
        }
        transaction.Commit();
        return new(generation, products);
    }
}
