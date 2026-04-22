// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Messages;
using Xunit;

#if NET
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.Tests;
#endif

namespace OpenTelemetry.OpAmp.Client.Tests.Messages;

public class EffectiveConfigFileTests
{
    [Fact]
    public void Constructor_NullContentType_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new EffectiveConfigFile(Array.Empty<byte>(), null!, "filename.yml"));

    [Fact]
    public void Constructor_NullFileName_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new EffectiveConfigFile(Array.Empty<byte>(), "application/octet-stream", null!));

    [Fact]
    public void CreateFromStream_NullStream_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => EffectiveConfigFile.CreateFromStream(null!, "application/octet-stream", "filename.yml", 1_024));

    [Fact]
    public void CreateFromStream_NullContentType_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream([]);

        Assert.Throws<ArgumentNullException>(() => EffectiveConfigFile.CreateFromStream(stream, null!, "filename.yml", 1_024));
    }

    [Fact]
    public void CreateFromStream_NullFileName_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream([]);

        Assert.Throws<ArgumentNullException>(() => EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", null!, 1_024));
    }

    [Fact]
    public void CreateFromStream_NegativeMaxBytes_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream([]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", -1));
    }

    [Fact]
    public void CreateFromStream_UnreadableStream_ThrowsArgumentException()
    {
        using var stream = new NonReadableStream();

        var exception = Assert.Throws<ArgumentException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 1_024));

        Assert.Equal("stream", exception.ParamName);
    }

    [Fact]
    public void CreateFromStream_SeekableStream_WithContentUnderMaxBytes_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new MemoryStream(content);

        var config = EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", content.Length + 1);

        Assert.Equal(content, config.Content.ToArray());
        Assert.Equal("application/octet-stream", config.ContentType);
        Assert.Equal("filename.yml", config.FileName);
    }

    [Fact]
    public void CreateFromStream_SeekableStream_WithCurrentPosition_UsesRemainingBytesAndPreservesMetadata()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new MemoryStream(content);
        stream.Position = 2;

        var config = EffectiveConfigFile.CreateFromStream(stream, "application/json", "config.json", 3);

        Assert.Equal([3, 4, 5], config.Content.ToArray());
        Assert.Equal("application/json", config.ContentType);
        Assert.Equal("config.json", config.FileName);
    }

    [Fact]
    public void CreateFromStream_SeekableStream_WithContentAtMaxBytes_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new MemoryStream(content);

        var config = EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", content.Length);

        Assert.Equal(content, config.Content.ToArray());
    }

    [Fact]
    public void CreateFromStream_SeekableStream_WithRemainingContentOverMaxBytes_Throws()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new MemoryStream(content);
        stream.Position = 1;

        Assert.Throws<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 3));
    }

    [Fact]
    public void CreateFromStream_SeekableStream_WithRemainingContentOverMaxBytes_DoesNotAdvancePosition()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new MemoryStream(content);
        stream.Position = 1;

        Assert.Throws<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 3));

        Assert.Equal(1, stream.Position);
    }

    [Fact]
    public void CreateFromStream_ZeroMaxBytes_WithEmptyStream_Succeeds()
    {
        using var stream = new MemoryStream([]);

        var config = EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 0);

        Assert.Empty(config.Content.ToArray());
    }

    [Fact]
    public void CreateFromStream_ZeroMaxBytes_WithContent_Throws()
    {
        using var stream = new MemoryStream([1]);

        Assert.Throws<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 0));
    }

    [Fact]
    public void CreateFromStream_ZeroMaxBytes_WithContent_DoesNotAdvancePosition()
    {
        using var stream = new MemoryStream([1, 2]);

        Assert.Throws<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 0));

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void CreateFromStream_EmptyStream_WithPositiveMaxBytes_Succeeds()
    {
        using var stream = new MemoryStream([]);

        var config = EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 4);

        Assert.Empty(config.Content.ToArray());
    }

    [Fact]
    public void CreateFromStream_NonSeekableStream_WithPartialReads_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new NonSeekableReadStream(content, maxReadSize: 1);

        var config = EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", content.Length);

        Assert.Equal(content, config.Content.ToArray());
    }

    [Fact]
    public void CreateFromStream_NonSeekableStream_WithContentAtMaxBytes_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new NonSeekableReadStream(content);

        var config = EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", content.Length);

        Assert.Equal(content, config.Content.ToArray());
    }

    [Fact]
    public void CreateFromStream_NonSeekableStream_WithContentOverMaxBytes_Throws()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new NonSeekableReadStream(content);

        Assert.Throws<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", content.Length - 1));
    }

    [Fact]
    public void CreateFromStream_NonSeekableStream_ZeroMaxBytes_WithContent_ConsumesOneByteBeforeThrow()
    {
        using var stream = new NonSeekableReadStream([1, 2]);

        Assert.Throws<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 0));

        Assert.Equal(1, stream.BytesConsumed);
    }

    // EventListener on .NET Framework uses the ETW stack whose initialization timing
    // differs from the in-process model on modern .NET, making this test unreliable
    // when test classes run in parallel. The test is therefore NET-only.
