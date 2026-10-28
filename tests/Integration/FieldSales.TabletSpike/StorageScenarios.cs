using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FieldSales.SnapshotSpike;
using FieldSales.TabletSpike.Core;
using FieldSales.TabletSpike.Cli;
using Microsoft.Data.Sqlite;

namespace FieldSales.TabletSpike.IntegrationTests;

public sealed class StorageScenarios
{
    [Fact]
    public async Task Should_ImportEveryPayloadFamily_When_TheFixtureIsComplete()
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture);
        var endpoint = await env.StartHost(); var report = env.Report();
        await new SpikeRunner(env.Workspace(), env.Http).RefreshAsync(endpoint, report);
        Assert.Equal("complete", report.Status);
        using var read = env.Store().Capture();
        Assert.NotNull(read.Active);
        foreach (string family in FixtureManifest.Families)
        {
            string count = family == "history" ? "historicalOrders" : family;
            Assert.Equal(fixture.Manifest.Counts[count], read.Count(family));
            using var source = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Directory, "snapshot.json")));
            Assert.Equal(source.RootElement.GetProperty(family)[0].GetRawText(), read.FirstJson(family));
        }
        using var product = JsonDocument.Parse(read.FirstJson("products")!);
        Assert.Equal(8, product.RootElement.GetProperty("attributes").GetArrayLength());
        Assert.Equal(4, product.RootElement.GetProperty("breadcrumbs").GetArrayLength());
        Assert.InRange(report.MaxParserBufferBytes, 1, 1024 * 1024);
        Assert.Equal(env.Work().Read().Fingerprint, report.UnsentFingerprintBefore);
        // Logical diagnostics are opt-in; verify the payload metric without scanning in refresh.
        Assert.All(report.DiskSamples, d => Assert.Null(d.GenerationPayloadBytes));
        Assert.NotEmpty(env.Workspace().Measure(includeGenerationPayloads: true).GenerationPayloadBytes!);
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("corrupt")]
    [InlineData("plain-hash")]
    [InlineData("foreign-header")]
    [InlineData("unsupported-header")]
    [InlineData("unsupported-manifest")]
    [InlineData("counts")]
    [InlineData("invalid-json")]
    [InlineData("oversized-row")]
    [InlineData("missing-family")]
    [InlineData("duplicate-header")]
    public async Task Should_KeepActiveDataAndUnsent_When_PayloadIsTruncatedCorruptForeignOrUnsupported(string variant)
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(); env.Seed(fixture);
        string original = await env.Apply(fixture);
        EditOrder(env); var work = env.Work().Read();
        var bad = fixture;
        string json = await File.ReadAllTextAsync(Path.Combine(fixture.Directory, "snapshot.json"));
        switch (variant)
        {
            case "truncated":
                var bytes = await File.ReadAllBytesAsync(fixture.Gzip);
                string truncated = Path.Combine(env.Root, "truncated.gz"); await File.WriteAllBytesAsync(truncated, bytes[..^10]);
                bad = fixture with { Gzip = truncated }; break;
            case "corrupt":
                var corrupt = await File.ReadAllBytesAsync(fixture.Gzip); corrupt[20] ^= 0xff;
                string path = Path.Combine(env.Root, "corrupt.gz"); await File.WriteAllBytesAsync(path, corrupt);
                bad = fixture with { Gzip = path }; break;
            case "plain-hash": bad = fixture with { Manifest = fixture.Manifest with { Json = fixture.Manifest.Json with { Sha256 = new string('0', 64) } } }; break;
            case "foreign-header": bad = await env.WithPayload(fixture, json.Replace("spike-rep-42", "spike-rep-77")); break;
            case "unsupported-header": bad = await env.WithPayload(fixture, json.Replace("spike-format-v1", "spike-format-v9")); break;
            case "unsupported-manifest": bad = fixture with { Manifest = fixture.Manifest with { Schema = "spike-format-v9" } }; break;
            case "counts":
                var counts = new Dictionary<string, long>(fixture.Manifest.Counts); counts["attributes"]++;
                bad = fixture with { Manifest = fixture.Manifest with { Counts = counts } }; break;
            case "invalid-json": bad = await env.WithPayload(fixture, json[..^2]); break;
            case "oversized-row":
                var huge = JsonNode.Parse(json)!; huge["locations"]![0]!["name"] = new string('x', 1024 * 1024 + 1);
                bad = await env.WithPayload(fixture, huge.ToJsonString()); break;
            case "missing-family":
                var missing = JsonNode.Parse(json)!.AsObject(); missing.Remove("leads"); bad = await env.WithPayload(fixture, missing.ToJsonString()); break;
            case "duplicate-header": bad = await env.WithPayload(fixture, "{\"schema\":\"spike-format-v1\"," + json[1..]); break;
        }
        await Assert.ThrowsAnyAsync<Exception>(() => env.Apply(bad));
        Assert.Equal(original, env.Store().ReadActive()!.Generation);
        Assert.Equal(work, env.Work().Read(), UnsentComparer.Instance);
        await env.Apply(fixture);
        Assert.Equal(work, env.Work().Read(), UnsentComparer.Instance);
    }

    [Theory]
    [InlineData("transfer")]
    [InlineData("import")]
    public async Task Should_KeepActiveDataAndUnsent_When_TransferOrImportIsCancelled(string phase)
    {
        await using var env = new FixtureEnvironment();
        var fixture = await env.Export(products: 1500); env.Seed(fixture);
        string active = await env.Apply(fixture); EditOrder(env); var work = env.Work().Read();
        using var cancellation = new CancellationTokenSource();
        bool partial = false;
        var hooks = new SpikeHooks
        {
            DownloadProgress = bytes => { if (phase == "transfer" && bytes < fixture.Manifest.Gzip.Bytes) { partial = true; cancellation.Cancel(); } },
            BatchWritten = rows => { if (phase == "import") { partial = rows > 0; cancellation.Cancel(); } }
        };
        var endpoint = await env.StartHost();
        // Transfer cancellation needs a partial download; import cancellation happens at
        // BatchWritten, so it must not spend time simulating a slow connection first.
        var report = env.Report(network: phase == "transfer"
            ? new("application-level-simulation", 0.1)
            : new("loopback-unthrottled"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SpikeRunner(env.Workspace(), env.Http).RefreshAsync(endpoint, report, hooks, cancellation.Token));
        Assert.True(partial); Assert.Equal("cancelled", report.Status);
        Assert.Equal(active, env.Store().ReadActive()!.Generation);
        Assert.Equal(work, env.Work().Read(), UnsentComparer.Instance);
        await new SpikeRunner(env.Workspace(), env.Http).RefreshAsync(endpoint, env.Report());
        Assert.NotEqual(active, env.Store().ReadActive()!.Generation);
        Assert.Equal(work, env.Work().Read(), UnsentComparer.Instance);
    }

    [Fact]
    public async Task Should_KeepEditedUnsentWork_When_RefreshingOrReopening()
    {
        await using var env = new FixtureEnvironment(); var fixture = await env.Export(); env.Seed(fixture);
        await env.Apply(fixture); EditOrder(env); var before = env.Work().Read();
        Assert.Equal(2, before.Items.Count(i => i.Kind == "Call"));
        Assert.Single(before.Items, i => i.Kind == "Order"); Assert.Single(before.Items, i => i.Kind == "ScheduleChange");
        await env.Apply(fixture); env.Seed(fixture); // A repeated seed request must not overwrite local work.
        Assert.Equal(before, new UnsentSqliteStore(env.Workspace()).Read(), UnsentComparer.Instance);
        foreach (var item in before.Items)
        {
            using var payload = JsonDocument.Parse(item.Json);
            Assert.Equal("spike-previous-snapshot", payload.RootElement.GetProperty("capturedUnderSnapshot").GetString());
        }
        Assert.Equal(fixture.Manifest.Unsent.Sha256, before.OriginalFixtureSha256);
    }

    [Fact]
    public async Task Should_IsolateSnapshotAndWork_When_SelectingAnotherSyntheticOwner()
    {
        await using var env = new FixtureEnvironment(); var first = await env.Export(); var other = await env.Export(77);
        env.Seed(first); env.Seed(other, 77); string generation = await env.Apply(first);
        EditOrder(env); var work = env.Work().Read(); await env.Apply(other, 77);
        Assert.NotEqual(env.Store().ReadActive()!.Generation, env.Store(77).ReadActive()!.Generation);
        Assert.NotEqual(env.Work().Read().Fingerprint, env.Work(77).Read().Fingerprint);
        await Assert.ThrowsAsync<InvalidDataException>(() => env.Apply(other));
        Assert.Throws<InvalidDataException>(() => env.Work().SeedOnce(File.ReadAllBytes(other.Unsent), other.Manifest.Unsent));
        Assert.Equal(generation, env.Store().ReadActive()!.Generation); Assert.Equal(work, env.Work().Read(), UnsentComparer.Instance);
    }

    [Theory]
    [InlineData("before-activation")]
    [InlineData("after-activation")]
    public async Task Should_RecoverOneWholeGeneration_When_ProcessEndsBeforeOrAfterActivation(string boundary)
    {
        await using var env = new FixtureEnvironment(); var fixture = await env.Export(); var next = await env.Export(profile: "target", products: 120);
        env.Seed(fixture); string old = await env.Apply(fixture); EditOrder(env); var work = env.Work().Read();
        string ready = Path.Combine(env.Root, "child-ready");
        string manifest = Path.Combine(next.Directory, "manifest.json");
        string testAssembly = typeof(StorageScenarios).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, ".runtimeconfig.json"), "--depsfile", Path.ChangeExtension(testAssembly, ".deps.json"),
            typeof(SpikeCliMarker).Assembly.Location, Path.Combine(env.Root, "store"), "spike-rep-42", "apply", next.Gzip, manifest, boundary, ready }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!;
        Task<string> errors = child.StandardError.ReadToEndAsync(); Task<string> output = child.StandardOutput.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!File.Exists(ready) && !child.HasExited) await Task.Delay(25, timeout.Token);
            Assert.True(File.Exists(ready), $"Child failed: {await (child.HasExited ? errors : Task.FromResult("still running"))}");
            child.Kill(entireProcessTree: true); await child.WaitForExitAsync(timeout.Token);
        }
        finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
        await output; await errors;
        var reopened = env.Store();
        Assert.Equal(boundary == "before-activation" ? old : next.Manifest.Json.Sha256, boundary == "before-activation" ? reopened.ReadActive()!.Generation : reopened.ReadActive()!.Manifest.Json.Sha256);
        using var read = reopened.Capture();
        Assert.Equal(boundary == "before-activation" ? 80 : 120, read.Count("products"));
        Assert.Equal(work, env.Work().Read(), UnsentComparer.Instance);
        await env.Apply(next); // Recovery reclaims incomplete rows and supports a clean refresh.
        Assert.Equal(120, env.Store().CaptureCount("products"));
    }

    [Theory]
    [InlineData("write")]
    [InlineData("commit")]
    [InlineData("disk-full")]
    public async Task Should_KeepPreviousData_When_StorageOrCommitFails(string failure)
    {
        await using var env = new FixtureEnvironment(); var fixture = await env.Export(); env.Seed(fixture);
        string old = await env.Apply(fixture); EditOrder(env); var work = env.Work().Read();
        var next = await env.Export(profile: "target", products: 600);
        int pages;
        using (var connection = SqliteEnvironment.Open(env.Workspace().SnapshotPath))
        using (var command = connection.CreateCommand()) { command.CommandText = "PRAGMA page_count"; pages = Convert.ToInt32(command.ExecuteScalar()); }
        var hooks = new SpikeHooks
        {
            BatchWritten = _ => { if (failure == "write") throw new IOException("Injected write boundary failure"); },
            BeforeCommit = () => { if (failure == "commit") throw new IOException("Injected activation commit failure"); },
            MaxDatabasePages = failure == "disk-full" ? pages + 2 : null
        };
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => env.Apply(next, hooks: hooks));
        if (failure == "disk-full") Assert.Equal(13, Assert.IsType<SqliteException>(exception).SqliteErrorCode);
        Assert.Equal(old, env.Store().ReadActive()!.Generation); Assert.Equal(work, env.Work().Read(), UnsentComparer.Instance);
        await env.Apply(next); Assert.NotEqual(old, env.Store().ReadActive()!.Generation);
    }

    [Fact]
    public async Task Should_KeepReadsCoherent_When_AnotherRefreshIsRequested()
    {
        await using var env = new FixtureEnvironment(); var first = await env.Export(); var next = await env.Export(profile: "target", products: 160);
        await env.Apply(first);
        using var reader = env.Store().Capture(); string old = reader.Active!.Generation;
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var update = env.Apply(next, hooks: new() { AtBoundary = b => { if (b == "before-activation") { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException(); } } });
        Assert.True(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(15))));
        bool secondEntered = false;
        var second = env.Apply(first, hooks: new() { BatchWritten = _ => secondEntered = true });
        Assert.Equal(80, reader.Count("products")); Assert.Equal(old, env.Store().ReadActive()!.Generation);
        Assert.False(secondEntered); release.Set(); await Task.WhenAll(update, second);
        // The original generation has been retired by the third completed generation, but its WAL reader is coherent.
        Assert.Equal(80, reader.Count("products")); Assert.Equal(old, reader.Active.Generation);
        Assert.Equal(80, env.Store().CaptureCount("products"));
    }

    [Fact]
    public async Task Should_ReopenDownloadedData_When_TheDeviceHasNoConnection()
    {
        await using var env = new FixtureEnvironment(); var fixture = await env.Export(); env.Seed(fixture); await env.Apply(fixture); EditOrder(env);
        var before = env.Work().Read();
        // No HTTP client/runner exists on this reopen path. Physical airplane-mode launch remains a device scenario.
        var reopened = new SnapshotSqliteStore(env.Workspace());
        Assert.Equal(30, reopened.Search("shop").TotalMatches);
        Assert.Equal(fixture.Manifest.Json.Sha256, reopened.ReadActive()!.Manifest.Json.Sha256);
        Assert.Equal(before, new UnsentSqliteStore(env.Workspace()).Read(), UnsentComparer.Instance);
    }

    [Fact]
    public async Task Should_ReturnExpectedMatches_When_SearchingRepresentativeNamesAndTowns()
    {
        await using var env = new FixtureEnvironment(); var fixture = await env.Export(locations: 60); await env.Apply(fixture);
        var options = new SpikeOptions("baseline", new(80, 60, 2, 3), 42, new(2026, 10, 20), fixture.Directory, fixture.Unsent);
        var locations = new SnapshotGenerator(options).Locations().ToArray();
        Assert.Equal(20, SearchTerms.Benchmark.Length);
        foreach (string term in SearchTerms.Benchmark)
        {
            var expected = locations.Where(l => SearchTerms.Normalize(l.Name + " " + l.Town).Contains(SearchTerms.Normalize(term), StringComparison.Ordinal))
                .OrderBy(l => SearchTerms.Normalize(l.Name + " " + l.Town), StringComparer.Ordinal).ThenBy(l => l.Id.ToString(), StringComparer.Ordinal).ToArray();
            var result = env.Store().Search(term, 10);
            Assert.Equal(expected.Length, result.TotalMatches); Assert.Equal(expected.Take(10).Select(l => l.Id.ToString()), result.Items.Select(l => l.Id));
            Assert.True(result.QueryMs >= 0);
        }
        Assert.Equal(env.Store().Search("Ó Briain").Items, env.Store().Search("o briain").Items);
        Assert.Equal(env.Store().Search("Murphy’s").Items, env.Store().Search("MURPHY'S").Items);
    }

    [Fact]
    public async Task Should_IdentifyActualConditions_When_ExportingAMeasurementReport()
    {
        await using var env = new FixtureEnvironment(); await env.Export(); var endpoint = await env.StartHost();
        var report = env.Report(network: new("application-level-simulation", 10, 100));
        Assert.Equal("incomplete", report.Status);
        await new SpikeRunner(env.Workspace(), env.Http).RefreshAsync(endpoint, report);
        using var json = JsonDocument.Parse(report.ToJson()); var root = json.RootElement;
        Assert.Equal("desktop", root.GetProperty("device").GetProperty("kind").GetString());
        Assert.Contains("unmeasured", root.GetProperty("network").GetProperty("physicalLink").GetString());
        Assert.Equal("complete", report.Status); Assert.NotNull(report.Manifest); Assert.NotNull(report.Sqlite);
        Assert.True(report.PhaseMs["totalRefresh"] >= report.PhaseMs["activation"]);
        Assert.True(report.PeakSampledDiskBytes > 0); Assert.Equal(report.UnsentFingerprintBefore, report.UnsentFingerprintAfter);
        Assert.Contains("bytes", report.MemoryMetric);
        var failed = env.Report();
        await Assert.ThrowsAsync<HttpRequestException>(() => new SpikeRunner(env.Workspace(), env.Http).RefreshAsync(new Uri(endpoint, "missing/"), failed));
        Assert.Equal("failed", failed.Status); Assert.NotNull(failed.Error);
        using var invalid = await env.Http.GetAsync(new Uri(endpoint, "payload?mbit=NaN"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        using var forbidden = await env.Http.GetAsync(new Uri(endpoint, "snapshot.json"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, forbidden.StatusCode);
        using var interrupted = await env.Http.GetAsync(new Uri(endpoint, "payload?stopAfter=100"), HttpCompletionOption.ResponseHeadersRead);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => interrupted.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Should_LoadThePinnedNativeEngine_When_TheReleaseRunnerStarts()
    {
        await using var env = new FixtureEnvironment(); var actual = env.Store().Environment;
        Assert.Equal("3.53.3", actual.Version); Assert.Equal("wal", actual.JournalMode); Assert.Equal(2, actual.Synchronous);
        Assert.Contains("THREADSAFE=1", actual.CompileOptions); Assert.Contains("3.53.3", actual.NativePackage);
        using var connection = SqliteEnvironment.Open(env.Workspace().SnapshotPath);
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA synchronous=NORMAL"; command.ExecuteNonQuery();
        Assert.Throws<InvalidOperationException>(() => SqliteEnvironment.Inspect(connection));
    }

    private static void EditOrder(FixtureEnvironment env) => env.Work().EditNote(env.Work().Read().Items.Single(i => i.Kind == "Order").Id, "Edited locally: keep this note");
    private sealed class UnsentComparer : IEqualityComparer<UnsentState>
    {
        public static UnsentComparer Instance { get; } = new();
        public bool Equals(UnsentState? x, UnsentState? y) => x?.Fingerprint == y?.Fingerprint && x?.OriginalFixtureSha256 == y?.OriginalFixtureSha256 && x!.Items.SequenceEqual(y!.Items);
        public int GetHashCode(UnsentState obj) => obj.Fingerprint.GetHashCode(StringComparison.Ordinal);
    }
}
internal static class StoreExtensions
{
    public static long CaptureCount(this SnapshotSqliteStore store, string family) { using var read = store.Capture(); return read.Count(family); }
}
