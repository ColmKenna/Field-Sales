using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace FieldSales.TabletSpike.Core;

public sealed class SpikeWorkspace
{
    public SpikeWorkspace(string root, string owner)
    {
        if (!owner.StartsWith("spike-rep-", StringComparison.Ordinal) ||
            !int.TryParse(owner[10..], out _)) throw new ArgumentException("Select a synthetic spike-rep-<seed> owner.");
        Owner = owner;
        DirectoryPath = Path.Combine(Path.GetFullPath(root), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(owner))));
        Directory.CreateDirectory(DirectoryPath);
    }
    public string Owner { get; }
    public string DirectoryPath { get; }
    public string SnapshotPath => Path.Combine(DirectoryPath, "snapshot.db");
    public string UnsentPath => Path.Combine(DirectoryPath, "unsent.db");
    public string TransferPath => Path.Combine(DirectoryPath, "download.gz");
    public DiskMeasurement Measure(bool includeGenerationPayloads = false)
    {
        long Size(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
        Dictionary<string, long>? generations = null;
        double? scanMs = null;
        if (includeGenerationPayloads && File.Exists(SnapshotPath))
        {
            var watch = Stopwatch.StartNew();
            generations = [];
            using (var connection = SqliteEnvironment.Open(SnapshotPath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT g.id,g.state,COALESCE(SUM(length(CAST(e.json AS BLOB))),0) FROM generations g LEFT JOIN entities e ON e.generation=g.id GROUP BY g.id,g.state";
                using var rows = command.ExecuteReader();
                while (rows.Read()) generations[$"{rows.GetString(0)}:{rows.GetString(1)}"] = rows.GetInt64(2);
            }
            scanMs = watch.Elapsed.TotalMilliseconds;
        }
        return new(Size(SnapshotPath), Size(SnapshotPath + "-wal"), Size(SnapshotPath + "-journal"), Size(SnapshotPath + "-shm"),
            Size(TransferPath), Size(UnsentPath) + Size(UnsentPath + "-wal") + Size(UnsentPath + "-journal") + Size(UnsentPath + "-shm"), generations, scanMs);
    }
}
