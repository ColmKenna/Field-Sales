using System.Globalization;
using System.Text.Json;
using FieldSales.Pricing;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>A downloaded product with what an order line captures: its breadcrumb, the base price in effect
/// on a day (null when none is) and the manifest of the snapshot that supplied them.</summary>
public sealed record OrderableProduct(SnapshotProduct Product, IReadOnlyList<string> Breadcrumb, decimal? Price, SnapshotManifest Manifest);

public sealed partial class SnapshotStore
{
    /// <summary>Reads a product, its breadcrumb and its price in effect on <paramref name="today"/> (the business day) from one
    /// active generation, even if Sync activates another during the read. The price is the newest
    /// <c>EffectiveFrom</c> on or before the day, so a later-dated price is ignored until its day; a day before the snapshot was
    /// built counts as its build day (WI-059 D2).</summary>
    public OrderableProduct? ReadOrderableProduct(Guid id, DateOnly today)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        DateOnly day = PriceDay(command, today);
        command.CommandText = """
            SELECT p.generation, p.code, p.name, p.category_id, p.unit, p.quantity_step, p.minimum_quantity, p.parent_product_id, g.manifest,
                   (SELECT b.amount FROM base_prices b WHERE b.generation=p.generation AND b.product_id=p.id AND b.effective_from <= $today
                    ORDER BY b.effective_from DESC LIMIT 1)
            FROM products p JOIN active a ON a.generation=p.generation JOIN generations g ON g.id=p.generation WHERE p.id=$id
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$today", day.ToString("O", CultureInfo.InvariantCulture));
        SnapshotProduct product;
        string generation;
        decimal? price;
        SnapshotManifest manifest;
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            if (!reader.Read()) return null;
            generation = reader.GetString(0);
            product = new(id, reader.GetString(1), reader.GetString(2), Guid.Parse(reader.GetString(3)), reader.GetString(4),
                Number(reader, 5), Number(reader, 6), reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)));
            manifest = JsonSerializer.Deserialize<SnapshotManifest>(reader.GetString(8), SnapshotContract.JsonOptions)!;
            price = Number(reader, 9);
        }
        List<string> breadcrumb = ReadBreadcrumb(connection, transaction, generation, product.CategoryId);
        transaction.Commit();
        return new(product, breadcrumb, price, manifest);
    }

    // The day the active snapshot prices on: today, or its build day when the tablet's clock is set before it (WI-059 D2). Read in
    // the caller's transaction, so it comes from the same generation as the prices.
    private static DateOnly PriceDay(SqliteCommand command, DateOnly today)
    {
        command.CommandText = "SELECT g.manifest FROM active a JOIN generations g ON g.id=a.generation";
        return command.ExecuteScalar() is string json
            && JsonSerializer.Deserialize<SnapshotManifest>(json, SnapshotContract.JsonOptions) is { } manifest
            ? BusinessCalendar.PriceDay(today, manifest.BuildDate)
            : today;
    }
}
