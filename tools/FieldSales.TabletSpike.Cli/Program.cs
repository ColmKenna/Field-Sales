using System.Text.Json;
using FieldSales.TabletSpike.Core;

// Desktop diagnostics and an owned child-process adapter. Never reports tablet performance.
if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: <root> <owner> <status|refresh|apply> [endpoint|gzip manifest] [pause-boundary ready-file]");
    return 2;
}
try
{
    var workspace = new SpikeWorkspace(args[0], args[1]);
    var store = new SnapshotSqliteStore(workspace);
    var report = new SpikeReport { Owner = args[1], Device = DeviceConditions.Desktop(),
        Network = new("desktop-local-diagnostic"), CacheState = store.ReadActive() is null ? "fresh" : "replacement; warm OS cache uncontrolled" };
    if (args[2] == "status")
    {
        Console.WriteLine(JsonSerializer.Serialize(new { Active = store.ReadActive(), Unsent = new UnsentSqliteStore(workspace).Read(), store.Environment }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        return 0;
    }
    int pauseIndex = args[2] == "apply" ? 5 : 4;
    var hooks = new SpikeHooks { AtBoundary = boundary =>
    {
        if (args.Length <= pauseIndex + 1 || boundary != args[pauseIndex]) return;
        File.WriteAllText(args[pauseIndex + 1], boundary);
        // Deliberate deterministic crash-test pause. Parent owns and terminates only this process.
        Thread.Sleep(Timeout.Infinite);
    } };
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    if (args[2] == "refresh")
    {
        using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None });
        await new SpikeRunner(workspace, http).RefreshAsync(new Uri(args[3]), report, hooks, cancellation.Token);
    }
    else if (args[2] == "apply")
    {
        var manifest = JsonSerializer.Deserialize<FixtureManifest>(await File.ReadAllTextAsync(args[4]), FixtureManifest.JsonOptions)!;
        report.Manifest = manifest; report.Sqlite = store.Environment;
        await store.ApplyAsync(args[3], manifest, report, hooks, cancellation.Token);
        report.Status = "complete"; report.SampleMemory();
    }
    else throw new ArgumentException("Unknown command.");
    Console.WriteLine(report.ToJson());
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}"); return 1; }
