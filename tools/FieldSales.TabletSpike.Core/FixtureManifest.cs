using System.Text.Json;

namespace FieldSales.TabletSpike.Core;

public sealed record FixtureFile(string FileName, long Bytes, string Sha256);
public sealed record UnsentEvidence(string Schema, string OwnerSubject, long Bytes, string Sha256);
public sealed record FixtureScale(int Products, int Locations, int OrdersPerLocation, int LinesPerOrder);
public sealed record FixtureManifest(string Schema, string Profile, int Seed, DateOnly AsOf,
    string OwnerSubject, FixtureScale Scale, Dictionary<string, long> Counts,
    FixtureFile Json, FixtureFile Gzip, UnsentEvidence Unsent)
{
    public const string Format = "spike-format-v1";
    public static JsonSerializerOptions JsonOptions { get; } = FixtureJsonContext.Default.Options;
    public static readonly string[] Families =
        ["categories", "products", "locations", "promotions", "replacements", "history", "thresholds", "leads", "masters"];
    public static readonly string[] CountNames =
        ["products", "locations", "categories", "attributes", "basePrices", "quantityBreaks", "promotions", "replacements",
        "historicalOrders", "historicalLines", "thresholds", "leads", "masters", "branchLinks"];

    public void Validate(string owner)
    {
        if (Schema != Format || OwnerSubject != owner || owner != $"spike-rep-{Seed}" ||
            Unsent.Schema != "spike-unsent-v1" || Unsent.OwnerSubject != owner)
            throw new InvalidDataException("Unsupported fixture format or foreign synthetic owner.");
        if (Json.FileName != "snapshot.json" || Gzip.FileName != "snapshot.json.gz" ||
            Counts.Count != CountNames.Length || CountNames.Any(n => !Counts.TryGetValue(n, out long c) || c < 0) ||
            Scale.Products < 1 || Scale.Locations < 1 || Counts["products"] != Scale.Products ||
            Counts["locations"] != Scale.Locations || Scale.OrdersPerLocation < 0 || Scale.LinesPerOrder < 0 ||
            Counts["historicalOrders"] != (long)Scale.Locations * Scale.OrdersPerLocation ||
            Counts["historicalLines"] != Counts["historicalOrders"] * Scale.LinesPerOrder)
            throw new InvalidDataException("Invalid fixture counts or file names.");
        foreach (var file in new[] { Json, Gzip, new FixtureFile("unsent.json", Unsent.Bytes, Unsent.Sha256) })
            if (file.Bytes <= 0 || file.Sha256.Length != 64 || file.Sha256.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("Invalid fixture length/hash.");
    }
}
