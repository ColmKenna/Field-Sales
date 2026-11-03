using System.Text.Json;
using System.Text.Json.Serialization;

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
/// <item><term><c>stockEntries[].low</c></term><description>T-5.5.1 Low marks, on the entry they belong to.</description></item>
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
public sealed record TabletCall(string Schema, int Version, Guid Id, Guid LocationId, string CapturingRep, DateOnly CalledOn,
    DateTimeOffset SavedAt, CallChannel Channel, CallSnapshotRef Snapshot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PitchNotes = null)
{
    /// <summary>What the Call records, for the same checks and summary the tablet used before saving.</summary>
    [JsonIgnore]
    public CallContent Content => new(Channel, PitchNotes);
}

/// <summary>What a Call records apart from who, where and when: the part the rep fills in on T-06. The tablet checks a
/// draft with it before saving, and the server checks a received Call with it. Later sections add their parts here.</summary>
public sealed record CallContent(CallChannel? Channel, string? PitchNotes)
{
    /// <summary>The notes as they are saved: trimmed, and null when blank.</summary>
    public string? Notes => string.IsNullOrWhiteSpace(PitchNotes) ? null : PitchNotes.Trim();

    public bool HasPitch => Notes is not null;

    /// <summary>Whether anything was recorded. Stock entries (T-5.4.1) and competitor notes (T-5.8.1) will count too.</summary>
    public bool RecordsSomething => HasPitch;

    /// <summary>Why this can't be saved yet, in the rep's words, or null when it can.</summary>
    public string? Problem =>
        Channel is null ? CallContract.ChooseChannel
        : !RecordsSomething ? CallContract.RecordSomething
        : Notes!.Length > CallContract.MaximumPitchNotesLength ? CallContract.PitchNotesTooLong
        : null;

    /// <summary>The save review's one line, e.g. "In person · Pitch notes". Later sections add "13 counted, 3 not checked",
    /// "2 Low" and "1 competitor note" in that order.</summary>
    public string Summary => string.Join(" · ", Parts());

    private IEnumerable<string> Parts()
    {
        if (Channel is { } channel) yield return CallContract.Label(channel);
        if (HasPitch) yield return "Pitch notes";
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(TabletCall))]
[JsonSerializable(typeof(CallSnapshotRef))]
public sealed partial class CallJsonContext : JsonSerializerContext;
