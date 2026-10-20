using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace FieldSales.SnapshotSpike;

public static class SnapshotWriter
{
    public static JsonSerializerOptions JsonOptions { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<SpikeExport> WriteAsync(SpikeOptions options, CancellationToken ct = default,
        Action<SpikeProgress>? progress = null)
    {
        options.Validate();
        ct.ThrowIfCancellationRequested();
        // Validate a previously captured fixture before producing another snapshot. Never rewrite it.
        await ValidateExistingUnsentAsync(options, ct);
        string parent = Path.GetDirectoryName(options.OutputDirectory)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".{Path.GetFileName(options.OutputDirectory)}.{Guid.NewGuid():N}.staging");
        Directory.CreateDirectory(staging);
        string jsonPath = Path.Combine(staging, "snapshot.json");
        string gzipPath = Path.Combine(staging, "snapshot.json.gz");
        string manifestPath = Path.Combine(staging, "manifest.json");
        try
        {
            var generator = new SnapshotGenerator(options);
            await WriteJsonAsync(jsonPath, options, generator, ct, progress);
            await using (var source = OpenRead(jsonPath))
            await using (var destination = Create(gzipPath))
            await using (var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: false))
                await source.CopyToAsync(gzip, ct);
            ct.ThrowIfCancellationRequested();
            await EnsureUnsentAsync(options, generator, ct);
            var unsent = await DescribeAsync(options.UnsentPath, ct);
            var manifest = new SpikeManifest("spike-format-v1", options.Profile, options.Seed, options.AsOf,
                options.OwnerSubject, options.Scale, generator.Counts,
                await DescribeAsync(jsonPath, ct), await DescribeAsync(gzipPath, ct),
                new("spike-unsent-v1", options.OwnerSubject, unsent.Bytes, unsent.Sha256));
            await using (var file = Create(manifestPath))
                await JsonSerializer.SerializeAsync(file, manifest, JsonOptions, ct);
            ct.ThrowIfCancellationRequested();
            // New sibling directory, same filesystem: no partial export is published and no old export is replaced.
            Directory.Move(staging, options.OutputDirectory);
            return new(options.OutputDirectory, options.UnsentPath, manifest);
        }
        finally
        {
            // Delete only our known files in our newly created staging directory, never recurse over user data.
            if (Directory.Exists(staging))
            {
                File.Delete(jsonPath);
                File.Delete(gzipPath);
                File.Delete(manifestPath);
                Directory.Delete(staging);
            }
        }
    }

    private static async Task WriteJsonAsync(string path, SpikeOptions options, SnapshotGenerator generator,
        CancellationToken ct, Action<SpikeProgress>? progress)
    {
        await using var file = Create(path);
        await using var writer = new Utf8JsonWriter(file);
        writer.WriteStartObject();
        writer.WriteString("schema", "spike-format-v1");
        writer.WriteString("ownerSubject", options.OwnerSubject);
        writer.WriteNumber("seed", options.Seed);
        writer.WritePropertyName("asOf");
        JsonSerializer.Serialize(writer, options.AsOf, JsonOptions);
        await Rows("categories", generator.Categories());
        await Rows("products", generator.Products());
        await Rows("locations", generator.Locations());
        await Rows("promotions", generator.Promotions());
        await Rows("replacements", generator.Replacements());
        await Rows("history", generator.History());
        await Rows("thresholds", generator.Thresholds());
        await Rows("leads", generator.Leads());
        await Rows("masters", generator.Masters());
        writer.WriteEndObject();
        await writer.FlushAsync(ct);

        async Task Rows<T>(string name, IEnumerable<T> rows)
        {
            writer.WriteStartArray(name);
            int count = 0;
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                JsonSerializer.Serialize(writer, row, JsonOptions);
                count++;
                if (count == 1 || count % 128 == 0)
                {
                    await writer.FlushAsync(ct);
                    progress?.Invoke(new(name, count));
                }
            }
            writer.WriteEndArray();
            await writer.FlushAsync(ct);
            progress?.Invoke(new(name, count));
            ct.ThrowIfCancellationRequested();
        }
    }

    private static async Task ValidateExistingUnsentAsync(SpikeOptions options, CancellationToken ct)
    {
        if (!File.Exists(options.UnsentPath)) return;
        await using var file = OpenRead(options.UnsentPath);
        using var document = await JsonDocument.ParseAsync(file, cancellationToken: ct);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.String || schema.GetString() != "spike-unsent-v1"
            || !root.TryGetProperty("ownerSubject", out var owner) || owner.ValueKind != JsonValueKind.String || owner.GetString() != options.OwnerSubject)
            throw new ArgumentException("The existing Unsent fixture has a different owner or format; choose its owner's file.");
    }

    private static async Task EnsureUnsentAsync(SpikeOptions options, SnapshotGenerator generator, CancellationToken ct)
    {
        if (File.Exists(options.UnsentPath))
        {
            await ValidateExistingUnsentAsync(options, ct);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(options.UnsentPath)!);
        string temporary = options.UnsentPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = Create(temporary))
                await JsonSerializer.SerializeAsync(file, generator.Unsent(), JsonOptions, ct);
            ct.ThrowIfCancellationRequested();
            try { File.Move(temporary, options.UnsentPath, overwrite: false); }
            catch (IOException) when (File.Exists(options.UnsentPath))
            {
                await ValidateExistingUnsentAsync(options, ct);
            }
        }
        finally { File.Delete(temporary); }
    }

    private static async Task<SpikeFile> DescribeAsync(string path, CancellationToken ct)
    {
        await using var file = OpenRead(path);
        long length = file.Length;
        string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, ct));
        return new(Path.GetFileName(path), length, hash);
    }

    private static FileStream Create(string path) => new(path, FileMode.CreateNew, FileAccess.Write,
        FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
    private static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read,
        FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
}
