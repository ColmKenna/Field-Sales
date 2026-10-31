using FieldSales.Ordering.Contracts;
using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

public sealed partial class TabletSessionScreenTests
{
    [Fact]
    public async Task Should_IncludeNeedsAttention_When_ShowingHomesUnsentCount()
    {
        await using Screen screen = new(typeof(Home));
        screen.Tablet.ReadSummary = _ => new(3, 1, 1, 1);
        await screen.Open(new());
        Assert.Contains("Unsent: 3 · 1 needs attention", screen.Renderer.Text(screen.Id));
    }

    private static UnsentView WaitingItem(UnsentKind kind = UnsentKind.Order, string? fault = "incomplete")
    {
        Guid id = Guid.NewGuid();
        OrderSnapshotRef snapshot = new("1.2", DateTimeOffset.Parse("2026-10-31T07:00:00Z"));
        TabletOrder? order = kind == UnsentKind.Order
            ? new(OrderContract.Schema, OrderContract.CurrentVersion, id, OrderState.ReadyToSend, Guid.NewGuid(), Guid.NewGuid(),
                "colm", new(2026, 10, 31), null, snapshot,
                [new(Guid.NewGuid(), Guid.NewGuid(), "VITD", "Vitamin D", 6, "Each", new(12, "Base", "Base price", []), snapshot)]) : null;
        return new(new(id, kind, DateTimeOffset.Parse("2026-10-31T07:30:00Z"), """{"state":"ReadyToSend"}""")
            { Rejection = new(fault, "Private database fault for support", 400, DateTimeOffset.UtcNow) }, "Quinn's Centra", order, "In person");
    }

