using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>The Low products a Location's most recent Call left unordered (T-5.1.5): from the snapshot's 1.4 family, or from
/// a newer Call this tablet holds. Carried to a later order at the Location that has no Call of its own.</summary>
public sealed record LastCallGaps(Guid CallId, DateOnly CalledOn, IReadOnlyList<Guid> ProductIds);

public sealed partial class SnapshotStore
{
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
