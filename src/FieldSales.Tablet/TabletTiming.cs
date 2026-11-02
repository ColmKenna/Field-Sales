using FieldSales.Tablet.Core;

namespace FieldSales.Tablet;

/// <summary>
/// Device diagnostics under <see cref="Tag"/>. Android open timing starts at process launch;
/// iOS timing starts at the first managed timing mark and excludes native startup. Debug timings are diagnostic only.
/// </summary>
public static class TabletTiming
{
    public const string Tag = "FieldSalesTiming";
    private static bool openPending = true;
    private static readonly List<string> marks = [];
#if !ANDROID
    private static readonly System.Diagnostics.Stopwatch managedStartup = System.Diagnostics.Stopwatch.StartNew();
#endif

    /// <summary>A sign-in or Sync in this process means the next Home is not a plain open of the day.</summary>
    public static void RepActed() => openPending = false;

    /// <summary>A step towards the first Home using the platform's startup clock (kept only for a plain open).</summary>
    public static void Mark(string step)
    {
        if (!openPending) return;
        lock (marks) marks.Add($"{step}={SinceProcessStart()}");
    }

    /// <summary>The first Home of a plain open, with iOS's managed-only clock identified in the output.</summary>
    public static void HomeShown(int locations)
    {
        if (!openPending) return;
        openPending = false;
        string steps;
        lock (marks) steps = string.Join(' ', marks);
#if IOS
        Write($"open clock=managed-start ms={SinceProcessStart()} locations={locations} steps: {steps}");
#else
        Write($"open ms={SinceProcessStart()} locations={locations} steps: {steps}");
#endif
    }

    private static long SinceProcessStart() =>
#if ANDROID
        Android.OS.SystemClock.ElapsedRealtime() - Android.OS.Process.StartElapsedRealtime;
#else
        managedStartup.ElapsedMilliseconds;
#endif

    /// <summary>The two parts of Home's own work before the list draws: the 24-hour check, then reading the shops.</summary>
    public static void HomePrepared(TimeSpan entry, TimeSpan read)
    {
        if (openPending) Write($"home entry ms={(long)entry.TotalMilliseconds} read ms={(long)read.TotalMilliseconds}");
    }

    public static void Synced(SyncOutcome outcome, TimeSpan elapsed) =>
        Write($"sync ms={(long)elapsed.TotalMilliseconds} status={outcome.Status} counts={outcome.Active?.Manifest.Counts}");

    public static void CleanedUp(TimeSpan elapsed) => Write($"cleanup ms={(long)elapsed.TotalMilliseconds}");

    public static void SearchIndexBuilt(int locations, TimeSpan elapsed) =>
        Write($"search-index locations={locations} ms={elapsed.TotalMilliseconds:F3}");

    public static void SearchShown(int matches, TimeSpan visible, TimeSpan handler) =>
        Write($"search-visible matches={matches} ms={visible.TotalMilliseconds:F3} handler-ms={handler.TotalMilliseconds:F3}");

    [System.Diagnostics.Conditional("DEBUG")]
    public static void NetworkRequest(Uri endpoint, int? status, HttpRequestException? error)
    {
        Write($"http host={endpoint.Host} port={endpoint.Port} path={endpoint.AbsolutePath} status={status} " +
            $"error={error?.HttpRequestError} cause={error?.GetBaseException().GetType().Name} hresult={error?.GetBaseException().HResult}");
#if IOS
        if (error?.GetBaseException() is Foundation.NSErrorException native)
            Write($"http-native domain={native.Error.Domain} code={native.Error.Code}");
#endif
    }

    private static void Write(string message)
    {
#if ANDROID
        Android.Util.Log.Info(Tag, message);
#elif IOS
        Console.WriteLine($"{Tag}: {message}");
#endif
    }
}
