using System.Text.Json.Serialization;

namespace FieldSales.TabletSpike.Core;

// Explicit metadata keeps manifest/report serialization available in trimmed mobile Release builds.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(FixtureManifest))]
[JsonSerializable(typeof(SpikeReport))]
internal partial class FixtureJsonContext : JsonSerializerContext;
