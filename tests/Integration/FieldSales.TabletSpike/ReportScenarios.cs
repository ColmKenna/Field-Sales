using System.Text.Json;
using System.Diagnostics;
using FieldSales.TabletSpike.Cli;
using FieldSales.TabletSpike.Core;

namespace FieldSales.TabletSpike.IntegrationTests;

public sealed class ReportScenarios
{
    [Fact]
    public async Task Should_PersistTheOutcomeAndEditedWork_When_RefreshSucceedsOrFails()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture);
        var work = env.Work();
        work.EditNote(work.Read().Items.Single(i => i.Kind == "Order").Id, "Keep this edited order");
        string fingerprint = work.Read().Fingerprint;
        var endpoint = await env.StartHost();
        var complete = env.Report();
        await new SpikeRunner(env.Workspace(), env.Http).RefreshAsync(endpoint, complete);
        var failed = env.Report();
        await Assert.ThrowsAsync<HttpRequestException>(() => new SpikeRunner(env.Workspace(), env.Http)
            .RefreshAsync(new Uri(endpoint, "missing/"), failed));
        foreach (var report in new[] { complete, failed })
        {
            using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(env.Workspace().DirectoryPath, $"report-{report.RunId}.json")));
            Assert.Equal(report.Status, saved.RootElement.GetProperty("status").GetString());
            Assert.Equal(fingerprint, saved.RootElement.GetProperty("unsentFingerprintAfter").GetString());
            Assert.True(saved.RootElement.GetProperty("phaseMs").GetProperty("totalRefresh").GetDouble() > 0);
        }
        Assert.Equal("complete", complete.Status); Assert.Equal("failed", failed.Status);
        Assert.Equal("manifest", failed.FailedOperation);
        Assert.True(failed.InterruptedPhaseMs["manifest"] >= 0);
        Assert.False(failed.PhaseMs.ContainsKey("manifest"));
        foreach (string phase in new[] { "activation", "completedGenerationDeletion", "walCheckpoint", "reportFinalization" })
            Assert.InRange(complete.PhaseMs[phase], 0, complete.PhaseMs["totalRefresh"]);
        Assert.Null(complete.PendingOperation); Assert.Null(failed.PendingOperation);
        Assert.Equal(fingerprint, work.Read().Fingerprint);
    }

    [Fact]
    public async Task Should_ReportOnlyPhysicalFileSizesInTotal_When_LogicalPayloadsExist()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); await env.Apply(fixture);
        var workspace = env.Workspace();
        var disk = workspace.Measure(false);
        long Size(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
        long expected = new[] { workspace.SnapshotPath, workspace.UnsentPath }
            .Sum(path => new[] { "", "-wal", "-journal", "-shm" }.Sum(suffix => Size(path + suffix)))
            + Size(workspace.TransferPath);
        Assert.Equal(expected, disk.TotalBytes); Assert.True(expected > 0);
        Assert.Equal(disk.TotalBytes, workspace.Measure(true).TotalBytes);
    }

    [Fact]
    public async Task Should_ExportPhysicalBytesWithoutOpeningSqlite_When_TheOptionalScanIsNotRequested()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); await env.Apply(fixture);
        var workspace = env.Workspace();
        DiskMeasurement physical;
        // Even a locked database must not stop a file-size sample or report serialization.
        using (File.Open(workspace.SnapshotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            physical = workspace.Measure();
        Assert.Null(physical.GenerationPayloadBytes); Assert.Null(physical.GenerationPayloadScanMs);
        var report = env.Report(); report.DiskSamples.Add(physical);
        using var json = JsonDocument.Parse(report.ToJson());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("diskSamples")[0].GetProperty("generationPayloadBytes").ValueKind);
        Assert.Contains("unavailable", physical.GenerationPayloadMetric);
        var logical = workspace.Measure(includeGenerationPayloads: true);
        Assert.NotEmpty(logical.GenerationPayloadBytes!); Assert.True(logical.GenerationPayloadScanMs >= 0);
        Assert.Equal(physical.TotalBytes, logical.TotalBytes);
    }

    [Theory]
    [InlineData("completedGenerationDeletion")]
    [InlineData("walCheckpoint")]
    [InlineData("reportFinalization")]
    [InlineData("reportPublication")]
    public async Task Should_KeepAnIncompletePendingReportAndWholeActiveData_When_TheProcessStopsAfterActivation(string operation)
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture); await env.Apply(fixture);
        await env.Apply(fixture); // The replacement must retire an older completed generation.
        env.Work().EditNote(env.Work().Read().Items.Single(i => i.Kind == "Order").Id, "Preserve edited work after process stop");
        string fingerprint = env.Work().Read().Fingerprint;
        await env.Export(profile: "target", products: 120);
        var endpoint = await env.StartHost(profile: "target");
        string ready = Path.Combine(env.Root, "report-child-ready");
        string assembly = typeof(ReportScenarios).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"), "--depsfile", Path.ChangeExtension(assembly, ".deps.json"),
            typeof(SpikeCliMarker).Assembly.Location, Path.Combine(env.Root, "store"), "spike-rep-42", "refresh", endpoint.ToString(), "pending-" + operation, ready }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!;
        Task<string> errors = child.StandardError.ReadToEndAsync(), output = child.StandardOutput.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!File.Exists(ready) && !child.HasExited) await Task.Delay(25, timeout.Token);
            Assert.True(File.Exists(ready), $"Child exited: {(child.HasExited ? await errors : "still running")}");
            child.Kill(entireProcessTree: true); await child.WaitForExitAsync(timeout.Token);
        }
        finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
        await errors; await output;
        string path = Directory.GetFiles(env.Workspace().DirectoryPath, "report-*.json").Single();
        string saved = await File.ReadAllTextAsync(path);
        using var json = JsonDocument.Parse(saved); var root = json.RootElement;
        Assert.Equal("incomplete", root.GetProperty("status").GetString());
        Assert.Equal(operation, root.GetProperty("pendingOperation").GetString());
        Assert.False(root.GetProperty("phaseMs").TryGetProperty("totalRefresh", out _));
        Assert.True(root.GetProperty("phaseMs").GetProperty("activation").GetDouble() >= 0);
        Assert.False(root.GetProperty("phaseMs").TryGetProperty(operation, out _));
        if (operation != "completedGenerationDeletion") Assert.True(root.GetProperty("phaseMs").GetProperty("completedGenerationDeletion").GetDouble() >= 0);
        if (operation == "reportPublication") Assert.True(root.GetProperty("phaseMs").GetProperty("reportFinalization").GetDouble() >= 0);
        using (var read = env.Store().Capture())
        {
            Assert.Equal(120, read.Count("products"));
            Assert.Equal(root.GetProperty("generation").GetString(), read.Active!.Generation);
        }
        Assert.Equal(fingerprint, env.Work().Read().Fingerprint);
        Assert.Equal(saved, await File.ReadAllTextAsync(path)); // Reopening never rewrites a refresh outcome.
    }
}
