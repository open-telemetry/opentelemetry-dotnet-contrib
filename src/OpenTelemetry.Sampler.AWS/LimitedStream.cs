// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.AWS;

/// <summary>
/// A read-only stream wrapper that throws <see cref="InvalidOperationException"/>
/// if the underlying stream exceeds a configured maximum number of bytes.
/// This protects against denial-of-service when reading from untrusted HTTP responses.
/// </summary>
internal sealed class LimitedStream : Stream
{
    private readonly Stream innerStream;
    private readonly long maxBytes;
    private long totalBytesRead;

    public LimitedStream(Stream innerStream, long maxBytes)
    {
        this.innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        this.maxBytes = maxBytes;
    }

    public override bool CanRead => this.innerStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = this.innerStream.Read(buffer, offset, count);
        this.totalBytesRead += bytesRead;
        if (this.totalBytesRead > this.maxBytes)
        {
            throw new InvalidOperationException(
                $"Response exceeded the maximum allowed size of {this.maxBytes} bytes.");
        }

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#if NET
        return await this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
#else
        var bytesRead = await this.innerStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        this.totalBytesRead += bytesRead;
        if (this.totalBytesRead > this.maxBytes)
        {
            throw new InvalidOperationException(
                $"Response exceeded the maximum allowed size of {this.maxBytes} bytes.");
        }

        return bytesRead;
#endif
    }

#if NET
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await this.innerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        this.totalBytesRead += bytesRead;
        if (this.totalBytesRead > this.maxBytes)
        {
            throw new InvalidOperationException(
                $"Response exceeded the maximum allowed size of {this.maxBytes} bytes.");
        }

        return bytesRead;
    }
#endif

    public override void Flush() => this.innerStream.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.innerStream.Dispose();
        }

        base.Dispose(disposing);
    }
}
