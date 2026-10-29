namespace FieldSales.Tablet.Core;

/// <summary>
/// The result of entry's snapshot read, offered briefly to the next screen. The caller must still validate its
/// session and local access window. It is taken once; a stale, cleared or missing handoff requires a fresh entry read.
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

    public void Clear() { lock (gate) offered = null; }

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
