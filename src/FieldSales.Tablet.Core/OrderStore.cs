using System.Collections.Concurrent;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Quantities;

namespace FieldSales.Tablet.Core;

/// <summary>The outcome of an order change: the saved order, or a message the rep can act on.</summary>
public sealed record OrderResult(TabletOrder? Order, string? Error)
{
    public bool Succeeded => Error is null;
    internal static OrderResult Failure(string error) => new(null, error);
}

/// <summary>The rep's orders, kept as Unsent Orders in their own database (T-5.1.1).</summary>
/// <remarks>
/// Nothing is stored until the first line is added; every change then saves at once as In Progress, so a
/// stopped app loses nothing. A line captures the product's code and name, the quantity with its unit, the base
/// price in effect today with its source, and the snapshot it came from; none of that changes afterwards.
/// Adding a product already on the order raises that line's quantity and keeps what the line captured first.
/// Changes are serialized per rep, because a change reads the saved order and writes it back.
/// </remarks>
public sealed class OrderStore
{
    public const string NotAssigned = "This shop isn't assigned to you.";
    public const string NotDownloaded = "This product isn't in your downloaded catalogue.";
    public const string NoPrice = "No price yet — this product can't be ordered.";
    public const string NotChangeable = "This order can't be changed here.";
    public const string AlreadyReady = "This order is ready to send.";
    public const string EmptyOrder = "Add at least one product";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    private readonly RepWorkspace workspace;
    private readonly TimeProvider clock;

    public OrderStore(RepWorkspace workspace, TimeProvider? clock = null)
    {
        this.workspace = workspace;
        this.clock = clock ?? TimeProvider.System;
    }

    private SemaphoreSlim Gate => Gates.GetOrAdd(workspace.UnsentPath, _ => new(1, 1));

    /// <summary>The order with this id, or null when none is saved or the saved item is not a readable order.</summary>
    public TabletOrder? Find(Guid orderId) => Parse(new UnsentStore(workspace).Find(orderId));

    /// <summary>Adds <paramref name="quantity"/> of a product to the order, starting the order at
    /// <paramref name="locationId"/> when it has no line yet. The shop must be assigned to the rep on the
    /// current snapshot, and the quantity must follow the product's rules.</summary>
    public OrderResult AddLine(Guid orderId, Guid locationId, Guid productId, decimal quantity)
    {
        Gate.Wait();
        try
        {
            UnsentStore unsent = new(workspace);
            UnsentItem? item = unsent.Find(orderId);
            TabletOrder? existing = Parse(item);
            if (item is not null && existing is null) return OrderResult.Failure(NotChangeable);
            if (existing is not null && (existing.LocationId != locationId)) return OrderResult.Failure(NotChangeable);
            if (existing is { State: not OrderState.InProgress }) return OrderResult.Failure(AlreadyReady);

            SnapshotStore snapshot = new(workspace, clock);
            if (snapshot.ReadLocation(locationId) is not { IsAssigned: true }) return OrderResult.Failure(NotAssigned);

            OrderableProduct? product = snapshot.ReadOrderableProduct(productId, Today());
            if (product is null) return OrderResult.Failure(NotDownloaded);
            if (product.Price is not decimal price) return OrderResult.Failure(NoPrice);

            if (!QuantityRules.TryCreate(product.Product.Unit, product.Product.QuantityStep, product.Product.MinimumQuantity,
                    out QuantityRules? rules, out _))
                return OrderResult.Failure("This product's ordering rules couldn't be read.");
            string unit = rules.Unit.ToCode();
            OrderLine? current = existing?.Lines.FirstOrDefault(line => line.ProductId == productId);
            if (current is not null && current.Unit != unit)
                return OrderResult.Failure("This product's unit changed since it was added. Start a new order for it.");
            // A new line must meet the product's minimum and step. Adding to a line only needs whole steps,
            // and the new total is what must meet the rules.
            string? invalid = current is null
                ? rules.ValidateOrder(quantity).Error
                : quantity <= 0 || quantity % rules.Step.Amount != 0 ? rules.OrderValidationMessage : null;
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
                    workspace.RepSubject, Today(), null, source, lines)
                : existing with { Lines = lines };
            Save(unsent, order);
            return new(order, null);
        }
        finally { Gate.Release(); }
    }

    /// <summary>Moves an In Progress order to Ready to Send, so it uploads at the next Sync. An order with no
    /// lines cannot be ready. This is the only transition: reopening, editing and deleting arrive with WI-046.
    /// It needs no connection and no current assignment, because an order is judged against the snapshot it was
    /// built from.</summary>
    public OrderResult MarkReadyToSend(Guid orderId)
    {
        Gate.Wait();
        try
        {
            UnsentStore unsent = new(workspace);
            UnsentItem? item = unsent.Find(orderId);
            TabletOrder? order = Parse(item);
            if (item is not null && order is null) return OrderResult.Failure(NotChangeable);
            if (order is null || order.Lines.Count == 0) return OrderResult.Failure(EmptyOrder);
            if (order.State != OrderState.InProgress) return OrderResult.Failure(AlreadyReady);
            TabletOrder ready = order with { State = OrderState.ReadyToSend };
            Save(unsent, ready);
            return new(ready, null);
        }
        finally { Gate.Release(); }
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    private void Save(UnsentStore unsent, TabletOrder order) => unsent.Save(new(order.Id, UnsentKind.Order, clock.GetUtcNow(),
        JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder)));

    private static TabletOrder? Parse(UnsentItem? item)
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
