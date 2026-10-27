using System.Text.Json;
using System.Diagnostics;
using FieldSales.TabletSpike.Core;
using Microsoft.Data.Sqlite;

namespace FieldSales.TabletSpike.IntegrationTests;

public sealed class LocalOpenScenarios
{
    [Fact]
    public async Task Should_RetainTheDayEditedWorkAndLatestReport_When_LocalDataIsRead()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture);
        string generation = await env.Apply(fixture);
        var work = env.Work();
        work.EditNote(work.Read().Items.Single(i => i.Kind == "Order").Id, "Keep the local-open characterization");
        var before = work.Read();
        var workspace = env.Workspace();
        string older = Path.Combine(workspace.DirectoryPath, "report-older.json");
        string latest = Path.Combine(workspace.DirectoryPath, "report-latest.json");
        await File.WriteAllTextAsync(older, "{\"status\":\"incomplete\"}");
        await File.WriteAllTextAsync(latest, "{\"status\":\"complete\"}");
        await File.WriteAllTextAsync(Path.Combine(workspace.DirectoryPath, "other.json"), "{}");
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(latest, new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc));

        var actual = ReadLocal(Path.Combine(env.Root, "store"), workspace.Owner);

        Assert.Equal(generation, actual.Active!.Generation);
        Assert.Equal(fixture.Manifest.Json.Sha256, actual.Active.Manifest.Json.Sha256);
        Assert.Equal(before.Fingerprint, actual.Work.Fingerprint);
        Assert.Equal(before.OriginalFixtureSha256, actual.Work.OriginalFixtureSha256);
        Assert.Equal(before.Items.ToArray(), actual.Work.Items.ToArray());
        Assert.Equal(new[] { latest, older }, actual.Reports);
        Assert.Equal(generation, env.Store().ReadActive()!.Generation);
        Assert.Equal("{\"status\":\"incomplete\"}", await File.ReadAllTextAsync(older));
        Assert.Equal("{\"status\":\"complete\"}", await File.ReadAllTextAsync(latest));
    }

    [Fact]
    public async Task Should_PreserveUnsentWorkAndTheError_When_SnapshotReadFails()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); await env.Apply(fixture);
        var work = env.Work();
        work.EditNote(work.Read().Items.Single(i => i.Kind == "Order").Id, "Keep work despite a bad snapshot");
        var before = work.Read();
        // SQLite cannot open a directory as a database. Preserve the original
        // database instead of corrupting it and exercising provider cleanup.
        var workspace = env.Workspace();
        string preserved = workspace.SnapshotPath + ".preserved";
        File.Move(workspace.SnapshotPath, preserved);
        Directory.CreateDirectory(workspace.SnapshotPath);

        try
        {
            Assert.Throws<SqliteException>(() => ReadLocal(Path.Combine(env.Root, "store"), workspace.Owner));
        }
        finally
        {
            Directory.Delete(workspace.SnapshotPath); // Empty path created above, within this test's unique workspace.
            File.Move(preserved, workspace.SnapshotPath);
        }

        Assert.Equal(before.Fingerprint, work.Read().Fingerprint);
        Assert.Equal(before.Items.ToArray(), work.Read().Items.ToArray());
    }

    [Fact]
    public async Task Should_KeepDisjointStagesInsideTheirEnclosingClocks_When_AUiReadCompletes()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); string generation = await env.Apply(fixture);
        var attempt = new LocalOpenAttempt(Stopwatch.GetTimestamp());
        await Task.Run(() => attempt.Read(() => Path.Combine(env.Root, "store"), "spike-rep-42"));
        attempt.ObserveUiContinuation();
        double enclosing = attempt.CompleteUi();
        var actual = attempt.Diagnostics;
        Assert.Equal("complete", actual.ReadStatus);
        Assert.Null(actual.FailedStage); Assert.Empty(actual.InterruptedStageMs);
        Assert.All(actual.StageMs.Values, value => Assert.InRange(value!.Value, 0, actual.WorkerBodyMs!.Value));
        Assert.True(actual.StageMs.Values.Sum(value => value!.Value) <= actual.WorkerBodyMs!.Value + 0.001);
        Assert.True(actual.DispatchToWorkerEntryMs!.Value + actual.WorkerBodyMs!.Value +
            actual.WorkerCompletionToUiContinuationMs!.Value <= enclosing + 0.001);
        var report = env.Report(); attempt.ApplyToNewReport(report);
        Assert.Equal("offline-open-complete", report.Status); Assert.Equal(generation, report.Generation);
        Assert.Equal(enclosing, report.PhaseMs["databaseAndWorkReopen"]);
        string original = report.ToJson();
        string path = Path.Combine(env.Workspace().DirectoryPath, "local-open-export.json");
        report.Save(path);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
        Assert.Equal(original, report.ToJson());
    }

    [Fact]
    public async Task Should_KeepTheFailedStageUnavailableAndNeverClaimSuccess_When_SqliteCannotOpen()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); string generation = await env.Apply(fixture);
        var workspace = env.Workspace();
        string fingerprint = env.Work().Read().Fingerprint;
        string preserved = workspace.SnapshotPath + ".preserved";
        File.Move(workspace.SnapshotPath, preserved); Directory.CreateDirectory(workspace.SnapshotPath);
        var attempt = new LocalOpenAttempt(Stopwatch.GetTimestamp());
        try
        {
            var error = Assert.Throws<SqliteException>(() => attempt.Read(() => Path.Combine(env.Root, "store"), workspace.Owner));
            attempt.ObserveUiContinuation(); attempt.CompleteUi();
            var report = env.Report(); attempt.ApplyToNewReport(report);
            Assert.Equal("failed", report.Status);
            Assert.Equal(error.Message, report.Error);
            Assert.Equal("localOpen.snapshotInitialization", report.FailedOperation);
            Assert.Equal("failed", attempt.Diagnostics.ReadStatus);
            Assert.NotNull(attempt.Diagnostics.StageMs["workspace"]);
            Assert.All(attempt.Diagnostics.StageMs.Where(pair => pair.Key != "workspace"), pair => Assert.Null(pair.Value));
            Assert.True(attempt.Diagnostics.InterruptedStageMs["snapshotInitialization"] >= 0);
            Assert.Null(report.UnsentFingerprintBefore); Assert.Null(report.UnsentFingerprintAfter);
            using var json = JsonDocument.Parse(report.ToJson());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("localOpen").GetProperty("stageMs").GetProperty("unsentRead").ValueKind);
        }
        finally
        {
            Directory.Delete(workspace.SnapshotPath);
            File.Move(preserved, workspace.SnapshotPath);
        }
        Assert.Equal(fingerprint, env.Work().Read().Fingerprint);
        Assert.Equal(generation, env.Store().ReadActive()!.Generation);
    }

    [Fact]
    public async Task Should_LeaveUiClocksUnavailableInExport_When_TheReadIsHeadless()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); await env.Apply(fixture);
        var attempt = new LocalOpenAttempt();
        attempt.Read(() => Path.Combine(env.Root, "store"), "spike-rep-42");
        var report = env.Report(); attempt.ApplyToNewReport(report);
        Assert.Equal("offline-open-complete", report.Status);
        Assert.False(report.PhaseMs.ContainsKey("databaseAndWorkReopen"));
        Assert.Null(attempt.Diagnostics.DispatchToWorkerEntryMs);
        Assert.Null(attempt.Diagnostics.WorkerCompletionToUiContinuationMs);
        Assert.Null(attempt.Diagnostics.EnclosingLocalOpenMs);
        Assert.NotNull(attempt.Diagnostics.WorkerBodyMs);
        using var json = JsonDocument.Parse(report.ToJson());
        Assert.Equal("spike-measurements-v4", json.RootElement.GetProperty("reportSchema").GetString());
        var local = json.RootElement.GetProperty("localOpen");
        foreach (string property in new[] { "dispatchToWorkerEntryMs", "workerCompletionToUiContinuationMs", "enclosingLocalOpenMs" })
            Assert.Equal(JsonValueKind.Null, local.GetProperty(property).ValueKind);
        var trace = attempt.Diagnostics.Trace;
        Assert.Equal(0, trace.Points["workerEntry"]!.OffsetMs);
        foreach (string point in new[] { "uiDispatch", "workerDelegateExit", "callerContinuation", "continuationObserved", "uiComplete" })
        {
            Assert.Null(trace.Points[point]);
            Assert.Equal(JsonValueKind.Null, local.GetProperty("trace").GetProperty("points").GetProperty(point).ValueKind);
        }
        Assert.All(trace.Points.Values.OfType<LocalOpenTracePoint>(), point =>
        {
            Assert.Null(point.Native); Assert.Null(point.HookStartedTimestamp); Assert.Null(point.HookCompletedTimestamp);
        });
    }

    [Fact]
    public async Task Should_RetainLocalDataAndOriginalTraceBoundaries_When_CallerMarkersAreEnabled()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); string generation = await env.Apply(fixture);
        var workspace = env.Workspace();
        var work = env.Work();
        work.EditNote(work.Read().Items.Single(i => i.Kind == "Order").Id, "Keep traced edited work");
        var before = work.Read();
        string older = Path.Combine(workspace.DirectoryPath, "report-older.json");
        string latest = Path.Combine(workspace.DirectoryPath, "report-latest.json");
        await File.WriteAllTextAsync(older, "older original report");
        await File.WriteAllTextAsync(latest, "latest original report");
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(latest, new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc));
        var emitted = new List<string>();
        var attempt = new LocalOpenAttempt(Stopwatch.GetTimestamp(), marker =>
        {
            emitted.Add(marker);
            return new(null, null, false); // No native thread identity is fabricated by the portable test.
        });
        await ReadWithCallerMarkers(attempt, env);

        Assert.Equal(generation, attempt.Active!.Generation);
        Assert.Equal(fixture.Manifest.Json.Sha256, attempt.Active.Manifest.Json.Sha256);
        Assert.Equal(before.Items.ToArray(), attempt.Work.Items.ToArray());
        Assert.Equal(before.Fingerprint, attempt.Work.Fingerprint);
        Assert.Equal(new[] { latest, older }, attempt.SavedReports); Assert.Equal(latest, attempt.SelectedSavedReport);
        Assert.Equal("older original report", await File.ReadAllTextAsync(older));
        Assert.Equal("latest original report", await File.ReadAllTextAsync(latest));
        var trace = attempt.Diagnostics.Trace;
        var points = trace.Points.Values.Select(point => Assert.IsType<LocalOpenTracePoint>(point)).ToArray();
        Assert.Equal(points.Select(point => point.MarkerName), emitted);
        Assert.Equal(points.OrderBy(point => point.CapturedTimestamp).ToArray(), points);
        Assert.All(points, point =>
        {
            Assert.StartsWith($"wi032:{trace.AttemptId:N}:", point.MarkerName);
            Assert.InRange(point.OffsetMs, 0, attempt.Diagnostics.EnclosingLocalOpenMs!.Value);
            Assert.True(point.CapturedTimestamp <= point.HookStartedTimestamp);
            Assert.True(point.HookStartedTimestamp <= point.HookCompletedTimestamp);
            Assert.True(point.ManagedThreadId > 0); Assert.False(point.Native!.Enabled); Assert.Null(point.HookError);
        });
        Assert.True(trace.Points["workerEntry"]!.ManagedThreadId == trace.Points["workerDelegateExit"]!.ManagedThreadId);
        var report = env.Report(); attempt.ApplyToNewReport(report);
        string original = report.ToJson();
        string path = Path.Combine(workspace.DirectoryPath, "traced-export.json"); report.Save(path);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
        Assert.Equal(original, report.ToJson());
        Assert.Equal(before.Fingerprint, work.Read().Fingerprint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_KeepTheStorageOutcomeAndSavedWork_When_TheTraceHookFails(bool failRead)
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); string generation = await env.Apply(fixture);
        var workspace = env.Workspace(); string before = env.Work().Read().Fingerprint;
        string preserved = workspace.SnapshotPath + ".preserved";
        if (failRead) { File.Move(workspace.SnapshotPath, preserved); Directory.CreateDirectory(workspace.SnapshotPath); }
        var attempt = new LocalOpenAttempt(Stopwatch.GetTimestamp(), _ => throw new InvalidOperationException("trace hook unavailable"));
        try
        {
            var error = await Record.ExceptionAsync(() => ReadWithCallerMarkers(attempt, env));
            var report = env.Report(); attempt.ApplyToNewReport(report);
            if (failRead)
            {
                Assert.IsType<SqliteException>(error);
                Assert.Equal(error.Message, report.Error); Assert.Equal("failed", report.Status);
                Assert.Equal("localOpen.snapshotInitialization", report.FailedOperation);
                Assert.Null(attempt.Diagnostics.StageMs["unsentRead"]); Assert.Null(report.UnsentFingerprintAfter);
            }
            else
            {
                Assert.Null(error); Assert.Null(report.Error); Assert.Equal("offline-open-complete", report.Status);
                Assert.Equal(generation, report.Generation); Assert.Equal(before, report.UnsentFingerprintAfter);
            }
            Assert.All(attempt.Diagnostics.Trace.Points.Values, point =>
            {
                Assert.NotNull(point); Assert.Null(point.Native);
                Assert.Contains("trace hook unavailable", point.HookError);
                Assert.True(point.HookCompletedTimestamp >= point.HookStartedTimestamp);
            });
        }
        finally
        {
            if (failRead) { Directory.Delete(workspace.SnapshotPath); File.Move(preserved, workspace.SnapshotPath); }
        }
        Assert.Equal(before, env.Work().Read().Fingerprint); Assert.Equal(generation, env.Store().ReadActive()!.Generation);
    }

    private static async Task ReadWithCallerMarkers(LocalOpenAttempt attempt, FixtureEnvironment env)
    {
        attempt.RecordTracePoint("uiDispatch", Stopwatch.GetTimestamp());
        try
        {
            await Task.Run(() =>
            {
                try { attempt.Read(() => Path.Combine(env.Root, "store"), "spike-rep-42"); }
                finally { long exited = Stopwatch.GetTimestamp(); attempt.RecordTracePoint("workerDelegateExit", exited); }
            });
        }
        finally
        {
            long continued = Stopwatch.GetTimestamp(); attempt.RecordTracePoint("callerContinuation", continued);
            attempt.ObserveUiContinuation(); attempt.CompleteUi();
        }
    }

    // The unchanged outcomes were verified against the inline worker before extraction.
    private static (ActiveSnapshot? Active, UnsentState Work, string[] Reports) ReadLocal(string root, string owner)
    {
        var attempt = new LocalOpenAttempt();
        attempt.Read(() => root, owner);
        return (attempt.Active, attempt.Work, attempt.SavedReports);
    }
}
