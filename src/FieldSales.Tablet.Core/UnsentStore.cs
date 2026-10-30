using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public enum UnsentKind { Call, Order, ScheduleChange }

/// <summary>What the rep's Unsent count shows: every saved item, and how many orders are still being built
/// or are ready to send. An order whose body has no recognised state is counted in the total only.</summary>
public sealed record UnsentSummary(int Total, int InProgress, int ReadyToSend);

/// <summary>A piece of the rep's captured work, waiting for Sync. Location work uses a GUID string
/// named locationId; capture screens (E5) define the remaining JSON body. Legacy bodies are preserved.</summary>
public sealed record UnsentItem(Guid Id, UnsentKind Kind, DateTimeOffset SavedUtc, string Json);

/// <summary>An item the server confirmed at Sync (T-4.1.2), exactly as it was sent. Read-only on the tablet.</summary>
public sealed record SentItem(UnsentItem Item, DateTimeOffset SentUtc);

/// <summary>The rep's Unsent work, in its own database. Snapshot replacement may read Location
/// references to retain shop details, but never rewrites this database.</summary>
/// <remarks>Format 2 (T-4.1.2) adds the items the server has confirmed. Confirming moves an item out of the Unsent
/// work in one transaction, so it stops counting as unsent and is never offered for upload again.</remarks>
public sealed class UnsentStore(RepWorkspace workspace)
{
    private const int SchemaVersion = 2;
    private const string SentTable = """
        CREATE TABLE sent(id TEXT PRIMARY KEY, kind TEXT NOT NULL CHECK(kind IN ('Call','Order','ScheduleChange')),
            saved_utc TEXT NOT NULL, sent_utc TEXT NOT NULL, json TEXT NOT NULL);
        """;
    private const string Schema = """
        CREATE TABLE work(id TEXT PRIMARY KEY, kind TEXT NOT NULL CHECK(kind IN ('Call','Order','ScheduleChange')),
            saved_utc TEXT NOT NULL, json TEXT NOT NULL);
        """ + SentTable;

    private SqliteConnection Open()
    {
        SqliteConnection connection = SqliteEngine.Open(workspace.UnsentPath);
        SqliteEngine.EnsureSchema(connection, SchemaVersion, Schema, upgradeFromPrevious: SentTable);
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
        while (reader.Read()) items.Add(ReadItem(reader));
        return items;
    }

    /// <summary>Counts the rep's saved work. Asking never creates the database.</summary>
    public UnsentSummary Summarise()
    {
        if (!File.Exists(workspace.UnsentPath)) return new(0, 0, 0);
        int total = 0, inProgress = 0, ready = 0;
        foreach (UnsentItem item in Read())
        {
            total++;
            if (item.Kind != UnsentKind.Order) continue;
            using JsonDocument body = JsonDocument.Parse(item.Json);
            if (!body.RootElement.TryGetProperty("state", out JsonElement state) || state.ValueKind != JsonValueKind.String) continue;
            if (state.ValueEquals(nameof(OrderState.InProgress))) inProgress++;
            else if (state.ValueEquals(nameof(OrderState.ReadyToSend))) ready++;
        }
        return new(total, inProgress, ready);
    }

    /// <summary>One saved item by id, or null when none has that id.</summary>
    public UnsentItem? Find(Guid id)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, kind, saved_utc, json FROM work WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    /// <summary>Records that the server confirmed this item: it leaves the Unsent work and is kept as Sent, in one
    /// transaction. False when no unsent item has this id (it was already marked).</summary>
    public bool MarkSent(Guid id, DateTimeOffset sentUtc)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sent(id, kind, saved_utc, sent_utc, json) SELECT id, kind, saved_utc, $sent, json FROM work WHERE id=$id;
            DELETE FROM work WHERE id=$id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$sent", sentUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        int changed = command.ExecuteNonQuery();
        transaction.Commit();
        return changed > 0;
    }

    /// <summary>The items the server has confirmed, as they were sent.</summary>
    public IReadOnlyList<SentItem> ReadSent()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, kind, saved_utc, json, sent_utc FROM sent ORDER BY id";
        using SqliteDataReader reader = command.ExecuteReader();
        List<SentItem> items = [];
        while (reader.Read())
            items.Add(new(ReadItem(reader), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
        return items;
    }

    /// <summary>Whether the server has confirmed an item with this id. Asking never creates the database.</summary>
    public bool IsSent(Guid id)
    {
        if (!File.Exists(workspace.UnsentPath)) return false;
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sent WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteScalar() is not null;
    }

    private static UnsentItem ReadItem(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), Enum.Parse<UnsentKind>(reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture), reader.GetString(3));

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
