using FieldSales.Calls.Contracts;
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
    public event Action? PriceDayChanged;
    /// <summary>The business day changes while the page is open (WI-059 D4).</summary>
    public void ChangePriceDay() => PriceDayChanged?.Invoke();
    public Func<Task<TabletEntry>> Enter { get; set; } = () => Task.FromResult(new TabletEntry(EntryScreen.Home, "colm", ""));
    public Func<string, Guid, OfflineLocationDetail?> Read { get; set; } = (_, _) => null;
    public Task<TabletEntry> EnterAsync() => Enter();
    public Task<TabletEntry> EnterOnArrivalAsync() => EnterAsync();
    public OfflineLocationDetail? ReadLocation(string rep, Guid id) => Read(rep, id);
    public Func<long, Task<IReadOnlyList<UnsentKind>>> ReadKinds { get; set; } = _ => Task.FromResult<IReadOnlyList<UnsentKind>>([]);
    public Func<long, Task<bool>> End { get; set; } = _ => Task.FromResult(false);
    public Task<IReadOnlyList<UnsentKind>> ReadUnsentKindsAsync(long version) => ReadKinds(version);
    public Task<bool> SignOutAsync(long version) => End(version);
    public const string SignInNeedsConnection = "Sign-in needs a connection. Try again when you have signal.";
    public SyncNotices SyncNotices { get; } = new();
    public CallDrafts CallDrafts { get; } = new();
    public Func<string, Guid, Guid, CallContent, CallResult> SaveWork { get; set; } = (_, _, _, _) => new CallResult(null, "Not set up in this test.");
    public CallResult SaveCall(string rep, Guid callId, Guid locationId, CallContent content) => SaveWork(rep, callId, locationId, content);
    public Func<string, Guid, Guid, CallContent, Guid, CallResult> FollowUpWork { get; set; } =
        (_, _, _, _, _) => new CallResult(null, "Not set up in this test.");
    public CallResult SaveCall(string rep, Guid callId, Guid locationId, CallContent content, Guid followUpOf) =>
        FollowUpWork(rep, callId, locationId, content, followUpOf);
    public Func<string, Guid, Guid, TabletOrder?> FindCallOrder { get; set; } = (_, _, _) => null;
    public TabletOrder? ReadCallOrder(string rep, Guid callId, Guid locationId) => FindCallOrder(rep, callId, locationId);
    public Func<string, Guid, Guid, Guid, decimal, OrderResult> AddFromCall { get; set; } =
        (_, _, _, _, _) => new OrderResult(null, "Order entry isn't set up in this test.");
    public OrderResult AddOrderLineFromCall(string rep, Guid callId, Guid locationId, Guid productId, decimal quantity) =>
        AddFromCall(rep, callId, locationId, productId, quantity);
    /// <summary>By default the plain add (AddWork), recording the Call it was for in <see cref="AddedForCalls"/>.</summary>
    public Func<string, Guid, Guid, Guid, decimal, Guid, OrderResult>? AddForCall { get; set; }
    public List<Guid> AddedForCalls { get; } = [];
    public OrderResult AddOrderLineForCall(string rep, Guid orderId, Guid locationId, Guid productId, decimal quantity, Guid callId)
    {
        AddedForCalls.Add(callId);
        return AddForCall is { } add ? add(rep, orderId, locationId, productId, quantity, callId) : AddWork(rep, orderId, locationId, productId, quantity);
    }
    public Func<string, Guid, TabletCall?> FindCall { get; set; } = (_, _) => null;
    public TabletCall? ReadCall(string rep, Guid callId) => FindCall(rep, callId);
    public Func<string, Guid, CallView?> FindCallView { get; set; } = (_, _) => null;
    public CallView? ReadCallView(string rep, Guid callId) => FindCallView(rep, callId);
    public Func<string, Guid, Guid, CallContent, CallResult> CorrectWork { get; set; } =
        (_, _, _, _) => new CallResult(null, "Not set up in this test.");
    public CallResult CorrectCall(string rep, Guid callId, Guid locationId, CallContent content) =>
        CorrectWork(rep, callId, locationId, content);
    /// <summary>The shop's recent order quantities per product (rep, shop); none by default.</summary>
    public Func<string, Guid, IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>>> FindQuantities { get; set; } =
        (_, _) => new Dictionary<Guid, IReadOnlyList<OrderedQuantity>>();
    public IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>> ReadOrderedQuantities(string rep, Guid locationId,
        ProductSearchIndex index) => FindQuantities(rep, locationId);
    public Func<string, Guid, Guid, OrderResult> LinkWork { get; set; } = (_, _, _) => new OrderResult(null, "Not set up in this test.");
    public OrderResult LinkOrderToCall(string rep, Guid orderId, Guid callId) => LinkWork(rep, orderId, callId);
    /// <summary>The shop's carried gaps for an order with no Call (rep, shop, order on screen); none by default.</summary>
    public Func<string, Guid, Guid, CarriedLow?> FindCarried { get; set; } = (_, _, _) => null;
    public CarriedLow? ReadCarriedLow(string rep, Guid locationId, Guid orderId, ProductSearchIndex index) =>
        FindCarried(rep, locationId, orderId);
    public Func<Task<SignInAnswer>> SignIn { get; set; } = () => Task.FromResult(new SignInAnswer(null, null));
    public Task<SignInAnswer> SignInAsync() => SignIn();
    public HomeView Home { get; set; } = new("Last synced 07:42", []);
    public HomeView ReadHome(string rep) => Home;
    public Func<string, HomeDayRecord>? ReadDay { get; set; }
    public HomeDayRecord ReadHomeRecord(string rep)
    {
        if (ReadDay is not null) return ReadDay(rep);
        UnsentSummary summary = ReadSummary(rep);
        return new(new DateOnly(2026, 10, 1), [new("unsent", "Unsent", summary.Total, "/sync",
            summary.NeedsAttention > 0 ? summary.AttentionText : null)],
            [new("today", "Today", [], "No visits scheduled today"), new("unplanned", "Unplanned today", [])]);
    }
    public void ResumeCleanup(string rep) { }
    public Func<string, ProductSearchIndex> ReadIndex { get; set; } = _ => new ProductSearchIndex(null, []);
    public Func<string, Guid, Guid, Guid, decimal, OrderResult> AddWork { get; set; } =
        (_, _, _, _, _) => new OrderResult(null, "Order entry isn't set up in this test.");
    public Func<string, Guid, TabletOrder?> FindOrder { get; set; } = (_, _) => null;
    public Func<string, Guid, OrderResult> MarkWork { get; set; } = (_, _) => new OrderResult(null, "Order entry isn't set up in this test.");
    public Func<string, UnsentSummary> ReadSummary { get; set; } = _ => new UnsentSummary(0, 0, 0);
    public OrderResult MarkOrderReady(string rep, Guid orderId) => MarkWork(rep, orderId);
    public UnsentSummary ReadUnsentSummary(string rep) => ReadSummary(rep);
    public Func<string, IReadOnlyList<UnsentView>> ReadViews { get; set; } = _ => [];
    public IReadOnlyList<UnsentView> ReadUnsentViews(string rep) => ReadViews(rep);
    public Func<string, Guid, long, bool, bool, Task<bool>> ChangeRejected { get; set; } = (_, _, _, _, _) => Task.FromResult(false);
    public Task<bool> ChangeRejectedWorkAsync(string rep, Guid id, long version, bool delete, bool confirmed) =>
        ChangeRejected(rep, id, version, delete, confirmed);
    public Func<string, Guid, long, Task<bool>> ReviewOrder { get; set; } = (_, _, _) => Task.FromResult(false);
    public Task<bool> MarkReviewedOrderReadyAsync(string rep, Guid id, long version) => ReviewOrder(rep, id, version);
    public Func<string, int> CountWaiting { get; set; } = _ => 0;
    public int CountWaitingToSend(string rep) => CountWaiting(rep);
    public ProductSearchIndex ReadProductIndex(string rep) => ReadIndex(rep);
    public Func<string, Guid, StockCheckStart> ReadStock { get; set; } = (_, _) => StockCheckStart.Empty;
    public StockCheckStart ReadStockCheckStart(string rep, Guid locationId) => ReadStock(rep, locationId);
    public OrderResult AddOrderLine(string rep, Guid orderId, Guid locationId, Guid productId, decimal quantity) =>
        AddWork(rep, orderId, locationId, productId, quantity);
    public TabletOrder? ReadOrder(string rep, Guid orderId) => FindOrder(rep, orderId);
    /// <summary>By default the order FindOrder returns, standing as its state says; set it to show Sending, Sent and so on.</summary>
    public Func<string, Guid, OrderView?>? FindView { get; set; }
    public OrderView? ReadOrderView(string rep, Guid orderId) => FindView is { } find ? find(rep, orderId)
        : FindOrder(rep, orderId) is { } order
            ? new(order, order.State == OrderState.ReadyToSend ? OrderStanding.ReadyToSend : OrderStanding.InProgress) : null;
    public Func<string, Guid, Guid, decimal, OrderResult> ChangeLine { get; set; } = (_, _, _, _) => new OrderResult(null, "Not set up in this test.");
    public OrderResult ChangeOrderLine(string rep, Guid orderId, Guid lineId, decimal quantity) => ChangeLine(rep, orderId, lineId, quantity);
    public Func<string, Guid, Guid, bool, OrderResult> RemoveLine { get; set; } = (_, _, _, _) => new OrderResult(null, "Not set up in this test.");
    public OrderResult RemoveOrderLine(string rep, Guid orderId, Guid lineId, bool deleteOrderIfLast) => RemoveLine(rep, orderId, lineId, deleteOrderIfLast);
    public Func<string, Guid, OrderResult> Reopen { get; set; } = (_, _) => new OrderResult(null, "Not set up in this test.");
    public OrderResult ReopenOrder(string rep, Guid orderId) => Reopen(rep, orderId);
    public Func<string, Guid, bool, OrderResult> DeleteWork { get; set; } = (_, _, _) => new OrderResult(null, "Not set up in this test.");
    public OrderResult DeleteOrder(string rep, Guid orderId, bool confirmed) => DeleteWork(rep, orderId, confirmed);
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
