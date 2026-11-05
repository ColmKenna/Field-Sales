using System.Globalization;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>A Call being recorded on T-06 and not yet saved. <see cref="CallId"/> is chosen when the page first opens, so
/// a restored draft still saves as the same Call.</summary>
/// <param name="Stock">The Stock Check rows (T-5.4.1), starting from the Suggested List; null until the page has built them.</param>
/// <param name="StockChanged">Whether the rep added or removed a row, so leaving would lose something.</param>
/// <param name="AddedToOrder">Whether a product went on the order from this Call (T-5.5.1). Those lines are already saved on the
/// order and stay there if the Call is left unsaved (D8).</param>
/// <param name="TakenOff">Products whose line the rep took off this Call's order in order entry (T-5.1.4 D5): the one fact the order
/// can't tell, so a Low item reads REMOVED rather than not added. It never decides ADDED; the order does.</param>
/// <param name="CompetitorNotes">The Competitor Notes as written (T-5.8.1), each with its link type and picks; null when none.</param>
/// <param name="CompetitorOpen">Whether the Competitor notes section is open.</param>
public sealed record CallDraft(Guid CallId, CallChannel? Channel, string Notes, bool NotesOpen,
    IReadOnlyList<StockLine>? Stock = null, bool StockChanged = false, bool AddedToOrder = false,
    IReadOnlyList<Guid>? TakenOff = null, IReadOnlyList<CallCompetitorNote>? CompetitorNotes = null, bool CompetitorOpen = false)
{
    public IReadOnlyList<CallCompetitorNote> Competitor => CompetitorNotes ?? [];

    public IReadOnlyList<StockLine> Lines => Stock ?? [];

    /// <summary>The Low tab for this Call against <paramref name="order"/> (T-5.1.4).</summary>
    public LowList Low(TabletOrder? order) => LowItems.Of(Lines.Select(line => line.Entry), order, TakenOff ?? []);

    /// <summary>Records that these products' lines were taken off the order.</summary>
    public CallDraft TakeOff(IEnumerable<Guid> products) => this with { TakenOff = [.. (TakenOff ?? []).Union(products)] };

    /// <summary>On Phone the same rows read as "Stock mentioned" (T-5.3.2 S8): Low, Out and Add count instead of a stepper.
    /// Switching channel keeps every row, count and mark (S12).</summary>
    public bool Mentioning => Channel == CallChannel.Phone;

    /// <summary>The rows a phone Call's "Filter or search" box leaves showing (T-5.3.2 S9): every word typed in the product's
    /// name or code, as the order search matches; all rows when nothing is typed.</summary>
    public IReadOnlyList<StockLine> Filtered(string? filter)
    {
        string[] terms = ProductSearchIndex.Terms(filter);
        return terms.Length == 0 ? Lines : [.. Lines.Where(line => ProductSearchIndex.Matches(line.Product, terms))];
    }

    /// <summary>The rows the Call would save. In person, every row: uncounted rows go as Not checked, removed rows aren't on
    /// the list at all. On Phone, only what was mentioned (S11): untouched rows are not saved.</summary>
    public IReadOnlyList<StockLine> Saved => Mentioning ? [.. Lines.Where(line => line.Mentioned)] : Lines;

    /// <summary>What would be saved.</summary>
    public CallContent Content => new(Channel, Notes,
        Channel is not null && Saved.Count > 0 ? [.. Saved.Select(line => line.Entry)] : null,
        Competitor.Count > 0 ? Competitor : null);

    /// <summary>The stock section's heading line: on Phone "2 marked Low (1 out)", blank when nothing is mentioned; In person
    /// "10 counted, 3 not checked", with "· 2 Low" when rows are marked.</summary>
    public string StockSummary
    {
        get
        {
            if (Mentioning)
                return CallContent.MentionedSummary(Lines.Count(line => line.Low), Lines.Count(line => line.Out),
                    Lines.Count(line => line.Counted && !line.Low));
            if (Lines.Count == 0) return "";
            string counts = CallContent.StockSummary(Lines.Count(line => line.Counted), Lines.Count(line => !line.Counted));
            int low = Lines.Count(line => line.Low);
            return low == 0 ? counts : $"{counts} · {low} Low";
        }
    }

    /// <summary>Why this can't be saved yet: a count that isn't valid on a row being saved comes before the Call-level checks.</summary>
    public string? Problem => Channel is null ? CallContract.ChooseChannel
        : Saved.Select(line => line.Problem).FirstOrDefault(problem => problem is not null) ?? Content.Problem;

    /// <summary>Whether the rep has entered anything that leaving would lose.</summary>
    public bool HasWork => Channel is not null || !string.IsNullOrWhiteSpace(Notes) || StockChanged || Lines.Any(line => line.Mentioned)
        || Competitor.Count > 0;
}

