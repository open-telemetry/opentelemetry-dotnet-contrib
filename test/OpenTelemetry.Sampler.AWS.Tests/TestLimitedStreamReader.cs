// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Xunit;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class TestLimitedStreamReader
{
    [Fact]
    public async Task ReadWithinLimitSucceeds()
    {
        var data = Encoding.UTF8.GetBytes("hello");
        using var inner = new MemoryStream(data);
        using var limited = new LimitedStream(inner, maxBytes: 1024);
        using var reader = new StreamReader(limited);

        var result = await reader.ReadToEndAsync();

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ReadExactlyAtLimitSucceeds()
    {
        var data = Encoding.UTF8.GetBytes("12345");
        using var inner = new MemoryStream(data);
        using var limited = new LimitedStream(inner, maxBytes: 5);
        using var reader = new StreamReader(limited);

        var result = await reader.ReadToEndAsync();

        Assert.Equal("12345", result);
    }

    [Fact]
    public async Task ReadExceedingLimitThrows()
    {
        var data = Encoding.UTF8.GetBytes(new string('x', 2048));
        using var inner = new MemoryStream(data);
        using var limited = new LimitedStream(inner, maxBytes: 1024);
        using var reader = new StreamReader(limited);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadToEndAsync());
    }

    [Fact]
    public void SyncReadExceedingLimitThrows()
    {
        var data = Encoding.UTF8.GetBytes(new string('x', 2048));
        using var inner = new MemoryStream(data);
        using var limited = new LimitedStream(inner, maxBytes: 1024);

        var buffer = new byte[2048];
        Assert.Throws<InvalidOperationException>(
            () => limited.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void CannotWrite()
    {
        using var inner = new MemoryStream();
        using var limited = new LimitedStream(inner, maxBytes: 1024);

        Assert.False(limited.CanWrite);
        Assert.Throws<NotSupportedException>(
            () => limited.Write(new byte[1], 0, 1));
    }

    [Fact]
    public void CannotSeek()
    {
        using var inner = new MemoryStream();
        using var limited = new LimitedStream(inner, maxBytes: 1024);

        Assert.False(limited.CanSeek);
        Assert.Throws<NotSupportedException>(
            () => limited.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void ThrowsOnNullInnerStream()
    {
        Assert.Throws<ArgumentNullException>(() => new LimitedStream(null!, maxBytes: 1024));
    }
}
