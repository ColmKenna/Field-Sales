using System.Net;
using System.Net.Http.Json;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core.Tests;

// WI-042: approved Scenario Review cases. Real rep-bound SQLite storage and uploader; faults at the HTTP boundary.
public sealed class NeedsAttentionTests : IDisposable
{
    private readonly TestTablet tablet = new();
    private readonly TabletSnapshot day = Snapshots.Build("colm", 3, 3);
    public void Dispose() => tablet.Dispose();
    private UnsentStore Store(string rep = "colm") => new(tablet.Workspace(rep));

    private async Task<Guid[]> CaptureAsync()
    {
        await tablet.SyncAsync(day);
        tablet.Server.Serve(EncodedSnapshot.From(day));
        return FinishedWork.SaveThree(tablet.Workspace(), day, tablet.Clock);
    }

    private void Reject(string? code = UploadFaults.Incomplete, string detail = "missing line snapshot") =>
        tablet.Server.RespondToUpload = _ => new(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { fault = code, detail })
        };

    [Fact]
    public async Task Should_KeepNeedsAttention_When_TheAppRestarts()
    {
        Guid[] ids = await CaptureAsync();
        string fingerprint = Store().Fingerprint();
        Reject();

        await tablet.Sync().SyncAsync();

        UnsentStore reopened = new(new RepWorkspace(tablet.Root, "colm"));
        Assert.Equal(fingerprint, reopened.Fingerprint()); // Captured JSON and identity never changed.
        Assert.Equal(ids.Order(), reopened.Read().Select(item => item.Id));
        Assert.All(reopened.Read(), item =>
        {
            Assert.Equal(UploadFaults.Incomplete, item.Rejection!.Fault);
            Assert.Contains("missing line snapshot", item.Rejection.RawReason);
            Assert.Equal(400, item.Rejection.HttpStatus);
            Assert.Equal(UploadFaultMessages.Incomplete, item.Rejection.Sentence);
        });
        Assert.Empty(Store("aoife").Read());
    }

    [Fact]
    public async Task Should_WaitForReview_When_AnItemNeedsAttention()
    {
        await CaptureAsync();
        Reject();
        SyncOutcome refused = await tablet.Sync().SyncAsync();
        Assert.Equal(3, refused.Upload.NeedsAttention);
        int arrivals = tablet.Server.Uploads.Count;
        tablet.Server.RespondToUpload = request => FakeReceiver.Receipt(Guid.Parse(request.RequestUri!.Segments[^1]), false);

        SyncOutcome next = await tablet.Sync().SyncAsync();

        Assert.Equal(arrivals, tablet.Server.Uploads.Count);
        Assert.Equal(0, WorkUploader.CountWaiting(tablet.Workspace()));
        Assert.Equal(UploadSummary.None, next.Upload);
        Assert.Equal(new UnsentSummary(3, 0, 0, 3), Store().Summarise());
        Assert.Equal("3 need attention.", SyncMessages.Explain(refused).Work);
    }

    [Fact]
    public async Task Should_SendAtNextSync_When_ARejectedOrderIsReviewedAndRemarked()
    {
        Guid order = (await CaptureAsync())[^1];
        Reject();
        await tablet.Sync().SyncAsync();
        UnsentItem original = Store().Find(order)!;

        Assert.True(Store().MarkReviewedReady(order));
        Assert.Equal(original.Json, Store().Find(order)!.Json);
        Assert.Null(Store().Find(order)!.Rejection);
        Assert.Equal(new UnsentSummary(3, 0, 1, 2), Store().Summarise());
        FakeReceiver receiver = new(tablet.Server);
        tablet.Server.RespondToUpload = receiver.Respond;

        SyncOutcome next = await tablet.Sync().SyncAsync();

        Assert.Equal([order], receiver.Arrivals);
        Assert.Equal(new UploadSummary(1, 1), next.Upload);
        Assert.Equal(original.Json, Assert.Single(Store().ReadSent()).Item.Json);
        Assert.False(Store().MarkReviewedReady(order)); // Sent cannot become a new upload.
    }

    [Theory]
    [InlineData(UnsentKind.Call)]
    [InlineData(UnsentKind.Order)]
    public async Task Should_DeleteRejectedItem_When_DeletionIsConfirmed(UnsentKind kind)
    {
        await CaptureAsync();
        Reject();
        await tablet.Sync().SyncAsync();
        UnsentItem item = Store().Read().First(item => item.Kind == kind);

        Assert.True(Store().DeleteRejected(item.Id, confirmed: true));

        Assert.Null(Store().Find(item.Id));
        Assert.Equal(new UnsentSummary(2, 0, 0, 2), Store().Summarise());
        Assert.False(Store().DeleteRejected(item.Id, confirmed: true));
        Assert.Empty(Store().ReadSent());
    }

    [Fact]
    public async Task Should_KeepRejectedItem_When_DeletionIsCancelled()
    {
        Guid call = (await CaptureAsync())[0];
        Reject();
        await tablet.Sync().SyncAsync();
        UnsentItem before = Store().Find(call)!;

        Assert.False(Store().DeleteRejected(call, confirmed: false));

        Assert.Equal(before, Store().Find(call));
        Assert.Equal(3, Store().Summarise().Total);
    }

    [Fact]
    public async Task Should_RefuseCallDeletion_When_ItIsNotInNeedsAttention()
    {
        Guid call = (await CaptureAsync())[0];
        Assert.False(Store().DeleteRejected(call, confirmed: true));
        Assert.NotNull(Store().Find(call));
        Reject();
        await tablet.Sync().SyncAsync();
        Assert.False(Store().MarkReviewedReady(call)); // Only Orders have the approved re-mark workflow.
        Assert.False(Store("aoife").DeleteRejected(call, confirmed: true));
        Assert.NotNull(Store().Find(call));
    }

    [Theory]
    [InlineData(UploadFaults.Incomplete, UploadFaultMessages.Incomplete)]
    [InlineData(UploadFaults.Corrupt, UploadFaultMessages.ContactOffice)]
    [InlineData(UploadFaults.Unsupported, UploadFaultMessages.ContactOffice)]
    [InlineData(UploadFaults.NotYours, UploadFaultMessages.ContactOffice)]
    [InlineData(UploadFaults.IdInUse, UploadFaultMessages.ContactOffice)]
    [InlineData(UploadFaults.TooLarge, UploadFaultMessages.ContactOffice)]
    [InlineData("unrecognised-technical-fault", UploadFaultMessages.ContactOffice)]
    [InlineData(null, UploadFaultMessages.ContactOffice)]
    public async Task Should_KeepRawReasonForSupport_When_TheRepCannotActOnIt(string? fault, string sentence)
    {
        Guid item = (await CaptureAsync())[0];
        Reject(fault, "Internal database detail for support");

        await tablet.Sync().SyncAsync();

        UploadRejection reason = Store().Find(item)!.Rejection!;
        Assert.Equal(sentence, reason.Sentence);
        Assert.Contains("Internal database detail for support", reason.RawReason);
        Assert.DoesNotContain("database", reason.Sentence);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Should_PreserveUnsentAndSentWork_When_AnOlderStoreIsUpgraded(int format)
    {
        Guid[] ids = await CaptureAsync();
        Store().MarkSent(ids[0], tablet.Clock.GetUtcNow());
        string before = Store().Fingerprint();
        using (SqliteConnection connection = SqliteEngine.Open(tablet.Workspace().UnsentPath))
        {
            SqliteEngine.Execute(connection, "DROP TABLE work_faults;");
            if (format == 1) SqliteEngine.Execute(connection, "DROP TABLE sent;");
            SqliteEngine.Execute(connection, $"PRAGMA user_version={format};");
        }

        Assert.Equal(before, Store().Fingerprint());
        Assert.Equal(format == 2 ? 1 : 0, Store().ReadSent().Count);
        Assert.True(Store().MarkSent(ids[1], tablet.Clock.GetUtcNow()));
        Assert.Equal(1, Store().Summarise().Total);
    }

    [Fact]
    public async Task Should_ReportOnlyWaitingItemsAsRetryable_When_AFaultIsFollowedByALostConnection()
    {
        Guid[] ids = await CaptureAsync();
        tablet.Server.RespondToUpload = request => Guid.Parse(request.RequestUri!.Segments[^1]) == ids[0]
            ? new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { fault = UploadFaults.Incomplete }) }
            : throw Faults.NoSignal();

        SyncOutcome result = await tablet.Sync().SyncAsync();

        Assert.Equal((3, 0, 1, 2), (result.Upload.Total, result.Upload.Sent, result.Upload.NeedsAttention, result.Upload.Waiting));
        Assert.Equal("2 items not sent — poor connection. They'll send next Sync. 1 needs attention.", SyncMessages.Explain(result).Work);
        Assert.Equal(new UnsentSummary(3, 0, 2, 1), Store().Summarise());
    }

    [Fact]
    public async Task Should_KeepABoundedSupportReason_When_TheServerSendsAnOversizedErrorPage()
    {
        Guid id = (await CaptureAsync())[0];
        tablet.Server.RespondToUpload = _ => new(HttpStatusCode.BadRequest)
        { Content = new StringContent(new string('x', 100_000)) };

        await tablet.Sync().SyncAsync();

        UploadRejection reason = Store().Find(id)!.Rejection!;
        Assert.EndsWith("[response truncated]", reason.RawReason);
        Assert.True(reason.RawReason.Length < 17_000);
        Assert.Equal(UploadFaultMessages.ContactOffice, reason.Sentence);
    }
}
