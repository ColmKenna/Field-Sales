using System.Security.Cryptography;

namespace FieldSales.Tablet.Core;

/// <summary>Hashes everything read through it and refuses to read past a byte limit (a decompression bound).</summary>
internal sealed class HashingReadStream(Stream inner, long maximumBytes) : Stream
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    public long Bytes { get; private set; }
    public string FinishHash() => Convert.ToHexStringLower(hash.GetHashAndReset());
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        int count = inner.Read(buffer);
        Bytes = checked(Bytes + count);
        if (Bytes > maximumBytes) throw new InvalidDataException("The snapshot is larger than this app accepts.");
        hash.AppendData(buffer[..count]);
        return count;
    }
    protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => Bytes; set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
