using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Pricing;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Core;
using Microsoft.Extensions.Logging;

namespace FieldSales.Tablet;

/// <summary>What Home shows: when the rep last synced and their downloaded Locations.</summary>
public sealed record HomeView(string LastSynced, IReadOnlyList<HomeLocation> Locations);

/// <summary>The screens' single entry point to the tablet's rules; the rules themselves live in Tablet.Core.</summary>
public sealed class TabletApp : IDisposable
{
    private readonly TabletDevice device;
    private readonly TabletEndpoints endpoints;
    private readonly IStorageSpace space;
    private readonly TimeProvider clock;
    private readonly EntryHandOff handOff;
    private readonly TabletSessionMonitor monitor;
    private readonly PriceDayWatch priceDay;
    private readonly object workGate = new();
    private CancellationTokenSource sessionWork = new();
    private readonly SemaphoreSlim syncGate = new(1, 1);

    public TabletApp(TabletDevice device, TabletEndpoints endpoints, IStorageSpace space, TimeProvider clock, ILogger<TabletApp> logger)
    {
        this.device = device;
        this.endpoints = endpoints;
        this.space = space;
        this.clock = clock;
        handOff = new(clock, TimeSpan.FromSeconds(5));
        device.SessionChanged += OnSessionChanged;
        monitor = new(device, clock, error => logger.LogError(error, "Could not check the tablet session."));
        monitor.Start();
        priceDay = new(clock, () => PriceDayChanged?.Invoke());
        priceDay.Start();
    }

    public const string SignInNeedsConnection = "Sign-in needs a connection. Try again when you have signal.";
    public const string SignInDidNotFinish = "Sign-in didn't finish. Try again.";

    /// <summary>Raised when the app comes back to the foreground, so the 24-hour rule is checked again.</summary>
    public event Action? Resumed;
    public event Action? SessionChanged;
    /// <summary>Raised when the business day changes while the app is in the foreground, so open pages show that day's prices
    /// (WI-059 D4).</summary>
    public event Action? PriceDayChanged;
    public long SessionVersion => device.SessionVersion;
    public void NotifyResumed() { monitor.Start(); priceDay.Start(); Resumed?.Invoke(); }
    public void NotifyStopped() { monitor.Stop(); priceDay.Stop(); }

    public bool Connected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    /// <summary>What the Sync screen last told the rep, kept for the session so a rebuilt page shows the same.</summary>
    public SyncNotices SyncNotices { get; } = new();

    public Task<TabletEntry> EnterAsync() => device.EnterAsync(Connected);

    /// <summary>Pass entry's snapshot result to the next screen; arrival still revalidates local session access.</summary>
    public void HandOff(TabletEntry entry) { if (entry.SessionVersion == SessionVersion) handOff.Offer(entry); }

    /// <summary>Reuse a fresh snapshot result only while its session and local access window are still valid.</summary>
    public async Task<TabletEntry> EnterOnArrivalAsync()
    {
        if (handOff.Take() is { } entry && entry.SessionVersion == SessionVersion
            && await device.CheckSessionAsync() is not null && entry.SessionVersion == SessionVersion) return entry;
        return await EnterAsync();
    }

    public Task<bool> SignOutAsync(long expectedVersion) => device.EndSessionAsync(expectedVersion);

    public async Task<IReadOnlyList<UnsentKind>> ReadUnsentKindsAsync(long expectedVersion)
    {
        TabletEntry entry = await EnterAsync();
        if (entry.RepSubject is null || entry.SessionVersion != expectedVersion || SessionVersion != expectedVersion)
            throw new OperationCanceledException("The tablet session changed.");
        UnsentKind[] kinds = await Task.Run(() => new UnsentStore(device.Workspace(entry.RepSubject)).Read().Select(item => item.Kind).ToArray());
        if (SessionVersion != expectedVersion) throw new OperationCanceledException("The tablet session changed.");
        return kinds;
    }

