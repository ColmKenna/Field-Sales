using System.Globalization;
using System.Text.Json;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>Platform secure storage; the tablet app backs it with Android SecureStorage (Keystore).</summary>
public interface ISecureStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    bool Remove(string key);
}

/// <summary>A completed connected sign-in, as the app's system-browser flow returns it.</summary>
public sealed record StaffSignIn(string Subject, IReadOnlyList<string> Roles, string AccessToken,
    DateTimeOffset AccessTokenExpiresUtc, string RefreshToken);

public enum LocalAccess { NoSignIn, Allowed, Expired, ClockMovedBack }

/// <summary>I3.1: the tablet day can be reopened offline for 24 hours after the last connected sign-in.</summary>
/// <remarks>Reopening the app or refreshing tokens never extends the window. A clock reading earlier than that
/// sign-in fails closed (P10), so moving the clock back cannot keep a day open.</remarks>
public static class LocalAccessRules
{
    public static TimeSpan Window => TabletClientContract.SignInLifetime;

    public static LocalAccess Evaluate(DateTimeOffset? lastConnectedSignInUtc, DateTimeOffset nowUtc) =>
        lastConnectedSignInUtc is not { } signedIn ? LocalAccess.NoSignIn
        : nowUtc < signedIn ? LocalAccess.ClockMovedBack
        : nowUtc - signedIn >= Window ? LocalAccess.Expired
        : LocalAccess.Allowed;
}

public enum EntryScreen { SignIn, ConnectionNeeded, ConnectToSignInAgain, Sync, Home }

/// <summary>Where the app opens. Only <see cref="EntryScreen.Sync"/> and <see cref="EntryScreen.Home"/> name a rep;
/// the locked screens never show another rep's name, Locations or work.</summary>
public sealed record TabletEntry(EntryScreen Screen, string? RepSubject, string Message)
{
    public long SessionVersion { get; init; }
}

public enum SignInStatus { Started, NotPermitted, Superseded }

public sealed record SignInOutcome(SignInStatus Status, string Message, RepWorkspace? Workspace);

/// <summary>What a connected sign-in from a screen came to: the rep it started a session for, or what to tell the rep. Both are
/// null when the rep closed the browser.</summary>
public sealed record SignInAnswer(string? SignedInRep, string? Problem);

/// <summary>I-03 text.</summary>
public static class EntryMessages
{
    public const string SignInToStart = "Sign in with a connection to start your day. Work saved on this tablet stays here.";
    public const string FirstUseNeedsConnection = "A connection is needed for your first sign-in and download. No downloaded day is available yet.";
    public const string ConnectToSignInAgain = "Connect to sign in again before opening your day. Work already saved on this tablet is still here.";
    public const string SyncFirst = "Sync to download your day.";
}

/// <summary>The tablet's own state: who is signed in, when they last signed in with a connection, and their tokens.</summary>
/// <remarks>Each rep's snapshot and Unsent work stay in their own <see cref="RepWorkspace"/>; signing another rep in
/// never opens them, and nothing here deletes saved work.</remarks>
public sealed class TabletDevice(string root, ISecureStore secure, TimeProvider? clock = null)
{
    internal const string CurrentRepKey = "fieldsales.current-rep";
    private const string PreviouslySignedInKey = "fieldsales.previously-signed-in";
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly TabletSessionState session = TabletSessionState.For(secure);

    public long SessionVersion => session.Version;
    public event Action? SessionChanged { add => session.Changed += value; remove => session.Changed -= value; }

    internal static string SessionKey(string subject) => $"fieldsales.session.{subject}";

    public Task<TabletEntry> EnterAsync(bool connected) => session.RunAsync(async () =>
    {
        var active = await ReadSessionAsync();
        TabletEntry entry;
        if (active is null)
        {
            bool returning = await secure.GetAsync(PreviouslySignedInKey) is not null;
            entry = connected ? new(EntryScreen.SignIn, null, EntryMessages.SignInToStart)
                : returning ? new(EntryScreen.ConnectToSignInAgain, null, EntryMessages.ConnectToSignInAgain)
                : new(EntryScreen.ConnectionNeeded, null, EntryMessages.FirstUseNeedsConnection);
        }
        else
        {
            string rep = active.Value.Rep;
            bool downloaded = new SnapshotStore(Workspace(rep), clock).ReadActive() is not null;
            entry = downloaded ? new(EntryScreen.Home, rep, "")
                : new(EntryScreen.Sync, rep, connected ? EntryMessages.SyncFirst : SyncMessages.NoConnection);
        }
        return entry with { SessionVersion = SessionVersion };
    });

