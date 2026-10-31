using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldSales.Snapshot.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>A snapshot as the server would send it: gzip bytes plus both hashes.</summary>
public sealed record EncodedSnapshot(byte[] Gzip, string GzipSha256, string PlainSha256, string Version = "1.0")
{
    public static EncodedSnapshot From(TabletSnapshot snapshot) =>
        FromJson(JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotContract.JsonOptions), snapshot.Manifest.Version);

    public static EncodedSnapshot FromJson(byte[] plain, string version = "1.0")
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.Fastest, leaveOpen: true)) gzip.Write(plain);
        byte[] bytes = output.ToArray();
        return new(bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)), Convert.ToHexStringLower(SHA256.HashData(plain)), version);
    }

    public ExpectedSnapshot Expected => new(GzipSha256, PlainSha256);
}

public static class Snapshots
{
    // Deterministic synthetic data: a run of shops in one town, and products with a few dated prices.
    public static TabletSnapshot Build(string rep, int locations, int products, int pricesPerProduct = 2, string label = "A",
        string version = "1.0", SnapshotCounts? counts = null)
    {
        Guid Id(int kind, int index) => new(kind, (short)(index >> 16), (short)(index & 0xFFFF), Encoding.ASCII.GetBytes(label.PadRight(8)[..8]));
        SnapshotTown town = new(Id(1, 0), "Arklow", "Wicklow");
        SnapshotCategory[] categories = [new(Id(2, 0), null, "Health"), new(Id(2, 1), Id(2, 0), "Remedies " + label)];
        SnapshotLocation[] shops = Enumerable.Range(0, locations).Select(index => new SnapshotLocation(Id(3, index),
            $"{label} shop {index:D4}", Id(4, index), $"{label} customer {index}", town, index % 2 == 0 ? "Y14 X2F9" : null,
            index % 3 == 0 ? new SnapshotContact(Id(5, index), "Mary Hickey", "Owner", "+353 402 31234", null) : null)).ToArray();
        SnapshotProduct[] items = Enumerable.Range(0, products).Select(index => new SnapshotProduct(Id(6, index),
            $"{label}-{index:D5}", $"{label} product {index}", categories[1].Id, "Each", null, null, null)).ToArray();
        SnapshotBasePrice[] prices = items.SelectMany(product => Enumerable.Range(0, pricesPerProduct).Select(offset =>
            new SnapshotBasePrice(product.Id, new DateOnly(2026, 10, 1).AddMonths(offset), 2.50m + offset))).ToArray();
        SnapshotManifest manifest = new(SnapshotContract.Schema, version, rep, new DateTimeOffset(2026, 10, 1, 6, 30, 0, TimeSpan.Zero),
            new DateOnly(2026, 10, 1), counts ?? new(shops.Length, categories.Length, items.Length, prices.Length));
        return new(manifest, shops, categories, items, prices, version == "1.0" ? null : []);
    }
}

/// <summary>Stands in for the Field Sales API, with the faults a real connection can produce.</summary>
/// <remarks>Uploads (T-4.1.2) have their own responder, so a test that only serves a snapshot still has every upload
/// refused as unavailable: saved work stays unsent and the download is attempted regardless.</remarks>
public sealed class FakeSnapshotServer : HttpMessageHandler
{
    public const string UploadPathPrefix = "/tablet/uploads/";

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new(HttpStatusCode.ServiceUnavailable);
    public Func<HttpRequestMessage, HttpResponseMessage> RespondToUpload { get; set; } = _ => new(HttpStatusCode.ServiceUnavailable);
    public List<(string Path, string? Authorization, string? Majors)> Requests { get; } = [];
    public List<(string Path, string Body)> Uploads { get; } = [];

    public void Serve(EncodedSnapshot snapshot, Func<byte[], HttpContent>? content = null, string? gzipHash = null,
        string? plainHash = null) => Respond = _ =>
    {
        HttpResponseMessage response = new(HttpStatusCode.OK) { Content = content?.Invoke(snapshot.Gzip) ?? new ByteArrayContent(snapshot.Gzip) };
        response.Content.Headers.ContentType = new(SnapshotContract.MediaType);
        response.Headers.Add(SnapshotContract.VersionHeader, snapshot.Version);
        response.Headers.Add(SnapshotContract.GzipSha256Header, gzipHash ?? snapshot.GzipSha256);
        response.Headers.Add(SnapshotContract.PlainSha256Header, plainHash ?? snapshot.PlainSha256);
        return response;
    };