    [Fact]
    public async Task Should_ShowNeedsAttentionFirst_When_SeveralStatesContainItems()
    {
        await using Screen screen = new(typeof(Sync));
        UnsentView rejected = WaitingItem(), another = WaitingItem(UnsentKind.Call);
        UnsentView ready = rejected with { Item = rejected.Item with { Id = Guid.NewGuid(), Rejection = null } };
        UnsentView building = ready with { Item = ready.Item with { Id = Guid.NewGuid(), Json = """{"state":"InProgress"}""" } };
        screen.Tablet.ReadViews = _ => [building, ready, rejected, another];
        screen.Tablet.ReadSummary = _ => new(4, 1, 1, 2);

        await screen.Open(new());

        string text = screen.Renderer.Text(screen.Id);
        Assert.True(text.IndexOf("Needs Attention (2)", StringComparison.Ordinal) < text.IndexOf("Ready to Send (1)", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Ready to Send (1)", StringComparison.Ordinal) < text.IndexOf("In Progress (1)", StringComparison.Ordinal));
        Assert.Equal(2, screen.Renderer.Buttons(screen.Id).Count(button => button == "Open"));
        Assert.Equal(2, screen.Renderer.Buttons(screen.Id).Count(button => button == "Delete"));
        Assert.Contains("2 need attention", text);
    }

    [Theory]
    [InlineData("incomplete", UploadFaultMessages.Incomplete)]
    [InlineData("corrupt", UploadFaultMessages.ContactOffice)]
    public async Task Should_ShowPlainReasonAndActions_When_UploadIsRejected(string fault, string sentence)
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadViews = _ => [WaitingItem(fault: fault)];
        screen.Tablet.ReadSummary = _ => new(1, 0, 0, 1);
        await screen.Open(new());
        Assert.Contains(sentence, screen.Renderer.Text(screen.Id));
        Assert.Contains("Unsent: 1 · 1 needs attention", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Private database fault", screen.Renderer.Text(screen.Id));
        Assert.Contains("Open", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("Delete", screen.Renderer.Buttons(screen.Id));
    }

    [Fact]
    public async Task Should_RequireReviewBeforeRemarking_When_ARejectedOrderIsOpened()
    {
        await using Screen screen = new(typeof(Sync));
        UnsentView rejected = WaitingItem();
        bool changed = false;
        screen.Tablet.ReadViews = _ => changed ? [] : [rejected];
        screen.Tablet.ReadSummary = _ => changed ? new(0, 0, 0) : new(1, 0, 0, 1);
        screen.Tablet.ChangeRejected = (rep, id, version, delete, confirmed) =>
        {
            Assert.Equal(("colm", rejected.Item.Id, 0L, false), (rep, id, version, delete));
            changed = true;
            return Task.FromResult(true);
        };
        await screen.Open(new());
        Assert.DoesNotContain("Mark ready to send", screen.Renderer.Buttons(screen.Id));

        await screen.Click("Open");
        Assert.Contains("Vitamin D", screen.Renderer.Text(screen.Id));
        Assert.Contains("6 Each", screen.Renderer.Text(screen.Id));
        await screen.Click("Mark ready to send");

        Assert.True(changed);
        Assert.Contains("Ready to Send. It goes at your next Sync.", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_KeepRejectedItem_When_DeletionIsCancelledOnScreen()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadViews = _ => [WaitingItem(UnsentKind.Call)];
        int changes = 0;
        screen.Tablet.ChangeRejected = (_, _, _, _, _) => { changes++; return Task.FromResult(true); };
        await screen.Open(new());

        await screen.Click("Delete");
        Assert.Contains("Delete this Call?", screen.Renderer.Text(screen.Id));
        await screen.Click("Keep item");

        Assert.Equal(0, changes);
        Assert.Contains("Open", screen.Renderer.Buttons(screen.Id));
    }

    [Theory]
    [InlineData(UnsentKind.Call)]
    [InlineData(UnsentKind.Order)]
    public async Task Should_DeleteRejectedItem_When_DeletionIsConfirmedOnScreen(UnsentKind kind)
    {
        await using Screen screen = new(typeof(Sync));
        UnsentView rejected = WaitingItem(kind);
        bool removed = false;
        screen.Tablet.ReadViews = _ => removed ? [] : [rejected];
        screen.Tablet.ReadSummary = _ => removed ? new(0, 0, 0) : new(1, 0, 0, 1);
        screen.Tablet.ChangeRejected = (rep, id, version, delete, confirmed) =>
        {
            Assert.Equal(("colm", rejected.Item.Id, 0L, true, true), (rep, id, version, delete, confirmed));
            removed = true;
            return Task.FromResult(true);
        };
        await screen.Open(new());

        await screen.Click("Delete");
        await screen.Click("Delete item");

        Assert.True(removed);
        Assert.Contains("No unsent items.", screen.Renderer.Text(screen.Id));
        Assert.DoesNotContain("Needs Attention (1)", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_StayOnSync_When_PreviousItemsStillNeedAttention()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadViews = _ => [WaitingItem()];
        screen.Tablet.ReadSummary = _ => new(1, 0, 0, 1);
        await screen.Open(new());

        await screen.Click("Sync now");

        Assert.Empty(screen.Navigation.Destinations);
        Assert.Contains("Needs Attention (1)", screen.Renderer.Text(screen.Id));
    }

    [Fact]
    public async Task Should_NotDelete_When_ConfirmationBelongsToAnEarlierSession()
    {
        await using Screen screen = new(typeof(Sync));
        screen.Tablet.ReadViews = _ => [WaitingItem(UnsentKind.Call)];
        int changes = 0;
        screen.Tablet.ChangeRejected = (_, _, _, _, _) => { changes++; return Task.FromResult(true); };
        await screen.Open(new());
        await screen.Click("Delete");

        // Even if a sign-in returns as the same rep before the old page is removed, its confirmation is stale.
        await screen.Renderer.Dispatcher.InvokeAsync(() => screen.Tablet.SessionVersion++);
        await screen.Click("Delete item");

        Assert.Equal(0, changes);
    }
}