/// <summary>One row of a Stock Check being recorded: the product, why it was suggested (none when the rep added it), the
/// count as typed (blank means Not checked) and the Low mark.</summary>
/// <param name="CountOpen">On Phone, the rep tapped "Add count", so the row shows its count box (T-5.3.2 D6).</param>
public sealed record StockLine(ProductSearchResult Product, SuggestionSources Sources, string Count = "", bool Low = false,
    bool CountOpen = false)
{
    private const NumberStyles CountStyle = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public bool Counted => !string.IsNullOrWhiteSpace(Count);

    /// <summary>Out is Low with a count of 0 (T-5.3.2 S10).</summary>
    public bool Out => Low && Counted && Amount == 0m;

    /// <summary>The rep said something about this product: a count or a Low mark. A phone Call saves only these rows.</summary>
    public bool Mentioned => Counted || Low;

    /// <summary>Whether a phone row shows its count box: after "Add count", or when it already has a count.</summary>
    public bool CountShown => CountOpen || Counted;

    public decimal? Amount => Counted && decimal.TryParse(Count, CountStyle, CultureInfo.InvariantCulture, out decimal amount) ? amount : null;

    /// <summary>"Enter 0 or more" and the other count checks (S5), or null for a valid count or Not checked.</summary>
    public string? Problem => !Counted ? null : Amount is { } amount ? CallContract.CountProblem(Product.Unit, amount) : CallContract.CountBelowZero;

    public string Label => SuggestedList.Label(Sources);

    public CallStockEntry Entry => new(Product.Id, Product.Code, Product.Name, Product.Unit, Counted ? Amount : null, Low);

    /// <summary>Low on or off. Turning Low off also clears Out: its count of 0 goes with it (D6).</summary>
    public StockLine ToggleLow() => !Low ? this with { Low = true }
        : Out ? this with { Low = false, Count = "", CountOpen = false } : this with { Low = false };

    /// <summary>The In-person Low tick (T-5.5.1 D3): on or off, keeping the count, because 0 counted on the shelf is a real count
    /// there.</summary>
    public StockLine Tick() => this with { Low = !Low };

    /// <summary>Out marks Low and records a count of 0, replacing any count (S10). Out off removes the 0 and keeps Low (D6).</summary>
    public StockLine ToggleOut() => Out ? this with { Count = "", CountOpen = false } : this with { Low = true, Count = "0" };

    /// <summary>"Add count" on a phone row: its count box opens, with no stepper. Clearing the box leaves the row untouched
    /// unless it is Low.</summary>
    public StockLine OpenCount() => this with { CountOpen = true };

    /// <summary>The count one step up or down (1 in the product's unit), never below 0. From Not checked, + gives 1 and − gives 0.</summary>
    public StockLine Step(int direction) => this with
    {
        Count = Math.Max(0m, (Amount ?? (direction > 0 ? 0m : 1m)) + direction).ToString("0.######", CultureInfo.InvariantCulture)
    };
}

