using System.Globalization;
using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>Where a product marked Low on a Call stands against the order (T-5.1.4, US-012 S7). REPLACED (E10) and CAN'T ADD (E8)
/// join with their items.</summary>
public enum LowItemState { NotAdded, Added, Removed }

/// <summary>One product marked Low on the Call, with its one state. <paramref name="Line"/> is its line on the order when
/// <see cref="LowItemState.Added"/>. <paramref name="MarkedLowOn"/> is the date of the earlier Call a carried gap was left on
/// (T-5.1.5).</summary>
public sealed record LowItem(CallStockEntry Entry, LowItemState State, OrderLine? Line, DateOnly? MarkedLowOn = null)
{
    /// <summary>"not added", "ADDED - 24" (or "ADDED - 2.5 kg"), "REMOVED".</summary>
    public string StateText => State switch
    {
        LowItemState.Added => $"ADDED - {Amount(Line!.Quantity, Line.Unit)}",
        LowItemState.Removed => "REMOVED",
        _ => "not added"
    };

    /// <summary>The line under the name: what was recorded, that the rep took it off the order, or, for a gap carried from an
    /// earlier Call, when it was marked Low ("marked Low 15 Sep").</summary>
    public string Detail => State == LowItemState.Removed ? "you took this off the order" : WhatWasRecorded;

    /// <summary>What the quantity popover shows about the item: the count, or when a carried gap was marked Low.</summary>
    public string WhatWasRecorded => MarkedLowOn is { } on
        ? $"marked Low {on.ToString("d MMM", CultureInfo.InvariantCulture)}" : Recorded(Entry);

    /// <summary>Not added and REMOVED items offer Add; an ADDED one is changed on the order's own line.</summary>
    public bool CanAdd => State != LowItemState.Added;

    /// <summary>"counted 3", "counted 2.5 kg" or "not counted", as the quantity popover reads it too.</summary>
    public static string Recorded(CallStockEntry entry) =>
        entry.Count is { } count ? $"counted {Amount(count, entry.Unit)}" : "not counted";

    private static string Amount(decimal quantity, string unit) =>
        quantity.ToString(unit == "Each" ? "0.######" : "0.0#####", CultureInfo.InvariantCulture) + (unit == "Each" ? "" : $" {unit}");
}

/// <summary>The Low tab's list: every product marked Low on the Call, in the Call's order, and how many still want something.
/// <paramref name="CarriedFrom"/> is set when the items are gaps carried from an earlier Call to an order with no Call (T-5.1.5).</summary>
public sealed record LowList(IReadOnlyList<LowItem> Items, CarriedLow? CarriedFrom = null)
{
    public static LowList None { get; } = new([]);

    /// <summary>The tab's count: not added only (US-012 S8); ADDED and REMOVED are done with. CAN'T ADD will count with E8.</summary>
    public int Outstanding => Items.Count(item => item.State == LowItemState.NotAdded);
}

/// <summary>Works out each Low item's state from one source, so the Low tab can never disagree with the order (T-5.1.4).</summary>
public static class LowItems
{
    /// <summary>Each entry marked Low gets one state: ADDED whenever the order has a line for the product, however it got there;
    /// otherwise REMOVED when the Call recorded that its line was taken off (<paramref name="takenOff"/>); otherwise not added.
    /// Unticking Low takes an item off the list and never touches the order (S13).</summary>
    public static LowList Of(IEnumerable<CallStockEntry> entries, TabletOrder? order, IReadOnlyCollection<Guid> takenOff) =>
        new([.. entries.Where(entry => entry.Low).Select(entry =>
            order?.Lines.FirstOrDefault(line => line.ProductId == entry.ProductId) is { } line
                ? new LowItem(entry, LowItemState.Added, line)
                : new LowItem(entry, takenOff.Contains(entry.ProductId) ? LowItemState.Removed : LowItemState.NotAdded, null))]);

    /// <summary>The gaps an earlier Call left, carried to an order with no Call (T-5.1.5): ADDED when on this order, else not
    /// added. Nothing is ever REMOVED here: that Call isn't being recorded, so nothing was taken off for it.</summary>
    public static LowList Carried(CarriedLow carried, TabletOrder? order) =>
        new([.. Of(carried.Entries, order, []).Items.Select(item => item with { MarkedLowOn = carried.CalledOn })], carried);
}
