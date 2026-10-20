using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FieldSales.TabletSpike.Core;

namespace FieldSales.TabletSpike.Host;

public static class FixtureHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = typeof(FixtureHost).Assembly.FullName });
        string root = Path.GetFullPath(builder.Configuration["fixtures"] ?? throw new ArgumentException("Supply --fixtures <synthetic export directory>."));
        if (!Directory.Exists(root)) throw new ArgumentException("Fixture directory does not exist.");
        builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:7206");
        var app = builder.Build();
        app.MapGet("/", () => Results.Json(new { tool = "Synthetic tablet fixtures only", endpoint = "/fixtures/{seed}/{baseline|target|stress}/", transport = "raw gzip; optional application-level simulation, not physical link shaping" }));
        app.MapGet("/fixtures/{seed:int}/{profile}/{file}", async (int seed, string profile, string file, HttpContext context) =>
        {
            if (profile is not ("baseline" or "target" or "stress") || file is not ("manifest" or "payload" or "unsent"))
                return Results.NotFound();
            string parent = Directory.Exists(Path.Combine(root, seed.ToString(CultureInfo.InvariantCulture)))
                ? Path.Combine(root, seed.ToString(CultureInfo.InvariantCulture)) : root;
            string directory = Path.Combine(parent, profile);
            string manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath)) return Results.NotFound();
            FixtureManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<FixtureManifest>(await File.ReadAllTextAsync(manifestPath, context.RequestAborted), FixtureManifest.JsonOptions)!;
                manifest.Validate($"spike-rep-{seed}");
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or NullReferenceException)
            { return Results.Problem("The allowlisted export has an invalid synthetic manifest.", statusCode: 422); }
            string path = file switch
            {
                "manifest" => manifestPath,
                "payload" => Path.Combine(directory, "snapshot.json.gz"),
                _ => Path.Combine(parent, $"unsent-{seed}.json")
            };
            if (!File.Exists(path)) return Results.NotFound();
            if (!TryConditions(context.Request, out double mbit, out int delay, out long? stopAfter))
                return Results.BadRequest("mbit: 0 or 0.01..1000; delayMs: 0..60000; stopAfter: positive bytes.");
            context.Response.Headers["X-Spike-Network"] = $"application-level-simulation; mbit={mbit}; initial-delay-ms={delay}; physical-link=unmeasured";
            if (delay > 0) await Task.Delay(delay, context.RequestAborted);
            context.Response.ContentType = file == "payload" ? "application/gzip" : "application/json";
            context.Response.ContentLength = new FileInfo(path).Length;
            await using var stream = File.OpenRead(path);
            byte[] buffer = new byte[16 * 1024]; long sent = 0;
            var watch = Stopwatch.StartNew();
            while (true)
            {
                int limit = stopAfter is long stop ? (int)Math.Min(buffer.Length, stop - sent) : buffer.Length;
                if (limit <= 0) { context.Abort(); return Results.Empty; }
                int count = await stream.ReadAsync(buffer.AsMemory(0, limit), context.RequestAborted);
                if (count == 0) break;
                await context.Response.Body.WriteAsync(buffer.AsMemory(0, count), context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                sent += count;
                if (mbit > 0)
                {
                    double waitMs = sent * 8d / (mbit * 1_000_000) * 1000 - watch.Elapsed.TotalMilliseconds;
                    if (waitMs > 0) await Task.Delay(TimeSpan.FromMilliseconds(waitMs), context.RequestAborted);
                }
            }
            return Results.Empty;
        });
        return app;
    }
    private static bool TryConditions(HttpRequest request, out double mbit, out int delay, out long? stopAfter)
    {
        mbit = 0; delay = 0; stopAfter = null;
        if (request.Query.TryGetValue("mbit", out var rate) && (!double.TryParse(rate, CultureInfo.InvariantCulture, out mbit) || !double.IsFinite(mbit) || (mbit != 0 && (mbit < .01 || mbit > 1000)))) return false;
        if (request.Query.TryGetValue("delayMs", out var wait) && (!int.TryParse(wait, out delay) || delay is < 0 or > 60000)) return false;
        if (request.Query.TryGetValue("stopAfter", out var stop))
        {
            if (!long.TryParse(stop, out long bytes) || bytes <= 0) return false;
            stopAfter = bytes;
        }
        return true;
    }
}
