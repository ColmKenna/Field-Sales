using System.Text.Json;
using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>Where a saved Call stands for correcting it (T-5.6.1): unsent and changeable, being sent, sent (read-only), or refused by
/// the server (WI-042's Needs Attention).</summary>
public enum CallStanding { Unsent, Sending, Sent, NeedsAttention }

/// <summary>A saved Call with its standing; <paramref name="SentUtc"/> once it is Sent.</summary>
public sealed record CallView(TabletCall Call, CallStanding Standing, DateTimeOffset? SentUtc = null)
{
    /// <summary>Only an unsent Call that no upload has claimed can be corrected on the tablet (US-015; WI-055 D5).</summary>
    public bool CanCorrect => Standing == CallStanding.Unsent;
}

public sealed partial class CallStore
{
    public const string NotOnTablet = "This call isn't on this tablet.";
    public const string AlreadySent = "This call has been sent. Record a follow-up call to add to it.";
    public const string BeingSent = "This call is being sent. Sync again before changing it.";
    public const string NotCorrectable = "This call can't be changed here.";
    public const string AddsContent = "A correction can only change or remove what was recorded. Record a follow-up call to add to it.";

    /// <summary>The saved Call with this id, unsent or Sent, with its standing; null when this tablet has none or it can't be read
    /// as a Call. Asking never creates the database.</summary>
    public CallView? ReadView(Guid callId)
    {
        if (!File.Exists(workspace.UnsentPath)) return null;
        UnsentStore unsent = new(workspace);
        if (unsent.Find(callId) is { } item)
            return Parse(item) is { } call
                ? new(call, item.Rejection is not null ? CallStanding.NeedsAttention
                    : unsent.IsClaimed(callId) ? CallStanding.Sending : CallStanding.Unsent)
                : null;
        return unsent.FindSent(callId) is { } sent && Parse(sent.Item) is { } sentCall ? new(sentCall, CallStanding.Sent, sent.SentUtc) : null;
    }

    /// <summary>Corrects a saved, unsent Call before Sync (T-5.6.1, US-015): its Channel, notes, counts and Low marks change, and
    /// lines can go, but nothing is added — no product it didn't have, no count on a Not checked line, no notes where it had none —
    /// and a count can't be cleared (WI-055 D2). The Call keeps its id, date, saved time, rep and snapshot, and its place in the
    /// queue. Under the gate the uploader claims items with, so a Call an upload has claimed is never changed (D5).</summary>
    public CallResult Correct(Guid callId, Guid locationId, CallContent content)
    {
        SemaphoreSlim gate = OrderStore.GateFor(workspace);
        gate.Wait();
        try
        {
            if (!File.Exists(workspace.UnsentPath)) return CallResult.Failure(NotOnTablet);
            UnsentStore unsent = new(workspace);
            if (unsent.Find(callId) is not { } item || Parse(item) is not { } saved)
                return CallResult.Failure(unsent.IsSent(callId) ? AlreadySent : NotOnTablet);
            if (item.Rejection is not null || saved.LocationId != locationId) return CallResult.Failure(NotCorrectable);
            if (unsent.IsClaimed(callId)) return CallResult.Failure(BeingSent);
            if (content.Problem is { } problem) return CallResult.Failure(problem);
            if (AddsTo(saved, content)) return CallResult.Failure(AddsContent);

            TabletCall corrected = saved with
            {
                Channel = content.Channel!.Value, PitchNotes = content.Notes,
                StockEntries = content.StockEntries is { Count: > 0 } stock ? [.. stock] : null
            };
            unsent.Save(item with { Json = JsonSerializer.Serialize(corrected, CallJsonContext.Default.TabletCall) });
            return new(corrected, null);
        }
        finally { gate.Release(); }
    }

    // D2: a correction changes or removes what was recorded and never adds to it.
    private static bool AddsTo(TabletCall saved, CallContent content)
    {
        Dictionary<Guid, CallStockEntry> before = (saved.StockEntries ?? []).ToDictionary(entry => entry.ProductId);
        return (content.Notes is not null && string.IsNullOrWhiteSpace(saved.PitchNotes))
            || (content.StockEntries ?? []).Any(entry => !before.TryGetValue(entry.ProductId, out CallStockEntry? was)
                || (was.Count is null) != (entry.Count is null));
    }
}
