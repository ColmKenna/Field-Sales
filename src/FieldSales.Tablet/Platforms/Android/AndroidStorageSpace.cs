using Android.OS;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet;

/// <summary>Free space on the volume holding the app's files, as Android reports it to apps.</summary>
public sealed class AndroidStorageSpace : IStorageSpace
{
    public long AvailableBytes(string directory) => new StatFs(directory).AvailableBytes;
}
