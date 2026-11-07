using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-046 (T-5.2.1, REP-US-013) on the order screen: a saved order opened by id offers only what its standing allows.</summary>
public sealed partial class NewOrderComponentTests
{
    private static readonly ProductSearchResult ColdFlu =
        new(Guid.Parse("66000000-0000-0000-0000-000000000010"), "CFR-16", "Cold & Flu Relief 16s", "Health > Remedies", "Each", 3.00m);

    private static readonly ProductSearchResult[] EditCatalogue = [ColdFlu, .. Catalogue];

    private static OrderLine LineFor(ProductSearchResult product, decimal quantity) => new(Guid.NewGuid(), product.Id, product.Code, product.Name,
        quantity, product.Unit, new(product.Price!.Value, "Base", "Base price", [new("Base", "Base price", product.Price.Value, true)]), Snapshot);

    /// <summary>Stands in for OrderStore's changes to one saved order, recording what the screen asked for.</summary>
    private sealed class SavedOrder
    {
        public SavedOrder(OrderState state, params OrderLine[] lines) => Order = new(OrderContract.Schema, OrderContract.CurrentVersion,
            Guid.NewGuid(), state, ShopId, ShopId, "colm", new DateOnly(2026, 10, 29), null, Snapshot, lines);

        public TabletOrder Order { get; private set; }
        public OrderStanding? Standing { get; init; }
        public DateTimeOffset? SentUtc { get; init; }
        public List<(Guid Line, decimal Quantity)> Changes { get; } = [];
        public List<(Guid Line, bool Confirmed)> Removals { get; } = [];
        public List<bool> Deletions { get; } = [];
        public int Reopens { get; private set; }

        public async Task<Screen> OpenAsync(OfflineLocationDetail? shop = null)
        {
            Screen screen = new(new FakeOrders(), assigned: true, new ProductSearchIndex("generation", EditCatalogue));
            if (shop is not null) screen.Tablet.Read = (_, _) => shop;
            screen.Tablet.FindView = (_, id) => id == Order.Id ? new(Order, Standing ?? StandingOf(Order), SentUtc) : null;
            screen.Tablet.ChangeLine = (_, _, line, quantity) =>
            {
                Changes.Add((line, quantity));
                Order = Order with { Lines = [.. Order.Lines.Select(each => each.Id == line ? each with { Quantity = quantity } : each)] };
                return new(Order, null);
            };
            screen.Tablet.RemoveLine = (_, _, line, confirmed) =>
            {
                Removals.Add((line, confirmed));
                if (Order.Lines.Count > 1) { Order = Order with { Lines = [.. Order.Lines.Where(each => each.Id != line)] }; return new(Order, null); }
                return confirmed ? new(null, null) { Deleted = true } : new(null, OrderStore.LastLinePrompt) { NeedsConfirmation = true };
            };
            screen.Tablet.Reopen = (_, _) => { Reopens++; Order = Order with { State = OrderState.InProgress }; return new(Order, null); };
            screen.Tablet.DeleteWork = (_, _, confirmed) =>
            {
                Deletions.Add(confirmed);
                return confirmed ? new(null, null) { Deleted = true } : new(null, OrderStore.DeletePrompt) { NeedsConfirmation = true };
            };
            await screen.ShowOrder(Order.Id);
            await screen.Until(() => screen.Renderer.Text(screen.Id).Contains(Order.Lines[0].ProductName));
            return screen;
        }

        private static OrderStanding StandingOf(TabletOrder order) =>
            order.State == OrderState.ReadyToSend ? OrderStanding.ReadyToSend : OrderStanding.InProgress;
    }

