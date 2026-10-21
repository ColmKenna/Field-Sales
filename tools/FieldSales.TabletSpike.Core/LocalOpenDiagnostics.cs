using System.Diagnostics;

namespace FieldSales.TabletSpike.Core;

public sealed record LocalOpenNativeTrace(int? ThreadId, bool? AndroidMainThread, bool? Enabled);
public sealed record LocalOpenTracePoint(string MarkerName, long CapturedTimestamp, double OffsetMs,
    int ManagedThreadId, long? HookStartedTimestamp, long? HookCompletedTimestamp,
    LocalOpenNativeTrace? Native, string? HookError);

public sealed class LocalOpenTrace
{
    public Guid AttemptId { get; } = Guid.NewGuid();
    public long TimestampFrequency => Stopwatch.Frequency;
    public long? OriginTimestamp { get; internal set; }
    public Dictionary<string, LocalOpenTracePoint?> Points { get; } = new()
    {
        ["uiDispatch"] = null, ["workerEntry"] = null, ["workerBodyEnd"] = null,
        ["workerDelegateExit"] = null, ["callerContinuation"] = null,
        ["continuationObserved"] = null, ["uiComplete"] = null
    };
    public string ClockScopes => "CapturedTimestamp is sampled before entering the point recorder; OffsetMs uses OriginTimestamp and TimestampFrequency, all from Stopwatch. Native marker execution is inside the hook timestamp bracket; helper entry before that bracket is not isolated scheduling. workerBodyEnd precedes the rest of the Read finally; workerDelegateExit is the caller finally after Read, before the remaining marker hook, delegate return and Task completion. callerContinuation is the first caller timestamp after await, before recorder and ObserveUiContinuation entry. continuationObserved retains the existing helper marker. uiComplete retains the enclosing clock endpoint; its hook follows that endpoint. Hook costs remain included wherever they occur, never subtracted as a performance correction. Thread identities describe each synchronous marker, not an assumed Android UI thread. Missing points/native fields/hook brackets are unavailable, not zero. Headless origin is worker entry; it has no UI/dispatch observations.";
}

public sealed class LocalOpenDiagnostics
{
    public string ReadStatus { get; internal set; } = "not-started";
    public string? FailedStage { get; internal set; }
    public string? Error { get; internal set; }
    public string? ErrorType { get; internal set; }
    public double? DispatchToWorkerEntryMs { get; internal set; }
    public Dictionary<string, double?> StageMs { get; } = new()
    {
        ["workspace"] = null, ["snapshotInitialization"] = null, ["activeRead"] = null,
        ["unsentRead"] = null, ["reportEnumeration"] = null
    };
    public Dictionary<string, double> InterruptedStageMs { get; } = [];
    public double? WorkerBodyMs { get; internal set; }
    public double? WorkerCompletionToUiContinuationMs { get; internal set; }
    public double? EnclosingLocalOpenMs { get; internal set; }
    public LocalOpenTrace Trace { get; } = new();
    public string ClockScopes => "One Stopwatch timestamp origin for the UI operation. dispatchToWorkerEntryMs ends at the first instrumented worker marker; includes dispatch/setup and any startup/JIT before that marker, not isolated thread-pool queue time. stageMs contains disjoint completed worker stages, null when unfinished/unobserved; workspace includes resolving the platform root. snapshotInitialization includes connection/schema/native inspection; activeRead and unsentRead include their own connection/schema setup. reportEnumeration includes filename enumeration, modification-time sorting and selection. workerBodyMs encloses all stages, failure recording and instrumentation gaps. workerCompletionToUiContinuationMs ends at the first observed continuation, not rendering/first paint. enclosingLocalOpenMs retains databaseAndWorkReopen's scope through UI-field updates. Do not add enclosing clocks to their subsets. Headless reads leave dispatch/UI/enclosing clocks unavailable.";
}

