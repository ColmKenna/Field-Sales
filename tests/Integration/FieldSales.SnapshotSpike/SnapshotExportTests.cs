using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.SnapshotSpike;

namespace FieldSales.SnapshotSpike.Tests;

public sealed class SnapshotExportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FieldSales.SnapshotSpike.Tests", Guid.NewGuid().ToString("N"));

    public SnapshotExportTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task Should_ReproduceFixtureBytes_When_SeedAsOfAndScaleAreTheSame()
    {
        var first = Options("first/export", "first/unsent.json");
        var second = Options("second/export", "second/unsent.json");
        await RunSuccessfully(Arguments(first));
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            await RunSuccessfully(Arguments(second));
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
        foreach (string name in new[] { "snapshot.json", "snapshot.json.gz", "manifest.json" })
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(first.OutputDirectory, name)),
                await File.ReadAllBytesAsync(Path.Combine(second.OutputDirectory, name)));
        Assert.Equal(await File.ReadAllBytesAsync(first.UnsentPath), await File.ReadAllBytesAsync(second.UnsentPath));
        var changed = Options("changed/export", "changed/unsent.json") with { Seed = 73 };
        await SnapshotWriter.WriteAsync(changed);
        Assert.NotEqual(await File.ReadAllTextAsync(Path.Combine(first.OutputDirectory, "snapshot.json")),
            await File.ReadAllTextAsync(Path.Combine(changed.OutputDirectory, "snapshot.json")));
    }

    [Theory]
    [InlineData("baseline", 5_000, 500, 6, 20)]
    [InlineData("target", 25_000, 500, 12, 20)]
    [InlineData("stress", 100_000, 2_000, 24, 30)]
    public async Task Should_GenerateTheRequestedCounts_When_UsingAProfileOrOverrides(
        string profile, int products, int locations, int orders, int lines)
    {
        // Lock the agreed full profiles, then exercise the CLI overrides with a small file fixture.
        var defaults = SpikeOptions.Parse(["--profile", profile, "--as-of", "2026-10-20", "--output", Path.Combine(root, "defaults")]);
        Assert.Equal(new SpikeScale(products, locations, orders, lines), defaults.Scale);
        var options = Options() with { Profile = profile };
        await RunSuccessfully(Arguments(options));
        using var snapshot = Read(options, "snapshot.json");
        var data = snapshot.RootElement;
        Assert.Equal(40, data.GetProperty("products").GetArrayLength());
        Assert.Equal(50, data.GetProperty("locations").GetArrayLength());
        Assert.Equal(160, data.GetProperty("categories").GetArrayLength());
        var history = Rows(data, "history");
        Assert.Equal(100, history.Length);
        Assert.Equal(300, history.Sum(order => order.GetProperty("lines").GetArrayLength()));
        Assert.All(history.GroupBy(order => order.GetProperty("locationId").GetGuid()), group => Assert.Equal(2, group.Count()));
        Assert.Equal(320, Rows(data, "products").Sum(p => p.GetProperty("attributes").GetArrayLength()));
        Assert.Equal(120, Rows(data, "products").Sum(p => p.GetProperty("basePrices").GetArrayLength()));
        Assert.Equal(360, Rows(data, "products").Sum(p => Rows(p, "priceTiers").Sum(t => t.GetProperty("breaks").GetArrayLength())));
        Assert.Equal(6, data.GetProperty("promotions").GetArrayLength());
        Assert.Equal(2, data.GetProperty("replacements").GetArrayLength());
        Assert.Equal(50, data.GetProperty("thresholds").GetArrayLength());
        Assert.Equal(5, data.GetProperty("leads").GetArrayLength());
        Assert.Equal(2, data.GetProperty("masters").GetArrayLength());
        Assert.Equal(10, Rows(data, "masters").Sum(m => m.GetProperty("branchLocationIds").GetArrayLength()));
        using var manifest = Read(options, "manifest.json");
        var counts = manifest.RootElement.GetProperty("counts");
        Assert.Equal(300, counts.GetProperty("historicalLines").GetInt32());
        Assert.Equal(40, counts.GetProperty("products").GetInt32());
        Assert.Equal(profile, manifest.RootElement.GetProperty("profile").GetString());
    }

    [Fact]
    public async Task Should_KeepAllReferencesValid_When_GeneratingFuturePayloadFamilies()
    {
        var options = Options();
        await SnapshotWriter.WriteAsync(options);
        using var snapshot = Read(options, "snapshot.json");
        var data = snapshot.RootElement;
        var categories = Rows(data, "categories").ToDictionary(row => row.GetProperty("id").GetGuid());
        var products = Rows(data, "products").ToDictionary(row => row.GetProperty("id").GetGuid());
        var locations = Rows(data, "locations").ToDictionary(row => row.GetProperty("id").GetGuid());
        var customers = locations.Values.Select(row => row.GetProperty("customerId").GetGuid()).ToHashSet();
        Assert.Equal(options.OwnerSubject, data.GetProperty("ownerSubject").GetString());
        foreach (var product in products.Values)
        {
            Assert.True(categories.ContainsKey(product.GetProperty("categoryId").GetGuid()));
            var path = Rows(product, "breadcrumbs");
            Assert.Equal(4, path.Length);
            for (int i = 0; i < path.Length; i++)
            {
                var category = categories[path[i].GetProperty("id").GetGuid()];
                Assert.Equal(path[i].GetRawText(), category.GetRawText());
                if (i == 0) Assert.Equal(JsonValueKind.Null, category.GetProperty("parentId").ValueKind);
                else Assert.Equal(path[i - 1].GetProperty("id").GetGuid(), category.GetProperty("parentId").GetGuid());
            }
            Assert.Equal(path[^1].GetProperty("id").GetGuid(), product.GetProperty("categoryId").GetGuid());
            Assert.Equal(new[] { "2026-10-20", "2026-11-19", "2027-01-18" },
                Rows(product, "basePrices").Select(p => p.GetProperty("effectiveFrom").GetString()));
            Assert.True(product.GetProperty("quantityStep").GetDecimal() > 0);
            Assert.All(Rows(product, "priceTiers"), tier => Assert.All(Rows(tier, "breaks"), quantityBreak =>
                Assert.True(quantityBreak.GetProperty("unitPrice").GetDecimal() >= 0)));
        }
        Assert.Equal(locations.Count, locations.Values.Select(l => l.GetProperty("mainContact").GetProperty("id").GetGuid()).Distinct().Count());
        foreach (var order in Rows(data, "history"))
        {
            Assert.True(locations.ContainsKey(order.GetProperty("locationId").GetGuid()));
            Assert.True(DateOnly.Parse(order.GetProperty("capturedOn").GetString()!, CultureInfo.InvariantCulture) < options.AsOf);
            CheckLines(order);
        }
        Assert.All(Rows(data, "promotions"), promotion => Assert.True(products.ContainsKey(promotion.GetProperty("productId").GetGuid())));
        foreach (var replacement in Rows(data, "replacements"))
        {
            var from = replacement.GetProperty("productId").GetGuid();
            var to = replacement.GetProperty("replacementProductId").GetGuid();
            Assert.True(products.ContainsKey(from) && products.ContainsKey(to));
            Assert.NotEqual(from, to);
        }
        Assert.Equal(locations.Keys.Order(), Rows(data, "thresholds").Select(t => t.GetProperty("locationId").GetGuid()).Order());
        Assert.All(Rows(data, "leads"), lead => Assert.True(locations.ContainsKey(lead.GetProperty("nearbyLocationId").GetGuid())));
        foreach (var master in Rows(data, "masters"))
        {
            Assert.Contains(master.GetProperty("customerId").GetGuid(), customers);
            var branches = master.GetProperty("branchLocationIds").EnumerateArray().Select(b => b.GetGuid()).ToArray();
            Assert.Equal(5, branches.Distinct().Count());
            Assert.All(branches, branch => Assert.True(locations.ContainsKey(branch)));
        }
        using var unsent = JsonDocument.Parse(await File.ReadAllBytesAsync(options.UnsentPath));
        Assert.Equal(options.OwnerSubject, unsent.RootElement.GetProperty("ownerSubject").GetString());
        var work = Rows(unsent.RootElement, "items");
        Assert.Equal(new[] { "Call", "Call", "Order", "ScheduleChange" }, work.Select(w => w.GetProperty("kind").GetString()));
        Assert.All(work, item =>
        {
            Assert.True(locations.ContainsKey(item.GetProperty("locationId").GetGuid()));
            Assert.Equal("spike-previous-snapshot", item.GetProperty("capturedUnderSnapshot").GetString());
            CheckLines(item);
        });

        void CheckLines(JsonElement order)
        {
            foreach (var line in Rows(order, "lines"))
            {
                var product = products[line.GetProperty("productId").GetGuid()];
                decimal quantity = line.GetProperty("quantity").GetDecimal();
                Assert.True(quantity >= product.GetProperty("minimumQuantity").GetDecimal());
                Assert.Equal(0m, quantity % product.GetProperty("quantityStep").GetDecimal());
                Assert.Equal(product.GetProperty("basePrices")[0].GetProperty("amount").GetDecimal(), line.GetProperty("unitPrice").GetDecimal());
            }
        }
    }

    [Fact]
    public async Task Should_ReportVerifiablePayloads_When_ExportingCompressedAndPlainData()
    {
        var options = Options();
        await RunSuccessfully(Arguments(options));
        byte[] json = await File.ReadAllBytesAsync(Path.Combine(options.OutputDirectory, "snapshot.json"));
        await using var compressed = File.OpenRead(Path.Combine(options.OutputDirectory, "snapshot.json.gz"));
        await using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        await gzip.CopyToAsync(decompressed);
        Assert.Equal(json, decompressed.ToArray());
        using var manifest = Read(options, "manifest.json");
        var data = manifest.RootElement;
        Assert.Equal("spike-format-v1", data.GetProperty("schema").GetString());
        Assert.Equal(options.OwnerSubject, data.GetProperty("ownerSubject").GetString());
        foreach (string name in new[] { "json", "gzip", "unsent" })
        {
            var file = data.GetProperty(name);
            string path = name == "unsent" ? options.UnsentPath : Path.Combine(options.OutputDirectory, file.GetProperty("fileName").GetString()!);
            byte[] bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal(bytes.LongLength, file.GetProperty("bytes").GetInt64());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), file.GetProperty("sha256").GetString());
        }
        Assert.Equal(new[] { "manifest.json", "snapshot.json", "snapshot.json.gz" },
            Directory.GetFiles(options.OutputDirectory).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task Should_LeaveUnsentFixtureUntouched_When_GeneratingAnotherSnapshot()
    {
        var first = Options("first");
        await SnapshotWriter.WriteAsync(first);
        string edited = (await File.ReadAllTextAsync(first.UnsentPath)).Replace("Ready to send", "Rep edited this offline", StringComparison.Ordinal);
        await File.WriteAllTextAsync(first.UnsentPath, edited);
        byte[] captured = await File.ReadAllBytesAsync(first.UnsentPath);
        var second = first with { OutputDirectory = Path.Combine(root, "second"), AsOf = first.AsOf.AddDays(1) };
        var result = await SnapshotWriter.WriteAsync(second);
        Assert.Equal(captured, await File.ReadAllBytesAsync(first.UnsentPath));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(captured)), result.Manifest.Unsent.Sha256);
        var otherOwner = second with { OutputDirectory = Path.Combine(root, "other-owner"), Seed = 77 };
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(1, await SpikeCommand.RunAsync(Arguments(otherOwner), output, error));
        Assert.Contains("different owner", error.ToString());
        Assert.False(Directory.Exists(otherOwner.OutputDirectory));
        Assert.Equal(captured, await File.ReadAllBytesAsync(first.UnsentPath));
    }

    public static IEnumerable<object[]> InvalidArguments()
    {
        yield return [new[] { "--products", "-1" }];
        yield return [new[] { "--products", "0" }];
        yield return [new[] { "--locations", "0" }];
        yield return [new[] { "--history-orders", "-1" }];
        yield return [new[] { "--history-lines", "0" }];
        yield return [new[] { "--products", "2147483647" }];
        yield return [new[] { "--locations", "2147483647", "--history-orders", "2" }];
        yield return [new[] { "--products", "2147483648" }];
        yield return [new[] { "--products", "1.5" }];
        yield return [new[] { "--profile", "unknown" }];
        yield return [new[] { "--seed", "42", "--seed", "43" }];
        yield return [new[] { "--unknown", "42" }];
        yield return [new[] { "--seed" }];
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task Should_RejectInvalidInputWithoutPublishingOutput_When_ScaleIsInvalid(string[] invalid)
    {
        string destination = Path.Combine(root, "invalid");
        string[] required = ["--output", destination, "--as-of", "2026-10-20"];
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, await SpikeCommand.RunAsync([.. required, .. invalid], output, error));
        Assert.Contains("Invalid spike options", error.ToString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));

        // Reject overlapping paths and existing output without touching prior artifacts.
        Assert.Equal(2, await SpikeCommand.RunAsync([.. required, "--unsent", Path.Combine(destination, "unsent.json")], output, error));
        Assert.Equal(2, await SpikeCommand.RunAsync([.. required, "--unsent", root], output, error));
        Assert.Equal(2, await SpikeCommand.RunAsync(["--output", destination, "--as-of", "0001-01-01"], output, error));
        Assert.Equal(2, await SpikeCommand.RunAsync(["--output", destination, "--as-of", "not-a-date"], output, error));
        Assert.Equal(2, await SpikeCommand.RunAsync(["--output", destination], output, error));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        Directory.CreateDirectory(destination);
        string sentinel = Path.Combine(destination, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "previous data");
        Assert.Equal(2, await SpikeCommand.RunAsync(required, output, error));
        Assert.Equal("previous data", await File.ReadAllTextAsync(sentinel));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_KeepPublishedFixturesComplete_When_ExportFailsOrIsCancelled(bool cancel)
    {
        var first = Options("published");
        await SnapshotWriter.WriteAsync(first);
        var saved = Directory.GetFiles(first.OutputDirectory).ToDictionary(path => Path.GetFileName(path),
            path => File.ReadAllBytes(path));
        byte[] unsent = await File.ReadAllBytesAsync(first.UnsentPath);
        var failing = first with { OutputDirectory = Path.Combine(root, "failed") };
        using var cancellation = new CancellationTokenSource();
        bool reachedPartialWrite = false;
        void Interrupt(SpikeProgress progress)
        {
            if (progress is not { Phase: "products", Records: 1 }) return;
            Assert.False(Directory.Exists(failing.OutputDirectory));
            string staging = Assert.Single(Directory.GetDirectories(root, ".failed.*.staging"));
            Assert.True(new FileInfo(Path.Combine(staging, "snapshot.json")).Length > 0);
            reachedPartialWrite = true;
            if (cancel) cancellation.Cancel();
            else throw new IOException("Simulated storage write failure");
        }
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotWriter.WriteAsync(failing, cancellation.Token, Interrupt));
        else
            await Assert.ThrowsAsync<IOException>(() => SnapshotWriter.WriteAsync(failing, cancellation.Token, Interrupt));
        Assert.True(reachedPartialWrite);
        Assert.False(Directory.Exists(failing.OutputDirectory));
        Assert.Empty(Directory.GetDirectories(root, "*.staging"));
        Assert.Equal(unsent, await File.ReadAllBytesAsync(first.UnsentPath));
        foreach (var file in saved) Assert.Equal(file.Value, await File.ReadAllBytesAsync(Path.Combine(first.OutputDirectory, file.Key!)));
        await SnapshotWriter.WriteAsync(failing); // A clean retry can publish the whole export.
        using var published = Read(failing, "manifest.json");
        Assert.Equal("spike-format-v1", published.RootElement.GetProperty("schema").GetString());

        using var output = new StringWriter();
        using var error = new StringWriter();
        cancellation.Cancel();
        var cancelled = Options("cancelled");
        Assert.Equal(130, await SpikeCommand.RunAsync(Arguments(cancelled), output, error, cancellation.Token));
        Assert.False(Directory.Exists(cancelled.OutputDirectory));
    }

    private SpikeOptions Options(string output = "export", string unsent = "unsent.json") => new("baseline",
        new(40, 50, 2, 3), 42, new(2026, 10, 20), Path.Combine(root, output), Path.Combine(root, unsent));

    private static string[] Arguments(SpikeOptions options) => ["--profile", options.Profile,
        "--products", options.Scale.Products.ToString(CultureInfo.InvariantCulture),
        "--locations", options.Scale.Locations.ToString(CultureInfo.InvariantCulture),
        "--history-orders", options.Scale.OrdersPerLocation.ToString(CultureInfo.InvariantCulture),
        "--history-lines", options.Scale.LinesPerOrder.ToString(CultureInfo.InvariantCulture),
        "--seed", options.Seed.ToString(CultureInfo.InvariantCulture), "--as-of", options.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "--output", options.OutputDirectory, "--unsent", options.UnsentPath];

    private static async Task RunSuccessfully(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, await SpikeCommand.RunAsync(args, output, error));
        Assert.Equal(string.Empty, error.ToString());
        using var report = JsonDocument.Parse(output.ToString());
        Assert.Equal("desktop-generation-only", report.RootElement.GetProperty("evidence").GetString());
        Assert.True(report.RootElement.GetProperty("elapsedSeconds").GetDouble() > 0);
    }

    private static JsonDocument Read(SpikeOptions options, string name) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(options.OutputDirectory, name)));
    private static JsonElement[] Rows(JsonElement parent, string name) => parent.GetProperty(name).EnumerateArray().ToArray();

    public void Dispose()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FieldSales.SnapshotSpike.Tests")) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(root);
        if (!target.StartsWith(parent, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Test cleanup escaped its temporary directory.");
        Directory.Delete(target, recursive: true);
    }
}
