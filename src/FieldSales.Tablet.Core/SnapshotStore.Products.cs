using System.Globalization;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public sealed record OfflineProductRow(Guid Id, string Code, string Name);
public sealed record OfflineProductPage(IReadOnlyList<OfflineProductRow> Products, bool HasNext);
public sealed record OfflineProductDetail(SnapshotProduct Product, IReadOnlyList<string> Breadcrumb,
    IReadOnlyList<SnapshotProductAttribute> Attributes);

public sealed partial class SnapshotStore
{
    public const int ProductPageSize = 50;

    /// <summary>A bounded page from the active generation, with no online lookup.</summary>
    public OfflineProductPage ReadProducts(int page)
    {
        if (page < 0 || page > int.MaxValue / ProductPageSize) throw new ArgumentOutOfRangeException(nameof(page));
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.code, p.name FROM products p JOIN active a ON a.generation=p.generation
            ORDER BY p.code COLLATE NOCASE, p.id LIMIT $limit OFFSET $offset
            """;
        command.Parameters.AddWithValue("$limit", ProductPageSize + 1);
        command.Parameters.AddWithValue("$offset", page * ProductPageSize);
        using SqliteDataReader reader = command.ExecuteReader();
        List<OfflineProductRow> products = [];
        while (reader.Read()) products.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)));
        bool hasNext = products.Count > ProductPageSize;
        if (hasNext) products.RemoveAt(ProductPageSize);
        return new(products, hasNext);
    }

    /// <summary>Reads a product and its facts from one active generation, even if Sync activates another during the read.</summary>
    public OfflineProductDetail? ReadProduct(Guid id)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT p.generation, p.code, p.name, p.category_id, p.unit, p.quantity_step, p.minimum_quantity, p.parent_product_id
            FROM products p JOIN active a ON a.generation=p.generation WHERE p.id=$id
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        SnapshotProduct product;
        string generation;
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            if (!reader.Read()) return null;
            generation = reader.GetString(0);
            product = new(id, reader.GetString(1), reader.GetString(2), Guid.Parse(reader.GetString(3)), reader.GetString(4),
                Number(reader, 5), Number(reader, 6), reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)));
        }
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$g", generation);
        command.Parameters.AddWithValue("$id", id.ToString());
        command.CommandText = "SELECT position, name, value, is_archived FROM product_attributes WHERE generation=$g AND product_id=$id ORDER BY position";
        List<SnapshotProductAttribute> attributes = [];
        using (SqliteDataReader reader = command.ExecuteReader())
            while (reader.Read()) attributes.Add(new(id, reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        command.CommandText = "SELECT parent_id, name FROM categories WHERE generation=$g AND id=$category";
        command.Parameters.AddWithValue("$category", product.CategoryId.ToString());
        List<string> breadcrumb = [];
        HashSet<Guid> visited = [];
        Guid? category = product.CategoryId;
        while (category is Guid current && visited.Add(current))
        {
            command.Parameters["$category"].Value = current.ToString();
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read()) break;
            breadcrumb.Insert(0, reader.GetString(1));
            category = reader.IsDBNull(0) ? null : Guid.Parse(reader.GetString(0));
        }
        transaction.Commit();
        return new(product, breadcrumb, attributes);
    }

    private static decimal? Number(SqliteDataReader reader, int column) => reader.IsDBNull(column) ? null
        : decimal.Parse(reader.GetString(column), CultureInfo.InvariantCulture);
}
