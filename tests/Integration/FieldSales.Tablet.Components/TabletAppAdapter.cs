using FieldSales.Ordering.Contracts;
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
    public SyncNotices SyncNotices { get; } = new();
    public Func<Task<SignInAnswer>> SignIn { get; set; } = () => Task.FromResult(new SignInAnswer(null, null));
    public Task<SignInAnswer> SignInAsync() => SignIn();
    public HomeView Home { get; set; } = new("Last synced 07:42", []);
    public HomeView ReadHome(string rep) => Home;
    public Func<string, ProductSearchIndex> ReadIndex { get; set; } = _ => new ProductSearchIndex(null, []);
    public Func<string, Guid, Guid, Guid, decimal, OrderResult> AddWork { get; set; } =
        (_, _, _, _, _) => new OrderResult(null, "Order entry isn't set up in this test.");
    public Func<string, Guid, TabletOrder?> FindOrder { get; set; } = (_, _) => null;
    public Func<string, Guid, OrderResult> MarkWork { get; set; } = (_, _) => new OrderResult(null, "Order entry isn't set up in this test.");
    public Func<string, UnsentSummary> ReadSummary { get; set; } = _ => new UnsentSummary(0, 0, 0);
    public OrderResult MarkOrderReady(string rep, Guid orderId) => MarkWork(rep, orderId);
    public UnsentSummary ReadUnsentSummary(string rep) => ReadSummary(rep);
    public Func<string, int> CountWaiting { get; set; } = _ => 0;
    public int CountWaitingToSend(string rep) => CountWaiting(rep);
    public ProductSearchIndex ReadProductIndex(string rep) => ReadIndex(rep);
    public OrderResult AddOrderLine(string rep, Guid orderId, Guid locationId, Guid productId, decimal quantity) =>
        AddWork(rep, orderId, locationId, productId, quantity);
    public TabletOrder? ReadOrder(string rep, Guid orderId) => FindOrder(rep, orderId);
    public Func<string, CancellationToken, Task<SyncOutcome>> SyncWork { get; set; } = (_, _) =>
        Task.FromResult(new SyncOutcome(SyncStatus.Completed, "Last synced 07:42", null));
    public IProgress<UploadProgress>? Progress { get; private set; }
    public Task<SyncOutcome> SyncAsync(string rep, CancellationToken ct = default, IProgress<UploadProgress>? progress = null)
    {
        Progress = progress;
        return SyncWork(rep, ct);
    }
}

public sealed record HomeView(string LastSynced, IReadOnlyList<HomeLocation> Locations);
