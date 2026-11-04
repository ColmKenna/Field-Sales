using System.Globalization;
using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>A Call being recorded on T-06 and not yet saved. <see cref="CallId"/> is chosen when the page first opens, so
/// a restored draft still saves as the same Call.</summary>
/// <param name="Stock">The Stock Check rows (T-5.4.1), starting from the Suggested List; null until the page has built them.</param>
/// <param name="StockChanged">Whether the rep added or removed a row, so leaving would lose something.</param>
public sealed record CallDraft(Guid CallId, CallChannel? Channel, string Notes, bool NotesOpen,
    IReadOnlyList<StockLine>? Stock = null, bool StockChanged = false)
{
    public IReadOnlyList<StockLine> Lines => Stock ?? [];

    /// <summary>The stock check is part of an In-person Call; on Phone it is hidden and not saved, but its rows are kept in
    /// case the rep switches back (WI-049 D5; Phone's "Stock mentioned" is T-5.3.2).</summary>
    public bool StockShown => Channel != CallChannel.Phone;

    /// <summary>What would be saved: uncounted rows go as Not checked; removed rows aren't on the list at all.</summary>
    public CallContent Content => new(Channel, Notes,
        Channel == CallChannel.InPerson && Lines.Count > 0 ? [.. Lines.Select(line => line.Entry)] : null);

    /// <summary>Why this can't be saved yet: a count that isn't valid comes before the Call-level checks.</summary>
    public string? Problem => Channel is null ? CallContract.ChooseChannel
        : (StockShown ? Lines.Select(line => line.Problem).FirstOrDefault(problem => problem is not null) : null) ?? Content.Problem;

    /// <summary>Whether the rep has entered anything that leaving would lose.</summary>
    public bool HasWork => Channel is not null || !string.IsNullOrWhiteSpace(Notes) || StockChanged || Lines.Any(line => line.Counted);
}

/// <summary>One row of a Stock Check being recorded: the product, why it was suggested (none when the rep added it), and the
/// count as typed. Blank means Not checked.</summary>
public sealed record StockLine(ProductSearchResult Product, SuggestionSources Sources, string Count = "")
{
    private const NumberStyles CountStyle = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public bool Counted => !string.IsNullOrWhiteSpace(Count);

    public decimal? Amount => Counted && decimal.TryParse(Count, CountStyle, CultureInfo.InvariantCulture, out decimal amount) ? amount : null;

    /// <summary>"Enter 0 or more" and the other count checks (S5), or null for a valid count or Not checked.</summary>
    public string? Problem => !Counted ? null : Amount is { } amount ? CallContract.CountProblem(Product.Unit, amount) : CallContract.CountBelowZero;

    public string Label => SuggestedList.Label(Sources);

    public CallStockEntry Entry => new(Product.Id, Product.Code, Product.Name, Product.Unit, Counted ? Amount : null);

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
