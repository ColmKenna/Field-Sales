using System.Diagnostics;
using System.Runtime.InteropServices;
using FieldSales.TabletSpike.Core;

namespace FieldSales.TabletSpike;

public sealed class DeviceReportAdapter
{
    private readonly Stopwatch startup = Stopwatch.StartNew();
    public string Root => Path.Combine(FileSystem.AppDataDirectory, "storage-spike");
    public double StartupElapsedMs => startup.Elapsed.TotalMilliseconds;
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
        report.SampleMemory();
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
