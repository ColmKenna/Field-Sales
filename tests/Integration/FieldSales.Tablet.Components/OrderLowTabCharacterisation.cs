using FieldSales.Calls.Contracts;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-053 checkpoint: T-5.1.4's Low tab, pinned before the last Call's gaps are carried to orders with no Call (T-5.1.5).
/// These must pass unchanged afterwards, alongside WI-052's <c>OrderLowTabTests</c>.</summary>
public sealed partial class TabletSessionScreenTests
{
    // C1: an order linked to a Call lists that Call's Low items only, even though another Call at the shop left other gaps.
    [Fact]
    public async Task Should_ListOnlyItsOwnCallsLowItems_When_TheOrderIsLinkedToACallAndAnotherCallAtTheShopLeftGaps()
    {
        TabletCall Saved(Guid id, DateOnly on, params ProductSearchResult[] low) => new(CallContract.Schema, 1, id, MurphysId, "colm", on,
            new DateTimeOffset(on.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero), CallChannel.InPerson, new("v1", DateTimeOffset.UnixEpoch),
            null, [.. low.Select(product => new CallStockEntry(product.Id, product.Code, product.Name, product.Unit, 1m, Low: true))]);
        TabletCall linked = Saved(Guid.NewGuid(), new(2026, 9, 15), ColdFlu, Lozenges);
        TabletCall other = Saved(Guid.NewGuid(), new(2026, 9, 18), Plasters, Sudocrem125);
        (Screen screen, _) = await OrderEntryAsync(null, ShopOrders.Holding(linked.Id, (ColdFlu, 24m)), null, linked);
        await using Screen disposing = screen;
        Func<string, Guid, TabletCall?> both = (_, id) => id == linked.Id ? linked : id == other.Id ? other : null;
        screen.Tablet.FindCall = both;

        await OpenLowTabAsync(screen);

        Assert.Equal(["Cold & Flu Relief 16s: ADDED - 24", "Throat Lozenges 36s: not added"], LowRows(screen));
        Assert.Equal("Low (1)", LowTabLabel(screen));
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("From your stock check at Murphy's Pharmacy", text);
        Assert.DoesNotContain("Still open from your call", text);
        Assert.DoesNotContain("No stock check with this order.", text);
    }
}
