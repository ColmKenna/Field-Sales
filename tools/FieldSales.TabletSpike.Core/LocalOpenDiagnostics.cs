using System.Diagnostics;

namespace FieldSales.TabletSpike.Core;

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
    public string ClockScopes => "One Stopwatch timestamp origin for the UI operation. dispatchToWorkerEntryMs ends at the first instrumented worker marker; includes dispatch/setup and any startup/JIT before that marker, not isolated thread-pool queue time. stageMs contains disjoint completed worker stages, null when unfinished/unobserved; workspace includes resolving the platform root. snapshotInitialization includes connection/schema/native inspection; activeRead and unsentRead include their own connection/schema setup. reportEnumeration includes filename enumeration, modification-time sorting and selection. workerBodyMs encloses all stages, failure recording and instrumentation gaps. workerCompletionToUiContinuationMs ends at the first observed continuation, not rendering/first paint. enclosingLocalOpenMs retains databaseAndWorkReopen's scope through UI-field updates. Do not add enclosing clocks to their subsets. Headless reads leave dispatch/UI/enclosing clocks unavailable.";
}

// The caller dispatches this synchronous read sequence. No provider settings,
// store operations or ordering differ from the original device worker.
public sealed class LocalOpenAttempt(long? uiOperationStartedTimestamp = null)
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
        if (uiOperationStartedTimestamp is long start)
            Diagnostics.DispatchToWorkerEntryMs = Stopwatch.GetElapsedTime(start, workerStarted).TotalMilliseconds;
        Diagnostics.ReadStatus = "reading";
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
    }

    public double CompleteUi()
    {
        if (uiOperationStartedTimestamp is not long start || uiContinuation is null || Diagnostics.EnclosingLocalOpenMs is not null)
            throw new InvalidOperationException("Complete the UI operation once, after observing its continuation.");
        double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Diagnostics.EnclosingLocalOpenMs = elapsed;
        return elapsed;
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
