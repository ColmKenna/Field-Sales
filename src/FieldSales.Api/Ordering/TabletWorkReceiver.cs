using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Ordering;

public enum ReceiveOutcome { Received, AlreadyReceived, Refused, Conflict }

/// <summary>What receiving one item did. A refused or conflicting item carries an <see cref="UploadFaults"/> code and a
/// sentence saying what was wrong.</summary>
public sealed record ReceiveResult(ReceiveOutcome Outcome, string? Fault = null, string? Detail = null)
{
    public static ReceiveResult Received { get; } = new(ReceiveOutcome.Received);
    public static ReceiveResult AlreadyReceived { get; } = new(ReceiveOutcome.AlreadyReceived);
    public static ReceiveResult Conflict { get; } = new(ReceiveOutcome.Conflict, UploadFaults.IdInUse, "Another item already has this id.");
    public static ReceiveResult Refused(string fault, string detail) => new(ReceiveOutcome.Refused, fault, detail);
}

/// <summary>Receives a rep's finished work exactly once (T-4.1.2).</summary>
/// <remarks>
/// Work is judged as captured (BR-NEW-006): only the item's own structure is checked, never the catalogue, the
/// directory or anything else that may have changed since the rep's snapshot. An item is stored with its id as the
/// key, so a re-send is answered "already received" and never stored twice, even when two copies arrive at once.
/// The rep is the signed-in caller; an item naming anyone else is refused.
/// </remarks>
public sealed class TabletWorkReceiver(OrderingDbContext db, TimeProvider clock)
{
    public async Task<ReceiveResult> ReceiveOrderAsync(Guid id, string repSubject, byte[] body, CancellationToken ct)
    {
        TabletOrder? order = ReadOrder(id, body, out ReceiveResult? refused);
        if (order is null) return refused!;
        if (!string.Equals(order.CapturingRep, repSubject, StringComparison.Ordinal))
            return ReceiveResult.Refused(UploadFaults.NotYours, "The order was captured by a different rep.");
        return await StoreAsync(id, ReceivedWorkKind.Order, repSubject, body, order, ct);
    }

    /// <summary>Stores a recorded Call as received (T-5.3.1). Only its structure is checked; the Location it names may have
    /// been reassigned since the rep's snapshot, and that never refuses it. Its stock entries are kept beside the ledger row
    /// so the Location's next Suggested List can start from them (T-5.4.1).</summary>
    public async Task<ReceiveResult> ReceiveCallAsync(Guid id, string repSubject, byte[] body, CancellationToken ct)
    {
        TabletCall? call = ReadCall(id, body, out ReceiveResult? refused);
        if (call is null) return refused!;
        if (!string.Equals(call.CapturingRep, repSubject, StringComparison.Ordinal))
            return ReceiveResult.Refused(UploadFaults.NotYours, "The call was recorded by a different rep.");
        return await StoreAsync(id, ReceivedWorkKind.Call, repSubject, body, null, ct, call);
    }

