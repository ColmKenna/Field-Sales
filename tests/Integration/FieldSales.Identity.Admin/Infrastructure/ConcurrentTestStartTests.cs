namespace FieldSales.Identity.Admin.Tests.Infrastructure;

public sealed class ConcurrentTestStartTests
{
    [Fact]
    public async Task ReleasesOnlyAfterEveryParticipantIsReady()
    {
        using var start = new ConcurrentTestStart(2);
        int ready = 0;
        Task<int> Worker() => start.Run(async () =>
        {
            Interlocked.Increment(ref ready);
            await start.ReadyAsync();
            Assert.Equal(2, Volatile.Read(ref ready));
            return 1;
        });
        Task<int>[] workers = [Worker(), Worker()];
        await start.CompleteAsync(workers);
        Assert.Equal(new[] { 1, 1 }, await Task.WhenAll(workers));
    }

    [Fact]
    public async Task SetupFailureCancelsWaitingParticipantAndPreservesOriginalError()
    {
        using var start = new ConcurrentTestStart(2);
        var failure = new InvalidOperationException("participant setup failed");
        Task<int> failed = start.Run<int>(() => throw failure);
        Task<int> waiting = start.Run(async () => { await start.ReadyAsync(); return 1; });
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => start.CompleteAsync(failed, waiting));
        Assert.Same(failure, actual);
        Assert.True(failed.IsCompleted);
        Assert.True(waiting.IsCompleted);
    }

    [Fact]
    public async Task MissingParticipantTimesOutAndDrainsWaitingWorker()
    {
        using var start = new ConcurrentTestStart(2, TimeSpan.FromMilliseconds(20));
        Task<int> waiting = start.Run(async () => { await start.ReadyAsync(); return 1; });
        await Assert.ThrowsAsync<TimeoutException>(() => start.CompleteAsync(waiting));
        Assert.True(waiting.IsCompleted);
    }
}
