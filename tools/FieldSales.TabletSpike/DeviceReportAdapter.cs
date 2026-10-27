using System.Diagnostics;
using System.Runtime.InteropServices;
using FieldSales.TabletSpike.Core;

namespace FieldSales.TabletSpike;

public sealed class DeviceReportAdapter
{
    private readonly Stopwatch startup = Stopwatch.StartNew();
    public string Root => Path.Combine(FileSystem.AppDataDirectory, "storage-spike");
    public double StartupElapsedMs => startup.Elapsed.TotalMilliseconds;
    public LocalOpenNativeTrace TraceLocalOpen(string markerName)
    {
#if ANDROID
        bool? enabled = OperatingSystem.IsAndroidVersionAtLeast(29) ? Android.OS.Trace.IsEnabled : null;
        Android.OS.Trace.BeginSection(markerName);
        try
        {
            return new(Android.OS.Process.MyTid(), Android.OS.Looper.MyLooper() == Android.OS.Looper.MainLooper, enabled);
        }
        finally { Android.OS.Trace.EndSection(); }
#else
        return new(null, null, null);
#endif
    }
    public DeviceConditions Conditions(string webView)
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        return new(DeviceInfo.DeviceType == DeviceType.Physical ? "physical" : "emulator",
            $"{DeviceInfo.Manufacturer} {DeviceInfo.Model}", $"{DeviceInfo.Platform} {DeviceInfo.VersionString}",
            RuntimeInformation.ProcessArchitecture.ToString(), webView, AppInfo.VersionString, configuration);
    }
    public Task<string> ExportAsync(SpikeReport report)
    {
        // Preserve the runner's end-of-refresh sample; exporting later is a different clock scope.
        return ExportJsonAsync(report.ToJson(), $"tablet-spike-{report.RunId}.json");
    }
    public async Task<string> ExportJsonAsync(string json, string name)
    {
        string privatePath = Path.Combine(FileSystem.CacheDirectory, name);
        await File.WriteAllTextAsync(privatePath, json);
#if ANDROID
        // App-specific external files are readable by adb without making the release app debuggable.
        string? external = Android.App.Application.Context.GetExternalFilesDir("reports")?.AbsolutePath;
        if (external is not null)
        {
            Directory.CreateDirectory(external);
            await File.WriteAllTextAsync(Path.Combine(external, name), json);
        }
#endif
        await Share.Default.RequestAsync(new ShareFileRequest { Title = "Synthetic storage spike report", File = new ShareFile(privatePath, "application/json") });
        return name;
    }
}