    /// <summary>Check local access without opening the snapshot. Expiry/rollback clears credentials, never work.</summary>
    public Task<DateTimeOffset?> CheckSessionAsync() => session.RunAsync(async () =>
        (await ReadSessionAsync())?.SignedIn);

    private async Task<(string Rep, DateTimeOffset SignedIn)?> ReadSessionAsync()
    {
        if (session.Blocked) return null;
        string? rep = await secure.GetAsync(CurrentRepKey);
        if (rep is null) return null;
        DateTimeOffset? signedIn = await LastConnectedSignInAsync(rep);
        if (LocalAccessRules.Evaluate(signedIn, clock.GetUtcNow()) == LocalAccess.Allowed)
            return (rep, signedIn!.Value);
        session.Invalidate();
        await ClearCredentialsAsync(rep);
        return null;
    }

    /// <summary>Ends only the session the caller observed; a late confirmation cannot sign a new rep out.</summary>
    public Task<bool> EndSessionAsync(long? expectedVersion = null) => session.RunAsync(async () =>
    {
        if (expectedVersion is { } expected && expected != SessionVersion) return false;
        session.Invalidate(); // Fail closed even if platform credential removal throws.
        string? rep = await secure.GetAsync(CurrentRepKey);
        await ClearCredentialsAsync(rep);
        return true;
    });

    private async Task ClearCredentialsAsync(string? rep)
    {
        if (rep is not null) await secure.SetAsync(PreviouslySignedInKey, "true");
        secure.Remove(CurrentRepKey);
        if (rep is not null)
        {
            TokenStore.Clear(secure, rep);
            secure.Remove(SessionKey(rep));
        }
        if (await secure.GetAsync(CurrentRepKey) is not null || rep is not null &&
            (await secure.GetAsync(SessionKey(rep)) is not null || await TokenStore.ReadAsync(secure, rep) is not null))
            throw new IOException("Local session credentials could not be removed.");
    }

    /// <summary>Starts the tablet day after a connected sign-in. Only a Field Salesperson may; otherwise nothing is kept.</summary>
    public Task<SignInOutcome> CompleteSignInAsync(StaffSignIn signIn, long? expectedVersion = null)
    {
        if (!signIn.Roles.Contains(TabletClientContract.RequiredRole, StringComparer.Ordinal))
            return Task.FromResult(new SignInOutcome(SignInStatus.NotPermitted, SyncMessages.NotPermitted, null));
        return session.RunAsync(async () =>
        {
            if (expectedVersion is { } expected && expected != SessionVersion)
                return new SignInOutcome(SignInStatus.Superseded, "Sign-in didn't finish. Try again.", null);
            session.Invalidate();
            await ClearCredentialsAsync(await secure.GetAsync(CurrentRepKey));
            await TokenStore.SaveAsync(secure, signIn.Subject, new(signIn.AccessToken, signIn.AccessTokenExpiresUtc, signIn.RefreshToken));
            await secure.SetAsync(SessionKey(signIn.Subject), clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
            await secure.SetAsync(PreviouslySignedInKey, "true");
            await secure.SetAsync(CurrentRepKey, signIn.Subject);
            session.Blocked = false;
            return new SignInOutcome(SignInStatus.Started, "", Workspace(signIn.Subject));
        });
    }

    public async Task<DateTimeOffset?> LastConnectedSignInAsync(string subject) =>
        await secure.GetAsync(SessionKey(subject)) is { } value
            ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;

    public RepWorkspace Workspace(string subject) => new(root, subject);

    /// <summary>The signed-in rep's tokens for Sync; refreshing them never changes the local window.</summary>
    public IAccessTokenSource Tokens(string subject, HttpClient identity) => new StoredTokenSource(secure, subject, identity, clock, SessionVersion);
}

internal sealed record StoredTokens(string AccessToken, DateTimeOffset AccessTokenExpiresUtc, string RefreshToken);

internal static class TokenStore
{
    private static string Key(string subject) => $"fieldsales.tokens.{subject}";

    public static Task SaveAsync(ISecureStore secure, string subject, StoredTokens tokens) =>
        secure.SetAsync(Key(subject), JsonSerializer.Serialize(tokens, TabletJsonContext.Default.StoredTokens));

    public static async Task<StoredTokens?> ReadAsync(ISecureStore secure, string subject) =>
        await secure.GetAsync(Key(subject)) is { } json ? JsonSerializer.Deserialize(json, TabletJsonContext.Default.StoredTokens) : null;

    public static void Clear(ISecureStore secure, string subject) => secure.Remove(Key(subject));
}
