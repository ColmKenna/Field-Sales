using System.Security.Cryptography;
using System.Text;

namespace FieldSales.Tablet.Core;

/// <summary>One rep's local files: the snapshot and Unsent databases are kept apart and bound to that identity.</summary>
/// <remarks>The folder name is a hash of the identity subject, so another rep on the same tablet never opens it.</remarks>
public sealed class RepWorkspace
{
    public RepWorkspace(string root, string repSubject)
    {
        if (string.IsNullOrWhiteSpace(repSubject) || repSubject.Length > 450)
            throw new ArgumentException("A rep identity subject is required.", nameof(repSubject));
        RepSubject = repSubject;
        DirectoryPath = Path.Combine(Path.GetFullPath(root), "reps",
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(repSubject))));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string RepSubject { get; }
    public string DirectoryPath { get; }
    public string SnapshotPath => Path.Combine(DirectoryPath, "snapshot.db");
    public string UnsentPath => Path.Combine(DirectoryPath, "unsent.db");
    public string TransferPath => Path.Combine(DirectoryPath, "download.gz");

    /// <summary>The snapshot database with its WAL and shared-memory files, as stored now.</summary>
    public long SnapshotBytes() => new[] { SnapshotPath, SnapshotPath + "-wal", SnapshotPath + "-shm" }
        .Sum(path => File.Exists(path) ? new FileInfo(path).Length : 0);
}

/// <summary>Free space where the rep's files live. The tablet app supplies the platform's own measure.</summary>
public interface IStorageSpace
{
    long AvailableBytes(string directory);
}

public sealed class DriveStorageSpace : IStorageSpace
{
    public long AvailableBytes(string directory) => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!).AvailableFreeSpace;
}
