using System.Collections.Concurrent;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Pricing;
using FieldSales.Quantities;

namespace FieldSales.Tablet.Core;

/// <summary>The outcome of an order change: the saved order, or a message the rep can act on. A deletion succeeds with no
/// order and <see cref="Deleted"/> set. A deletion still waiting for the rep's confirmation is refused with
/// <see cref="NeedsConfirmation"/> set and the question as the message.</summary>
public sealed record OrderResult(TabletOrder? Order, string? Error)
{
    public bool Succeeded => Error is null;
    public bool Deleted { get; init; }
    public bool NeedsConfirmation { get; init; }
    internal static OrderResult Failure(string error) => new(null, error);
    internal static OrderResult Ask(string question) => new(null, question) { NeedsConfirmation = true };
    internal static OrderResult Gone { get; } = new(null, null) { Deleted = true };
}

/// <summary>An order as the order screen shows it: its body, where it stands, and when it was sent if it was.</summary>
public sealed record OrderView(TabletOrder Order, OrderStanding Standing, DateTimeOffset? SentUtc = null);

/// <summary>The rep's orders, kept as Unsent Orders in their own database (T-5.1.1).</summary>
/// <remarks>
/// Nothing is stored until the first line is added; every change then saves at once as In Progress, so a
/// stopped app loses nothing. A line captures the product's code and name, the quantity with its unit, the base
/// price in effect today with its source, and the snapshot it came from; none of that changes afterwards.
/// Adding a product already on the order raises that line's quantity and keeps what the line captured first.
/// Before Sync the rep can change a line's quantity, remove a line, reopen a Ready to Send order or delete an order
/// (T-5.2.1); <see cref="OrderTransitions"/> is the whole table of what is allowed from where.
/// Changes are serialized per rep, because a change reads the saved order and writes it back. The uploader claims an item
/// under the same gate, so a change either lands before the claim (and the changed order isn't sent) or is refused.
/// </remarks>
public sealed class OrderStore
{
    public const string NotAssigned = "This shop isn't assigned to you.";
    public const string NotDownloaded = "This product isn't in your downloaded catalogue.";
    public const string NoPrice = "No price yet — this product can't be ordered.";
    public const string NotChangeable = "This order can't be changed here.";
    public const string AlreadyReady = "This order is ready to send.";
    public const string EmptyOrder = "Add at least one product";
    public const string NoLongerAvailable = "No longer available to you";
    public const string NotFound = "This order isn't on this tablet.";
    public const string AlreadyInProgress = "This order is already in progress.";
    public const string SendingUnconfirmed = "Sending this order wasn't confirmed. Sync again before changing it.";
    public const string LastLinePrompt = "Remove the last line and delete this order?";
    public const string DeletePrompt = "Delete this order? It hasn't been sent. Deleting removes it from this tablet.";
    public const string NoSuchLine = "This line isn't on the order any more.";
    public const string CantChangeMissing = "No longer available to you — you can remove this line but not change it.";
    public const string UnitChanged = "This product's unit changed since it was added. Start a new order for it.";
    public const string RulesUnreadable = "This product's ordering rules couldn't be read.";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    private readonly RepWorkspace workspace;
    private readonly TimeProvider clock;

    public OrderStore(RepWorkspace workspace, TimeProvider? clock = null)
    {
        this.workspace = workspace;
        this.clock = clock ?? TimeProvider.System;
    }

    private SemaphoreSlim Gate => GateFor(workspace);

    /// <summary>The per-rep gate every order change holds; the uploader holds it to claim an item.</summary>
    internal static SemaphoreSlim GateFor(RepWorkspace workspace) => Gates.GetOrAdd(workspace.UnsentPath, _ => new(1, 1));

    /// <summary>The order with this id, or null when none is saved or the saved item is not a readable order.</summary>
    public TabletOrder? Find(Guid orderId) => Parse(new UnsentStore(workspace).Find(orderId));

    /// <summary>The order with this id wherever it is, unsent or Sent, with its standing; null when there is none or it
    /// can't be read as an order.</summary>
    public OrderView? Read(Guid orderId)
    {
        if (!File.Exists(workspace.UnsentPath)) return null;
        UnsentStore unsent = new(workspace);
        if (unsent.Find(orderId) is { } item)
            return Parse(item) is { } order ? new(order, StandingOf(item, order, unsent.IsClaimed(orderId))) : null;
        return unsent.FindSent(orderId) is { } sent && Parse(sent.Item) is { } sentOrder
            ? new(sentOrder, OrderStanding.Sent, sent.SentUtc) : null;
    }

