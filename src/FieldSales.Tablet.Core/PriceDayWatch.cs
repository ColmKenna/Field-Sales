using FieldSales.Pricing;

namespace FieldSales.Tablet.Core;

/// <summary>Tells open screens when the business day changes (WI-059 D4), so a page left open over midnight in Ireland shows the
/// prices a new line would capture. It looks once a minute while the app is in the foreground; coming back to the foreground
/// already re-reads every page, so a day that changes meanwhile is picked up there.</summary>
public sealed class PriceDayWatch : IDisposable
{
    /// <summary>How often the day is looked at: prices on screen switch at most this long after midnight.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly TimeProvider clock;
    private readonly Action changed;
    private readonly ITimer timer;
    private readonly Lock gate = new();
    private DateOnly day;
    private bool disposed;

    public PriceDayWatch(TimeProvider clock, Action changed)
    {
        this.clock = clock;
        this.changed = changed;
        day = Today();
        timer = clock.CreateTimer(_ => Check(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Starts looking, from the day it is now.</summary>
    public void Start()
    {
        lock (gate)
        {
            if (disposed) return;
            day = Today();
            timer.Change(Interval, Interval);
        }
    }

    public void Stop() { lock (gate) if (!disposed) timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }

    private void Check()
    {
        lock (gate)
        {
            DateOnly now = Today();
            if (disposed || now == day) return;
            day = now;
        }
        changed();
    }

    private DateOnly Today() => BusinessCalendar.Today(clock.GetUtcNow());

    public void Dispose() { lock (gate) { disposed = true; timer.Dispose(); } }
}
