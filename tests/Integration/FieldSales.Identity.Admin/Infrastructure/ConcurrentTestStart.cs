using System.Runtime.ExceptionServices;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

internal sealed class ConcurrentTestStart(int participants, TimeSpan? readinessTimeout = null) : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ExceptionDispatchInfo? _failure;
    private int _arrived;

    public Task<T> Run<T>(Func<Task<T>> worker) => Task.Run(async () =>
    {
        try { return await worker(); }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(error), null);
            _cancel.Cancel();
            throw;
        }
    });

    public async Task ReadyAsync()
    {
        if (Interlocked.Increment(ref _arrived) == participants) _ready.TrySetResult();
        await _release.Task.WaitAsync(_cancel.Token);
    }

    public async Task CompleteAsync(params Task[] workers)
    {
        Task all = Task.WhenAll(workers);
        try
        {
            await _ready.Task.WaitAsync(readinessTimeout ?? TimeSpan.FromSeconds(30), _cancel.Token);
            _release.TrySetResult();
            await all;
        }
        catch (Exception coordinationError)
        {
            _cancel.Cancel();
            _release.TrySetCanceled(_cancel.Token);
            try { await all.WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (Exception cleanupError)
            {
                // Observe eventual errors even if a participant ignores cancellation during setup.
                _ = all.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                if (cleanupError is TimeoutException) coordinationError.Data["WorkerCleanupFailure"] = cleanupError;
            }
            if (coordinationError is not TimeoutException) _failure?.Throw();
            throw;
        }
    }

    public void Dispose() => _cancel.Dispose();
}
