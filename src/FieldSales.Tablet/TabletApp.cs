using FieldSales.Ordering.Contracts;
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
    }

    public const string SignInNeedsConnection = "Sign-in needs a connection. Try again when you have signal.";
    public const string SignInDidNotFinish = "Sign-in didn't finish. Try again.";

    /// <summary>Raised when the app comes back to the foreground, so the 24-hour rule is checked again.</summary>
    public event Action? Resumed;
    public event Action? SessionChanged;
    public long SessionVersion => device.SessionVersion;
    public void NotifyResumed() { monitor.Start(); Resumed?.Invoke(); }
    public void NotifyStopped() => monitor.Stop();

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

    public OfflineProductPage ReadProducts(string rep, int page) => new SnapshotStore(device.Workspace(rep), clock).ReadProducts(page);
    public OfflineProductDetail? ReadProduct(string rep, Guid id) => new SnapshotStore(device.Workspace(rep), clock).ReadProduct(id);

    public LocationSearchIndex ReadLocationIndex(string rep) => new SnapshotStore(device.Workspace(rep), clock).ReadLocationIndex();

    public OfflineLocationDetail? ReadLocation(string rep, Guid id) => new SnapshotStore(device.Workspace(rep), clock).ReadLocation(id);

    // Order entry: products are found offline in the active snapshot, and each added line is saved at once.
    public ProductSearchIndex ReadProductIndex(string rep) => new SnapshotStore(device.Workspace(rep), clock).ReadProductIndex(Today());

    public OrderResult AddOrderLine(string rep, Guid orderId, Guid locationId, Guid productId, decimal quantity) =>
        new OrderStore(device.Workspace(rep), clock).AddLine(orderId, locationId, productId, quantity);

    public TabletOrder? ReadOrder(string rep, Guid orderId) => new OrderStore(device.Workspace(rep), clock).Find(orderId);

    public OrderResult MarkOrderReady(string rep, Guid orderId) => new OrderStore(device.Workspace(rep), clock).MarkReadyToSend(orderId);

    /// <summary>The rep's Unsent count: every saved item, and how many orders are in progress or ready to send.</summary>
    public UnsentSummary ReadUnsentSummary(string rep) => new UnsentStore(device.Workspace(rep)).Summarise();

    public int CountWaitingToSend(string rep) => WorkUploader.CountWaiting(device.Workspace(rep));

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    private TabletSync Sync(string rep) => new(device.Workspace(rep), endpoints.Api, device.Tokens(rep, endpoints.Identity), space, clock);
}
