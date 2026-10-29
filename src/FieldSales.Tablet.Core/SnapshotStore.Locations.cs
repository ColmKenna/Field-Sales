using System.Text.Json;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public sealed record OfflineLocationDetail(SnapshotLocation Location, bool IsAssigned, bool HasUnsentOrder);

public sealed partial class SnapshotStore
{
    /// <summary>The current assignment wins. A retained identity is visible only while this rep
    /// has Unsent work referencing it, and never grants permission to start new work.</summary>
    public OfflineLocationDetail? ReadLocation(Guid id)
    {
        Gate.Wait();
        try
        {
            using SqliteConnection connection = Open();
            SnapshotLocation? assigned = ReadAssignedLocation(connection, id);
            if (assigned is not null) return new(assigned, true, false);
            Dictionary<Guid, bool> references = LocationRetention.References(workspace);
            if (!references.TryGetValue(id, out bool hasOrder)) return null;
            SnapshotLocation? retained = LocationRetention.Read(workspace).GetValueOrDefault(id);
            return retained is null ? null : new(retained, false, hasOrder);
        }
        finally { Gate.Release(); }
    }

    // One statement binds the active pointer and the entire identity to the same generation.
    internal static SnapshotLocation? ReadAssignedLocation(SqliteConnection connection, Guid id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.name, l.customer_id, l.customer_name, l.town_id, l.town_name,
                   l.county_name, l.eircode, l.main_contact, g.manifest
            FROM locations l JOIN active a ON a.generation=l.generation
            JOIN generations g ON g.id=l.generation WHERE l.id=$id
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? new(id, reader.GetString(0), Guid.Parse(reader.GetString(1)), reader.GetString(2),
            new(Guid.Parse(reader.GetString(3)), reader.GetString(4), reader.GetString(5)),
            reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null :
                JsonSerializer.Deserialize<SnapshotContact>(reader.GetString(7), SnapshotContract.JsonOptions),
            ReadContactReplacement(reader.GetString(8), id)) : null;
    }
}
