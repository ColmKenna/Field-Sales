using System.Buffers.Binary;
using System.Globalization;

namespace FieldSales.SnapshotSpike;

// Iterators bound memory to one record/order, including at the stress scale.
public sealed class SnapshotGenerator(SpikeOptions options)
{
    private static readonly string[] Names = ["Murphy's", "Ó Briain", "Quinn's", "Carey's", "Ní Mhurchú", "Walsh"];
    private static readonly string[] Towns = ["Rathdrum", "Aughrim", "Arklow", "Dún Laoghaire", "Bray", "Wicklow"];
    public SpikeCounts Counts { get; } = options.Scale.Counts();

    private Guid Id(int kind, int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, options.Seed);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[4..], kind);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[8..], index);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[12..], 0x53504B31);
        return new Guid(bytes);
    }

    private uint Variation(int index) => unchecked((uint)options.Seed * 16777619u + (uint)index * 2654435761u);
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private string Label(int index) => $"{Names[index % Names.Length]} {Variation(index):X8}";
    private int CategoryPaths => Counts.Categories / 4;

    private string AttributeValue(int index, int attribute)
    {
        string value = $"{Label(index + attribute)} / {Number(index)} / {Towns[(index + attribute) % Towns.Length]}";
        if (index % 7 != attribute) return value;
        return value + "; " + string.Join("; ", Enumerable.Range(0, 6).Select(part =>
            $"batch {Variation(index + attribute + part * 17):X8} {Towns[(index + part) % Towns.Length]}"));
    }

    private SpikeCategory Category(int path, int depth) => new(Id(2, path * 4 + depth),
        depth == 0 ? null : Id(2, path * 4 + depth - 1), $"Category {Number(path)} / {Number(depth)}");

    public IEnumerable<SpikeCategory> Categories()
    {
        for (int path = 0; path < CategoryPaths; path++)
            for (int depth = 0; depth < 4; depth++) yield return Category(path, depth);
    }

    public IEnumerable<SpikeProduct> Products()
    {
        for (int i = 0; i < Counts.Products; i++)
        {
            decimal price = 1m + Variation(i) % 100_000 / 100m;
            var breadcrumbs = Enumerable.Range(0, 4).Select(depth => Category(i % CategoryPaths, depth)).ToArray();
            var attributes = Enumerable.Range(0, 8).Select(a => new SpikeAttribute($"Attribute {Number(a)}",
                AttributeValue(i, a))).ToArray();
            var prices = new[] { new SpikePrice(options.AsOf, price), new SpikePrice(options.AsOf.AddDays(30), price + 0.25m),
                new SpikePrice(options.AsOf.AddDays(90), price + 0.50m) };
            var tiers = Enumerable.Range(0, 3).Select(t => new SpikeTier($"Tier {Number(t)}",
                Enumerable.Range(0, 3).Select(b => new SpikeBreak(1 + b * 12, decimal.Round(price * (1m - (t + b) / 100m), 2))).ToArray())).ToArray();
            yield return new(Id(1, i), $"SPIKE-{i:D7}", $"{Label(i)} care product {Number(i % 500)} / {Towns[i % Towns.Length]}",
                breadcrumbs[^1].Id, breadcrumbs, attributes, prices, i % 5 == 0 ? "kg" : "each",
                Step(i), Step(i), tiers);
        }
    }

    private static decimal Step(int product) => product % 5 == 0 ? 0.25m : 1m;

    public IEnumerable<SpikeLocation> Locations()
    {
        for (int i = 0; i < Counts.Locations; i++)
            yield return new(Id(4, i), Id(3, i), $"{Label(i)} customer", $"{Label(i)} shop",
                Towns[i % Towns.Length], $"{Number(1 + i % 250)} Main Street, {Towns[i % Towns.Length]}, synthetic address",
                new(Id(5, i), $"{Label(i + 1)} contact", $"+353 1 {i:D7}", $"contact-{Number(i)}@example.invalid"));
    }

    public IEnumerable<SpikePromotion> Promotions()
    {
        for (int i = 0; i < Counts.Promotions; i++) yield return new(Id(1, i), options.AsOf,
            options.AsOf.AddDays(30), 5 + i % 20);
    }

    public IEnumerable<SpikeReplacement> Replacements()
    {
        for (int i = 0; i < Counts.Replacements; i++) yield return new(Id(1, i), Id(1, Counts.Products - i - 1));
    }

    private SpikeOrderLine Line(int product, int ordinal) => new(Id(1, product), Step(product) * (1 + ordinal % 12),
        1m + Variation(product) % 100_000 / 100m);

    public IEnumerable<SpikeHistoricalOrder> History()
    {
        for (int location = 0; location < Counts.Locations; location++)
            for (int order = 0; order < options.Scale.OrdersPerLocation; order++)
            {
                int ordinal = location * options.Scale.OrdersPerLocation + order;
                var lines = Enumerable.Range(0, options.Scale.LinesPerOrder).Select(line =>
                    Line((int)(((long)ordinal * options.Scale.LinesPerOrder + line) % Counts.Products), line)).ToArray();
                yield return new(Id(6, ordinal), Id(4, location), options.AsOf.AddDays(-30 * (order + 1)), lines);
            }
    }

    public IEnumerable<SpikeThreshold> Thresholds()
    {
        for (int i = 0; i < Counts.Thresholds; i++) yield return new(Id(4, i), 50m + i % 100);
    }

    public IEnumerable<SpikeLead> Leads()
    {
        for (int i = 0; i < Counts.Leads; i++) yield return new(Id(7, i), Id(4, i * 10),
            $"{Label(i)} prospect", $"Synthetic lead near {Towns[i % Towns.Length]} / {Variation(i):X8}");
    }

    public IEnumerable<SpikeMaster> Masters()
    {
        for (int i = 0; i < Counts.Masters; i++) yield return new(Id(8, i), Id(3, i * 25),
            Enumerable.Range(1, 5).Select(branch => Id(4, i * 25 + branch)).ToArray());
    }

    public SpikeUnsent Unsent() => new("spike-unsent-v1", options.OwnerSubject,
    [
        new(Id(9, 0), "Call", Id(4, 0), "spike-previous-snapshot", "Visit captured offline", []),
        new(Id(9, 1), "Call", Id(4, 0), "spike-previous-snapshot", "Phone call captured offline", []),
        new(Id(9, 2), "Order", Id(4, 0), "spike-previous-snapshot", "Ready to send", [Line(0, 0)]),
        new(Id(9, 3), "ScheduleChange", Id(4, 0), "spike-previous-snapshot", "Visit moved to tomorrow", [])
    ]);
}
