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
    long UnsentBytes, Dictionary<string, long>? GenerationPayloadBytes, double? GenerationPayloadScanMs = null)
{
    public long TotalBytes => DatabaseBytes + WalBytes + JournalBytes + SharedMemoryBytes + TransferBytes + UnsentBytes;
    public string GenerationPayloadMetric => GenerationPayloadBytes is null
        ? "unavailable (optional logical scan not performed)"
        : "logical raw JSON bytes per generation; not additional physical bytes; separate optional scan clock";
    public string IndexMetric => "included in DatabaseBytes; per-index allocation unavailable (not sampled)";
}
public sealed record SearchMeasurement(string Term, double QueryMs, double? InputToVisibleMs,
    int Matches, string Clock = "Stopwatch query; DOM submit-to-render-and-two-animation-frames via performance.now; automated terms start at JS invocation");
public sealed class SpikeReport
{
    public string ReportSchema { get; init; } = "spike-measurements-v3";
    public Guid RunId { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string Owner { get; init; }
    public required DeviceConditions Device { get; init; }
    public required NetworkConditions Network { get; init; }
    public required string CacheState { get; init; }
    public string Status { get; set; } = "incomplete";
    public string? Error { get; set; }
    public string? PendingOperation { get; set; }
    public DateTimeOffset? PendingOperationStartedUtc { get; set; }
    public string PendingOperationMetric => "last persisted pending operation; may have completed since that write; not proof of a stall";
    public string? FailedOperation { get; set; }
    public string? Generation { get; set; }
    public FixtureManifest? Manifest { get; set; }
    public SqliteEnvironment? Sqlite { get; set; }
    public Dictionary<string, double> PhaseMs { get; } = [];
    public Dictionary<string, double> InterruptedPhaseMs { get; } = [];
    public LocalOpenDiagnostics? LocalOpen { get; set; }
    public string ClockScopes => "PhaseMs contains completed operations; InterruptedPhaseMs contains caught unfinished attempts. Operation clocks exclude their pending-boundary report write and pause; hooks inside an operation are included. totalRefresh starts after the owner gate and ends before final atomic report publication; includes preparation, physical sampling and intermediate report writes; excludes optional logical scans and UI reopen. reportFinalization covers end samples and Unsent read; final atomic publication is excluded. refreshAwait includes runner publication and the UI continuation; refreshAndLocalReopenAwait also includes OpenLocal, neither measures first paint. databaseAndWorkReopen includes native/schema initialization, active/work/report-list reads, worker scheduling and UI continuation; not isolated database IO or whole cold launch. Phase subsets overlap their enclosing pipeline; do not sum them.";
    public List<DiskMeasurement> DiskSamples { get; } = [];
    public long PeakSampledDiskBytes => DiskSamples.Count == 0 ? 0 : DiskSamples.Max(d => d.TotalBytes);
    public long? ProcessWorkingSetBytes { get; set; }
    public string MemoryMetric { get; set; } = "unavailable";
    public string? OriginalUnsentSha256 { get; set; }
    public string? UnsentFingerprintBefore { get; set; }
    public string? UnsentFingerprintAfter { get; set; }
    public List<SearchMeasurement> Searches { get; } = [];
    public int MaxParserBufferBytes { get; set; }

    public SpikeOperation BeginOperation(string name, SpikeHooks hooks)
    {
        PendingOperation = name;
        PendingOperationStartedUtc = DateTimeOffset.UtcNow;
        hooks.AtBoundary?.Invoke($"pending-{name}");
        return new(this, name);
    }

    // Same-directory replacement keeps the previous valid report if the process stops mid-write.
    public void Save(string path)
    {
        string temporary = path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (var writer = new StreamWriter(file, leaveOpen: true)) { writer.Write(ToJson()); writer.Flush(); }
            file.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }

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

public sealed class SpikeOperation(SpikeReport report, string name) : IDisposable
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private bool complete;
    public void Complete()
    {
        report.PhaseMs[name] = watch.Elapsed.TotalMilliseconds;
        report.PendingOperation = null;
        report.PendingOperationStartedUtc = null;
        complete = true;
    }
    public void Dispose()
    {
        if (!complete) report.InterruptedPhaseMs[name] = watch.Elapsed.TotalMilliseconds;
    }
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
