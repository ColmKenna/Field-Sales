using FieldSales.Tablet.Core;

namespace FieldSales.Tablet;

/// <summary>Desktop adapter for the MAUI boundary only. The linked component and guard are
/// production source; storage/session rules are covered in Tablet.Core integration tests.</summary>
public sealed class TabletApp
{
    public event Action? Resumed;
    public event Action? SessionChanged;
    public long SessionVersion { get; set; }
    public bool Connected { get; set; } = true;
    public void ChangeSession() { SessionVersion++; SessionChanged?.Invoke(); }
    public void Resume() => Resumed?.Invoke();
    public Func<Task<TabletEntry>> Enter { get; set; } = () => Task.FromResult(new TabletEntry(EntryScreen.Home, "colm", ""));
    public Func<string, Guid, OfflineLocationDetail?> Read { get; set; } = (_, _) => null;
    public Task<TabletEntry> EnterAsync() => Enter();
    public Task<TabletEntry> EnterOnArrivalAsync() => EnterAsync();
    public OfflineLocationDetail? ReadLocation(string rep, Guid id) => Read(rep, id);
    public Func<long, Task<IReadOnlyList<UnsentKind>>> ReadKinds { get; set; } = _ => Task.FromResult<IReadOnlyList<UnsentKind>>([]);
    public Func<long, Task<bool>> End { get; set; } = _ => Task.FromResult(false);
    public Task<IReadOnlyList<UnsentKind>> ReadUnsentKindsAsync(long version) => ReadKinds(version);
    public Task<bool> SignOutAsync(long version) => End(version);
    public HomeView ReadHome(string rep) => new("Last synced 07:42", []);
    public Func<string, CancellationToken, Task<SyncOutcome>> SyncWork { get; set; } = (_, _) =>
        Task.FromResult(new SyncOutcome(SyncStatus.Completed, "Last synced 07:42", null));
    public Task<SyncOutcome> SyncAsync(string rep, CancellationToken ct = default) => SyncWork(rep, ct);
}

public sealed record HomeView(string LastSynced, IReadOnlyList<HomeLocation> Locations);
