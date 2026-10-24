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
public sealed record TabletEntry(EntryScreen Screen, string? RepSubject, string Message);

public enum SignInStatus { Started, NotPermitted }

public sealed record SignInOutcome(SignInStatus Status, string Message, RepWorkspace? Workspace);

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
    private const string CurrentRepKey = "fieldsales.current-rep";
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    internal static string SessionKey(string subject) => $"fieldsales.session.{subject}";

    public async Task<TabletEntry> EnterAsync(bool connected)
    {
        string? rep = await secure.GetAsync(CurrentRepKey);
        if (rep is null)
            return connected ? new(EntryScreen.SignIn, null, EntryMessages.SignInToStart)
                : new(EntryScreen.ConnectionNeeded, null, EntryMessages.FirstUseNeedsConnection);
        if (LocalAccessRules.Evaluate(await LastConnectedSignInAsync(rep), clock.GetUtcNow()) != LocalAccess.Allowed)
            return connected ? new(EntryScreen.SignIn, null, EntryMessages.SignInToStart)
                : new(EntryScreen.ConnectToSignInAgain, null, EntryMessages.ConnectToSignInAgain);
        bool downloaded = new SnapshotStore(Workspace(rep), clock).ReadActive() is not null;
        return downloaded ? new(EntryScreen.Home, rep, "")
            : new(EntryScreen.Sync, rep, connected ? EntryMessages.SyncFirst : SyncMessages.NoConnection);
    }

    /// <summary>Starts the tablet day after a connected sign-in. Only a Field Salesperson may; otherwise nothing is kept.</summary>
    public async Task<SignInOutcome> CompleteSignInAsync(StaffSignIn signIn)
    {
        if (!signIn.Roles.Contains(TabletClientContract.RequiredRole, StringComparer.Ordinal))
            return new(SignInStatus.NotPermitted, SyncMessages.NotPermitted, null);
        await TokenStore.SaveAsync(secure, signIn.Subject, new(signIn.AccessToken, signIn.AccessTokenExpiresUtc, signIn.RefreshToken));
        await secure.SetAsync(SessionKey(signIn.Subject), clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        await secure.SetAsync(CurrentRepKey, signIn.Subject);
        return new(SignInStatus.Started, "", Workspace(signIn.Subject));
    }

    public async Task<DateTimeOffset?> LastConnectedSignInAsync(string subject) =>
        await secure.GetAsync(SessionKey(subject)) is { } value
            ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;

    public RepWorkspace Workspace(string subject) => new(root, subject);

    /// <summary>The signed-in rep's tokens for Sync; refreshing them never changes the local window.</summary>
    public IAccessTokenSource Tokens(string subject, HttpClient identity) => new StoredTokenSource(secure, subject, identity, clock);
}

internal sealed record StoredTokens(string AccessToken, DateTimeOffset AccessTokenExpiresUtc, string RefreshToken);

internal static class TokenStore
{
    private static string Key(string subject) => $"fieldsales.tokens.{subject}";

    public static Task SaveAsync(ISecureStore secure, string subject, StoredTokens tokens) =>
        secure.SetAsync(Key(subject), JsonSerializer.Serialize(tokens));

    public static async Task<StoredTokens?> ReadAsync(ISecureStore secure, string subject) =>
        await secure.GetAsync(Key(subject)) is { } json ? JsonSerializer.Deserialize<StoredTokens>(json) : null;

    public static void Clear(ISecureStore secure, string subject) => secure.Remove(Key(subject));
}
