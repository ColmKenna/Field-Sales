using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-046 (T-5.2.1, REP-US-013): changing, reopening and deleting an unsent order before Sync, and the upload claim
/// that stops an order changing once its upload has started. Agreed scenarios 1–17.</summary>
[Trait("Category", "Integration")]
public sealed class OrderEditTests : IDisposable
{
    private readonly TestTablet tablet = new();
    private readonly FakeReceiver receiver;

    public OrderEditTests()
    {
        receiver = new(tablet.Server);
        tablet.Server.RespondToUpload = receiver.Respond;
    }

    public void Dispose() => tablet.Dispose();

    private OrderStore Orders() => new(tablet.Workspace(), tablet.Clock);
    private UnsentStore Unsent() => new(tablet.Workspace());
    private static string Json(TabletOrder order) => JsonSerializer.Serialize(order, OrderJsonContext.Default.TabletOrder);

    // Murphy's Pharmacy and Byrne's Chemist; Cold & Flu Relief 16s, two more Each products, and a measured tea (0.5 kg steps, 1 kg minimum).
    private static TabletSnapshot Day()
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 3, 4);
        return snapshot with
        {
            Locations = [snapshot.Locations[0] with { Name = "Murphy's Pharmacy" }, snapshot.Locations[1] with { Name = "Byrne's Chemist" },
                snapshot.Locations[2]],
            Products = [snapshot.Products[0] with { Name = "Cold & Flu Relief 16s" }, snapshot.Products[1], snapshot.Products[2],
                snapshot.Products[3] with { Name = "Loose Herbal Tea", Unit = "kg", QuantityStep = 0.5m, MinimumQuantity = 1m }]
        };
    }

    private async Task<TabletSnapshot> SyncedDayAsync()
    {
        TabletSnapshot day = Day();
        await tablet.SyncAsync(day);
        return day;
    }

    private Guid NewOrder(TabletSnapshot day, int shop, params (int Product, decimal Quantity)[] lines)
    {
        Guid id = Guid.NewGuid();
        foreach ((int product, decimal quantity) in lines)
        {
            OrderResult added = Orders().AddLine(id, day.Locations[shop].Id, day.Products[product].Id, quantity);
            Assert.True(added.Succeeded, added.Error);
        }
        return id;
    }

    private Guid ReadyOrder(TabletSnapshot day, int shop, params (int Product, decimal Quantity)[] lines)
    {
        Guid id = NewOrder(day, shop, lines);
        Assert.True(Orders().MarkReadyToSend(id).Succeeded);
        return id;
    }

    private Task<SyncOutcome> SyncAsync(TabletSnapshot day)
    {
        tablet.Clock.Advance(TimeSpan.FromMinutes(1));
        tablet.Server.Serve(EncodedSnapshot.From(day));
        return tablet.Sync().SyncAsync();
    }

    private Guid LineOf(Guid order, string name) => Orders().Find(order)!.Lines.Single(line => line.ProductName == name).Id;

    // ---- S1: change a quantity ----

    [Fact]
    public async Task Should_SaveAndShow36_When_ColdFluRelief16sOnAnInProgressOrderIsChangedFrom24To36()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = NewOrder(day, 1, (0, 24m), (1, 6m));
        OrderLine before = Orders().Find(order)!.Lines[0];
        int requests = tablet.Server.Requests.Count;

        OrderResult changed = Orders().ChangeQuantity(order, before.Id, 36m);

        Assert.True(changed.Succeeded, changed.Error);
        TabletOrder saved = Orders().Find(order)!;
        Assert.Equal(OrderState.InProgress, saved.State);
        Assert.Equal(Json(saved with { Lines = [before with { Quantity = 36m }, saved.Lines[1]] }), Json(saved)); // Price, unit and snapshot as captured (D6).
        Assert.Equal(6m, saved.Lines[1].Quantity);
        Assert.Equal(Json(saved), Json(changed.Order!));
        Assert.Equal(requests, tablet.Server.Requests.Count); // Nothing goes until the rep syncs.
    }

    [Theory]
    [InlineData(0, 24, 0, "Enter a quantity of 1 or more")]
    [InlineData(3, 1.5, 1.2, "Enter multiples of 0.5 kg")]
    public async Task Should_RefuseTheChangeAndKeep24_When_TheNewQuantityBreaksTheProductRules(int product, double start, double bad, string message)
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = NewOrder(day, 1, (product, (decimal)start));
        string before = Json(Orders().Find(order)!);

        OrderResult refused = Orders().ChangeQuantity(order, Orders().Find(order)!.Lines[0].Id, (decimal)bad);

        Assert.Equal(message, refused.Error);
        Assert.Equal(before, Json(Orders().Find(order)!));
    }

    // ---- S2: reopen ----

    [Fact]
    public async Task Should_ReturnToInProgressAndAcceptEdits_When_AnUnsyncedReadyToSendOrderForMurphysPharmacyIsReopened()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = ReadyOrder(day, 0, (0, 24m));
        Guid line = LineOf(order, "Cold & Flu Relief 16s");
        Assert.Equal(OrderStore.AlreadyReady, Orders().ChangeQuantity(order, line, 30m).Error); // Reopen comes first.

        OrderResult reopened = Orders().Reopen(order);

        Assert.True(reopened.Succeeded, reopened.Error);
        Assert.Equal(OrderState.InProgress, Orders().Find(order)!.State);
        Assert.Equal(day.Locations[0].Id, Orders().Find(order)!.LocationId);
        Assert.True(Orders().ChangeQuantity(order, line, 30m).Succeeded);
        Assert.Equal(new UnsentSummary(1, 1, 0), Unsent().Summarise());
        await SyncAsync(day);
        Assert.Empty(receiver.Arrivals); // In Progress again, so it waits until it is marked ready.
        Assert.Equal(30m, Orders().Find(order)!.Lines[0].Quantity);
    }

    // ---- S3: delete ----

    [Fact]
    public async Task Should_RemoveTheOrderAndSendNothing_When_DeleteOrderIsConfirmedOnAnInProgressOrderWith3Lines()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = NewOrder(day, 1, (0, 24m), (1, 6m), (2, 1m));

        OrderResult deleted = Orders().Delete(order, confirmed: true);

        Assert.True(deleted.Succeeded, deleted.Error);
        Assert.True(deleted.Deleted);
        Assert.Null(Orders().Find(order));
        Assert.Equal(new UnsentSummary(0, 0, 0), Unsent().Summarise());
        await SyncAsync(day);
        Assert.Empty(receiver.Arrivals);
        Assert.Empty(Unsent().ReadSent());
    }

    [Fact]
    public async Task Should_KeepTheOrder_When_DeleteOrderIsCancelled()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = NewOrder(day, 1, (0, 24m), (1, 6m), (2, 1m));
        string before = Json(Orders().Find(order)!);

        OrderResult asked = Orders().Delete(order, confirmed: false);

        Assert.True(asked.NeedsConfirmation);
        Assert.Equal(OrderStore.DeletePrompt, asked.Error);
        Assert.Equal(before, Json(Orders().Find(order)!));
    }

    [Fact]
    public async Task Should_DeleteAndSendNothing_When_AReadyToSendOrderIsDeletedBeforeSync()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = ReadyOrder(day, 0, (0, 24m));

        Assert.True(Orders().Delete(order, confirmed: true).Deleted);

        Assert.Equal(0, WorkUploader.CountWaiting(tablet.Workspace()));
        await SyncAsync(day);
        Assert.Empty(receiver.Arrivals);
    }

    // ---- S4: last line ----

    [Fact]
    public async Task Should_AskRemoveTheLastLineAndDeleteThisOrder_When_TheOnlyLineIsRemoved()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = NewOrder(day, 1, (0, 24m));
        Guid line = LineOf(order, "Cold & Flu Relief 16s");
        string before = Json(Orders().Find(order)!);

        OrderResult asked = Orders().RemoveLine(order, line, deleteOrderIfLast: false);

        Assert.True(asked.NeedsConfirmation);
        Assert.Equal("Remove the last line and delete this order?", asked.Error);
        Assert.Equal(before, Json(Orders().Find(order)!));

        OrderResult confirmed = Orders().RemoveLine(order, line, deleteOrderIfLast: true);

        Assert.True(confirmed.Deleted);
        Assert.Null(Orders().Find(order));
    }

    [Fact]
    public async Task Should_RemoveOnlyThatLine_When_OneOfSeveralLinesIsRemoved()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = NewOrder(day, 1, (0, 24m), (1, 6m), (2, 1m));
        TabletOrder before = Orders().Find(order)!;

        OrderResult removed = Orders().RemoveLine(order, before.Lines[1].Id, deleteOrderIfLast: false);

        Assert.True(removed.Succeeded, removed.Error);
        Assert.Equal(Json(before with { Lines = [before.Lines[0], before.Lines[2]] }), Json(Orders().Find(order)!));
    }

    // ---- S5: Sent ----

    [Fact]
    public async Task Should_RefuseEveryChange_When_TheOrderIsSent()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = ReadyOrder(day, 0, (0, 24m));
        Guid line = LineOf(order, "Cold & Flu Relief 16s");
        await SyncAsync(day);
        string sentBody = Assert.Single(Unsent().ReadSent()).Item.Json;

        OrderView view = Orders().Read(order)!;
        Assert.Equal(OrderStanding.Sent, view.Standing);
        Assert.NotNull(view.SentUtc);
        Assert.Equal(OrderStore.NotChangeable, Orders().Reopen(order).Error);
        Assert.Equal(OrderStore.NotChangeable, Orders().Delete(order, confirmed: true).Error);
        Assert.Equal(OrderStore.NotChangeable, Orders().ChangeQuantity(order, line, 36m).Error);
        Assert.Equal(OrderStore.NotChangeable, Orders().RemoveLine(order, line, deleteOrderIfLast: true).Error);
        Assert.Equal(OrderStore.NotChangeable, Orders().AddLine(order, day.Locations[0].Id, day.Products[1].Id, 1m).Error);
        Assert.Equal(sentBody, Assert.Single(Unsent().ReadSent()).Item.Json);
    }

    // ---- Criterion 6 and the upload claim ----

    [Fact]
    public async Task Should_RefuseChangesAndSendTheUploadedBody_When_ASyncIsUploadingTheOrder()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = ReadyOrder(day, 0, (0, 24m));
        string ready = Unsent().Find(order)!.Json;
        List<OrderResult> during = [];
        // While the order's upload is at the server, the rep tries every change.
        receiver.Interfere = (_, _) =>
        {
            during.Add(Orders().Reopen(order));
            during.Add(Orders().Delete(order, confirmed: true));
            Assert.Equal(OrderStanding.Sending, Orders().Read(order)!.Standing);
            return null;
        };

        SyncOutcome outcome = await SyncAsync(day);

        Assert.Equal(new UploadSummary(1, 1), outcome.Upload);
        Assert.All(during, refused => Assert.Equal(OrderStore.SendingUnconfirmed, refused.Error));
        Assert.Equal(ready, receiver.Stored[order]);
        Assert.Equal(ready, Assert.Single(Unsent().ReadSent()).Item.Json);
        Assert.Equal(OrderStanding.Sent, Orders().Read(order)!.Standing);
    }

    [Fact]
    public async Task Should_StayLockedUntilConfirmed_When_TheUploadConfirmationIsLost()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = ReadyOrder(day, 0, (0, 24m));
        // The server stores the order, but its confirmation never reaches the tablet.
        receiver.Interfere = (id, arrival) =>
        {
            if (arrival != 1) return null;
            receiver.Stored[id] = tablet.Server.Uploads[^1].Body;
            throw new HttpRequestException("Confirmation lost");
        };

        SyncOutcome lost = await SyncAsync(day);

        Assert.Equal(UploadStop.ConnectionLost, lost.Upload.Stop);
        Assert.Equal(OrderStanding.Sending, Orders().Read(order)!.Standing);
        Assert.Equal(OrderStore.SendingUnconfirmed, Orders().Reopen(order).Error);
        Assert.Equal(OrderStore.SendingUnconfirmed, Orders().Delete(order, confirmed: true).Error);
        Assert.False(Unsent().DeleteUnclaimed(Unsent().Find(order)!));

        SyncOutcome next = await SyncAsync(day);

        Assert.Equal(new UploadSummary(1, 1), next.Upload);
        Assert.Equal(OrderStanding.Sent, Orders().Read(order)!.Standing);
        Assert.Equal(tablet.Server.Uploads[0].Body, tablet.Server.Uploads[1].Body); // The same order, byte for byte.
        Assert.Single(receiver.Stored);
    }

    public static TheoryData<string> NeverReached => ["no-connection", "sign-in-refused", "permission-refused"];

    [Theory]
    [MemberData(nameof(NeverReached))]
    public async Task Should_AllowChangesAgain_When_TheUploadNeverReachedTheServer(string failure)
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = ReadyOrder(day, 0, (0, 24m));
        receiver.Interfere = (_, _) => failure switch
        {
            "no-connection" => throw Faults.NoSignal(),
            "sign-in-refused" => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        };

        SyncOutcome outcome = await SyncAsync(day);

        Assert.Equal(0, outcome.Upload.Sent);
        Assert.Empty(receiver.Stored);
        Assert.Equal(OrderStanding.ReadyToSend, Orders().Read(order)!.Standing);
        Assert.True(Orders().Reopen(order).Succeeded);
    }

    [Fact]
    public async Task Should_NotSendTheOrder_When_ItIsReopenedAfterTheSyncReadItsQueue()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid first = ReadyOrder(day, 0, (0, 24m));
        tablet.Clock.Advance(TimeSpan.FromMinutes(5));
        Guid second = ReadyOrder(day, 1, (1, 6m));
        OrderResult? reopened = null;
        // The Sync has queued both; while the first is at the server the rep reopens the second.
        receiver.Interfere = (id, _) =>
        {
            if (id == first) reopened = Orders().Reopen(second);
            return null;
        };

        SyncOutcome outcome = await SyncAsync(day);

        Assert.True(reopened!.Succeeded, reopened.Error);
        Assert.Equal([first], receiver.Arrivals);
        Assert.Equal(new UploadSummary(1, 1), outcome.Upload); // Not counted as "not sent": it is In Progress now.
        Assert.Null(SyncMessages.Explain(outcome).Work);
        Assert.Equal(OrderStanding.InProgress, Orders().Read(second)!.Standing);
    }

    // ---- Derivable edges (D2, D3, D4/D5) ----

    [Fact]
    public async Task Should_RefuseReopenAndEditing_When_TheOrderNeedsAttention()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = ReadyOrder(day, 0, (0, 24m));
        Guid line = LineOf(order, "Cold & Flu Relief 16s");
        receiver.Interfere = (_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
            { Content = JsonContent.Create(new Dictionary<string, string> { [UploadFaults.ProblemKey] = UploadFaults.Incomplete }) };
        await SyncAsync(day);
        string body = Unsent().Find(order)!.Json;

        Assert.Equal(OrderStanding.NeedsAttention, Orders().Read(order)!.Standing);
        Assert.False(Unsent().IsClaimed(order)); // The server kept nothing, so the claim went with the refusal.
        Assert.Equal(OrderStore.NotChangeable, Orders().Reopen(order).Error);
        Assert.Equal(OrderStore.NotChangeable, Orders().ChangeQuantity(order, line, 36m).Error);
        Assert.Equal(OrderStore.NotChangeable, Orders().Delete(order, confirmed: true).Error);
        Assert.Equal(body, Unsent().Find(order)!.Json);
        Assert.True(Unsent().DeleteRejected(order, confirmed: true)); // WI-042's Delete still applies.
    }

    [Fact]
    public async Task Should_KeepTheSavedCall_When_AnOrderIsDeleted()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid call = Guid.NewGuid();
        Unsent().Save(new(call, UnsentKind.Call, tablet.Clock.GetUtcNow(),
            $$"""{"id":"{{call}}","locationId":"{{day.Locations[1].Id}}","channel":"InPerson"}"""));
        Guid order = NewOrder(day, 1, (0, 24m));

        Assert.True(Orders().Delete(order, confirmed: true).Deleted);

        Assert.Equal([call], Unsent().Read().Select(item => item.Id));
        await SyncAsync(day);
        Assert.Equal([call], receiver.Arrivals);
    }

    [Fact]
    public async Task Should_AllowChangesButNotAdditions_When_TheShopIsNoLongerAssigned()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid order = NewOrder(day, 2, (0, 24m), (2, 6m));
        // The next day drops the shop and the third product; the order keeps both lines as captured.
        TabletSnapshot next = Snapshots.Build("colm", 2, 2);
        await tablet.SyncAsync(next);
        TabletOrder before = Orders().Find(order)!;

        Assert.True(Orders().ChangeQuantity(order, before.Lines[0].Id, 36m).Succeeded);
        Assert.Equal(OrderStore.CantChangeMissing, Orders().ChangeQuantity(order, before.Lines[1].Id, 12m).Error);
        Assert.True(Orders().RemoveLine(order, before.Lines[1].Id, deleteOrderIfLast: false).Succeeded);
        Assert.Equal(OrderStore.NotAssigned, Orders().AddLine(order, day.Locations[2].Id, day.Products[1].Id, 1m).Error);
        Assert.True(Orders().MarkReadyToSend(order).Succeeded);
        Assert.True(Orders().Reopen(order).Succeeded);
        Assert.Equal(36m, Assert.Single(Orders().Find(order)!.Lines).Quantity);
        Assert.True(Orders().Delete(order, confirmed: true).Deleted);
    }

    // ---- D1: format 4 ----

    [Fact]
    public async Task Should_KeepEveryUnsentAndSentItem_When_AFormat3DatabaseIsUpgraded()
    {
        TabletSnapshot day = await SyncedDayAsync();
        Guid sent = ReadyOrder(day, 0, (0, 24m));
        Guid refused = ReadyOrder(day, 1, (1, 6m));
        Guid building = NewOrder(day, 1, (2, 1m));
        receiver.Interfere = (id, _) => id == refused
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                { Content = JsonContent.Create(new Dictionary<string, string> { [UploadFaults.ProblemKey] = UploadFaults.Incomplete }) }
            : null;
        await SyncAsync(day);
        string fingerprint = Unsent().Fingerprint();
        UploadRejection rejection = Unsent().Find(refused)!.Rejection!;
        using (SqliteConnection connection = SqliteEngine.Open(tablet.Workspace().UnsentPath))
            SqliteEngine.Execute(connection, "DROP TABLE upload_claims; PRAGMA user_version=3;");

        Assert.Equal(fingerprint, Unsent().Fingerprint());
        Assert.Equal([sent], Unsent().ReadSent().Select(item => item.Item.Id));
        Assert.Equal(rejection, Unsent().Find(refused)!.Rejection);
        Assert.Equal(OrderStanding.InProgress, Orders().Read(building)!.Standing);
        using SqliteConnection check = SqliteEngine.Open(tablet.Workspace().UnsentPath);
        using SqliteCommand version = check.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        Assert.Equal(4L, (long)version.ExecuteScalar()!);
        Assert.True(Orders().MarkReadyToSend(building).Succeeded);
        Assert.True(Unsent().Claim(Unsent().Find(building)!, tablet.Clock.GetUtcNow())); // The new table works after the upgrade.
    }
}
