namespace FieldSales.Tablet.Core;

/// <summary>Foreground checks for expiry and clock rollback. No idle timeout or network requests.</summary>
public sealed class TabletSessionMonitor : IDisposable
{
    private readonly TabletDevice device;
    private readonly TimeProvider clock;
    private readonly Action<Exception> report;
    private readonly ITimer timer;
    private int checking;
    private volatile bool active;
    private bool disposed;

    public TabletSessionMonitor(TabletDevice device, TimeProvider clock, Action<Exception> report)
    {
        this.device = device;
        this.clock = clock;
        this.report = report;
        timer = clock.CreateTimer(_ => _ = CheckAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        device.SessionChanged += Changed;
    }

    public void Start() { active = true; Schedule(TimeSpan.Zero); }
    public void Stop() { active = false; Schedule(Timeout.InfiniteTimeSpan); }
    private void Changed() { if (active) Schedule(TimeSpan.Zero); }
    private void Schedule(TimeSpan due) { lock (timer) { if (!disposed) timer.Change(due, Timeout.InfiniteTimeSpan); } }

    private async Task CheckAsync()
    {
        if (!active || Interlocked.Exchange(ref checking, 1) != 0) return;
        TimeSpan next = TimeSpan.FromSeconds(1);
        try
        {
            if (await device.CheckSessionAsync() is { } signedIn)
            {
                TimeSpan remaining = signedIn + LocalAccessRules.Window - clock.GetUtcNow();
                next = remaining <= TimeSpan.Zero ? TimeSpan.Zero
                    : remaining < next ? remaining : next;
            }
        }
        catch (Exception error)
        {
            report(error);
            try { await device.EndSessionAsync(); }
            catch (Exception removalError) { report(removalError); }
        }
        finally
        {
            Interlocked.Exchange(ref checking, 0);
            if (active) Schedule(next);
        }
    }

    public void Dispose()
    {
        active = false;
        device.SessionChanged -= Changed;
        lock (timer) { disposed = true; timer.Dispose(); }
    }
}
