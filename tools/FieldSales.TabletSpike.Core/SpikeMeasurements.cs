using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FieldSales.TabletSpike.Core;

public sealed record DeviceConditions(string Kind, string Model, string Os, string Abi, string WebView,
    string AppVersion, string BuildConfiguration)
{
    public static DeviceConditions Desktop() => new("desktop", Environment.MachineName,
        RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(), "unavailable (headless)",
        "1.0", "diagnostic");
}
public sealed record NetworkConditions(string Kind, double? SimulatedMbitPerSecond = null,
    int InitialDelayMs = 0, string PhysicalLink = "unmeasured; no physical throughput/RTT claim");
public sealed record DiskMeasurement(long DatabaseBytes, long WalBytes, long JournalBytes, long SharedMemoryBytes, long TransferBytes,
    long UnsentBytes, Dictionary<string, long> GenerationPayloadBytes)
{
    public long TotalBytes => DatabaseBytes + WalBytes + JournalBytes + SharedMemoryBytes + TransferBytes + UnsentBytes;
    public string GenerationPayloadMetric => "logical raw JSON bytes per generation; not additional physical bytes";
    public string IndexMetric => "included in DatabaseBytes; per-index allocation unavailable (not sampled)";
}
public sealed record SearchMeasurement(string Term, double QueryMs, double? InputToVisibleMs,
    int Matches, string Clock = "Stopwatch query; DOM submit-to-render-and-two-animation-frames via performance.now; automated terms start at JS invocation");
public sealed class SpikeReport
{
    public string ReportSchema { get; init; } = "spike-measurements-v1";
    public Guid RunId { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string Owner { get; init; }
    public required DeviceConditions Device { get; init; }
    public required NetworkConditions Network { get; init; }
    public required string CacheState { get; init; }
    public string Status { get; set; } = "incomplete";
    public string? Error { get; set; }
    public string? Generation { get; set; }
    public FixtureManifest? Manifest { get; set; }
    public SqliteEnvironment? Sqlite { get; set; }
    public Dictionary<string, double> PhaseMs { get; } = [];
    public List<DiskMeasurement> DiskSamples { get; } = [];
    public long PeakSampledDiskBytes => DiskSamples.Count == 0 ? 0 : DiskSamples.Max(d => d.TotalBytes);
    public long? ProcessWorkingSetBytes { get; set; }
    public string MemoryMetric { get; set; } = "unavailable";
    public string? OriginalUnsentSha256 { get; set; }
    public string? UnsentFingerprintBefore { get; set; }
    public string? UnsentFingerprintAfter { get; set; }
    public List<SearchMeasurement> Searches { get; } = [];
    public int MaxParserBufferBytes { get; set; }

    public void SampleMemory()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            ProcessWorkingSetBytes = process.WorkingSet64;
            MemoryMetric = "Process.WorkingSet64 bytes; one end-of-run sample (not peak RSS)";
        }
        catch (PlatformNotSupportedException) { }
    }
    public string ToJson() => JsonSerializer.Serialize(this, FixtureManifest.JsonOptions);
}

// Test seams also drive the explicitly labelled pause controls in the isolated device runner.
public sealed class SpikeHooks
{
    public Action<string>? AtBoundary { get; init; }
    public Action<long>? DownloadProgress { get; init; }
    public Action<int>? BatchWritten { get; init; }
    public int? MaxDatabasePages { get; init; }
    public Action? BeforeCommit { get; init; }
}
