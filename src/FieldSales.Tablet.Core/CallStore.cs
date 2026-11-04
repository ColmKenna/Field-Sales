using System.Text.Json;
using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>The outcome of saving a Call: the saved Call, or a message the rep can act on.</summary>
public sealed record CallResult(TabletCall? Call, string? Error)
{
    public bool Succeeded => Error is null;
    internal static CallResult Failure(string error) => new(null, error);
}

/// <summary>The rep's recorded Calls, kept as Unsent Calls until Sync sends them (T-5.3.1).</summary>
/// <remarks>
/// A Call is saved once, with the id the screen chose when it opened, so saving again with that id returns the Call
/// already saved instead of recording a second one. There is no update: a saved Call is a record of what happened, and
/// corrections (T-5.6.1) arrive as their own operation. The Call is judged as captured, so a shop reassigned since the
/// snapshot doesn't stop it saving; the snapshot it was captured against travels with it.
/// </remarks>
public sealed class CallStore(RepWorkspace workspace, TimeProvider? clock = null)
{
    public const string NotDownloaded = "Your day hasn't downloaded yet, so this call can't be saved. Sync first.";

    private readonly TimeProvider time = clock ?? TimeProvider.System;

    /// <summary>Saves the Call the rep reviewed. The content must pass <see cref="CallContent.Problem"/>.</summary>
    public CallResult Save(Guid callId, Guid locationId, CallContent content)
    {
        if (content.Problem is { } problem) return CallResult.Failure(problem);
        // Serialized with order changes and the uploader's claim, so a repeated Save can't record the Call twice.
        SemaphoreSlim gate = OrderStore.GateFor(workspace);
        gate.Wait();
        try
        {
            UnsentStore unsent = new(workspace);
            if (Find(callId) is { } saved) return new(saved, null);
            if (unsent.IsSent(callId)) return new(Parse(unsent.FindSent(callId)?.Item), null);
            if (new SnapshotStore(workspace, time).ReadActive() is not { } active) return CallResult.Failure(NotDownloaded);

            DateTimeOffset now = time.GetLocalNow();
            TabletCall call = new(CallContract.Schema, CallContract.CurrentVersion, callId, locationId, workspace.RepSubject,
                DateOnly.FromDateTime(now.DateTime), now, content.Channel!.Value,
                new(active.Manifest.Version, active.Manifest.BuiltUtc), content.Notes,
                content.StockEntries is { Count: > 0 } stock ? [.. stock] : null);
            unsent.Save(new(callId, UnsentKind.Call, time.GetUtcNow(), JsonSerializer.Serialize(call, CallJsonContext.Default.TabletCall)));
            return new(call, null);
        }
        finally { gate.Release(); }
    }

    /// <summary>The unsent Call with this id, or null when there is none or it can't be read as a Call.</summary>
    public TabletCall? Find(Guid callId) => Parse(new UnsentStore(workspace).Find(callId));

    internal static TabletCall? Parse(UnsentItem? item)
    {
        if (item is not { Kind: UnsentKind.Call }) return null;
        try
        {
            TabletCall? call = JsonSerializer.Deserialize(item.Json, CallJsonContext.Default.TabletCall);
            return call is { Schema: CallContract.Schema } && call.Id == item.Id ? call : null;
        }
        catch (JsonException) { return null; }
    }
}