    /// <summary>The connected sign-in in the system browser: the rep whose day started, or a message to show.</summary>
    public async Task<SignInAnswer> SignInAsync()
    {
        TabletTiming.RepActed();
        long version = SessionVersion;
        TabletSignInFlow flow = new(endpoints.Identity, endpoints.Api, clock);
        PendingSignIn pending = flow.Start();
        try
        {
            WebAuthenticatorResult result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = pending.AuthorizeUrl,
                CallbackUrl = new Uri(TabletClientContract.RedirectUri)
            });
            StaffSignIn? signIn = await flow.CompleteAsync(pending, result.Properties);
            if (signIn is null) return new(null, SignInDidNotFinish);
            SignInOutcome outcome = await device.CompleteSignInAsync(signIn, version);
            return outcome.Status == SignInStatus.Started ? new(signIn.Subject, null) : new(null, outcome.Message);
        }
        catch (TaskCanceledException) { return new(null, null); } // The rep closed the browser; stay where they were.
        catch (HttpRequestException) { return new(null, SignInNeedsConnection); }
    }

    /// <summary>Sends the rep's finished work, then downloads their day; copies retired by the switch are deleted afterwards
    /// in the background (D2-B). <paramref name="progress"/> hears "Sending 2 of 3" as each item goes.</summary>
    public async Task<SyncOutcome> SyncAsync(string rep, CancellationToken ct = default, IProgress<UploadProgress>? progress = null)
    {
        TabletTiming.RepActed();
        TabletEntry entry = await EnterAsync();
        if (entry.RepSubject != rep) return Cancelled();
        long version = entry.SessionVersion;
        CancellationToken cancellation;
        lock (workGate) cancellation = sessionWork.Token;
        if (version != SessionVersion) return Cancelled();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, ct);
        try { await syncGate.WaitAsync(linked.Token); }
        catch (OperationCanceledException) { return Cancelled(); }
        try
        {
            if (version != SessionVersion || linked.IsCancellationRequested) return Cancelled();
            TabletSync sync = Sync(rep);
            long started = clock.GetTimestamp();
            SyncOutcome outcome = await sync.SyncAsync(ct: linked.Token, progress: progress);
            if (version != SessionVersion) return Cancelled();
            TabletTiming.Synced(outcome, clock.GetElapsedTime(started));
            _ = Task.Run(async () =>
            {
                long cleanup = clock.GetTimestamp();
                await sync.CleanupAsync();
                TabletTiming.CleanedUp(clock.GetElapsedTime(cleanup));
            });
            return outcome;
        }
        finally { syncGate.Release(); }
    }

    private static SyncOutcome Cancelled() => new(SyncStatus.Cancelled, SyncMessages.Cancelled, null);

    private void OnSessionChanged()
    {
        handOff.Clear();
        CancellationTokenSource previous;
        lock (workGate) { previous = sessionWork; sessionWork = new(); }
        previous.Cancel();
        previous.Dispose();
        SessionChanged?.Invoke();
    }

    public void Dispose()
    {
        device.SessionChanged -= OnSessionChanged;
        monitor.Dispose();
        priceDay.Dispose();
        lock (workGate) { sessionWork.Cancel(); sessionWork.Dispose(); }
    }

    /// <summary>Finishes any cleanup an earlier session left, after the day has opened.</summary>
    public void ResumeCleanup(string rep) => _ = Task.Run(() => Sync(rep).CleanupAsync());

    public HomeView ReadHome(string rep)
    {
        SnapshotStore store = new(device.Workspace(rep), clock);
        ActiveSnapshot? active = store.ReadActive();
        return new(active is null ? "No downloaded day yet" : SyncMessages.LastSynced(active.CompletedUtc, clock), store.ReadLocations());
    }

    public HomeDayRecord ReadHomeRecord(string rep) => new HomeRecordReader(device.Workspace(rep), clock).Read();

    public OfflineProductPage ReadProducts(string rep, int page) => new SnapshotStore(device.Workspace(rep), clock).ReadProducts(page);
    public OfflineProductDetail? ReadProduct(string rep, Guid id) => new SnapshotStore(device.Workspace(rep), clock).ReadProduct(id);

    public LocationSearchIndex ReadLocationIndex(string rep) => new SnapshotStore(device.Workspace(rep), clock).ReadLocationIndex();

    public OfflineLocationDetail? ReadLocation(string rep, Guid id) => new SnapshotStore(device.Workspace(rep), clock).ReadLocation(id);

    // Order entry: products are found offline in the active snapshot, and each added line is saved at once.
    public ProductSearchIndex ReadProductIndex(string rep) => new SnapshotStore(device.Workspace(rep), clock).ReadProductIndex(PriceToday());

    /// <summary>T-06's stock check: the downloaded products and the shop's Suggested List (T-5.4.1).</summary>
    public StockCheckStart ReadStockCheckStart(string rep, Guid locationId) =>
        new SnapshotStore(device.Workspace(rep), clock).ReadStockCheckStart(locationId, PriceToday());

    public OrderResult AddOrderLine(string rep, Guid orderId, Guid locationId, Guid productId, decimal quantity) =>
        new OrderStore(device.Workspace(rep), clock).AddLine(orderId, locationId, productId, quantity);

    public TabletOrder? ReadOrder(string rep, Guid orderId) => new OrderStore(device.Workspace(rep), clock).Find(orderId);

    public OrderResult MarkOrderReady(string rep, Guid orderId) => new OrderStore(device.Workspace(rep), clock).MarkReadyToSend(orderId);

    // Recording a Call (T-5.3.1): the draft survives page rebuilds; saving records it once as an Unsent Call.
    public CallDrafts CallDrafts { get; } = new();

    public CallResult SaveCall(string rep, Guid callId, Guid locationId, CallContent content) =>
        new CallStore(device.Workspace(rep), clock).Save(callId, locationId, content);

    /// <summary>T-5.7.1: a follow-up, saved as a new Call linked to the one it follows up.</summary>
    public CallResult SaveCall(string rep, Guid callId, Guid locationId, CallContent content, Guid followUpOf) =>
        new CallStore(device.Workspace(rep), clock).Save(callId, locationId, content, followUpOf);

    // Adding to the order from a Call (T-5.5.1): the Call's order is found or started under the order gate.
    public TabletOrder? ReadCallOrder(string rep, Guid callId, Guid locationId) =>
        new OrderStore(device.Workspace(rep), clock).FindForCall(callId, locationId);

    public OrderResult AddOrderLineFromCall(string rep, Guid callId, Guid locationId, Guid productId, decimal quantity) =>
        new OrderStore(device.Workspace(rep), clock).AddFromCall(callId, locationId, productId, quantity);

    // Order entry opened from a Call (T-5.1.4): lines go on the order on screen, linking a new order to the Call; its Low tab reads
    // the Call's draft, or the saved Call once it is saved.
    public OrderResult AddOrderLineForCall(string rep, Guid orderId, Guid locationId, Guid productId, decimal quantity, Guid callId) =>
        new OrderStore(device.Workspace(rep), clock).AddLine(orderId, locationId, productId, quantity, callId);

    public TabletCall? ReadCall(string rep, Guid callId) => new CallStore(device.Workspace(rep), clock).Read(callId);

    // T-5.6.1: a saved Call with its standing, and its correction before Sync.
    public CallView? ReadCallView(string rep, Guid callId) => new CallStore(device.Workspace(rep), clock).ReadView(callId);

    public CallResult CorrectCall(string rep, Guid callId, Guid locationId, CallContent content) =>
        new CallStore(device.Workspace(rep), clock).Correct(callId, locationId, content);

    /// <summary>T-5.1.5 D5: a Call recorded from an order's Low tab was saved; the order is linked to it.</summary>
    public OrderResult LinkOrderToCall(string rep, Guid orderId, Guid callId) =>
        new OrderStore(device.Workspace(rep), clock).LinkToCall(orderId, callId);

    /// <summary>T-5.1.5: the shop's last Call gaps still open, for an order with no Call; <paramref name="index"/> is the products
    /// order entry already read, so the gaps come from the same downloaded day.</summary>
    public CarriedLow? ReadCarriedLow(string rep, Guid locationId, Guid orderId, ProductSearchIndex index) =>
        CarriedGaps.Read(device.Workspace(rep), locationId, orderId, index);

    /// <summary>T-5.1.6: each product's quantity on the shop's recent accepted orders, from the same downloaded day as
    /// <paramref name="index"/>, for the out-of-pattern marker on Review.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<OrderedQuantity>> ReadOrderedQuantities(string rep, Guid locationId,
        ProductSearchIndex index) => new SnapshotStore(device.Workspace(rep), clock).ReadOrderedQuantities(index.Generation, locationId);

    // Changing an unsent order before Sync (T-5.2.1); OrderTransitions decides what each standing allows.
    public OrderView? ReadOrderView(string rep, Guid orderId) => new OrderStore(device.Workspace(rep), clock).Read(orderId);

    public OrderResult ChangeOrderLine(string rep, Guid orderId, Guid lineId, decimal quantity) =>
        new OrderStore(device.Workspace(rep), clock).ChangeQuantity(orderId, lineId, quantity);

    public OrderResult RemoveOrderLine(string rep, Guid orderId, Guid lineId, bool deleteOrderIfLast) =>
        new OrderStore(device.Workspace(rep), clock).RemoveLine(orderId, lineId, deleteOrderIfLast);

    public OrderResult ReopenOrder(string rep, Guid orderId) => new OrderStore(device.Workspace(rep), clock).Reopen(orderId);

    public OrderResult DeleteOrder(string rep, Guid orderId, bool confirmed) =>
        new OrderStore(device.Workspace(rep), clock).Delete(orderId, confirmed);

    /// <summary>The rep's Unsent count: every saved item, and how many orders are in progress or ready to send.</summary>
    public UnsentSummary ReadUnsentSummary(string rep) => new UnsentStore(device.Workspace(rep)).Summarise();

    public IReadOnlyList<UnsentView> ReadUnsentViews(string rep) => new UnsentStore(device.Workspace(rep)).ReadViews();

    /// <summary>Marks a reviewed In Progress order ready, without starting Sync or revalidating captured products.</summary>
    public async Task<bool> MarkReviewedOrderReadyAsync(string rep, Guid id, long expectedVersion)
    {
        TabletEntry entry = await EnterAsync();
        if (SessionVersion != expectedVersion || entry.RepSubject != rep
            || entry.Screen is not (EntryScreen.Home or EntryScreen.Sync)) return false;
        return new OrderStore(device.Workspace(rep), clock).MarkReadyToSend(id).Succeeded;
    }

    // Recheck local access and the observed session immediately before changing captured work.
    public async Task<bool> ChangeRejectedWorkAsync(string rep, Guid id, long expectedVersion, bool delete, bool confirmed)
    {
        TabletEntry entry = await EnterAsync();
        if (SessionVersion != expectedVersion || entry.RepSubject != rep
            || entry.Screen is not (EntryScreen.Home or EntryScreen.Sync)) return false;
        UnsentStore store = new(device.Workspace(rep));
        return delete ? store.DeleteRejected(id, confirmed) : store.MarkReviewedReady(id);
    }

    public int CountWaitingToSend(string rep) => WorkUploader.CountWaiting(device.Workspace(rep));

    // Prices follow the business day in Ireland, not the tablet's own time zone (WI-059 D1).
    private DateOnly PriceToday() => BusinessCalendar.Today(clock.GetUtcNow());

    private TabletSync Sync(string rep) => new(device.Workspace(rep), endpoints.Api, device.Tokens(rep, endpoints.Identity), space, clock);
}