    [Fact]
    public async Task Should_SaveAndShow36_When_ColdFluRelief16sOnAnInProgressOrderIsChangedFrom24To36()
    {
        SavedOrder saved = new(OrderState.InProgress, LineFor(ColdFlu, 24m));
        await using Screen screen = await saved.OpenAsync();
        Assert.Contains("1 line · €72.00", screen.Renderer.Text(screen.Id));

        await screen.ClickLabelled("Change Cold & Flu Relief 16s");
        Assert.Contains("24", screen.Renderer.AttributeValues(screen.Id, "value")); // Opens with what is on the order.
        await screen.Type("edit-quantity", "36");
        await screen.Click("Save");
        await screen.Until(() => screen.Renderer.Text(screen.Id).Contains("1 line · €108.00"));

        Assert.Equal([(saved.Order.Lines[0].Id, 36m)], saved.Changes);
        Assert.DoesNotContain("edit-quantity", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.Contains("36", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_ReturnToInProgressAndAcceptEdits_When_AnUnsyncedReadyToSendOrderForMurphysPharmacyIsReopened()
    {
        OfflineLocationDetail murphys = new(new SnapshotLocation(ShopId, "Murphy's Pharmacy", Guid.NewGuid(), "Murphy Retail",
            new(Guid.NewGuid(), "Aughrim", "Wicklow"), null, null), true, false);
        SavedOrder saved = new(OrderState.ReadyToSend, LineFor(ColdFlu, 24m));
        await using Screen screen = await saved.OpenAsync(murphys);
        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Murphy's Pharmacy", text);
        Assert.Contains("Ready to Send. It goes to head office at your next Sync.", text);
        Assert.Equal(["(i)", "Close", "Reopen", "Delete order"], screen.Renderer.Buttons(screen.Id)); // (i) only explains the line's price (WI-065).

        await screen.Click("Reopen");
        await screen.Until(() => screen.Renderer.Buttons(screen.Id).Contains("Mark Ready to Send"));

        Assert.Equal(1, saved.Reopens);
        Assert.NotEmpty(screen.Renderer.LabelledButton(screen.Id, "Change Cold & Flu Relief 16s"));
        Assert.Contains("product-search", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.DoesNotContain("Ready to Send. It goes", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_RemoveTheOrderAndSendNothing_When_DeleteOrderIsConfirmedOnAnInProgressOrderWith3Lines()
    {
        SavedOrder saved = new(OrderState.InProgress, LineFor(ColdFlu, 24m), LineFor(Catalogue[0], 6m), LineFor(Catalogue[1], 1.5m));
        await using Screen screen = await saved.OpenAsync();

        await screen.Click("Delete order");
        Assert.Contains(OrderStore.DeletePrompt, screen.Renderer.Text(screen.Id));
        Assert.Empty(saved.Deletions); // Nothing happens until the rep confirms.
        Assert.DoesNotContain("Save and close", screen.Renderer.Buttons(screen.Id));
        await screen.Click("Delete order");
        await screen.Until(() => screen.Navigation.Destinations.Count > 0);

        Assert.Equal([true], saved.Deletions);
        Assert.Equal([$"/locations/{ShopId}"], screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_KeepTheOrder_When_DeleteOrderIsCancelled()
    {
        SavedOrder saved = new(OrderState.InProgress, LineFor(ColdFlu, 24m), LineFor(Catalogue[0], 6m));
        await using Screen screen = await saved.OpenAsync();

        await screen.Click("Delete order");
        await screen.Click("Keep order");

        Assert.Empty(saved.Deletions);
        Assert.DoesNotContain(OrderStore.DeletePrompt, screen.Renderer.Text(screen.Id));
        Assert.Contains("Delete order", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("2 lines", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_AskRemoveTheLastLineAndDeleteThisOrder_When_TheOnlyLineIsRemoved()
    {
        SavedOrder saved = new(OrderState.InProgress, LineFor(ColdFlu, 24m));
        await using Screen screen = await saved.OpenAsync();

        await screen.ClickLabelled("Remove Cold & Flu Relief 16s");
        Assert.Contains("Remove the last line and delete this order?", screen.Renderer.Text(screen.Id));
        Assert.Empty(saved.Removals);
        await screen.Click("Remove and delete");
        await screen.Until(() => screen.Navigation.Destinations.Count > 0);

        Assert.Equal([(saved.Order.Lines[0].Id, true)], saved.Removals);
        Assert.Equal([$"/locations/{ShopId}"], screen.Navigation.Destinations);
    }

    [Fact]
    public async Task Should_ShowNoEditReopenOrDeleteControls_When_ASentOrderIsOpened()
    {
        SavedOrder saved = new(OrderState.ReadyToSend, LineFor(ColdFlu, 24m))
            { Standing = OrderStanding.Sent, SentUtc = new DateTimeOffset(2026, 10, 29, 7, 42, 0, TimeSpan.Zero) };
        await using Screen screen = await saved.OpenAsync();

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Cold & Flu Relief 16s", text);
        Assert.Contains("It can't be changed on this tablet.", text);
        Assert.Equal(["(i)", "Close"], screen.Renderer.Buttons(screen.Id)); // (i) only explains the line's price (WI-065).
        Assert.DoesNotContain("product-search", screen.Renderer.AttributeValues(screen.Id, "id"));
        Assert.DoesNotContain("Change Cold & Flu Relief 16s", screen.Renderer.AttributeValues(screen.Id, "aria-label"));
    }
}
