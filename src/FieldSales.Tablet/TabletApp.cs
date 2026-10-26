using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet;

/// <summary>What Home shows: when the rep last synced and their downloaded Locations.</summary>
public sealed record HomeView(string LastSynced, IReadOnlyList<HomeLocation> Locations);

/// <summary>The screens' single entry point to the tablet's rules; the rules themselves live in Tablet.Core.</summary>
public sealed class TabletApp(TabletDevice device, TabletEndpoints endpoints, IStorageSpace space, TimeProvider clock)
{
    public const string SignInNeedsConnection = "Sign-in needs a connection. Try again when you have signal.";
    public const string SignInDidNotFinish = "Sign-in didn't finish. Try again.";

    /// <summary>Raised when the app comes back to the foreground, so the 24-hour rule is checked again.</summary>
    public event Action? Resumed;
    public void NotifyResumed() => Resumed?.Invoke();

    public bool Connected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public Task<TabletEntry> EnterAsync() => device.EnterAsync(Connected);

    /// <summary>The connected sign-in in the system browser. Returns a message to show, or null once the day starts.</summary>
    public async Task<string?> SignInAsync()
    {
        TabletTiming.RepActed();
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
            if (signIn is null) return SignInDidNotFinish;
            SignInOutcome outcome = await device.CompleteSignInAsync(signIn);
            return outcome.Status == SignInStatus.Started ? null : outcome.Message;
        }
        catch (TaskCanceledException) { return null; } // The rep closed the browser; stay on the sign-in screen.
        catch (HttpRequestException) { return SignInNeedsConnection; }
    }

    /// <summary>Downloads the rep's day; copies retired by the switch are deleted afterwards in the background (D2-B).</summary>
    public async Task<SyncOutcome> SyncAsync(string rep)
    {
        TabletTiming.RepActed();
        TabletSync sync = Sync(rep);
        long started = clock.GetTimestamp();
        SyncOutcome outcome = await sync.SyncAsync();
        TabletTiming.Synced(outcome, clock.GetElapsedTime(started));
        _ = Task.Run(async () =>
        {
            long cleanup = clock.GetTimestamp();
            await sync.CleanupAsync();
            TabletTiming.CleanedUp(clock.GetElapsedTime(cleanup));
        });
        return outcome;
    }

    /// <summary>Finishes any cleanup an earlier session left, after the day has opened.</summary>
    public void ResumeCleanup(string rep) => _ = Task.Run(() => Sync(rep).CleanupAsync());

    public HomeView ReadHome(string rep)
    {
        SnapshotStore store = new(device.Workspace(rep), clock);
        ActiveSnapshot? active = store.ReadActive();
        return new(active is null ? "No downloaded day yet" : SyncMessages.LastSynced(active.CompletedUtc, clock), store.ReadLocations());
    }

    private TabletSync Sync(string rep) => new(device.Workspace(rep), endpoints.Api, device.Tokens(rep, endpoints.Identity), space, clock);
}
