using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Api.Ordering;

public enum ReceivedWorkKind { Order, Call }

/// <summary>One item a tablet sent, keyed by the id the tablet gave it at capture, with its body exactly as received.</summary>
/// <remarks>The first version received is the one kept. A later re-send with a different body is counted here, never
/// stored: the rep's tablet could not change an item after it was finished when this was written (D1).</remarks>
public sealed class ReceivedWork
{
    private ReceivedWork() { }

    public Guid Id { get; private set; }
    public ReceivedWorkKind Kind { get; private set; }
    public string RepSubject { get; private set; } = "";
    public DateTimeOffset ReceivedUtc { get; private set; }
    public string ContentSha256 { get; private set; } = "";
    public string Body { get; private set; } = "";
    public int ResentWithChangesCount { get; private set; }
    public DateTimeOffset? LastResentWithChangesUtc { get; private set; }

    public static ReceivedWork Create(Guid id, ReceivedWorkKind kind, string repSubject, DateTimeOffset receivedUtc,
        string contentSha256, string body) => new()
    {
        Id = id, Kind = kind, RepSubject = repSubject, ReceivedUtc = receivedUtc.ToUniversalTime(),
        ContentSha256 = contentSha256, Body = body
    };
}

/// <summary>Where a received order stands in the order lifecycle.</summary>
/// <remarks><see cref="Accepted"/> feeds the Suggested List's "Ordered recently" (T-5.4.1). Nothing sets it yet: acceptance at
/// the cut-off arrives with T-7.1.1 (WI-067), so until then every received order stays Pending (WI-049 D1).</remarks>
public enum OrderLifecycleStatus { Pending, Accepted }

/// <summary>An order received from a tablet. Its lines stay in the received body; this row holds what the order
/// lifecycle needs to find it.</summary>
public sealed class ReceivedOrder
{
    private ReceivedOrder() { }

    public Guid Id { get; private set; }
    public OrderLifecycleStatus Status { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid OrderedById { get; private set; }
    public string CapturingRep { get; private set; } = "";
    public DateOnly TakenOn { get; private set; }
    public Guid? CallId { get; private set; }
    public string SnapshotVersion { get; private set; } = "";
    public DateTimeOffset SnapshotBuiltUtc { get; private set; }
    public DateTimeOffset ReceivedUtc { get; private set; }

    public static ReceivedOrder Pending(TabletOrder order, DateTimeOffset receivedUtc) => new()
    {
        Id = order.Id, Status = OrderLifecycleStatus.Pending, LocationId = order.LocationId, OrderedById = order.OrderedById,
        CapturingRep = order.CapturingRep, TakenOn = order.TakenOn, CallId = order.CallId,
        SnapshotVersion = order.Snapshot.Version, SnapshotBuiltUtc = order.Snapshot.BuiltUtc,
        ReceivedUtc = receivedUtc.ToUniversalTime()
    };
}

/// <summary>A Call received from a tablet (T-5.3.1). Its full record stays in the received body; this row and its stock
/// entries hold what the next Morning Snapshot needs to find a Location's last Stock Check (T-5.4.1).</summary>
public sealed class ReceivedCall
{
    private ReceivedCall() { }

    public Guid Id { get; private set; }
    public Guid LocationId { get; private set; }
    public string CapturingRep { get; private set; } = "";
    public DateOnly CalledOn { get; private set; }
    public DateTimeOffset SavedAt { get; private set; }
    public CallChannel Channel { get; private set; }
    public DateTimeOffset ReceivedUtc { get; private set; }
    public List<ReceivedCallStockEntry> StockEntries { get; private set; } = [];

    public static ReceivedCall From(TabletCall call, DateTimeOffset receivedUtc) => new()
    {
        Id = call.Id, LocationId = call.LocationId, CapturingRep = call.CapturingRep, CalledOn = call.CalledOn,
        SavedAt = call.SavedAt, Channel = call.Channel, ReceivedUtc = receivedUtc.ToUniversalTime(),
        StockEntries = [.. (call.StockEntries ?? []).Select(entry => new ReceivedCallStockEntry(call.Id, entry))]
    };
}

/// <summary>One product on a received Call: its count in its unit, or Not checked (no count), and the rep's Low mark. On a
/// phone Call only mentioned products are kept, so each has a count or Low (T-5.3.2); Out is Low with a count of 0.</summary>
public sealed class ReceivedCallStockEntry
{
    private ReceivedCallStockEntry() { }

    internal ReceivedCallStockEntry(Guid callId, CallStockEntry entry)
    {
        CallId = callId; ProductId = entry.ProductId; Unit = entry.Unit; Count = entry.Count; Low = entry.Low;
    }

    public Guid CallId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Unit { get; private set; } = "";
    public decimal? Count { get; private set; }
    public bool Low { get; private set; }
}