    /// <summary>Where the order stands for changing it.</summary>
    private static OrderStanding Standing(UnsentStore unsent, Guid orderId, out UnsentItem? item, out TabletOrder? order)
    {
        item = unsent.Find(orderId);
        order = Parse(item);
        if (item is null) return unsent.IsSent(orderId) ? OrderStanding.Sent : OrderStanding.New;
        return StandingOf(item, order, unsent.IsClaimed(orderId));
    }

    // A claim outranks everything: the server may hold that exact body.
    private static OrderStanding StandingOf(UnsentItem item, TabletOrder? order, bool claimed) =>
        claimed ? OrderStanding.Sending
        : item.Rejection is not null ? OrderStanding.NeedsAttention
        : order?.State switch
        {
            OrderState.InProgress => OrderStanding.InProgress,
            OrderState.ReadyToSend => OrderStanding.ReadyToSend,
            _ => OrderStanding.Unreadable
        };

    /// <summary>Adds <paramref name="quantity"/> of a product to the order, starting the order at
    /// <paramref name="locationId"/> when it has no line yet. The shop must be assigned to the rep on the
    /// current snapshot, and the quantity must follow the product's rules.</summary>
    public OrderResult AddLine(Guid orderId, Guid locationId, Guid productId, decimal quantity) =>
        AddLine(orderId, locationId, productId, quantity, callId: null);

    /// <summary>The same add for order entry opened from a Call (T-5.1.4 D3): the line goes on <paramref name="orderId"/>, and an
    /// order this starts, or one with no Call yet, is linked to <paramref name="callId"/>. An order linked to another Call keeps
    /// that link.</summary>
    public OrderResult AddLine(Guid orderId, Guid locationId, Guid productId, decimal quantity, Guid? callId)
    {
        Gate.Wait();
        try { return AddLine(new UnsentStore(workspace), orderId, locationId, productId, quantity, callId); }
        finally { Gate.Release(); }
    }

    /// <summary>The order a Call at <paramref name="locationId"/> adds to (T-5.5.1 D2): the In Progress order linked to the
    /// Call, else the rep's most recently saved In Progress order at that shop, else null (the first add starts one). An order
    /// being sent, refused or Ready to Send is never added to, so if the linked order left In Progress the Call finds or starts
    /// another.</summary>
    public TabletOrder? FindForCall(Guid callId, Guid locationId) =>
        File.Exists(workspace.UnsentPath) ? ForCall(new UnsentStore(workspace), callId, locationId) : null;

    /// <summary>Adds a product from a Call (T-5.5.1): to the order <see cref="FindForCall"/> picks, or to a new order linked to the
    /// Call. Picking and adding happen under the one per-rep gate, so two adds for one Call can't start two orders. An order
    /// with no Call yet becomes linked to this one; an order already linked to another Call keeps that link.</summary>
    public OrderResult AddFromCall(Guid callId, Guid locationId, Guid productId, decimal quantity)
    {
        Gate.Wait();
        try
        {
            UnsentStore unsent = new(workspace);
            Guid orderId = ForCall(unsent, callId, locationId)?.Id ?? Guid.NewGuid();
            return AddLine(unsent, orderId, locationId, productId, quantity, callId);
        }
        finally { Gate.Release(); }
    }

    /// <summary>Links an In Progress order with no Call to <paramref name="callId"/> (T-5.1.5 D5): a Call recorded from the order's
    /// Low tab was saved. An order already linked to that Call is left as it is. One linked to another Call, or no longer In
    /// Progress, isn't changed; with no such order the answer is <see cref="NotFound"/>.</summary>
    public OrderResult LinkToCall(Guid orderId, Guid callId)
    {
        Gate.Wait();
        try
        {
            UnsentStore unsent = new(workspace);
            OrderStanding standing = Standing(unsent, orderId, out _, out TabletOrder? order);
            if (standing == OrderStanding.New) return OrderResult.Failure(NotFound);
            if (standing != OrderStanding.InProgress || order is null || (order.CallId is { } linked && linked != callId))
                return OrderResult.Failure(NotChangeable);
            if (order.CallId == callId) return Saved(order);
            TabletOrder changed = order with { CallId = callId };
            Save(unsent, changed);
            return Saved(changed);
        }
        finally { Gate.Release(); }
    }

