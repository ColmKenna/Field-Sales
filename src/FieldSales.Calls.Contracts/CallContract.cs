using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSales.Quantities;

namespace FieldSales.Calls.Contracts;

/// <summary>The Call record the tablet captures and the server accepts unchanged (T-5.3.1, uploaded by T-4.1.2).</summary>
/// <remarks>
/// <para>One type serves both sides, so an uploaded Call needs no mapping. The tablet stores the serialized body as an
/// Unsent Call; <c>locationId</c> stays a top-level property because the tablet reads it from any Unsent item to keep
/// that shop's details. A saved Call is a record of what happened: the tablet never rewrites it (corrections arrive
/// with T-5.6.1 as their own operation).</para>
/// <para><b>Extension points.</b> Later items attach to a Call as optional sections within version 1. An absent section
/// means nothing of that kind was recorded, so older bodies stay valid and nothing is reshaped. Reserved names:</para>
/// <list type="table">
/// <item><term><c>stockEntries</c></term><description>T-5.4.1 Stock Check from the Suggested List (and T-5.3.2 Stock mentioned
/// on a phone Call): one entry per product with its count or Not checked, captured name and code.</description></item>
/// <item><term><c>stockEntries[].low</c></term><description>Low marks, on the entry they belong to (T-5.3.2 on Phone, T-5.5.1 In
/// person). Out is Low with a count of 0.</description></item>
/// <item><term><c>competitorNotes</c></term><description>T-5.8.1 Competitor Notes, each with one link type.</description></item>
/// <item><term><c>followUpOf</c></term><description>T-5.7.1 the earlier Call at the same Location that this one follows up.</description></item>
/// <item><term><c>campaignOutcomes</c></term><description>E20 one outcome per open campaign.</description></item>
/// <item><term><c>rangeReview</c></term><description>E23 the Range Review purpose and its data.</description></item>
/// </list>
/// <para>Call Purposes are not stored: they follow from the sections present (Pitch when there are pitch notes, Stock
/// Check when there are stock entries, Range Review when that section exists). Visit Due completion (E13) needs no field:
/// Visit Planning derives it from the Location, the rep and the date. Each new section also extends
/// <see cref="CallContent"/>, which decides whether something was recorded and how the save review reads.</para>
/// </remarks>
public static class CallContract
{
    public const string Schema = "field-sales-call";
    public const int CurrentVersion = 1;
    public const int MaximumPitchNotesLength = 4000;

    public const string ChooseChannel = "Choose In person or Phone";
    public const string RecordSomething = "Add pitch notes, a stock check or a competitor note";
    public static readonly string PitchNotesTooLong = $"Pitch notes can be up to {MaximumPitchNotesLength:N0} characters";

    public const string CountBelowZero = "Enter 0 or more";
    public const string CountNotWhole = "Enter a whole number";
    public const string CountTooPrecise = "Enter a count with up to six decimal places";

    /// <summary>A phone Call keeps only the products that were mentioned (T-5.3.2 S11). The tablet never sends an untouched
    /// row, so only a malformed upload meets this.</summary>
    public const string MentionedOnly = "A product on a phone call needs Low, Out or a count";

    /// <summary>Why a stock count can't be saved for a product counted in <paramref name="unit"/>, or null when it can
    /// (T-5.4.1 S5). No count means Not checked, which is always allowed. Zero is a count.</summary>
    public static string? CountProblem(string? unit, decimal? count)
    {
        if (count is not { } amount) return null;
        if (amount < 0) return CountBelowZero;
        bool known = UnitOfMeasureExtensions.TryParse(unit, out UnitOfMeasure measure);
        if (known && measure == UnitOfMeasure.Each && amount % 1m != 0) return CountNotWhole;
        return known && Quantity.TryCreate(measure, amount, out _) ? null : CountTooPrecise;
    }

    /// <summary>The reserved optional sections, in the order the summary will read them.</summary>
    public static readonly IReadOnlyList<string> ReservedSections =
        ["stockEntries", "competitorNotes", "followUpOf", "campaignOutcomes", "rangeReview"];

    /// <summary>The channel as the rep reads it.</summary>
    public static string Label(CallChannel channel) => channel == CallChannel.InPerson ? "In person" : "Phone";

    /// <summary>Web defaults with source-generated metadata, so trimmed tablet Release builds keep (de)serialization.</summary>
    public static JsonSerializerOptions JsonOptions => CallJsonContext.Default.Options;
}

/// <summary>How the contact happened. Chosen by the rep every time; neither is assumed.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CallChannel>))]
public enum CallChannel { InPerson, Phone }

/// <summary>The snapshot a Call was captured against: its contract version and the moment it was built.</summary>
public sealed record CallSnapshotRef(string Version, DateTimeOffset BuiltUtc);

/// <summary>A recorded contact between the rep and one Location.</summary>
/// <param name="CalledOn">The tablet's local date when the Call was saved.</param>
/// <param name="SavedAt">When it was saved, in the tablet's local time with its offset.</param>
/// <param name="PitchNotes">What was pitched; absent when the Call has no Pitch.</param>
/// <param name="StockEntries">In person, the Stock Check (T-5.4.1): one entry per product kept on the list, counted or Not
/// checked. On Phone, Stock mentioned (T-5.3.2): only the products marked Low or Out or given a count. Absent when there are
/// none.</param>
public sealed record TabletCall(string Schema, int Version, Guid Id, Guid LocationId, string CapturingRep, DateOnly CalledOn,
    DateTimeOffset SavedAt, CallChannel Channel, CallSnapshotRef Snapshot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PitchNotes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CallStockEntry>? StockEntries = null)
{
    /// <summary>What the Call records, for the same checks and summary the tablet used before saving.</summary>
    [JsonIgnore]
    public CallContent Content => new(Channel, PitchNotes, StockEntries);
}