/// <summary>Keeps the Call being recorded for the rep, session and shop it belongs to (WI-047 D7). Any resume rebuilds the
/// page (WI-040), and on a phone Call the rep is likely to switch to the dialler and back; the rebuilt page restores the
/// draft. It lives in memory only: an app restart or a new session starts without it. Saving or choosing Leave clears it.</summary>
/// <remarks>It also carries "Call saved" to the shop screen the rep returns to, once.</remarks>
public sealed class CallDrafts
{
    private (string Rep, long Version, Guid Location, CallDraft Draft)? kept;
    private (string Rep, long Version, Guid Location)? saved;
    // T-5.6.1 (WI-055 D6): a correction being made to a saved Call, kept apart from a Call being recorded at the same shop.
    private (string Rep, long Version, CallDraft Draft)? editing;
    private (string Rep, long Version, Guid Call)? corrected;
    // T-5.7.1 (WI-056 D2): a follow-up being recorded, kept apart from a Call being recorded or corrected at the same shop.
    private (string Rep, long Version, Guid Location, Guid Original, CallDraft Draft)? followingUp;

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

    /// <summary>The correction being made to saved Call <paramref name="call"/> (T-5.6.1), restored when Edit Call is rebuilt.</summary>
    public CallDraft? ReadEdit(string rep, long version, Guid call) =>
        editing is { } edit && edit.Rep == rep && edit.Version == version && edit.Draft.CallId == call ? edit.Draft : null;

    public void KeepEdit(string rep, long version, CallDraft? draft) => editing = draft is null ? null : (rep, version, draft);

    /// <summary>The correction was saved: it goes, and the call page says so once.</summary>
    public void Corrected(string rep, long version, Guid call)
    {
        editing = null;
        corrected = (rep, version, call);
    }

    /// <summary>The follow-up being recorded to Call <paramref name="original"/> (T-5.7.1), restored when the page is rebuilt.</summary>
    public CallDraft? ReadFollowUp(string rep, long version, Guid original) =>
        followingUp is { } next && next.Rep == rep && next.Version == version && next.Original == original ? next.Draft : null;

    public void KeepFollowUp(string rep, long version, Guid location, Guid original, CallDraft? draft) =>
        followingUp = draft is null ? null : (rep, version, location, original, draft);

    /// <summary>The follow-up at this shop was saved: it goes, and the shop screen says so once. A Call being recorded there stays.</summary>
    public void FollowedUp(string rep, long version, Guid location)
    {
        followingUp = null;
        saved = (rep, version, location);
    }

    /// <summary>The draft of Call <paramref name="call"/> being recorded at this shop, new or a follow-up, for order entry opened from it.</summary>
    public CallDraft? ReadCall(string rep, long version, Guid location, Guid call) =>
        Read(rep, version, location) is { } draft && draft.CallId == call ? draft
        : followingUp is { } next && next.Rep == rep && next.Version == version && next.Location == location && next.Draft.CallId == call
            ? next.Draft : null;

    /// <summary>Keeps <paramref name="draft"/> where it came from: the follow-up's slot when it is that follow-up, else the shop's.</summary>
    public void KeepCall(string rep, long version, Guid location, CallDraft draft)
    {
        if (followingUp is { } next && next.Rep == rep && next.Version == version && next.Location == location
            && next.Draft.CallId == draft.CallId)
            followingUp = next with { Draft = draft };
        else Keep(rep, version, location, draft);
    }

    /// <summary>The Call that <paramref name="call"/>, a follow-up being recorded, follows up; null for any other Call.</summary>
    public Guid? OriginalOf(string rep, long version, Guid call) =>
        followingUp is { } next && next.Rep == rep && next.Version == version && next.Draft.CallId == call ? next.Original : null;

    /// <summary>True once after a correction, for the same rep, session and Call.</summary>
    public bool TakeCorrected(string rep, long version, Guid call)
    {
        if (corrected is not { } done || done.Rep != rep || done.Version != version || done.Call != call) return false;
        corrected = null;
        return true;
    }
}
