using System.Diagnostics;
using System.Text.Json;

namespace FieldSales.SnapshotSpike;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try { return await SpikeCommand.RunAsync(args, Console.Out, Console.Error, cancellation.Token); }
        finally { Console.CancelKeyPress -= handler; }
    }
}

public static class SpikeCommand
{
    public const string Usage = """
        Usage: dotnet run --project tools/FieldSales.SnapshotSpike --
          --output <new-directory> --as-of <yyyy-MM-dd>
          [--profile baseline|target|stress] [--seed <integer>] [--unsent <separate-file>]
          [--products <count>] [--locations <count>] [--history-orders <per-location>] [--history-lines <per-order>]
        Defaults: baseline, seed 42; Unsent file: unsent-<seed>.json beside the output directory.
        Output directories must be new. Existing owner-matched Unsent fixtures are retained byte for byte.
        This tool exports synthetic capacity fixtures, not a production Sync contract or tablet benchmark.
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken ct = default)
    {
        if (args is ["--help"])
        {
            await output.WriteLineAsync(Usage);
            return 0;
        }
        SpikeOptions options;
        try { options = SpikeOptions.Parse(args); }
        catch (Exception exception) when (exception is ArgumentException or OverflowException or NotSupportedException)
        {
            await error.WriteLineAsync("Invalid spike options: " + exception.Message);
            return 2;
        }
        try
        {
            var timer = Stopwatch.StartNew();
            var exported = await SnapshotWriter.WriteAsync(options, ct);
            await output.WriteLineAsync(JsonSerializer.Serialize(new
            {
                evidence = "desktop-generation-only", elapsedSeconds = timer.Elapsed.TotalSeconds,
                exported.OutputDirectory, exported.UnsentPath, exported.Manifest
            }, SnapshotWriter.JsonOptions));
            return 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync("Generation cancelled. No incomplete snapshot was published; retry with a new output directory.");
            return 130;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            await error.WriteLineAsync("Snapshot export failed: " + exception.Message);
            return 1;
        }
    }
}
