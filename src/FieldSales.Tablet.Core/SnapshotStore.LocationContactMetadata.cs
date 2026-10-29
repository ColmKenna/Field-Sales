using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public sealed partial class SnapshotStore
{
    // Private local metadata, not a change to the wire manifest. Keeping it on the generation
    // row gives it the existing atomic activation/cleanup lifecycle without a SQLite migration.
    private const string ContactReplacementsKey = "locationMainContactReplacements";

    private static void WriteContactReplacements(SqliteConnection connection, string generation,
        SnapshotManifest manifest, Dictionary<Guid, SnapshotMainContactReplacement> replacements)
    {
        if (replacements.Count == 0) return;
        if (int.Parse(manifest.Version.Split('.')[1], CultureInfo.InvariantCulture) < 2)
            throw new InvalidDataException("Main-contact replacement status requires snapshot 1.2 or later.");
        JsonObject metadata = JsonSerializer.SerializeToNode(manifest, SnapshotContract.JsonOptions)!.AsObject();
        metadata[ContactReplacementsKey] = JsonSerializer.SerializeToNode(replacements, LocationContactMetadataJson.Default.ContactReplacements);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE generations SET manifest=$manifest WHERE id=$g AND state='staged'";
        command.Parameters.AddWithValue("$manifest", metadata.ToJsonString());
        command.Parameters.AddWithValue("$g", generation);
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidDataException("The staged snapshot is no longer available for contact status.");
    }

    private static SnapshotMainContactReplacement? ReadContactReplacement(string metadata, Guid location)
    {
        using JsonDocument document = JsonDocument.Parse(metadata);
        return document.RootElement.TryGetProperty(ContactReplacementsKey, out JsonElement replacements)
            && replacements.TryGetProperty(location.ToString(), out JsonElement replacement)
                ? replacement.Deserialize<SnapshotMainContactReplacement>(SnapshotContract.JsonOptions) : null;
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Dictionary<Guid, SnapshotMainContactReplacement>), TypeInfoPropertyName = "ContactReplacements")]
internal sealed partial class LocationContactMetadataJson : JsonSerializerContext;
