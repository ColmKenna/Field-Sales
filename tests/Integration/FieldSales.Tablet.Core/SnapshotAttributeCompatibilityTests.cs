using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Snapshot.Contracts;
using LegacyContract = FieldSales.Snapshot.V10.Contracts;
using V10Reader = FieldSales.Tablet.V10.Core.SnapshotDocumentReader;

namespace FieldSales.Tablet.Core.Tests;

public sealed class SnapshotAttributeCompatibilityTests
{
    [Fact]
    public void Should_KeepAllOriginalFields_When_V10ReaderReceivesAdditiveV11Attributes()
    {
        TabletSnapshot original = Snapshots.Build("colm", locations: 3, products: 200);
        JsonObject payload = JsonSerializer.SerializeToNode(original, SnapshotContract.JsonOptions)!.AsObject();
        payload["manifest"]!["version"] = "1.1";
        payload["manifest"]!["counts"]!["productAttributes"] = original.Products.Count * 3;
        JsonArray attributes = [];
        foreach (var product in original.Products)
        {
            attributes.Add(Row(product.Id, 0, "Weight", "200 g"));
            attributes.Add(Row(product.Id, 1, "Shelf life", "24 months"));
            attributes.Add(Row(product.Id, 2, "Barcode", "5391234567890"));
        }
        payload["productAttributes"] = attributes;
        using MemoryStream stream = new(JsonSerializer.SerializeToUtf8Bytes(payload));
        LegacyContract.SnapshotManifest? manifest = null;
        Dictionary<string, List<JsonElement>> families = [];
        new V10Reader(stream).Read(value => manifest = value, (family, row) =>
        {
            if (!families.TryGetValue(family, out var rows)) families[family] = rows = [];
            rows.Add(row.Clone());
        }, CancellationToken.None);

        Assert.Equal("1.1", manifest!.Version);
        Assert.Equal("colm", manifest.RepSubject);
        Assert.Equal(new LegacyContract.SnapshotCounts(original.Locations.Count, original.Categories.Count,
            original.Products.Count, original.BasePrices.Count), manifest.Counts);
        Assert.Equal(4, families.Count);
        // Compare every existing serialized field, not just counts or a few selected values.
        Compare("locations", original.Locations);
        Compare("categories", original.Categories);
        Compare("products", original.Products);
        Compare("basePrices", original.BasePrices);

        void Compare<T>(string family, IReadOnlyList<T> expected)
        {
            Assert.Equal(expected.Count, families[family].Count);
            for (int index = 0; index < expected.Count; index++)
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected[index], SnapshotContract.JsonOptions),
                    families[family][index]));
        }
    }

    private static JsonObject Row(Guid productId, int position, string name, string value) => new()
    {
        ["productId"] = productId.ToString(), ["position"] = position,
        ["name"] = name, ["value"] = value, ["isArchived"] = false
    };
}
