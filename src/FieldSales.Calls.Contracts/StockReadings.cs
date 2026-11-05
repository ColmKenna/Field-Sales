namespace FieldSales.Calls.Contracts;

/// <summary>One counted reading of a product at a Location: its count in its unit, and the Call and moment it was recorded.</summary>
public sealed record StockReading(Guid ProductId, decimal Count, string Unit, Guid CallId, DateTimeOffset SavedAt);

/// <summary>A Location's current stock level (T-5.7.1, US-016 S2): for each product, the latest reading across its Calls.</summary>
public static class StockReadings
{
    /// <summary>For each product counted at <paramref name="locationId"/>, the count on the Call saved latest. "Latest" is the
    /// moment the Call was saved, compared as an instant whatever its offset; the order Calls were uploaded, received or listed in,
    /// and the date they name, never decide it. Equal moments go to the larger Call id, so the answer never depends on order.
    /// Only counts are readings: a Not checked entry or a Low mark with no count says nothing about the level, and Out (a count
    /// of 0) does. Every reading stays on its own Call; this only says which is current.</summary>
    public static IReadOnlyDictionary<Guid, StockReading> Current(Guid locationId, IEnumerable<TabletCall> calls) =>
        calls.Where(call => call.LocationId == locationId)
            .SelectMany(call => (call.StockEntries ?? [])
                .Where(entry => entry.Count is not null)
                .Select(entry => new StockReading(entry.ProductId, entry.Count!.Value, entry.Unit, call.Id, call.SavedAt)))
            .GroupBy(reading => reading.ProductId)
            .ToDictionary(product => product.Key,
                product => product.OrderByDescending(reading => reading.SavedAt.UtcDateTime).ThenByDescending(reading => reading.CallId).First());
}
