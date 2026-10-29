using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.Snapshot.Contracts;
using LegacyContract = FieldSales.Snapshot.V10.Contracts;

namespace FieldSales.Tablet.Core.Tests;

public sealed class SnapshotAttributeContractTests
{
    [Fact]
    public void Should_ReadAllOriginalDataWithNoAttributes_When_SnapshotHasVersion10FieldsOnly()
    {
        TabletSnapshot original = Snapshots.Build("colm", locations: 3, products: 20);
        JsonObject legacy = JsonSerializer.SerializeToNode(original, SnapshotContract.JsonOptions)!.AsObject();
        legacy.Remove("productAttributes");
        legacy["manifest"]!["counts"]!.AsObject().Remove("productAttributes");

        TabletSnapshot read = JsonSerializer.Deserialize<TabletSnapshot>(
            JsonSerializer.SerializeToUtf8Bytes(legacy), SnapshotContract.JsonOptions)!;

        Assert.Equal("1.0", read.Manifest.Version);
        Assert.Equal(0, read.Manifest.Counts.ProductAttributes);
        Assert.Null(read.ProductAttributes);
        Assert.Equal(original.Manifest, read.Manifest);
        Assert.Equal(original.Locations, read.Locations);
        Assert.Equal(original.Categories, read.Categories);
        Assert.Equal(original.Products, read.Products);
        Assert.Equal(original.BasePrices, read.BasePrices);
    }

    [Fact]
    public void Should_RoundTripAttributesAndKeepOldReadersCompatible_When_UsingVersion11Metadata()
    {
        Assert.Equal("1.2", SnapshotContract.CurrentVersion); // The explicit 1.1 fixture below remains supported.
        Assert.Equal([1], SnapshotContract.ServedMajors);
        TabletSnapshot original = Snapshots.Build("colm", locations: 3, products: 20);
        Guid productId = original.Products[0].Id;
        SnapshotProductAttribute[] attributes = [new(productId, 0, "Weight", "200 g", false),
            new(productId, 1, "Shelf life", "24 months", false),
            new(productId, 2, "Barcode", "5391234567890", true)];
        TabletSnapshot snapshot = original with
        {
            Manifest = original.Manifest with { Version = "1.1", Counts = original.Manifest.Counts with { ProductAttributes = 3 } },
            ProductAttributes = attributes
        };
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotContract.JsonOptions);
        using JsonDocument json = JsonDocument.Parse(plain);
        Assert.Equal(3, json.RootElement.GetProperty("manifest").GetProperty("counts").GetProperty("productAttributes").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("productAttributes").GetArrayLength());
        Assert.Equal(["productId", "position", "name", "value", "isArchived"],
            json.RootElement.GetProperty("productAttributes")[0].EnumerateObject().Select(field => field.Name));

        TabletSnapshot read = JsonSerializer.Deserialize<TabletSnapshot>(plain, SnapshotContract.JsonOptions)!;
        Assert.Equal(attributes, read.ProductAttributes!);
        Assert.Equal(3, read.Manifest.Counts.ProductAttributes);
        // Frozen metadata from 933c044 must ignore both new properties, including on trimmed apps.
        var legacy = JsonSerializer.Deserialize<LegacyContract.TabletSnapshot>(plain, LegacyContract.SnapshotContract.JsonOptions)!;
        Assert.Equal("1.1", legacy.Manifest.Version);
        Assert.Equal(original.Products.Count, legacy.Manifest.Counts.Products);
        Assert.Equal(original.Products.Select(product => product.Id), legacy.Products.Select(product => product.Id));
        Assert.Equal(original.BasePrices.Select(price => price.Amount), legacy.BasePrices.Select(price => price.Amount));
    }
}
