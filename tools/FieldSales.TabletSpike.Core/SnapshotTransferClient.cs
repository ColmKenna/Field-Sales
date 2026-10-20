using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace FieldSales.TabletSpike.Core;

public sealed class SnapshotTransferClient(HttpClient http)
{
    public async Task<FixtureManifest> ManifestAsync(Uri endpoint, string owner, CancellationToken ct)
    {
        byte[] bytes = await SmallFileAsync(new Uri(endpoint, "manifest"), 1024 * 1024, ct);
        var manifest = JsonSerializer.Deserialize<FixtureManifest>(bytes, FixtureManifest.JsonOptions)
            ?? throw new InvalidDataException("Empty fixture manifest.");
        manifest.Validate(owner);
        return manifest;
    }
    public async Task<byte[]> UnsentAsync(Uri endpoint, FixtureManifest manifest, CancellationToken ct) =>
        await SmallFileAsync(new Uri(endpoint, "unsent"), 4 * 1024 * 1024, ct);

    public async Task DownloadAsync(Uri endpoint, NetworkConditions conditions, string path, FixtureFile expected,
        SpikeHooks hooks, CancellationToken ct)
    {
        string query = $"payload?mbit={conditions.SimulatedMbitPerSecond?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0"}&delayMs={conditions.InitialDelayMs}";
        using var response = await http.GetAsync(new Uri(endpoint, query), HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentEncoding.Count != 0)
            throw new InvalidDataException("Payload must be raw gzip bytes, without Content-Encoding transformation.");
        if (response.Content.Headers.ContentLength is long length && length != expected.Bytes)
            throw new InvalidDataException("HTTP payload length mismatch.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024]; long total = 0; bool boundary = false;
        while (true)
        {
            int count = await source.ReadAsync(buffer, ct);
            if (count == 0) break;
            total = checked(total + count);
            if (total > expected.Bytes) throw new InvalidDataException("Payload exceeds declared size.");
            await file.WriteAsync(buffer.AsMemory(0, count), ct);
            hash.AppendData(buffer.AsSpan(0, count));
            hooks.DownloadProgress?.Invoke(total);
            if (!boundary)
            {
                await file.FlushAsync(ct);
                hooks.AtBoundary?.Invoke("download"); boundary = true;
            }
        }
        await file.FlushAsync(ct);
        if (total != expected.Bytes || Convert.ToHexStringLower(hash.GetHashAndReset()) != expected.Sha256.ToLowerInvariant())
            throw new InvalidDataException("Downloaded payload length/hash mismatch.");
    }
    private async Task<byte[]> SmallFileAsync(Uri uri, int maximum, CancellationToken ct)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var result = new MemoryStream(); byte[] buffer = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0) return result.ToArray();
            if (result.Length + read > maximum) throw new InvalidDataException("Fixture metadata exceeds size limit.");
            result.Write(buffer, 0, read);
        }
    }
}

public sealed class SpikeRunner(SpikeWorkspace workspace, HttpClient http)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    public async Task RefreshAsync(Uri endpoint, SpikeReport report, SpikeHooks? hooks = null, CancellationToken ct = default)
    {
        if (endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length != 0 || !endpoint.AbsolutePath.EndsWith('/'))
            throw new ArgumentException("Use a synthetic fixture HTTP(S) endpoint ending in '/'.");
        var gate = Gates.GetOrAdd(workspace.DirectoryPath, _ => new(1, 1));
        await gate.WaitAsync(ct);
        var watch = Stopwatch.StartNew();
        string reportPath = Path.Combine(workspace.DirectoryPath, $"report-{report.RunId}.json");
        var innerHooks = hooks ?? new();
        var persistedBoundaries = new HashSet<string>(StringComparer.Ordinal);
        var forwarded = new SpikeHooks
        {
            MaxDatabasePages = innerHooks.MaxDatabasePages, BeforeCommit = innerHooks.BeforeCommit,
            BatchWritten = innerHooks.BatchWritten, DownloadProgress = innerHooks.DownloadProgress,
            AtBoundary = name =>
            {
                // Persist each recovery boundary once, without writing a report on every insert batch.
                if (persistedBoundaries.Add(name)) File.WriteAllText(reportPath, report.ToJson());
                innerHooks.AtBoundary?.Invoke(name);
            }
        };
        try
        {
            var store = await Task.Run(() => new SnapshotSqliteStore(workspace), ct);
            report.Sqlite = store.Environment;
            var unsent = new UnsentSqliteStore(workspace);
            var client = new SnapshotTransferClient(http);
            var phase = Stopwatch.StartNew();
            var manifest = await client.ManifestAsync(endpoint, workspace.Owner, ct);
            report.Manifest = manifest;
            report.PhaseMs["manifest"] = phase.Elapsed.TotalMilliseconds;
            if (!await Task.Run(unsent.IsSeeded, ct))
            {
                byte[] fixture = await client.UnsentAsync(endpoint, manifest, ct);
                await Task.Run(() => unsent.SeedOnce(fixture, manifest.Unsent), ct);
            }
            var before = await Task.Run(unsent.Read, ct);
            report.OriginalUnsentSha256 = before.OriginalFixtureSha256;
            report.UnsentFingerprintBefore = before.Fingerprint;
            report.DiskSamples.Add(await Task.Run(() => workspace.Measure(), ct));
            phase.Restart();
            // Pause hooks can block deliberately, so the transfer also starts off the UI thread.
            await Task.Run(() => client.DownloadAsync(endpoint, report.Network, workspace.TransferPath, manifest.Gzip, forwarded, ct), ct);
            report.PhaseMs["httpTransferAndCompressedHash"] = phase.Elapsed.TotalMilliseconds;
            report.DiskSamples.Add(await Task.Run(() => workspace.Measure(), ct));
            report.Generation = await store.ApplyAsync(workspace.TransferPath, manifest, report, forwarded, ct);
            report.UnsentFingerprintAfter = (await Task.Run(unsent.Read, ct)).Fingerprint;
            if (report.UnsentFingerprintBefore != report.UnsentFingerprintAfter)
                throw new InvalidDataException("Unsent work changed during refresh.");
            File.Delete(workspace.TransferPath);
            report.DiskSamples.Add(await Task.Run(() => workspace.Measure(), ct));
            report.Status = "complete";
        }
        catch (Exception exception)
        {
            report.Status = exception is OperationCanceledException ? "cancelled" : "failed";
            report.Error = $"{exception.GetType().Name}: {exception.Message}";
            throw;
        }
        finally
        {
            report.PhaseMs["totalRefresh"] = watch.Elapsed.TotalMilliseconds;
            report.SampleMemory();
            try
            {
                report.UnsentFingerprintAfter = (await Task.Run(() => new UnsentSqliteStore(workspace).Read())).Fingerprint;
                report.DiskSamples.Add(await Task.Run(() => workspace.Measure()));
                await File.WriteAllTextAsync(reportPath, report.ToJson());
            }
            finally { gate.Release(); }
        }
    }
}
