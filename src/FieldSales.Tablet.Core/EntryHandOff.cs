namespace FieldSales.Tablet.Core;

/// <summary>
/// The result of the entry check, passed to the screen the app opens next, so a cold start checks the 24-hour rule once
/// instead of twice. It is taken once and only for a moment: a screen reached any other way (the app resumed, back, a
/// later navigation) does its own check, and a stale or missing hand-off always means "check again".
/// </summary>
public sealed class EntryHandOff(TimeProvider clock, TimeSpan window)
{
    private readonly object gate = new();
    private TabletEntry? offered;
    private long offeredAt;

    public void Offer(TabletEntry entry)
    {
        lock (gate) { offered = entry; offeredAt = clock.GetTimestamp(); }
    }

    /// <summary>The offered entry if it is still fresh, otherwise null. Either way it cannot be taken again.</summary>
    public TabletEntry? Take()
    {
        lock (gate)
        {
            TabletEntry? entry = offered;
            offered = null;
            return entry is not null && clock.GetElapsedTime(offeredAt) <= window ? entry : null;
        }
    }
}
