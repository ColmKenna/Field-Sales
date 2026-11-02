using FieldSales.Tablet.Core;
using Foundation;

namespace FieldSales.Tablet;

internal static class IosTabletStorage
{
    public static string Prepare()
    {
        string support = NSFileManager.DefaultManager.GetUrls(
            NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User)[0].Path!;
        string root = Path.Combine(support, "FieldSales");
        NSFileAttributes attributes = new() { ProtectionKey = NSFileProtection.CompleteUntilFirstUserAuthentication };
        if (!NSFileManager.DefaultManager.CreateDirectory(root, true, attributes, out NSError? error))
            throw new IOException($"Cannot create tablet storage: {error?.LocalizedDescription}");
        if (!NSFileManager.DefaultManager.SetAttributes(attributes, root, out error))
            throw new IOException($"Cannot protect tablet storage: {error?.LocalizedDescription}");
        // Descendants inherit the directory's protection and backup exclusion, including SQLite WAL/SHM files.
        using NSUrl url = NSUrl.FromFilename(root);
        if (!url.SetResource(NSUrl.IsExcludedFromBackupKey, NSNumber.FromBoolean(true), out error))
            throw new IOException($"Cannot exclude tablet storage from backup: {error?.LocalizedDescription}");
#if DEBUG
        VerifyNativeStorage(root);
#endif
        return root;
    }

#if DEBUG
    private static void VerifyNativeStorage(string root)
    {
        // Exercise the actual bundled provider before sign-in, without touching a rep's data.
        string probe = Path.Combine(root, ".storage-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            RepWorkspace workspace = new(probe, "storage-check");
            SqliteEngine engine = new SnapshotStore(workspace).Engine;
            UnsentStore work = new(workspace);
            work.Save(new(Guid.NewGuid(), UnsentKind.Call, DateTimeOffset.UtcNow, "{}"));
            if (work.Read().Count != 1) throw new IOException("Tablet native storage readback failed.");
            NSFileProtection? protection = NSFileManager.DefaultManager.GetAttributes(workspace.UnsentPath)?.ProtectionKey;
            if (DeviceInfo.Current.DeviceType == DeviceType.Physical
                && protection != NSFileProtection.CompleteUntilFirstUserAuthentication)
                throw new IOException($"Tablet file protection was not inherited: {protection}.");
            Console.WriteLine($"FieldSalesTiming: native-storage sqlite={engine.Version} journal={engine.JournalMode} synchronous={engine.Synchronous} protection={protection} backup-excluded={NSFileManager.GetSkipBackupAttribute(root)} free-bytes={new IosStorageSpace().AvailableBytes(root)}");
        }
        finally { if (Directory.Exists(probe)) Directory.Delete(probe, recursive: true); }
    }
#endif
}

public sealed class IosStorageSpace : IStorageSpace
{
    public long AvailableBytes(string directory)
    {
        NSFileSystemAttributes? attributes = NSFileManager.DefaultManager.GetFileSystemAttributes(directory, out NSError? error);
        if (attributes?.FreeSize is not { } bytes)
            throw new IOException($"Cannot measure tablet free space: {error?.LocalizedDescription}");
        return checked((long)bytes);
    }
}