    private static TabletOrder? ForCall(UnsentStore unsent, Guid callId, Guid locationId)
    {
        TabletOrder[] open = [.. unsent.Read()
            .Where(item => item.Kind == UnsentKind.Order)
            .Select(item => (Item: item, Order: Parse(item)))
            .Where(each => each.Order is { State: OrderState.InProgress } order && order.LocationId == locationId
                && StandingOf(each.Item, order, unsent.IsClaimed(order.Id)) == OrderStanding.InProgress)
            .OrderByDescending(each => each.Item.SavedUtc)
            .Select(each => each.Order!)];
        return open.FirstOrDefault(order => order.CallId == callId) ?? open.FirstOrDefault();
    }

    // Called under the gate.
    private OrderResult AddLine(UnsentStore unsent, Guid orderId, Guid locationId, Guid productId, decimal quantity, Guid? callId)
    {
        // A Sent order is read-only, and its id must never start a second order the server would treat as a re-send.
        OrderStanding standing = Standing(unsent, orderId, out _, out TabletOrder? existing);
        if (OrderTransitions.After(standing, OrderAction.AddLine) is null)
            return OrderResult.Failure(OrderTransitions.Refusal(standing, OrderAction.AddLine));
        if (existing is not null && (existing.LocationId != locationId)) return OrderResult.Failure(NotChangeable);

        SnapshotStore snapshot = new(workspace, clock);
        if (snapshot.ReadLocation(locationId) is not { IsAssigned: true }) return OrderResult.Failure(NotAssigned);

        OrderableProduct? product = snapshot.ReadOrderableProduct(productId, PriceToday());
        if (product is null) return OrderResult.Failure(NotDownloaded);
        if (product.Price is not decimal price) return OrderResult.Failure(NoPrice);

        if (!QuantityRules.TryCreate(product.Product.Unit, product.Product.QuantityStep, product.Product.MinimumQuantity,
                out QuantityRules? rules, out _))
            return OrderResult.Failure(RulesUnreadable);
        string unit = rules.Unit.ToCode();
        OrderLine? current = existing?.Lines.FirstOrDefault(line => line.ProductId == productId);
        if (current is not null && current.Unit != unit) return OrderResult.Failure(UnitChanged);
        // A new line must meet the product's minimum and step. Adding to a line only needs whole steps,
        // and the new total is what must meet the rules.
        string? invalid = current is null
            ? rules.ValidateOrder(quantity).Error
            : rules.ValidateIncrement(quantity).Error;
        if (invalid is not null) return OrderResult.Failure(invalid);
        decimal total = (current?.Quantity ?? 0m) + quantity;
        QuantityValidationResult sum = rules.ValidateOrder(total);
        if (!sum.IsValid) return OrderResult.Failure(sum.Error!);

        OrderSnapshotRef source = new(product.Manifest.Version, product.Manifest.BuiltUtc);
        IReadOnlyList<OrderLine> lines = current is null
            ? [.. existing?.Lines ?? [], new OrderLine(Guid.NewGuid(), productId, product.Product.Code, product.Product.Name, total, unit,
                new OrderLinePrice(price, OrderContract.BasePriceKind, OrderContract.BasePriceLabel,
                    [new OrderPriceCandidate(OrderContract.BasePriceKind, OrderContract.BasePriceLabel, price, true)]), source)]
            : [.. existing!.Lines.Select(line => line.ProductId == productId ? line with { Quantity = total } : line)];
        TabletOrder order = existing is null
            ? new(OrderContract.Schema, OrderContract.CurrentVersion, orderId, OrderState.InProgress, locationId, locationId,
                workspace.RepSubject, Today(), callId, source, lines)
            : existing with { Lines = lines, CallId = existing.CallId ?? callId };
        Save(unsent, order);
        return new(order, null);
    }

    /// <summary>Moves an In Progress order to Ready to Send, so it uploads at the next Sync. An order with no
    /// lines cannot be ready. It needs no connection and no current assignment, because an order is judged against the
    /// snapshot it was built from.</summary>
    public OrderResult MarkReadyToSend(Guid orderId) => Change(orderId, OrderAction.MarkReady, (_, order) =>
        order.Lines.Count == 0 ? OrderResult.Failure(EmptyOrder) : Saved(order with { State = OrderState.ReadyToSend }));

    /// <summary>Returns an unsynced Ready to Send order to In Progress so the rep can change it (US-013 S2). Refused once an
    /// upload has claimed it: it stays locked until a Sync gets the server's answer.</summary>
    public OrderResult Reopen(Guid orderId) =>
        Change(orderId, OrderAction.Reopen, (_, order) => Saved(order with { State = OrderState.InProgress }));

