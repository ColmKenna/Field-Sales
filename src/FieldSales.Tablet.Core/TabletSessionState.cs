using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FieldSales.Tablet.Core;

// One process-wide boundary for each platform secure store, shared by reopened Device
// objects. Network requests never hold Gate: logout can invalidate them immediately.
internal sealed class TabletSessionState
{
    private static readonly ConditionalWeakTable<ISecureStore, TabletSessionState> States = new();
    public static TabletSessionState For(ISecureStore secure) => States.GetValue(secure, _ => new());
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public ConcurrentDictionary<string, SemaphoreSlim> RefreshGates { get; } = new(StringComparer.Ordinal);
    private long version;
    public long Version => Interlocked.Read(ref version);
    public bool Blocked { get; set; }
    public event Action? Changed;
    public void Invalidate() { Blocked = true; Interlocked.Increment(ref version); }

    public async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        await Gate.WaitAsync();
        long previous = Version;
        try { return await action(); }
        finally
        {
            bool changed = previous != Version;
            Gate.Release();
            if (changed) Changed?.Invoke();
        }
    }
}
