using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public enum UnsentKind { Call, Order, ScheduleChange }

/// <summary>A piece of the rep's captured work, waiting for Sync. Location work uses a GUID string
/// named locationId; capture screens (E5) define the remaining JSON body. Legacy bodies are preserved.</summary>
public sealed record UnsentItem(Guid Id, UnsentKind Kind, DateTimeOffset SavedUtc, string Json);

/// <summary>The rep's Unsent work, in its own database. Snapshot replacement may read Location
/// references to retain shop details, but never rewrites this database.</summary>
public sealed class UnsentStore(RepWorkspace workspace)
{
    private const int SchemaVersion = 1;
    private const string Schema = """
        CREATE TABLE work(id TEXT PRIMARY KEY, kind TEXT NOT NULL CHECK(kind IN ('Call','Order','ScheduleChange')),
            saved_utc TEXT NOT NULL, json TEXT NOT NULL);
        """;

    private SqliteConnection Open()
    {
        SqliteConnection connection = SqliteEngine.Open(workspace.UnsentPath);
        SqliteEngine.EnsureSchema(connection, SchemaVersion, Schema);
        return connection;
    }

    public void Save(UnsentItem item)
    {
        using (JsonDocument body = JsonDocument.Parse(item.Json))
            if (body.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Saved work must be a JSON object.", nameof(item));
        // Serialize capture and snapshot activation so a shop's context is kept before either
        // the saved work commits or a newer snapshot removes that shop.
        SemaphoreSlim gate = SnapshotStore.GateFor(workspace);
        gate.Wait();
        try
        {
            LocationRetention.BeforeSave(workspace, item);
            SaveCore(item);
        }
        finally { gate.Release(); }
    }

    private void SaveCore(UnsentItem item)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO work(id, kind, saved_utc, json) VALUES($id, $kind, $saved, $json)
                ON CONFLICT(id) DO UPDATE SET kind=excluded.kind, saved_utc=excluded.saved_utc, json=excluded.json
            """;
        command.Parameters.AddWithValue("$id", item.Id.ToString());
        command.Parameters.AddWithValue("$kind", item.Kind.ToString());
        command.Parameters.AddWithValue("$saved", item.SavedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$json", item.Json);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UnsentItem> Read()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, kind, saved_utc, json FROM work ORDER BY id";
        using SqliteDataReader reader = command.ExecuteReader();
        List<UnsentItem> items = [];
        while (reader.Read())
            items.Add(new(Guid.Parse(reader.GetString(0)), Enum.Parse<UnsentKind>(reader.GetString(1)),
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture), reader.GetString(3)));
        return items;
    }

    /// <summary>A logical fingerprint of every item (properties in ordinal order), independent of file bytes.</summary>
    public string Fingerprint()
    {
        using MemoryStream bytes = new();
        using (Utf8JsonWriter writer = new(bytes))
        {
            writer.WriteStartObject();
            writer.WriteString("rep", workspace.RepSubject);
            writer.WriteStartArray("items");
            foreach (UnsentItem item in Read())
            {
                writer.WriteStartObject();
                writer.WriteString("id", item.Id);
                writer.WriteString("kind", item.Kind.ToString());
                writer.WriteString("savedUtc", item.SavedUtc.ToString("O", CultureInfo.InvariantCulture));
                writer.WritePropertyName("body");
                using (JsonDocument body = JsonDocument.Parse(item.Json)) WriteCanonical(writer, body.RootElement);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