    public HttpClient Client() => new(this) { BaseAddress = new Uri("https://api.test/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        Requests.Add((path, request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues(SnapshotContract.AcceptedMajorsHeader, out IEnumerable<string>? majors) ? string.Join(",", majors) : null));
        if (!path.StartsWith(UploadPathPrefix, StringComparison.Ordinal)) return Respond(request);
        Uploads.Add((path, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
        return RespondToUpload(request);
    }
}

/// <summary>Body content that stops after some bytes, declaring the full length: either a reset connection
/// (the read throws, as a dropped socket does) or a body that simply ends early.</summary>
public static class FailingContent
{
    public static HttpContent Create(byte[] bytes, int deliver, bool throwAtEnd)
    {
        StreamContent content = new(new StoppingStream(bytes, deliver, throwAtEnd));
        content.Headers.ContentLength = bytes.Length;
        return content;
    }

    private sealed class StoppingStream(byte[] bytes, int deliver, bool throwAtEnd) : MemoryStream(bytes, 0, deliver)
    {
        public override int Read(byte[] buffer, int offset, int count) => Stop(base.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Stop(base.Read(buffer));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Stop(base.Read(buffer.Span)));
        private int Stop(int read) => read == 0 && throwAtEnd ? throw new IOException("The connection was reset.") : read;
    }
}

/// <summary>The faults a real connection and sign-in produce, by name, for the Sync failure scenarios (T-4.1.3).</summary>
public static class Faults
{
    /// <summary>No route, a refused or a dropped connection: the request fails before any answer arrives.</summary>
    public static HttpRequestException NoSignal() => new(HttpRequestError.ConnectionError, "No route to host.");

    /// <summary>What the identity host answers a refresh once the sign-in has expired or been revoked.</summary>
    public static HttpResponseMessage ExpiredSignIn() =>
        new(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"invalid_grant"}""") };
}

/// <summary>Finished work as a rep saves it: two Calls and a Ready to Send Order, in the order they go.</summary>
public static class FinishedWork
{
    public static Guid[] SaveThree(RepWorkspace workspace, TabletSnapshot day, TimeProvider clock)
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), order = Guid.NewGuid();
        foreach ((Guid id, int minutesAgo) in new[] { (first, 30), (second, 20) })
            new UnsentStore(workspace).Save(new(id, UnsentKind.Call, clock.GetUtcNow().AddMinutes(-minutesAgo),
                $$"""{"id":"{{id}}","locationId":"{{day.Locations[0].Id}}","channel":"InPerson"}"""));
        OrderStore orders = new(workspace, clock);
        Assert.True(orders.AddLine(order, day.Locations[1].Id, day.Products[0].Id, 6m).Succeeded);
        Assert.True(orders.MarkReadyToSend(order).Succeeded);
        return [first, second, order];
    }
}

/// <summary>Hears each progress report as it is made, unlike <see cref="Progress{T}"/>, which posts it for later.</summary>
public sealed class SynchronousProgress(Action<UploadProgress> report) : IProgress<UploadProgress>
{
    public void Report(UploadProgress value) => report(value);
}

public sealed class TestTokens(string? token = "rep-token") : IAccessTokenSource
{
    public string? Token { get; set; } = token;
    public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult(Token);
}

public sealed class TestSpace : IStorageSpace
{
    public long Available { get; set; } = long.MaxValue;
    public long AvailableBytes(string directory) => Available;
}

/// <summary>A temporary tablet with one or more reps' files.</summary>
public sealed class TestTablet : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "fieldsales-tablet-tests", Guid.NewGuid().ToString("N"));
    public FakeSnapshotServer Server { get; } = new();
    public TestTokens Tokens { get; } = new();
    public TestSpace Space { get; } = new();
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 1, 6, 42, 0, TimeSpan.Zero));

    public RepWorkspace Workspace(string rep = "colm") => new(Root, rep);
    public TabletSync Sync(string rep = "colm") => new(Workspace(rep), Server.Client(), Tokens, Space, Clock);

    // Each Sync happens a minute after the last, so "newest" never depends on generation ids.
    public async Task<ActiveSnapshot> SyncAsync(TabletSnapshot snapshot, string rep = "colm")
    {
        Clock.Advance(TimeSpan.FromMinutes(1));
        Server.Serve(EncodedSnapshot.From(snapshot));
        SyncOutcome outcome = await Sync(rep).SyncAsync();
        Assert.Equal(SyncStatus.Completed, outcome.Status);
        return outcome.Active!;
    }

    public UnsentStore SaveWork(string rep = "colm")
    {
        UnsentStore work = new(Workspace(rep));
        work.Save(new(new Guid("6a1f2b30-0000-0000-0000-000000000001"), UnsentKind.Call, Clock.GetUtcNow(),
            """{"location":"Hickey's Pharmacy","outcome":"Visited"}"""));
        work.Save(new(new Guid("6a1f2b30-0000-0000-0000-000000000002"), UnsentKind.Call, Clock.GetUtcNow(),
            """{"location":"Byrne's Londis","outcome":"No order"}"""));
        work.Save(new(new Guid("6a1f2b30-0000-0000-0000-000000000003"), UnsentKind.Order, Clock.GetUtcNow(),
            """{"location":"Quinn's Centra","state":"In Progress","lines":[{"product":"A-00001","quantity":6}]}"""));
        return work;
    }

    /// <summary>A SHA-256 over the Unsent database files, read without opening the database.</summary>
    public string UnsentFileHash(string rep = "colm")
    {
        RepWorkspace workspace = Workspace(rep);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in new[] { workspace.UnsentPath, workspace.UnsentPath + "-wal" }.Where(File.Exists))
            hash.AppendData(File.ReadAllBytes(path));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Runs the crash host until it reaches the boundary, then kills it, as a force-close would.</summary>
    public async Task KillAtAsync(string boundary, params string[] arguments)
    {
        string ready = Path.Combine(Root, $"ready-{Guid.NewGuid():N}");
        string tests = typeof(TestTablet).Assembly.Location;
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(tests, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(tests, ".deps.json"), typeof(Program).Assembly.Location, Root }
                     .Concat(arguments).Concat([boundary, ready]))
            start.ArgumentList.Add(argument);
        using Process child = Process.Start(start)!;
        Task<string> errors = child.StandardError.ReadToEndAsync();
        Task<string> output = child.StandardOutput.ReadToEndAsync();
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
            while (!File.Exists(ready) && !child.HasExited) await Task.Delay(25, timeout.Token);
            Assert.True(File.Exists(ready), $"The crash host stopped before {boundary}: {(child.HasExited ? await errors : "still running")}");
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
        }
        await output; await errors;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
