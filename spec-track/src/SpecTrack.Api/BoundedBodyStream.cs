namespace SpecTrack.Api;

/// <summary>Enforces the request limit even when a client streams without Content-Length.</summary>
internal sealed class BoundedBodyStream(Stream inner, long maximumBytes) : Stream
{
    private long _read;
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    private int Limit(int requested) => (int)Math.Min(requested, maximumBytes - _read + 1);
    private int Count(int bytes)
    {
        _read += bytes;
        if (_read > maximumBytes)
            throw new BadHttpRequestException("Request exceeds the 4 MiB limit.", StatusCodes.Status413PayloadTooLarge);
        return bytes;
    }
    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, Limit(count)));
    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer[..Limit(buffer.Length)]));
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Count(await inner.ReadAsync(buffer.AsMemory(offset, Limit(count)), cancellationToken));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Count(await inner.ReadAsync(buffer[..Limit(buffer.Length)], cancellationToken));
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
