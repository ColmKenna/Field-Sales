using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FieldSales.TabletSpike.Core;

public sealed record ActiveSnapshot(string Generation, DateTimeOffset CompletedUtc, FixtureManifest Manifest);
public sealed record LocationMatch(string Id, string Name, string Town);
public sealed record SearchResult(string Generation, IReadOnlyList<LocationMatch> Items, int TotalMatches, double QueryMs);

public sealed class SnapshotSqliteStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    private readonly SpikeWorkspace workspace;
    public SnapshotSqliteStore(SpikeWorkspace workspace)
    {
        this.workspace = workspace;
        using var connection = Open();
        Environment = SqliteEnvironment.Inspect(connection);
    }
    public SqliteEnvironment Environment { get; }
    private SqliteConnection Open()
    {
        var connection = SqliteEnvironment.Open(workspace.SnapshotPath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS generations(id TEXT PRIMARY KEY,state TEXT NOT NULL,manifest TEXT NOT NULL,completed TEXT);
            CREATE TABLE IF NOT EXISTS active(singleton INTEGER PRIMARY KEY CHECK(singleton=1),generation TEXT NOT NULL REFERENCES generations(id));
            CREATE TABLE IF NOT EXISTS entities(generation TEXT NOT NULL REFERENCES generations(id) ON DELETE CASCADE,family TEXT NOT NULL,ordinal INTEGER NOT NULL,json TEXT NOT NULL,PRIMARY KEY(generation,family,ordinal));
            CREATE TABLE IF NOT EXISTS search(generation TEXT NOT NULL REFERENCES generations(id) ON DELETE CASCADE,family TEXT NOT NULL,id TEXT NOT NULL,name TEXT NOT NULL,town TEXT NOT NULL,normalized TEXT NOT NULL,PRIMARY KEY(generation,family,id));
            CREATE INDEX IF NOT EXISTS search_order ON search(generation,family,normalized,id);
            """;
        command.ExecuteNonQuery();
        return connection;
    }
    public SnapshotReadSession Capture() => new(Open());
    public ActiveSnapshot? ReadActive() { using var read = Capture(); return read.Active; }
    public SearchResult Search(string term, int limit = 50) { using var read = Capture(); return read.Search(term, limit); }

    // All synchronous provider/parsing operations run on a worker, including startup/recovery and activation.
    public async Task<string> ApplyAsync(string gzipPath, FixtureManifest manifest, SpikeReport report,
        SpikeHooks? hooks = null, CancellationToken ct = default)
    {
        var gate = Gates.GetOrAdd(workspace.SnapshotPath, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try { return await Task.Run(() => Apply(gzipPath, manifest, report, hooks ?? new(), ct), ct); }
        finally { gate.Release(); }
    }
    private string Apply(string gzipPath, FixtureManifest manifest, SpikeReport report, SpikeHooks hooks, CancellationToken ct)
    {
        manifest.Validate(workspace.Owner);
        var compressedWatch = Stopwatch.StartNew();
        using (var compressed = File.OpenRead(gzipPath))
            if (compressed.Length != manifest.Gzip.Bytes || Convert.ToHexStringLower(SHA256.HashData(compressed)) != manifest.Gzip.Sha256.ToLowerInvariant())
                throw new InvalidDataException("Compressed payload length/hash mismatch.");
        report.PhaseMs["localCompressedFileVerification"] = compressedWatch.Elapsed.TotalMilliseconds;
        using var connection = Open();
        Execute(connection, "DELETE FROM generations WHERE state='staged'");
        if (hooks.MaxDatabasePages is int maxPages) Execute(connection, $"PRAGMA max_page_count={maxPages}");
        string generation = Guid.NewGuid().ToString("N");
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "INSERT INTO generations(id,state,manifest) VALUES($id,'staged',$manifest)";
            create.Parameters.AddWithValue("$id", generation);
            create.Parameters.AddWithValue("$manifest", JsonSerializer.Serialize(manifest, FixtureManifest.JsonOptions));
            create.ExecuteNonQuery();
        }
        var watch = Stopwatch.StartNew();
        var counts = FixtureManifest.CountNames.ToDictionary(n => n, _ => 0L);
        SqliteTransaction? batch = null;
        int total = 0;
        double rowWorkMs = 0;
        try
        {
            using var file = File.OpenRead(gzipPath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var hashing = new HashingReadStream(gzip, manifest.Json.Bytes);
            var parser = new SnapshotRowReader(hashing);
            parser.Read(manifest, (family, entity) =>
            {
                long rowStart = Stopwatch.GetTimestamp();
                ct.ThrowIfCancellationRequested();
                batch ??= connection.BeginTransaction();
                Count(counts, family, entity);
                long ordinal = counts[family == "history" ? "historicalOrders" : family];
                using var insert = connection.CreateCommand();
                insert.Transaction = batch;
                insert.CommandText = "INSERT INTO entities(generation,family,ordinal,json) VALUES($g,$f,$n,$j)";
                insert.Parameters.AddWithValue("$g", generation);
                insert.Parameters.AddWithValue("$f", family);
                insert.Parameters.AddWithValue("$n", ordinal);
                insert.Parameters.AddWithValue("$j", entity.GetRawText());
                insert.ExecuteNonQuery();
                if (family is "locations" or "products")
                {
                    using var projection = connection.CreateCommand();
                    projection.Transaction = batch;
                    projection.CommandText = "INSERT INTO search(generation,family,id,name,town,normalized) VALUES($g,$f,$id,$name,$town,$norm)";
                    string name = entity.GetProperty("name").GetString()!;
                    string town = family == "locations" ? entity.GetProperty("town").GetString()! : "";
                    projection.Parameters.AddWithValue("$g", generation);
                    projection.Parameters.AddWithValue("$f", family);
                    projection.Parameters.AddWithValue("$id", entity.GetProperty("id").GetGuid().ToString());
                    projection.Parameters.AddWithValue("$name", name);
                    projection.Parameters.AddWithValue("$town", town);
                    projection.Parameters.AddWithValue("$norm", SearchTerms.Normalize(name + " " + town));
                    projection.ExecuteNonQuery();
                }
                total++;
                if (total % 128 == 0)
                {
                    batch.Commit(); batch.Dispose(); batch = null;
                    hooks.BatchWritten?.Invoke(total);
                    hooks.AtBoundary?.Invoke("import");
                    if (total == 128 || total % 4096 == 0) report.DiskSamples.Add(workspace.Measure(false));
                }
                rowWorkMs += Stopwatch.GetElapsedTime(rowStart).TotalMilliseconds;
            }, ct);
            batch?.Commit(); batch?.Dispose(); batch = null;
            report.MaxParserBufferBytes = parser.MaxBufferBytes;
            report.PhaseMs["decompressParseInsertAndProjectionIndexes"] = watch.Elapsed.TotalMilliseconds;
            report.PhaseMs["pipelineSubset_decompressionReadAndHash"] = hashing.ReadAndHashMs;
            report.PhaseMs["pipelineSubset_entityCountInsertProjectionAndBatchCommit"] = rowWorkMs;
            report.PhaseMs["pipelineRemainder_parseDispatchAndOverhead"] = Math.Max(0, watch.Elapsed.TotalMilliseconds - hashing.ReadAndHashMs - rowWorkMs);
            watch.Restart();
            if (hashing.Bytes != manifest.Json.Bytes || hashing.FinishHash() != manifest.Json.Sha256.ToLowerInvariant() ||
                counts.Any(pair => manifest.Counts[pair.Key] != pair.Value))
                throw new InvalidDataException("Plain payload hash, length or nested counts mismatch.");
            report.PhaseMs["plainHashAndCountVerification"] = watch.Elapsed.TotalMilliseconds;
            report.DiskSamples.Add(workspace.Measure());
            hooks.AtBoundary?.Invoke("before-activation");
            ct.ThrowIfCancellationRequested();
            watch.Restart();
            using (var transaction = connection.BeginTransaction())
            using (var activate = connection.CreateCommand())
            {
                activate.Transaction = transaction;
                activate.CommandText = "UPDATE generations SET state='complete',completed=$when WHERE id=$g; INSERT INTO active(singleton,generation) VALUES(1,$g) ON CONFLICT(singleton) DO UPDATE SET generation=excluded.generation;";
                activate.Parameters.AddWithValue("$g", generation);
                activate.Parameters.AddWithValue("$when", DateTimeOffset.UtcNow.ToString("O"));
                activate.ExecuteNonQuery();
                hooks.BeforeCommit?.Invoke();
                ct.ThrowIfCancellationRequested();
                transaction.Commit();
            }
            report.PhaseMs["activation"] = watch.Elapsed.TotalMilliseconds;
            report.Generation = generation;
            hooks.AtBoundary?.Invoke("after-activation");
            watch.Restart();
            // Keep active + immediately preceding completed generation. WAL readers retain their captured view.
            Execute(connection, "DELETE FROM generations WHERE state='complete' AND id NOT IN (SELECT id FROM generations WHERE state='complete' ORDER BY completed DESC,id DESC LIMIT 2)");
            Execute(connection, "PRAGMA wal_checkpoint(PASSIVE)");
            report.PhaseMs["checkpointAndRetention"] = watch.Elapsed.TotalMilliseconds;
            report.DiskSamples.Add(workspace.Measure());
            return generation;
        }
        finally { batch?.Dispose(); }
    }
    private static void Count(Dictionary<string, long> counts, string family, JsonElement row)
    {
        counts[family == "history" ? "historicalOrders" : family]++;
        if (family == "products")
        {
            counts["attributes"] += row.GetProperty("attributes").GetArrayLength();
            counts["basePrices"] += row.GetProperty("basePrices").GetArrayLength();
            foreach (var tier in row.GetProperty("priceTiers").EnumerateArray()) counts["quantityBreaks"] += tier.GetProperty("breaks").GetArrayLength();
        }
        if (family == "history") counts["historicalLines"] += row.GetProperty("lines").GetArrayLength();
        if (family == "masters") counts["branchLinks"] += row.GetProperty("branchLocationIds").GetArrayLength();
    }
    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
}

public sealed class SnapshotReadSession : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SqliteTransaction transaction;
    public ActiveSnapshot? Active { get; }
    internal SnapshotReadSession(SqliteConnection connection)
    {
        this.connection = connection;
        transaction = connection.BeginTransaction(deferred: true);
        using var command = Command("SELECT g.id,g.completed,g.manifest FROM active a JOIN generations g ON g.id=a.generation WHERE g.state='complete'");
        using var reader = command.ExecuteReader();
        if (reader.Read()) Active = new(reader.GetString(0), DateTimeOffset.Parse(reader.GetString(1)),
            JsonSerializer.Deserialize<FixtureManifest>(reader.GetString(2), FixtureManifest.JsonOptions)!);
    }
    public long Count(string family)
    {
        using var command = Command("SELECT COUNT(*) FROM entities WHERE generation=$g AND family=$f");
        command.Parameters.AddWithValue("$g", Active?.Generation ?? ""); command.Parameters.AddWithValue("$f", family);
        return (long)command.ExecuteScalar()!;
    }
    public string? FirstJson(string family)
    {
        using var command = Command("SELECT json FROM entities WHERE generation=$g AND family=$f ORDER BY ordinal LIMIT 1");
        command.Parameters.AddWithValue("$g", Active?.Generation ?? ""); command.Parameters.AddWithValue("$f", family);
        return command.ExecuteScalar() as string;
    }
    public SearchResult Search(string term, int limit = 50)
    {
        if (term.Length > 256 || limit is < 1 or > 200) throw new ArgumentException("Search term/limit exceeds the benchmark bounds.");
        var watch = Stopwatch.StartNew();
        using var command = Command("SELECT id,name,town,COUNT(*) OVER() FROM search WHERE generation=$g AND family='locations' AND instr(normalized,$term)>0 ORDER BY normalized,id LIMIT $limit");
        command.Parameters.AddWithValue("$g", Active?.Generation ?? "");
        command.Parameters.AddWithValue("$term", SearchTerms.Normalize(term)); command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        var items = new List<LocationMatch>(); int total = 0;
        while (reader.Read()) { items.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2))); total = reader.GetInt32(3); }
        return new(Active?.Generation ?? "", items, total, watch.Elapsed.TotalMilliseconds);
    }
    private SqliteCommand Command(string sql)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command;
    }
    public void Dispose() { transaction.Dispose(); connection.Dispose(); }
}
