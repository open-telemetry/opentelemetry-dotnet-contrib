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

        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Value must be greater than zero.");
        }

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
        var remaining = this.maxBytes - this.totalBytesRead;
        if (remaining <= 0)
        {
            // Allowance exhausted - signal EOF so callers stop reading.
            return 0;
        }

        var clampedCount = (int)Math.Min(count, remaining);
        var bytesRead = this.innerStream.Read(buffer, offset, clampedCount);
        this.totalBytesRead += bytesRead;
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#if NET
        return await this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
#else
        var remaining = this.maxBytes - this.totalBytesRead;
        if (remaining <= 0)
        {
            return 0;
        }

        var clampedCount = (int)Math.Min(count, remaining);
        var bytesRead = await this.innerStream.ReadAsync(buffer, offset, clampedCount, cancellationToken).ConfigureAwait(false);
        this.totalBytesRead += bytesRead;
        return bytesRead;
#endif
    }

#if NET
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var remaining = this.maxBytes - this.totalBytesRead;
        if (remaining <= 0)
        {
            return 0;
        }

        var clampedLength = (int)Math.Min(buffer.Length, remaining);
        var bytesRead = await this.innerStream.ReadAsync(buffer[..clampedLength], cancellationToken).ConfigureAwait(false);
        this.totalBytesRead += bytesRead;
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