    /// <summary>Sets a line of an In Progress order to <paramref name="quantity"/> (US-013 S1). The line keeps the price,
    /// unit and snapshot it captured (D6); the quantity must follow the product's rules on the current snapshot, so a
    /// product no longer downloaded can only be removed (D5). The shop needn't still be assigned (D4).</summary>
    public OrderResult ChangeQuantity(Guid orderId, Guid lineId, decimal quantity) =>
        Change(orderId, OrderAction.ChangeQuantity, (saved, order) =>
        {
            if (order.Lines.FirstOrDefault(line => line.Id == lineId) is not { } line) return OrderResult.Failure(NoSuchLine);
            OrderableProduct? product = new SnapshotStore(workspace, clock).ReadOrderableProduct(line.ProductId, PriceToday());
            if (product is null) return OrderResult.Failure(CantChangeMissing);
            if (!QuantityRules.TryCreate(product.Product.Unit, product.Product.QuantityStep, product.Product.MinimumQuantity,
                    out QuantityRules? rules, out _)) return OrderResult.Failure(RulesUnreadable);
            if (rules.Unit.ToCode() != line.Unit) return OrderResult.Failure(UnitChanged);
            QuantityValidationResult valid = rules.ValidateOrder(quantity);
            if (!valid.IsValid) return OrderResult.Failure(valid.Error!);
            return Saved(order with { Lines = [.. order.Lines.Select(each => each.Id == lineId ? each with { Quantity = quantity } : each)] });
        });

    /// <summary>Takes a line off an In Progress order. The last line goes only with the order: without
    /// <paramref name="deleteOrderIfLast"/> the rep is asked "Remove the last line and delete this order?" (US-013 S4).</summary>
    public OrderResult RemoveLine(Guid orderId, Guid lineId, bool deleteOrderIfLast) =>
        Change(orderId, OrderAction.RemoveLine, (item, order) =>
            order.Lines.All(line => line.Id != lineId) ? OrderResult.Failure(NoSuchLine)
            : order.Lines.Count > 1 ? Saved(order with { Lines = [.. order.Lines.Where(line => line.Id != lineId)] })
            : deleteOrderIfLast ? Remove(item)
            : OrderResult.Ask(LastLinePrompt));

    /// <summary>Deletes an In Progress or Ready to Send order once the rep confirms (US-013 S3). It leaves the tablet and
    /// nothing is sent. An order an upload has claimed, a refused one and a Sent one are never deleted here.</summary>
    public OrderResult Delete(Guid orderId, bool confirmed) =>
        Change(orderId, OrderAction.Delete, (item, _) => confirmed ? Remove(item) : OrderResult.Ask(DeletePrompt));

    // Every change to a saved order: read it under the gate, check the table, then apply and save.
    private OrderResult Change(Guid orderId, OrderAction action, Func<UnsentItem, TabletOrder, OrderResult> apply)
    {
        Gate.Wait();
        try
        {
            UnsentStore unsent = new(workspace);
            OrderStanding standing = Standing(unsent, orderId, out UnsentItem? item, out TabletOrder? order);
            if (OrderTransitions.After(standing, action) is null) return OrderResult.Failure(OrderTransitions.Refusal(standing, action));
            OrderResult result = apply(item!, order!);
            if (result is { Succeeded: true, Order: { } changed }) Save(unsent, changed);
            return result;
        }
        finally { Gate.Release(); }
    }

    private static OrderResult Saved(TabletOrder order) => new(order, null);

    // The delete re-checks, in the same statement, that the item is unchanged, unrefused and unclaimed.
    private OrderResult Remove(UnsentItem item) =>
        new UnsentStore(workspace).DeleteUnclaimed(item) ? OrderResult.Gone : OrderResult.Failure(NotChangeable);

    // The order's date is the tablet's own day (unchanged); a line's price follows the business day in Ireland (WI-059 D1, D7).
    private DateOnly Today() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    private DateOnly PriceToday() => BusinessCalendar.Today(clock.GetUtcNow());

    private void Save(UnsentStore unsent, TabletOrder order) => unsent.Save(new(order.Id, UnsentKind.Order, clock.GetUtcNow(),
        JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder)));

    internal static TabletOrder? Parse(UnsentItem? item)
    {
        if (item is not { Kind: UnsentKind.Order }) return null;
        try
        {
            TabletOrder? order = JsonSerializer.Deserialize(item.Json, OrderJsonContext.Default.TabletOrder);
            return order is { Schema: OrderContract.Schema, Lines: not null } && order.Id == item.Id ? order : null;
        }
        catch (JsonException) { return null; }
    }
}
