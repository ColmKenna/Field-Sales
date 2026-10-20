using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace FieldSales.TabletSpike.Core;

public sealed record SavedWork(string Id, string Kind, string Note, string Json);
public sealed record UnsentState(string? OriginalFixtureSha256, string Fingerprint, IReadOnlyList<SavedWork> Items);

public sealed class UnsentSqliteStore(SpikeWorkspace workspace)
{
    private SqliteConnection Open()
    {
        var connection = SqliteEnvironment.Open(workspace.UnsentPath);
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL); CREATE TABLE IF NOT EXISTS work(id TEXT PRIMARY KEY,json TEXT NOT NULL);";
        command.ExecuteNonQuery();
        return connection;
    }
    public bool IsSeeded()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM metadata WHERE key='originalHash'";
        return (long)command.ExecuteScalar()! == 1;
    }
    public void SeedOnce(ReadOnlySpan<byte> fixture, UnsentEvidence evidence)
    {
        if (evidence.Schema != "spike-unsent-v1" || fixture.Length > 4 * 1024 * 1024 || fixture.Length != evidence.Bytes ||
            Convert.ToHexStringLower(SHA256.HashData(fixture)) != evidence.Sha256.ToLowerInvariant())
            throw new InvalidDataException("Invalid Unsent fixture length/hash.");
        using var document = JsonDocument.Parse(fixture.ToArray());
        var root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "spike-unsent-v1" ||
            root.GetProperty("ownerSubject").GetString() != workspace.Owner || evidence.OwnerSubject != workspace.Owner)
            throw new InvalidDataException("Foreign Unsent fixture.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT COUNT(*) FROM metadata WHERE key='originalHash'";
        if ((long)exists.ExecuteScalar()! != 0) return;
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO work(id,json) VALUES($id,$json)";
            insert.Parameters.AddWithValue("$id", item.GetProperty("id").GetGuid().ToString());
            insert.Parameters.AddWithValue("$json", item.GetRawText());
            insert.ExecuteNonQuery();
        }
        using var meta = connection.CreateCommand();
        meta.Transaction = transaction;
        meta.CommandText = "INSERT INTO metadata(key,value) VALUES('owner',$owner),('originalHash',$hash)";
        meta.Parameters.AddWithValue("$owner", workspace.Owner);
        meta.Parameters.AddWithValue("$hash", evidence.Sha256);
        meta.ExecuteNonQuery();
        transaction.Commit();
    }
    public UnsentState Read()
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        using var meta = connection.CreateCommand();
        meta.Transaction = transaction;
        meta.CommandText = "SELECT value FROM metadata WHERE key='originalHash'";
        string? original = meta.ExecuteScalar() as string;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id,json FROM work ORDER BY id";
        using var reader = command.ExecuteReader();
        var items = new List<SavedWork>();
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            writer.WriteString("owner", workspace.Owner);
            writer.WriteStartArray("items");
            while (reader.Read())
            {
                string json = reader.GetString(1);
                using var doc = JsonDocument.Parse(json);
                var item = doc.RootElement;
                items.Add(new(reader.GetString(0), item.GetProperty("kind").GetString()!, item.GetProperty("note").GetString()!, json));
                WriteCanonical(writer, item);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return new(original, Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())), items);
    }
    public void EditNote(string id, string note)
    {
        if (note.Length > 4096) throw new ArgumentException("Test note exceeds 4096 characters.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT json FROM work WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        string json = command.ExecuteScalar() as string ?? throw new InvalidOperationException("Saved item missing.");
        var item = JsonNode.Parse(json)!;
        item["note"] = note;
        command.CommandText = "UPDATE work SET json=$json WHERE id=$id";
        command.Parameters.AddWithValue("$json", item.ToJsonString());
        command.ExecuteNonQuery();
        transaction.Commit();
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
