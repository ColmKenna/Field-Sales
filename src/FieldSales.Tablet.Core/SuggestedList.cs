using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>What one snapshot says about a Location's stock history (snapshot 1.3): the products on its last Stock Check,
/// counted or Not checked, and the products on its most recent Accepted Orders.</summary>
public sealed record LocationStockHistory(IReadOnlySet<Guid> LastVisit, IReadOnlySet<Guid> OrderedRecently)
{
    public static LocationStockHistory None { get; } = new(new HashSet<Guid>(), new HashSet<Guid>());
}

/// <summary>Why a product is on the Suggested List. A product on both sources is listed once with both.</summary>
[Flags]
public enum SuggestionSources { None = 0, LastVisit = 1, OrderedRecently = 2 }

/// <summary>One row of the Suggested List: the product as downloaded, and why it is suggested.</summary>
public sealed record SuggestedProduct(ProductSearchResult Product, SuggestionSources Sources)
{
    /// <summary>"Last visit", "Ordered recently", or "Last visit · Ordered recently".</summary>
    public string Label => SuggestedList.Label(Sources);
}

/// <summary>The Suggested List a Stock Check starts from (T-5.4.1): every product on the Location's last Stock Check plus
/// every product on its last Accepted Orders, each once, labelled by source. This is the only place the list is worked out.</summary>
/// <remarks>
/// A pure function of one snapshot. The server has already left out orders not yet Accepted, other reps' Locations and
/// products the rep can't see (restricted), and the import refused any row naming a product outside the snapshot; a
/// product missing from <paramref name="products"/> is still skipped, so the list only ever offers products the rep has.
/// Rows are in name order.
/// </remarks>
public static class SuggestedList
{
    public const string Empty = "No suggested products yet — search or browse to add";

    public static IReadOnlyList<SuggestedProduct> Build(LocationStockHistory history, ProductSearchIndex products)
    {
        Dictionary<Guid, SuggestionSources> sources = [];
        foreach (Guid id in history.LastVisit) sources[id] = SuggestionSources.LastVisit;
        foreach (Guid id in history.OrderedRecently) sources[id] = sources.GetValueOrDefault(id) | SuggestionSources.OrderedRecently;
        return [.. sources
            .Select(source => products.Find(source.Key) is { } product ? new SuggestedProduct(product, source.Value) : null)
            .OfType<SuggestedProduct>()
            .OrderBy(row => row.Product.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.Product.Id)];
    }

    public static string Label(SuggestionSources sources) => sources switch
    {
        SuggestionSources.LastVisit => "Last visit",
        SuggestionSources.OrderedRecently => "Ordered recently",
        SuggestionSources.LastVisit | SuggestionSources.OrderedRecently => "Last visit · Ordered recently",
        _ => ""
    };
}

public sealed partial class SnapshotStore
{
    /// <summary>The Location's stock history in one generation, normally <see cref="ProductSearchIndex.Generation"/>, so the
    /// list and its products come from the same copy. Empty when the generation has none (a 1.2 snapshot, or no history).</summary>
    public LocationStockHistory ReadStockHistory(string? generation, Guid locationId)
    {
        if (generation is null) return LocationStockHistory.None;
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        HashSet<Guid> Read(string table)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT product_id FROM {table} WHERE generation=$g AND location_id=$l";
            command.Parameters.AddWithValue("$g", generation);
            command.Parameters.AddWithValue("$l", locationId.ToString());
            using SqliteDataReader reader = command.ExecuteReader();
            HashSet<Guid> ids = [];
            while (reader.Read()) ids.Add(Guid.Parse(reader.GetString(0)));
            return ids;
        }
        LocationStockHistory history = new(Read("last_stock_check_lines"), Read("recent_order_lines"));
        transaction.Commit();
        return history;
    }
}
