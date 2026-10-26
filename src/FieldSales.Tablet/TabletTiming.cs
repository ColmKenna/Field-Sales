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

    /// <summary>A sign-in or Sync in this process means the next Home is not a plain open of the day.</summary>
    public static void RepActed() => openPending = false;

    /// <summary>The first Home of a plain open: milliseconds from Android starting the process to the shops list.</summary>
    public static void HomeShown(int locations)
    {
        if (!openPending) return;
        openPending = false;
#if ANDROID
        long elapsed = Android.OS.SystemClock.ElapsedRealtime() - Android.OS.Process.StartElapsedRealtime;
        Write($"open ms={elapsed} locations={locations}");
#endif
    }

    public static void Synced(SyncOutcome outcome, TimeSpan elapsed) =>
        Write($"sync ms={(long)elapsed.TotalMilliseconds} status={outcome.Status} counts={outcome.Active?.Manifest.Counts}");

    public static void CleanedUp(TimeSpan elapsed) => Write($"cleanup ms={(long)elapsed.TotalMilliseconds}");

    private static void Write(string message)
    {
#if ANDROID
        Android.Util.Log.Info(Tag, message);
#endif
    }
}