/// <summary>One product on a Stock Check. Code and name are kept as captured, like an order line, so the entry still reads
/// correctly if the product is later renamed or hidden. <paramref name="Count"/> is in <paramref name="Unit"/>; absent means
/// Not checked: the product stays on the next visit's Suggested List. <paramref name="Low"/> is the rep's "reorder this" mark,
/// written only when true; Out is Low with a count of 0 (T-5.3.2 D1), so it needs no field of its own.</summary>
public sealed record CallStockEntry(Guid ProductId, string ProductCode, string ProductName, string Unit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Count = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Low = false)
{
    [JsonIgnore]
    public bool Counted => Count is not null;

    [JsonIgnore]
    public bool Out => Low && Count == 0m;

    /// <summary>Whether the entry says anything: a count or a Low mark. A phone Call saves only these.</summary>
    [JsonIgnore]
    public bool Mentioned => Counted || Low;
}

/// <summary>What a Call records apart from who, where and when: the part the rep fills in on T-06. The tablet checks a
/// draft with it before saving, and the server checks a received Call with it. Later sections add their parts here.</summary>
public sealed record CallContent(CallChannel? Channel, string? PitchNotes, IReadOnlyList<CallStockEntry>? StockEntries = null)
{
    private IReadOnlyList<CallStockEntry> Stock => StockEntries ?? [];

    /// <summary>The notes as they are saved: trimmed, and null when blank.</summary>
    public string? Notes => string.IsNullOrWhiteSpace(PitchNotes) ? null : PitchNotes.Trim();

    public bool HasPitch => Notes is not null;

    public int Counted => Stock.Count(entry => entry.Counted);
    public int NotChecked => Stock.Count(entry => !entry.Counted);
    public int Low => Stock.Count(entry => entry.Low);
    public int Out => Stock.Count(entry => entry.Out);

    /// <summary>A Stock Check counts as recorded only when at least one product was counted (WI-049 D6): a list left
    /// entirely Not checked records nothing.</summary>
    public bool HasStockCheck => Counted > 0;

    /// <summary>Whether anything was recorded: a Low mark counts on either channel (T-5.3.2 D3). Competitor notes (T-5.8.1)
    /// will count too.</summary>
    public bool RecordsSomething => HasPitch || HasStockCheck || Low > 0;

    /// <summary>Why this can't be saved yet, in the rep's words, or null when it can.</summary>
    public string? Problem =>
        Channel is null ? CallContract.ChooseChannel
        : Stock.Select(entry => CallContract.CountProblem(entry.Unit, entry.Count)).FirstOrDefault(problem => problem is not null) is { } count ? count
        : Channel == CallChannel.Phone && !Stock.All(entry => entry.Mentioned) ? CallContract.MentionedOnly
        : !RecordsSomething ? CallContract.RecordSomething
        : Notes?.Length > CallContract.MaximumPitchNotesLength ? CallContract.PitchNotesTooLong
        : null;

    /// <summary>The save review's one line, e.g. "In person · Pitch notes · 10 counted, 3 not checked · 2 Low", or on Phone
    /// "Phone · 2 marked Low (1 out)". Competitor notes (T-5.8.1) add "1 competitor note" last.</summary>
    public string Summary => string.Join(" · ", Parts());

    private IEnumerable<string> Parts()
    {
        if (Channel is { } channel) yield return CallContract.Label(channel);
        if (HasPitch) yield return "Pitch notes";
        if (Stock.Count == 0) yield break;
        if (Channel == CallChannel.Phone)
        {
            yield return MentionedSummary(Low, Out, Stock.Count(entry => entry.Counted && !entry.Low));
            yield break;
        }
        yield return StockSummary(Counted, NotChecked);
        if (Low > 0) yield return $"{Low} Low";
    }

    /// <summary>Stock mentioned on a phone Call (T-5.3.2 D4): "2 marked Low (1 out) · 1 counted", leaving out "(… out)" at 0
    /// and each part with nothing in it; empty when nothing was mentioned.</summary>
    public static string MentionedSummary(int low, int @out, int counted) => string.Join(" · ", new[]
    {
        low == 0 ? "" : @out == 0 ? $"{low} marked Low" : $"{low} marked Low ({@out} out)",
        counted == 0 ? "" : $"{counted} counted"
    }.Where(part => part.Length > 0));

    /// <summary>"10 counted, 3 not checked"; "13 counted" when nothing was left; "3 not checked" when nothing was counted.</summary>
    public static string StockSummary(int counted, int notChecked) => (counted, notChecked) switch
    {
        (_, 0) => $"{counted} counted",
        (0, _) => $"{notChecked} not checked",
        _ => $"{counted} counted, {notChecked} not checked"
    };
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(TabletCall))]
[JsonSerializable(typeof(CallSnapshotRef))]
[JsonSerializable(typeof(CallStockEntry))]
public sealed partial class CallJsonContext : JsonSerializerContext;
