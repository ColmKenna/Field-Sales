namespace FieldSales.Tablet.Core;

/// <summary>What the Sync screen last told the rep: the report, whether Sign in takes Sync now's place, and what the sign-in
/// itself answered, if anything.</summary>
public sealed record SyncNotice(SyncReport Report, bool SignInNeeded, string? SignInProblem = null);

/// <summary>Keeps the Sync screen's notice for the rep and session it was said in (WI-040). Returning from the sign-in browser
/// resumes the app, which rebuilds the page; the rebuilt page shows the same, and hears a sign-in answer that arrives later.
/// A new session, a new Sync or leaving for Home starts without one.</summary>
/// <remarks>WI-041: it also carries a Sync the rep started into the session their sign-in from the Sync screen began, so that
/// session's Sync screen resumes it, once. Nothing else ever starts a Sync (T1.3).</remarks>
public sealed class SyncNotices
{
    private (string Rep, long Version, SyncNotice Notice)? kept;
    private (string Rep, long Version)? resume;

    public event Action? Changed;

    public SyncNotice? Read(string rep, long version) =>
        kept is { } notice && notice.Rep == rep && notice.Version == version ? notice.Notice : null;

    public void Keep(string rep, long version, SyncNotice? notice)
    {
        kept = notice is null ? null : (rep, version, notice);
        Changed?.Invoke();
    }

    /// <summary>The rep signed in again from the Sync screen and <paramref name="version"/> is the session that started.</summary>
    public void RequestResume(string rep, long version)
    {
        resume = (rep, version);
        Changed?.Invoke();
    }

    /// <summary>True once, for the same rep in the session the sign-in started; never for another rep or a later session.</summary>
    public bool TakeResume(string rep, long version)
    {
        if (resume is not { } asked || asked.Rep != rep || asked.Version != version) return false;
        resume = null;
        return true;
    }
}
