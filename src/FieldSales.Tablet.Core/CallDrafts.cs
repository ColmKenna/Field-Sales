using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>A Call being recorded on T-06 and not yet saved. <see cref="CallId"/> is chosen when the page first opens, so
/// a restored draft still saves as the same Call.</summary>
public sealed record CallDraft(Guid CallId, CallChannel? Channel, string Notes, bool NotesOpen)
{
    public CallContent Content => new(Channel, Notes);

    /// <summary>Whether the rep has entered anything that leaving would lose.</summary>
    public bool HasWork => Channel is not null || !string.IsNullOrWhiteSpace(Notes);
}

/// <summary>Keeps the Call being recorded for the rep, session and shop it belongs to (WI-047 D7). Any resume rebuilds the
/// page (WI-040), and on a phone Call the rep is likely to switch to the dialler and back; the rebuilt page restores the
/// draft. It lives in memory only: an app restart or a new session starts without it. Saving or choosing Leave clears it.</summary>
/// <remarks>It also carries "Call saved" to the shop screen the rep returns to, once.</remarks>
public sealed class CallDrafts
{
    private (string Rep, long Version, Guid Location, CallDraft Draft)? kept;
    private (string Rep, long Version, Guid Location)? saved;

    public CallDraft? Read(string rep, long version, Guid location) =>
        kept is { } draft && draft.Rep == rep && draft.Version == version && draft.Location == location ? draft.Draft : null;

    public void Keep(string rep, long version, Guid location, CallDraft? draft) =>
        kept = draft is null ? null : (rep, version, location, draft);

    /// <summary>The Call at this shop was saved: the draft goes, and the shop screen says so once.</summary>
    public void Saved(string rep, long version, Guid location)
    {
        kept = null;
        saved = (rep, version, location);
    }

    /// <summary>True once after a save, for the same rep, session and shop.</summary>
    public bool TakeSaved(string rep, long version, Guid location)
    {
        if (saved is not { } done || done.Rep != rep || done.Version != version || done.Location != location) return false;
        saved = null;
        return true;
    }
}