#if NET
    [Fact]
    public void CreateFromStream_WhenExceedsMaxBytes_LogsEvent()
    {
        using var listener = new InMemoryEventListener(OpAmpClientEventSource.Log);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(content);

        Assert.Throws<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStream(stream, "application/octet-stream", "filename.yml", 3));

        var @event = Assert.Single(listener.Events);
        Assert.Equal("EffectiveConfigSizeLimitViolation", @event.EventName);
        Assert.Equal(3, (int)@event.Payload![0]!);
    }
#endif

    [Fact]
    public async Task CreateFromStreamAsync_NullStream_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(null!, "application/octet-stream", "filename.yml", 1_024));

    [Fact]
    public async Task CreateFromStreamAsync_NullContentType_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, null!, "filename.yml", 1_024));
    }

    [Fact]
    public async Task CreateFromStreamAsync_NullFileName_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", null!, 1_024));
    }

    [Fact]
    public async Task CreateFromStreamAsync_NegativeMaxBytes_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream([]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", -1));
    }

    [Fact]
    public async Task CreateFromStreamAsync_UnreadableStream_ThrowsArgumentException()
    {
        using var stream = new NonReadableStream();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 1_024));

        Assert.Equal("stream", exception.ParamName);
    }

    [Fact]
    public async Task CreateFromStreamAsync_SeekableStream_WithContentUnderMaxBytes_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new MemoryStream(content);

        var config = await EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", content.Length + 1);

        Assert.Equal(content, config.Content.ToArray());
        Assert.Equal("application/octet-stream", config.ContentType);
        Assert.Equal("filename.yml", config.FileName);
    }

    [Fact]
    public async Task CreateFromStreamAsync_SeekableStream_WithCurrentPosition_UsesRemainingBytesAndPreservesMetadata()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new MemoryStream(content);
        stream.Position = 2;

        var config = await EffectiveConfigFile.CreateFromStreamAsync(stream, "application/json", "config.json", 3);

        Assert.Equal([3, 4, 5], config.Content.ToArray());
        Assert.Equal("application/json", config.ContentType);
        Assert.Equal("config.json", config.FileName);
    }

    [Fact]
    public async Task CreateFromStreamAsync_SeekableStream_WithContentAtMaxBytes_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new MemoryStream(content);

        var config = await EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", content.Length);

        Assert.Equal(content, config.Content.ToArray());
    }

    [Fact]
    public async Task CreateFromStreamAsync_SeekableStream_WithRemainingContentOverMaxBytes_Throws()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new MemoryStream(content);
        stream.Position = 1;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 3));
    }

    [Fact]
    public async Task CreateFromStreamAsync_SeekableStream_WithRemainingContentOverMaxBytes_DoesNotAdvancePosition()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new MemoryStream(content);
        stream.Position = 1;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 3));

        Assert.Equal(1, stream.Position);
    }

    [Fact]
    public async Task CreateFromStreamAsync_ZeroMaxBytes_WithEmptyStream_Succeeds()
    {
        using var stream = new MemoryStream([]);

        var config = await EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 0);

        Assert.Empty(config.Content.ToArray());
    }

    [Fact]
    public async Task CreateFromStreamAsync_ZeroMaxBytes_WithContent_Throws()
    {
        using var stream = new MemoryStream([1]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 0));
    }

    [Fact]
    public async Task CreateFromStreamAsync_ZeroMaxBytes_WithContent_DoesNotAdvancePosition()
    {
        using var stream = new MemoryStream([1, 2]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 0));

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task CreateFromStreamAsync_EmptyStream_WithPositiveMaxBytes_Succeeds()
    {
        using var stream = new MemoryStream([]);

        var config = await EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 4);

        Assert.Empty(config.Content.ToArray());
    }

    [Fact]
    public async Task CreateFromStreamAsync_NonSeekableStream_WithPartialReads_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new NonSeekableReadStream(content, maxReadSize: 1);

        var config = await EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", content.Length);

        Assert.Equal(content, config.Content.ToArray());
    }

    [Fact]
    public async Task CreateFromStreamAsync_NonSeekableStream_WithContentAtMaxBytes_Succeeds()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        using var stream = new NonSeekableReadStream(content);

        var config = await EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", content.Length);

        Assert.Equal(content, config.Content.ToArray());
    }

    [Fact]
    public async Task CreateFromStreamAsync_NonSeekableStream_WithContentOverMaxBytes_Throws()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        using var stream = new NonSeekableReadStream(content);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", content.Length - 1));
    }

    [Fact]
    public async Task CreateFromStreamAsync_NonSeekableStream_ZeroMaxBytes_WithContent_ConsumesOneByteBeforeThrow()
    {
        using var stream = new NonSeekableReadStream([1, 2]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 0));

        Assert.Equal(1, stream.BytesConsumed);
    }

    [Fact]
    public async Task CreateFromStreamAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var content = new byte[] { 1, 2, 3, 4 };
        using var stream = new NonSeekableReadStream(content);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", content.Length, cancellationToken: cts.Token));
    }

    // EventListener on .NET Framework uses the ETW stack whose initialization timing
    // differs from the in-process model on modern .NET, making this test unreliable
    // when test classes run in parallel. The test is therefore NET-only.
