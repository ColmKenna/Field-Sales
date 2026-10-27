namespace FieldSales.SnapshotSpike;

public sealed record SpikeScale(int Products, int Locations, int OrdersPerLocation, int LinesPerOrder)
{
    public SpikeCounts Counts()
    {
        if (Products < 1 || Locations < 1 || OrdersPerLocation < 0 || LinesPerOrder < 1)
            throw new ArgumentException("Products, locations and lines per order must be positive; orders per location may be zero.");
        checked
        {
            return new(Products, Locations, Math.Min(Products, 200) * 4, Products * 8,
                Products * 3, Products * 9, (int)((long)Products * 15 / 100), Products / 20,
                Locations * OrdersPerLocation, Locations * OrdersPerLocation * LinesPerOrder,
                Locations, Locations / 10, Locations / 25, Locations / 25 * 5);
        }
    }
}

public static class SpikeProfiles
{
    public static SpikeScale Get(string name) => name switch
    {
        "baseline" => new(5_000, 500, 6, 20),
        "target" => new(25_000, 500, 12, 20),
        "stress" => new(100_000, 2_000, 24, 30),
        _ => throw new ArgumentException("Profile must be baseline, target or stress.")
    };
}