    private async Task<ReceiveResult> StoreAsync(Guid id, ReceivedWorkKind kind, string repSubject, byte[] body,
        TabletOrder? order, CancellationToken ct, TabletCall? call = null)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(body));
        if (await ExistingAsync(id, kind, repSubject, hash, ct) is { } existing) return existing;

        DateTimeOffset now = clock.GetUtcNow();
        db.ReceivedWork.Add(ReceivedWork.Create(id, kind, repSubject, now, hash, Encoding.UTF8.GetString(body)));
        if (order is not null) db.Orders.Add(ReceivedOrder.Pending(order, now));
        if (call is not null) db.Calls.Add(ReceivedCall.From(call, now));
        try
        {
            // One SaveChanges is one transaction: the ledger row and its order or Call row are stored together or not at all.
            await db.SaveChangesAsync(ct);
            return ReceiveResult.Received;
        }
        catch (DbUpdateException error) when (SqlServerErrors.IsUniqueViolation(error))
        {
            // Another copy of the same item was stored first.
            db.ChangeTracker.Clear();
            return await ExistingAsync(id, kind, repSubject, hash, ct) ?? throw new InvalidOperationException("The stored item vanished.", error);
        }
    }

    private async Task<ReceiveResult?> ExistingAsync(Guid id, ReceivedWorkKind kind, string repSubject, string hash, CancellationToken ct)
    {
        var stored = await db.ReceivedWork.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new { item.Kind, item.RepSubject, item.ContentSha256 }).SingleOrDefaultAsync(ct);
        if (stored is null) return null;
        // Never confirm another rep's item, nor an item of another kind, as this caller's.
        if (stored.Kind != kind || !string.Equals(stored.RepSubject, repSubject, StringComparison.Ordinal)) return ReceiveResult.Conflict;
        if (stored.ContentSha256 != hash)
        {
            DateTimeOffset now = clock.GetUtcNow().ToUniversalTime();
            await db.ReceivedWork.Where(item => item.Id == id).ExecuteUpdateAsync(set => set
                .SetProperty(item => item.ResentWithChangesCount, item => item.ResentWithChangesCount + 1)
                .SetProperty(item => item.LastResentWithChangesUtc, now), ct);
        }
        return ReceiveResult.AlreadyReceived;
    }

    // Structure only: the body must be a finished order this server can read, with what the order lifecycle needs.
    internal static TabletOrder? ReadOrder(Guid id, byte[] body, out ReceiveResult? refused)
    {
        refused = null;
        TabletOrder? order = null;
        try
        {
            using (JsonDocument document = JsonDocument.Parse(body))
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                    order = JsonSerializer.Deserialize(body, OrderJsonContext.Default.TabletOrder);
        }
        catch (JsonException) { }
        if (order is null) refused = ReceiveResult.Refused(UploadFaults.Corrupt, "The order couldn't be read.");
        else refused = StructureFault(id, order);
        return refused is null ? order : null;
    }

    private static ReceiveResult? StructureFault(Guid id, TabletOrder order) => order switch
    {
        { Schema: not OrderContract.Schema } => ReceiveResult.Refused(UploadFaults.Unsupported, "This isn't an order."),
        { Version: < 1 or > OrderContract.CurrentVersion } => ReceiveResult.Refused(UploadFaults.Unsupported, "This order version isn't supported."),
        _ when order.Id != id => Incomplete("The order's id doesn't match where it was sent."),
        { State: not OrderState.ReadyToSend } => Incomplete("Only an order that is Ready to Send can be received."),
        _ when order.LocationId == Guid.Empty || order.OrderedById == Guid.Empty => Incomplete("The order has no Location."),
        _ when string.IsNullOrWhiteSpace(order.CapturingRep) => Incomplete("The order has no capturing rep."),
        _ when order.TakenOn == default => Incomplete("The order has no date."),
        _ when !IsSnapshot(order.Snapshot) => Incomplete("The order has no snapshot reference."),
        _ when order.Lines is not { Count: > 0 } => Incomplete("The order has no lines."),
        _ when !order.Lines.All(IsLine) => Incomplete("An order line is incomplete."),
        _ => null
    };

    // Structure only: the body must be a Call this server can read, naming who, where, when, how, and something recorded.
    internal static TabletCall? ReadCall(Guid id, byte[] body, out ReceiveResult? refused)
    {
        refused = null;
        TabletCall? call = null;
        bool hasChannel = false;
        try
        {
            using (JsonDocument document = JsonDocument.Parse(body))
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // A missing channel would read as the enum's default, so its presence is checked on the raw body.
                    hasChannel = document.RootElement.TryGetProperty("channel", out JsonElement channel) && channel.ValueKind == JsonValueKind.String;
                    call = JsonSerializer.Deserialize(body, CallJsonContext.Default.TabletCall);
                }
        }
        catch (JsonException) { }
        refused = call is null ? ReceiveResult.Refused(UploadFaults.Corrupt, "The call couldn't be read.") : CallFault(id, call, hasChannel);
        return refused is null ? call : null;
    }

    private static ReceiveResult? CallFault(Guid id, TabletCall call, bool hasChannel) => call switch
    {
        { Schema: not CallContract.Schema } => ReceiveResult.Refused(UploadFaults.Unsupported, "This isn't a call."),
        { Version: < 1 or > CallContract.CurrentVersion } => ReceiveResult.Refused(UploadFaults.Unsupported, "This call version isn't supported."),
        _ when call.Id != id => Incomplete("The call's id doesn't match where it was sent."),
        _ when call.LocationId == Guid.Empty => Incomplete("The call has no Location."),
        _ when string.IsNullOrWhiteSpace(call.CapturingRep) => Incomplete("The call has no rep."),
        _ when call.CalledOn == default || call.SavedAt == default => Incomplete("The call has no date."),
        _ when !hasChannel => Incomplete("The call has no channel."),
        _ when call.Snapshot is null || string.IsNullOrWhiteSpace(call.Snapshot.Version) || call.Snapshot.Version.Length > 20
            || call.Snapshot.BuiltUtc == default => Incomplete("The call has no snapshot reference."),
        _ when call.StockEntries is { } stock && !AreStockEntries(stock) => Incomplete("A stock entry is incomplete."),
        _ when call.Content.Problem is { } problem => Incomplete($"The call can't be accepted: {problem}."),
        _ => null
    };

    // Each entry names a product once, with its captured code and name and a unit this system knows; counts are judged by
    // the shared rule in CallContent.Problem.
    private static bool AreStockEntries(IReadOnlyList<CallStockEntry?> stock) =>
        stock.All(entry => entry is not null && entry.ProductId != Guid.Empty && !string.IsNullOrWhiteSpace(entry.ProductCode)
            && !string.IsNullOrWhiteSpace(entry.ProductName) && FieldSales.Quantities.UnitOfMeasureExtensions.TryParse(entry.Unit, out _))
        && stock.Select(entry => entry!.ProductId).Distinct().Count() == stock.Count;

    private static ReceiveResult Incomplete(string detail) => ReceiveResult.Refused(UploadFaults.Incomplete, detail);

    private static bool IsSnapshot(OrderSnapshotRef? snapshot) =>
        snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.Version) && snapshot.Version.Length <= 20 && snapshot.BuiltUtc != default;

    private static bool IsLine(OrderLine? line) => line is not null && line.Id != Guid.Empty && line.ProductId != Guid.Empty
        && line.Quantity > 0 && !string.IsNullOrWhiteSpace(line.Unit) && line.Price is { Amount: >= 0 } && IsSnapshot(line.Snapshot);
}
