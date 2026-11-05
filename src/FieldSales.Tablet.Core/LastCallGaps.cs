using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>The Low products a Location's most recent Call left unordered (T-5.1.5): from the snapshot's 1.4 family, or from
/// a newer Call this tablet holds. Carried to a later order at the Location that has no Call of its own.</summary>
public sealed record LastCallGaps(Guid CallId, DateOnly CalledOn, IReadOnlyList<Guid> ProductIds);

/// <summary>The gaps carried to an order with no Call (T-5.1.5): the Call they were left on, and its Low products still open, as
/// Low entries the Low tab lists. The order on screen isn't taken into account here: its lines show those items ADDED.</summary>
public sealed record CarriedLow(Guid CallId, DateOnly CalledOn, IReadOnlyList<CallStockEntry> Entries);

/// <summary>Works out which of a shop's last Call gaps are still open on this tablet (WI-053 D2, D3).</summary>
public static class CarriedGaps
{
    /// <summary>The most recent Call is the snapshot's, unless this tablet holds a Call at the shop saved after the snapshot was
    /// built: that one can't be in the snapshot, so it is newer, and its own Low items carry. Either way, a product on any other
    /// order at the shop dated on or after the Call's date, in any state, is no longer a gap ("any order since the date").</summary>
    public static CarriedLow? Of(Guid locationId, Guid orderOnScreen, LastCallGaps? fromSnapshot, DateTimeOffset snapshotBuiltUtc,
        IEnumerable<TabletCall> tabletCalls, IEnumerable<TabletOrder> tabletOrders, ProductSearchIndex index)
    {
        TabletCall? newer = tabletCalls.Where(call => call.LocationId == locationId && call.SavedAt > snapshotBuiltUtc)
            .OrderByDescending(call => call.CalledOn).ThenByDescending(call => call.SavedAt).FirstOrDefault();
        (Guid callId, DateOnly calledOn, IEnumerable<CallStockEntry> low) = newer is not null
            ? (newer.Id, newer.CalledOn, (newer.StockEntries ?? []).Where(entry => entry.Low))
            : fromSnapshot is { } gaps
                ? (gaps.CallId, gaps.CalledOn, gaps.ProductIds.Select(index.Find).OfType<ProductSearchResult>()
                    .Select(product => new CallStockEntry(product.Id, product.Code, product.Name, product.Unit, null, Low: true)))
                : (Guid.Empty, default, []);
        if (callId == Guid.Empty) return null;
        HashSet<Guid> orderedSince = [.. tabletOrders
            .Where(order => order.LocationId == locationId && order.Id != orderOnScreen && order.TakenOn >= calledOn)
            .SelectMany(order => order.Lines.Select(line => line.ProductId))];
        CallStockEntry[] open = [.. low.Where(entry => !orderedSince.Contains(entry.ProductId)).DistinctBy(entry => entry.ProductId)];
        return open.Length == 0 ? null : new(callId, calledOn, open);
    }

    /// <summary>Reads what <see cref="Of"/> needs from this tablet: the gaps in <paramref name="index"/>'s generation, and every
    /// saved Call and order, unsent (in any state) or already sent.</summary>
    public static CarriedLow? Read(RepWorkspace workspace, Guid locationId, Guid orderOnScreen, ProductSearchIndex index)
    {
        if (index.Generation is not { } generation) return null;
        SnapshotStore snapshot = new(workspace);
        if (snapshot.ReadManifest(generation) is not { } manifest) return null;
        LastCallGaps? gaps = snapshot.ReadLastCallGaps(generation, locationId);
        List<TabletCall> calls = [];
        List<TabletOrder> orders = [];
        if (File.Exists(workspace.UnsentPath))
        {
            UnsentStore unsent = new(workspace);
            foreach (UnsentItem item in unsent.Read().Concat(unsent.ReadSent().Select(sent => sent.Item)))
            {
                if (CallStore.Parse(item) is { } call) calls.Add(call);
                else if (OrderStore.Parse(item) is { } order) orders.Add(order);
            }
        }
        return Of(locationId, orderOnScreen, gaps, manifest.BuiltUtc, calls, orders, index);
    }
}

public sealed partial class SnapshotStore
{
    /// <summary>The manifest of one generation, or null when there is no such generation.</summary>
    public SnapshotManifest? ReadManifest(string generation)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT manifest FROM generations WHERE id=$g";
        command.Parameters.AddWithValue("$g", generation);
        return command.ExecuteScalar() is string manifest
            ? JsonSerializer.Deserialize<SnapshotManifest>(manifest, SnapshotContract.JsonOptions) : null;
    }

    /// <summary>The Location's last Call gaps in one generation, normally <see cref="ProductSearchIndex.Generation"/>; null when
    /// the generation has none (a snapshot before 1.4, no Call, or every gap ordered since).</summary>
    public LastCallGaps? ReadLastCallGaps(string? generation, Guid locationId)
    {
        if (generation is null) return null;
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT call_id, called_on, product_id FROM last_call_low_lines WHERE generation=$g AND location_id=$l ORDER BY product_id
            """;
        command.Parameters.AddWithValue("$g", generation);
        command.Parameters.AddWithValue("$l", locationId.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        Guid? callId = null;
        DateOnly calledOn = default;
        List<Guid> products = [];
        while (reader.Read())
        {
            callId ??= Guid.Parse(reader.GetString(0));
            if (products.Count == 0) calledOn = DateOnly.ParseExact(reader.GetString(1), "O", System.Globalization.CultureInfo.InvariantCulture);
            products.Add(Guid.Parse(reader.GetString(2)));
        }
        return callId is Guid id ? new(id, calledOn, products) : null;
    }
}