// The caller dispatches this synchronous read sequence. No provider settings,
// store operations or ordering differ from the original device worker.
public sealed class LocalOpenAttempt(long? uiOperationStartedTimestamp = null,
    Func<string, LocalOpenNativeTrace>? nativeMarker = null)
{
    public LocalOpenDiagnostics Diagnostics { get; } = new();
    public SpikeWorkspace? Workspace { get; private set; }
    public SnapshotSqliteStore? Store { get; private set; }
    public ActiveSnapshot? Active { get; private set; }
    public UnsentState Work { get; private set; } = new(null, "", []);
    public string[] SavedReports { get; private set; } = [];
    public string SelectedSavedReport { get; private set; } = "";
    private long? workerCompleted, uiContinuation;
    private string? stage;
    private long stageStarted;

    public void Read(Func<string> root, string owner)
    {
        long workerStarted = Stopwatch.GetTimestamp();
        if (Diagnostics.ReadStatus != "not-started") throw new InvalidOperationException("A local-open attempt can only run once.");
        Diagnostics.Trace.OriginTimestamp ??= uiOperationStartedTimestamp ?? workerStarted;
        if (uiOperationStartedTimestamp is long start)
            Diagnostics.DispatchToWorkerEntryMs = Stopwatch.GetElapsedTime(start, workerStarted).TotalMilliseconds;
        Diagnostics.ReadStatus = "reading";
        RecordTracePoint("workerEntry", workerStarted);
        try
        {
            var workspace = Measure("workspace", () => new SpikeWorkspace(root(), owner));
            Workspace = workspace;
            var store = Measure("snapshotInitialization", () => new SnapshotSqliteStore(workspace));
            Store = store;
            Active = Measure("activeRead", store.ReadActive);
            Work = Measure("unsentRead", () => new UnsentSqliteStore(workspace).Read());
            Measure("reportEnumeration", () =>
            {
                SavedReports = Directory.GetFiles(workspace.DirectoryPath, "report-*.json")
                    .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
                SelectedSavedReport = SavedReports.FirstOrDefault() ?? "";
                return true;
            });
            Diagnostics.ReadStatus = "complete";
        }
        catch (Exception error)
        {
            Diagnostics.ReadStatus = "failed";
            Diagnostics.FailedStage = stage;
            Diagnostics.Error = error.Message;
            Diagnostics.ErrorType = error.GetType().FullName;
            if (stage is not null)
                Diagnostics.InterruptedStageMs[stage] = Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds;
            throw;
        }
        finally
        {
            workerCompleted = Stopwatch.GetTimestamp();
            Diagnostics.WorkerBodyMs = Stopwatch.GetElapsedTime(workerStarted, workerCompleted.Value).TotalMilliseconds;
            RecordTracePoint("workerBodyEnd", workerCompleted.Value);
        }
    }

    private T Measure<T>(string name, Func<T> action)
    {
        stage = name;
        stageStarted = Stopwatch.GetTimestamp();
        T value = action();
        Diagnostics.StageMs[name] = Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds;
        stage = null;
        return value;
    }

    public void ObserveUiContinuation()
    {
        long observed = Stopwatch.GetTimestamp();
        if (uiOperationStartedTimestamp is null || workerCompleted is null || uiContinuation is not null)
            throw new InvalidOperationException("A UI continuation requires one finished, UI-dispatched worker.");
        uiContinuation = observed;
        Diagnostics.WorkerCompletionToUiContinuationMs = Stopwatch.GetElapsedTime(workerCompleted.Value, observed).TotalMilliseconds;
        RecordTracePoint("continuationObserved", observed);
    }

    public double CompleteUi()
    {
        if (uiOperationStartedTimestamp is not long start || uiContinuation is null || Diagnostics.EnclosingLocalOpenMs is not null)
            throw new InvalidOperationException("Complete the UI operation once, after observing its continuation.");
        long completed = Stopwatch.GetTimestamp();
        double elapsed = Stopwatch.GetElapsedTime(start, completed).TotalMilliseconds;
        Diagnostics.EnclosingLocalOpenMs = elapsed;
        RecordTracePoint("uiComplete", completed);
        return elapsed;
    }

    public void RecordTracePoint(string name, long capturedTimestamp)
    {
        var trace = Diagnostics.Trace;
        trace.OriginTimestamp ??= uiOperationStartedTimestamp ?? capturedTimestamp;
        string markerName = $"wi032:{trace.AttemptId:N}:{name}";
        int managedThreadId = Environment.CurrentManagedThreadId;
        long? hookStarted = null, hookCompleted = null;
        LocalOpenNativeTrace? native = null;
        string? hookError = null;
        if (nativeMarker is not null)
        {
            hookStarted = Stopwatch.GetTimestamp();
            try { native = nativeMarker(markerName); }
            catch (Exception error) { hookError = $"{error.GetType().FullName}: {error.Message}"; }
            finally { hookCompleted = Stopwatch.GetTimestamp(); }
        }
        trace.Points[name] = new(markerName, capturedTimestamp,
            Stopwatch.GetElapsedTime(trace.OriginTimestamp.Value, capturedTimestamp).TotalMilliseconds,
            managedThreadId, hookStarted, hookCompleted, native, hookError);
    }

    // Startup/owner-switch reports are new outcomes. A refresh retains its own
    // outcome and attaches only Diagnostics for its subsequent local reopen.
    public void ApplyToNewReport(SpikeReport report)
    {
        report.LocalOpen = Diagnostics;
        report.Sqlite = Store?.Environment;
        report.Manifest = Active?.Manifest;
        report.Generation = Active?.Generation;
        report.UnsentFingerprintBefore = Diagnostics.StageMs["unsentRead"] is null ? null : Work.Fingerprint;
        report.UnsentFingerprintAfter = report.UnsentFingerprintBefore;
        report.OriginalUnsentSha256 = Work.OriginalFixtureSha256;
        if (Diagnostics.EnclosingLocalOpenMs is double elapsed)
            report.PhaseMs["databaseAndWorkReopen"] = elapsed;
        report.Status = Diagnostics.ReadStatus == "failed" ? "failed"
            : Diagnostics.ReadStatus == "complete" && Active is not null ? "offline-open-complete" : "incomplete";
        report.Error = Diagnostics.Error;
        report.FailedOperation = Diagnostics.FailedStage is null ? null : "localOpen." + Diagnostics.FailedStage;
    }
}
