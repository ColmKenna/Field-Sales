using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>A small rep-bound identity cache, independent of the snapshot generations and Unsent
/// payloads. All mutations run under the workspace's snapshot/capture gate.</summary>
internal static class LocationRetention
{
    private static Guid? LocationId(UnsentItem item)
    {
        using JsonDocument document = JsonDocument.Parse(item.Json);
        return document.RootElement.TryGetProperty("locationId", out JsonElement value)
            && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out Guid id) && id != Guid.Empty ? id : null;
    }

    // The value records whether at least one reference is an Order, for S4's exact message.
    internal static Dictionary<Guid, bool> References(RepWorkspace workspace)
    {
        Dictionary<Guid, bool> references = [];
        if (!File.Exists(workspace.UnsentPath)) return references;
        foreach (UnsentItem item in new UnsentStore(workspace).Read())
            if (LocationId(item) is Guid id)
                references[id] = references.GetValueOrDefault(id) || item.Kind == UnsentKind.Order;
        return references;
    }

    internal static Dictionary<Guid, SnapshotLocation> Read(RepWorkspace workspace)
    {
        if (!File.Exists(workspace.RetainedLocationsPath)) return [];
        try
        {
            using FileStream file = File.OpenRead(workspace.RetainedLocationsPath);
            RetainedLocations? saved = JsonSerializer.Deserialize(file, LocationRetentionJson.Default.RetainedLocations);
            if (saved is null || saved.RepSubject != workspace.RepSubject || saved.Locations is null)
                throw new InvalidDataException("Retained shop details do not belong to this rep.");
            return saved.Locations.ToDictionary(location => location.Id);
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Retained shop details could not be read.", error);
        }
    }

    internal static void BeforeSave(RepWorkspace workspace, UnsentItem item)
    {
        if (LocationId(item) is not Guid id || !File.Exists(workspace.SnapshotPath)) return;
        using SqliteConnection connection = SqliteEngine.Open(workspace.SnapshotPath);
        SnapshotLocation? assigned = SnapshotStore.ReadAssignedLocation(connection, id);
        if (assigned is null) return;
        Dictionary<Guid, SnapshotLocation> retained = Read(workspace);
        retained[id] = assigned;
        // Persist identity first: every successful capture has its fallback even if the app stops next.
        Write(workspace, retained.Values);
    }

    internal static void BeforeActivation(RepWorkspace workspace, SqliteConnection connection)
    {
        Dictionary<Guid, bool> references = References(workspace);
        Dictionary<Guid, SnapshotLocation> retained = Read(workspace);
        foreach (Guid id in retained.Keys.Except(references.Keys).ToArray()) retained.Remove(id);
        foreach (Guid id in references.Keys)
            if (SnapshotStore.ReadAssignedLocation(connection, id) is { } assigned) retained[id] = assigned;
        // Only the old active generation is read. An aborted download can never publish staged identity.
        Write(workspace, retained.Values);
    }

    private static void Write(RepWorkspace workspace, IEnumerable<SnapshotLocation> locations)
    {
        SnapshotLocation[] rows = locations.OrderBy(location => location.Id).ToArray();
        if (rows.Length == 0) { File.Delete(workspace.RetainedLocationsPath); return; }
        string temporary = workspace.RetainedLocationsPath + ".tmp";
        try
        {
            using (FileStream file = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(file, new RetainedLocations(workspace.RepSubject, rows),
                    LocationRetentionJson.Default.RetainedLocations);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, workspace.RetainedLocationsPath, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}

internal sealed record RetainedLocations(string RepSubject, SnapshotLocation[] Locations);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(RetainedLocations))]
internal sealed partial class LocationRetentionJson : JsonSerializerContext;
