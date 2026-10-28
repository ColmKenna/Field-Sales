using FieldSales.Tablet.Core;

namespace FieldSales.Tablet;

/// <summary>
/// Scenario 25 evidence (D3 budgets): how long opening the day and a Sync take, written to the Android log under
/// <see cref="Tag"/> for the device session to read with adb logcat. Nothing here changes what the rep sees.
/// </summary>
public static class TabletTiming
{
    public const string Tag = "FieldSalesTiming";
    private static bool openPending = true;
    private static readonly List<string> marks = [];

    /// <summary>A sign-in or Sync in this process means the next Home is not a plain open of the day.</summary>
    public static void RepActed() => openPending = false;

    /// <summary>A step on the way to the first Home, in milliseconds since Android started the process (kept only for a plain open).</summary>
    public static void Mark(string step)
    {
        if (!openPending) return;
        lock (marks) marks.Add($"{step}={SinceProcessStart()}");
    }

    /// <summary>The first Home of a plain open: milliseconds from Android starting the process to the shops list.</summary>
    public static void HomeShown(int locations)
    {
        if (!openPending) return;
        openPending = false;
#if ANDROID
        string steps;
        lock (marks) steps = string.Join(' ', marks);
        Write($"open ms={SinceProcessStart()} locations={locations} steps: {steps}");
#endif
    }

    private static long SinceProcessStart() =>
#if ANDROID
        Android.OS.SystemClock.ElapsedRealtime() - Android.OS.Process.StartElapsedRealtime;
#else
        0;
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

    private static void Write(string message)
    {
#if ANDROID
        Android.Util.Log.Info(Tag, message);
#endif
    }
}
