// Frozen from main 933c044 for WI-033 compatibility tests; only namespaces changed.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldSales.Snapshot.V10.Contracts;

// Explicit metadata keeps snapshot serialization available in trimmed mobile Release builds (as in the spike).
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(TabletSnapshot))]
[JsonSerializable(typeof(SnapshotManifest))]
[JsonSerializable(typeof(SnapshotLocation))]
[JsonSerializable(typeof(SnapshotCategory))]
[JsonSerializable(typeof(SnapshotProduct))]
[JsonSerializable(typeof(SnapshotBasePrice))]
[JsonSerializable(typeof(SnapshotContact))]
public sealed partial class SnapshotJsonContext : JsonSerializerContext;