#if NET
    [Fact]
    public async Task CreateFromStreamAsync_WhenExceedsMaxBytes_LogsEvent()
    {
        using var listener = new InMemoryEventListener(OpAmpClientEventSource.Log);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(content);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EffectiveConfigFile.CreateFromStreamAsync(stream, "application/octet-stream", "filename.yml", 3));

        var @event = Assert.Single(listener.Events);
        Assert.Equal("EffectiveConfigSizeLimitViolation", @event.EventName);
        Assert.Equal(3, (int)@event.Payload![0]!);
    }
#endif

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream innerStream;
        private readonly int maxReadSize;

        public NonSeekableReadStream(byte[] content, int maxReadSize = int.MaxValue)
        {
            this.innerStream = new MemoryStream(content);
            this.maxReadSize = maxReadSize;
        }

        public int BytesConsumed => checked((int)this.innerStream.Position);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => this.innerStream.Read(buffer, offset, Math.Min(count, this.maxReadSize));

        public override int ReadByte()
            => this.innerStream.ReadByte();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<int>(cancellationToken)
                : Task.FromResult(this.innerStream.Read(buffer, offset, Math.Min(count, this.maxReadSize)));

#if NET
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            var count = Math.Min(buffer.Length, this.maxReadSize);
            return new ValueTask<int>(this.innerStream.Read(buffer.Span[..count]));
        }
#endif

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.innerStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class NonReadableStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override int ReadByte()
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
