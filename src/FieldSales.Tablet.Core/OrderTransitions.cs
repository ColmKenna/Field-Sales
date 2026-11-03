namespace FieldSales.Tablet.Core;

/// <summary>Where an order stands on the tablet, as far as changing it goes.</summary>
public enum OrderStanding
{
    /// <summary>No order with this id is saved yet: the screen chose the id and nothing has been added.</summary>
    New,
    InProgress,
    ReadyToSend,
    /// <summary>Ready to Send and claimed by an upload: a Sync is sending it, or sent it without a confirmation coming back.
    /// The server may hold it, so it must not change or disappear until a Sync gets an answer.</summary>
    Sending,
    /// <summary>The server refused it for a technical fault (WI-042). Only Open, re-mark and Delete apply there.</summary>
    NeedsAttention,
    /// <summary>Confirmed by the server. Read-only on the tablet.</summary>
    Sent,
    /// <summary>Saved work that can't be read as an order.</summary>
    Unreadable,
    /// <summary>The result of deleting: the order is gone from the tablet and nothing is sent.</summary>
    Deleted
}

public enum OrderAction { AddLine, ChangeQuantity, RemoveLine, MarkReady, Reopen, Delete }

/// <summary>The order state machine (T-5.1.1, T-5.2.1): every change a rep can make, from each standing. Anything not in
/// <see cref="Allowed"/> is refused with <see cref="Refusal"/>.</summary>
/// <remarks>
/// <para>In Progress and Ready to Send move both ways only before upload. Delete works from either. Nothing leaves
/// Sending, Needs Attention or Sent through a rep's change: an upload's answer moves Sending to Sent or Needs Attention, or
/// back to Ready to Send when the server provably never got it (WorkUploader); WI-042 owns Needs Attention.</para>
/// <para>Further guards that sit outside the table: Mark Ready needs a line; Delete and removing the last line need the
/// rep's confirmation; adding a line needs the shop assigned and the product on the current snapshot; changing a quantity
/// needs the product on the current snapshot and a quantity that follows its rules.</para>
/// </remarks>
public static class OrderTransitions
{
    public static readonly IReadOnlyDictionary<(OrderStanding From, OrderAction Action), OrderStanding> Allowed =
        new Dictionary<(OrderStanding, OrderAction), OrderStanding>
        {
            [(OrderStanding.New, OrderAction.AddLine)] = OrderStanding.InProgress,
            [(OrderStanding.InProgress, OrderAction.AddLine)] = OrderStanding.InProgress,
            [(OrderStanding.InProgress, OrderAction.ChangeQuantity)] = OrderStanding.InProgress,
            // The last line can go only with the order, after the rep confirms: then the result is Deleted.
            [(OrderStanding.InProgress, OrderAction.RemoveLine)] = OrderStanding.InProgress,
            [(OrderStanding.InProgress, OrderAction.MarkReady)] = OrderStanding.ReadyToSend,
            [(OrderStanding.InProgress, OrderAction.Delete)] = OrderStanding.Deleted,
            [(OrderStanding.ReadyToSend, OrderAction.Reopen)] = OrderStanding.InProgress,
            [(OrderStanding.ReadyToSend, OrderAction.Delete)] = OrderStanding.Deleted,
        };

    /// <summary>The standing an allowed change leads to, or null when the change is refused.</summary>
    public static OrderStanding? After(OrderStanding from, OrderAction action) =>
        Allowed.TryGetValue((from, action), out OrderStanding to) ? to : null;

    /// <summary>What the rep reads when a change is refused.</summary>
    public static string Refusal(OrderStanding from, OrderAction action) => (from, action) switch
    {
        (OrderStanding.Sending, _) => OrderStore.SendingUnconfirmed,
        (OrderStanding.Sent or OrderStanding.NeedsAttention or OrderStanding.Unreadable or OrderStanding.Deleted, _) => OrderStore.NotChangeable,
        (OrderStanding.New, OrderAction.MarkReady) => OrderStore.EmptyOrder,
        (OrderStanding.New, _) => OrderStore.NotFound,
        // A Ready to Send order is reopened before it changes, so a half-agreed order never sends by accident.
        (OrderStanding.ReadyToSend, _) => OrderStore.AlreadyReady,
        (OrderStanding.InProgress, OrderAction.Reopen) => OrderStore.AlreadyInProgress,
        _ => OrderStore.NotChangeable
    };
}
