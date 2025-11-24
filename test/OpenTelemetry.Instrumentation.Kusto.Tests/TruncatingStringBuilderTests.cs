// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class TruncatingStringBuilderTests
{
    [Fact]
    public void Append_String_AppendsValue()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append("hello");
        builder.Append(" ");
        builder.Append("world");

        Assert.Equal("hello world", builder.ToString());
        Assert.Equal(11, builder.Length);
        Assert.False(builder.IsTruncated);
    }

    [Fact]
    public void Append_Char_AppendsValue()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append('a');
        builder.Append('b');
        builder.Append('c');

        Assert.Equal("abc", builder.ToString());
        Assert.Equal(3, builder.Length);
        Assert.False(builder.IsTruncated);
    }

    [Fact]
    public void Append_Span_AppendsValue()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append("hello".AsSpan());
        builder.Append(" ".AsSpan());
        builder.Append("world".AsSpan());

        Assert.Equal("hello world", builder.ToString());
        Assert.Equal(11, builder.Length);
        Assert.False(builder.IsTruncated);
    }

    [Fact]
    public void Append_ExceedsMaxLength_DoesNotAppendPartialValue()
    {
        using var builder = new TruncatingStringBuilder();

        // Fill up to 250 characters
        var segment = new string('a', 50);
        for (int i = 0; i < 5; i++)
        {
            builder.Append(segment);
        }

        Assert.Equal(250, builder.Length);
        Assert.False(builder.IsTruncated);

        // Try to append 10 more characters (would exceed 255)
        builder.Append("1234567890");

        // Should be truncated and not append partial value
        Assert.Equal(250, builder.Length);
        Assert.True(builder.IsTruncated);
        Assert.DoesNotContain("1234567890", builder.ToString());
    }

    [Fact]
    public void Append_AfterTruncation_IgnoresFutureAppends()
    {
        using var builder = new TruncatingStringBuilder();

        // Fill to 250 characters
        builder.Append(new string('a', 250));

        // Trigger truncation
        builder.Append("123456");
        Assert.True(builder.IsTruncated);
        Assert.Equal(250, builder.Length);

        // Try to append something that would fit in remaining space
        builder.Append("x");

        // Should still be ignored
        Assert.Equal(250, builder.Length);
        Assert.DoesNotContain("x", builder.ToString());
    }

    [Fact]
    public void Append_ExactlyMaxLength_DoesNotTruncate()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append(new string('a', 255));

        Assert.Equal(255, builder.Length);
        Assert.False(builder.IsTruncated);
    }

    [Fact]
    public void Append_OneCharOverMaxLength_Truncates()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append(new string('a', 255));
        builder.Append('b');

        Assert.Equal(255, builder.Length);
        Assert.True(builder.IsTruncated);
        Assert.DoesNotContain("b", builder.ToString());
    }

    [Fact]
    public void TrimEnd_RemovesTrailingWhitespace()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append("hello   ");
        builder.TrimEnd();

        Assert.Equal("hello", builder.ToString());
        Assert.Equal(5, builder.Length);
    }

    [Fact]
    public void TrimEnd_EmptyBuilder_DoesNotThrow()
    {
        using var builder = new TruncatingStringBuilder();
        builder.TrimEnd();

        Assert.Equal(string.Empty, builder.ToString());
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void Append_NullString_DoesNothing()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append(null!);

        Assert.Equal(string.Empty, builder.ToString());
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void Append_EmptyString_DoesNothing()
    {
        using var builder = new TruncatingStringBuilder();
        builder.Append(string.Empty);

        Assert.Equal(string.Empty, builder.ToString());
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var builder = new TruncatingStringBuilder();
        builder.Append("test");
        builder.Dispose();
        builder.Dispose(); // Should not throw
    }

    [Fact]
    public void ToString_AfterDispose_ReturnsEmpty()
    {
        var builder = new TruncatingStringBuilder();
        builder.Append("test");
        builder.Dispose();

        Assert.Equal(string.Empty, builder.ToString());
    }
}
