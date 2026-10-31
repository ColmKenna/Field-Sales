using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public enum UnsentKind { Call, Order, ScheduleChange }

/// <summary>Every unsent item, with counts for the three T-01 sections. Legacy work with an
/// unrecognised order state remains in the total, even when it cannot be classified.</summary>
public sealed record UnsentSummary(int Total, int InProgress, int ReadyToSend, int NeedsAttention = 0)
{
    public string AttentionText => NeedsAttention == 1 ? "1 needs attention" : $"{NeedsAttention} need attention";
}

/// <summary>A piece of the rep's captured work, waiting for Sync. Location work uses a GUID string
/// named locationId; capture screens (E5) define the remaining JSON body. Legacy bodies are preserved.</summary>
public sealed record UnsentItem(Guid Id, UnsentKind Kind, DateTimeOffset SavedUtc, string Json)
{
    public UploadRejection? Rejection { get; init; }
}

/// <summary>The server's technical refusal, separate from the captured work. RawReason is for support only.</summary>
public sealed record UploadRejection(string? Fault, string RawReason, int HttpStatus, DateTimeOffset RejectedUtc)
{
    public string Sentence => UploadFaultMessages.For(Fault);
}

/// <summary>An item the server confirmed at Sync (T-4.1.2), exactly as it was sent. Read-only on the tablet.</summary>
public sealed record SentItem(UnsentItem Item, DateTimeOffset SentUtc);

/// <summary>The rep's Unsent work, in its own database. Snapshot replacement may read Location
/// references to retain shop details, but never rewrites this database.</summary>
/// <remarks>Format 2 adds confirmed items. Format 3 adds technical refusals separately from captured payloads.
/// Confirmation moves work to Sent in one transaction and also removes its associated fault.</remarks>
public sealed partial class UnsentStore(RepWorkspace workspace)
{
    private const int SchemaVersion = 3;
    private const string SentTable = """
        CREATE TABLE sent(id TEXT PRIMARY KEY, kind TEXT NOT NULL CHECK(kind IN ('Call','Order','ScheduleChange')),
            saved_utc TEXT NOT NULL, sent_utc TEXT NOT NULL, json TEXT NOT NULL);
        """;
    private const string Schema = """
        CREATE TABLE work(id TEXT PRIMARY KEY, kind TEXT NOT NULL CHECK(kind IN ('Call','Order','ScheduleChange')),
            saved_utc TEXT NOT NULL, json TEXT NOT NULL);
        """ + SentTable + FaultTable;

    private const string SelectWork = """
        SELECT w.id, w.kind, w.saved_utc, w.json, f.fault, f.raw_reason, f.http_status, f.rejected_utc
        FROM work w LEFT JOIN work_faults f ON f.id=w.id
        """;

    private SqliteConnection Open()
    {
        SqliteConnection connection = SqliteEngine.Open(workspace.UnsentPath);
        try
        {
            using SqliteCommand version = connection.CreateCommand();
            version.CommandText = "PRAGMA user_version";
            if ((long)version.ExecuteScalar()! == 1)
                SqliteEngine.EnsureSchema(connection, 2, "", upgradeFromPrevious: SentTable);
            SqliteEngine.EnsureSchema(connection, SchemaVersion, Schema, upgradeFromPrevious: FaultTable);
            return connection;
        }
        catch { connection.Dispose(); throw; }
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
        command.CommandText = SelectWork + " ORDER BY w.id";
        using SqliteDataReader reader = command.ExecuteReader();
        List<UnsentItem> items = [];
        while (reader.Read()) items.Add(ReadItem(reader));
        return items;
    }

    /// <summary>Counts the rep's saved work. Asking never creates the database.</summary>
    public UnsentSummary Summarise()
    {
        if (!File.Exists(workspace.UnsentPath)) return new(0, 0, 0);
        int total = 0, inProgress = 0, ready = 0, attention = 0;
        foreach (UnsentItem item in Read())
        {
            total++;
            if (item.Rejection is not null) { attention++; continue; }
            if (item.Kind == UnsentKind.Call) { ready++; continue; }
            if (item.Kind != UnsentKind.Order) continue;
            using JsonDocument body = JsonDocument.Parse(item.Json);
            if (!body.RootElement.TryGetProperty("state", out JsonElement state) || state.ValueKind != JsonValueKind.String) continue;
            if (state.ValueEquals(nameof(OrderState.InProgress))) inProgress++;
            else if (state.ValueEquals(nameof(OrderState.ReadyToSend))) ready++;
        }
        return new(total, inProgress, ready, attention);
    }

    /// <summary>One saved item by id, or null when none has that id.</summary>
    public UnsentItem? Find(Guid id)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectWork + " WHERE w.id=$id";
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
            DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture), reader.GetString(3))
        {
            Rejection = reader.FieldCount >= 8 && !reader.IsDBNull(7)
                ? new(reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetInt32(6),
                    DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture)) : null
        };

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
