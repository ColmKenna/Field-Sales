using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldSales.SnapshotSpike;
using FieldSales.TabletSpike.Core;
using FieldSales.TabletSpike.Host;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.TabletSpike.IntegrationTests;

internal sealed record ExportedFixture(string Directory, string Gzip, FixtureManifest Manifest, string Unsent);
internal sealed class FixtureEnvironment : IAsyncDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "fieldsales-tablet-spike-tests", Guid.NewGuid().ToString("N"));
    private WebApplication? host;
    public HttpClient Http { get; } = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None }) { Timeout = TimeSpan.FromSeconds(30) };
    public SpikeWorkspace Workspace(int seed = 42) => new(Path.Combine(Root, "store"), $"spike-rep-{seed}");
    public SnapshotSqliteStore Store(int seed = 42) => new(Workspace(seed));
    public UnsentSqliteStore Work(int seed = 42) => new(Workspace(seed));
    public SpikeReport Report(int seed = 42, NetworkConditions? network = null) => new()
    {
        Owner = $"spike-rep-{seed}", Device = DeviceConditions.Desktop(), Network = network ?? new("desktop-local-diagnostic"), CacheState = "test controlled fixture; OS cache uncontrolled"
    };
    public async Task<ExportedFixture> Export(int seed = 42, string profile = "baseline", int products = 80, int locations = 30)
    {
        string parent = Path.Combine(Root, "fixtures", seed.ToString());
        string directory = Path.Combine(parent, profile);
        string unsent = Path.Combine(parent, $"unsent-{seed}.json");
        await SnapshotWriter.WriteAsync(new(profile, new(products, locations, 2, 3), seed,
            new DateOnly(2026, 10, 20), directory, unsent));
        var manifest = JsonSerializer.Deserialize<FixtureManifest>(await File.ReadAllTextAsync(Path.Combine(directory, "manifest.json")), FixtureManifest.JsonOptions)!;
        return new(directory, Path.Combine(directory, "snapshot.json.gz"), manifest, unsent);
    }
    public async Task<Uri> StartHost(int seed = 42, string profile = "baseline")
    {
        host = FixtureHost.Build(["--fixtures", Path.Combine(Root, "fixtures"), "--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default", "Warning"]);
        await host.StartAsync();
        string address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new Uri($"{address}/fixtures/{seed}/{profile}/");
    }
    public void Seed(ExportedFixture fixture, int seed = 42) => Work(seed).SeedOnce(File.ReadAllBytes(fixture.Unsent), fixture.Manifest.Unsent);
    public async Task<string> Apply(ExportedFixture fixture, int seed = 42, SpikeHooks? hooks = null, CancellationToken ct = default) =>
        await Store(seed).ApplyAsync(fixture.Gzip, fixture.Manifest, Report(seed), hooks, ct);
    public async Task<ExportedFixture> WithPayload(ExportedFixture fixture, string json)
    {
        string directory = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        string path = Path.Combine(directory, "snapshot.json.gz");
        await using (var file = File.Create(path))
        await using (var gzip = new GZipStream(file, CompressionLevel.Optimal)) await gzip.WriteAsync(plain);
        byte[] compressed = await File.ReadAllBytesAsync(path);
        return fixture with { Gzip = path, Manifest = fixture.Manifest with
        {
            Json = new("snapshot.json", plain.Length, Convert.ToHexStringLower(SHA256.HashData(plain))),
            Gzip = new("snapshot.json.gz", compressed.Length, Convert.ToHexStringLower(SHA256.HashData(compressed)))
        } };
    }
    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        if (host is not null) { await host.StopAsync(); await host.DisposeAsync(); }
        // Only this test's newly created unique directory; no shared or user files.
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
