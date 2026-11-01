using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

public sealed partial class TabletSessionScreenTests
{
    private static UnsentView InProgressReview()
    {
        UnsentView rejected = WaitingItem();
        TabletOrder order = rejected.Order! with { State = OrderState.InProgress };
        OrderLine first = order.Lines[0];
        order = order with { Lines = [first, first with { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = "Still available" }] };
        return rejected with
        {
            Item = rejected.Item with { Rejection = null, Json = """{"state":"InProgress"}""" },
            Order = order, MissingProductIds = new HashSet<Guid> { first.ProductId }
        };
    }

    [Fact]
    public async Task Should_MarkOnlyMissingLinesUnavailable_When_AStoredOrderIsReviewed()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadViews = _ => [InProgressReview()];
        screen.Tablet.ReadSummary = _ => new(1, 1, 0);
        await screen.Open(new());
        Assert.DoesNotContain("Mark ready to send", screen.Renderer.Buttons(screen.Id));

        await screen.ClickLink("Quinn's Centra");

        string text = screen.Renderer.Text(screen.Id);
        Assert.Equal(1, text.Split("No longer available to you", StringSplitOptions.None).Length - 1);
        Assert.Contains("Vitamin D", text);
        Assert.Contains("6 Each", text);
        Assert.Contains("12.00", text);
        Assert.Contains("Still available", text);
        Assert.Contains("Mark ready to send", screen.Renderer.Buttons(screen.Id));
        Assert.DoesNotContain("Add to order", screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_MarkAnInProgressOrderReady_When_TheRepUsesSyncReview()
    {
        await using Screen screen = new(typeof(Sync));
        UnsentView view = InProgressReview();
        bool ready = false;
        screen.Tablet.ReadViews = _ => [ready ? view with
            { Item = view.Item with { Json = """{"state":"ReadyToSend"}""" }, Order = view.Order! with { State = OrderState.ReadyToSend } } : view];
        screen.Tablet.ReadSummary = _ => ready ? new(1, 0, 1) : new(1, 1, 0);
        screen.Tablet.ReviewOrder = (rep, id, version) =>
        {
            Assert.Equal(("colm", view.Item.Id, 0L), (rep, id, version));
            ready = true; return Task.FromResult(true);
        };
        screen.Tablet.ChangeRejected = (_, _, _, _, _) => throw new InvalidOperationException("An In Progress order is not a rejected item.");
        await screen.Open(new());
        await screen.ClickLink("Quinn's Centra");

        await screen.Click("Mark ready to send");

        Assert.True(ready);
        Assert.Contains("Ready to Send. It goes at your next Sync.", screen.Renderer.Text(screen.Id));
        Assert.Contains("Ready to Send (1)", screen.Renderer.Text(screen.Id));
        Assert.Null(screen.Tablet.Progress); // The review action does not start Sync.
    }

    [Fact]
    public async Task Should_LeaveTheOrderUnchanged_When_TheReviewPageBelongsToAnEarlierSession()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadViews = _ => [InProgressReview()];
        int changes = 0;
        screen.Tablet.ReviewOrder = (_, _, _) => { changes++; return Task.FromResult(true); };
        await screen.Open(new());
        await screen.ClickLink("Quinn's Centra");
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.SessionVersion++);

        await screen.Click("Mark ready to send");

        Assert.Equal(0, changes);
    }
}
