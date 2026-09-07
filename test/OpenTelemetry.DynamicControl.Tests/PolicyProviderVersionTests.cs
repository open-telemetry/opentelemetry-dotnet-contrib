// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyProviderVersionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Constructor_WithNullEmptyOrWhitespace_Throws(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => _ = new PolicyProviderVersion(value!));

    [Fact]
    public void Empty_IsEmpty_True() => Assert.True(PolicyProviderVersion.Empty.IsEmpty, "Empty.IsEmpty should be true");

    [Fact]
    public void Empty_Value_ReturnsEmptyString() => Assert.Equal(string.Empty, PolicyProviderVersion.Empty.Value);

    [Fact]
    public void Empty_IsDefaultStruct() => Assert.Equal(default, PolicyProviderVersion.Empty);

    [Fact]
    public void Constructor_IsEmpty_False()
    {
        var version = new PolicyProviderVersion("abc");
        Assert.False(version.IsEmpty, "a constructed version should not be empty");
    }

    [Fact]
    public void Constructor_Value_ReturnsSuppliedValue()
    {
        var version = new PolicyProviderVersion("etag-123");
        Assert.Equal("etag-123", version.Value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("hash-xyz-456")]
    public void EqualValues_AreEqual_WithSameHashCode(string raw)
    {
        var x = new PolicyProviderVersion(raw);
        var y = new PolicyProviderVersion(raw);

        Assert.True(x.Equals(y), "Typed Equals should be true");
        Assert.True(x.Equals((object)y), "Object Equals should be true");
        Assert.True(x == y, "== operator should be true");
        Assert.False(x != y, "!= operator should be false");
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void DifferentCase_NotEqual_OrdinalCaseSensitive()
    {
        var lower = new PolicyProviderVersion("abc");
        var upper = new PolicyProviderVersion("ABC");

        Assert.False(lower.Equals(upper), "'abc' and 'ABC' should not be equal");
        Assert.True(lower != upper, "!= operator should be true");
    }

    [Fact]
    public void Empty_NotEqualToConstructed()
    {
        var version = new PolicyProviderVersion("anything");
        Assert.False(PolicyProviderVersion.Empty.Equals(version), "Empty should not equal a constructed version");
        Assert.False(version.Equals(PolicyProviderVersion.Empty), "a constructed version should not equal Empty");
        Assert.True(version != PolicyProviderVersion.Empty, "!= operator should be true for non-equal versions");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var version = new PolicyProviderVersion("etag-abc");
        Assert.Equal("etag-abc", version.ToString());
    }

    [Fact]
    public void Empty_ToString_ReturnsEmptyString() => Assert.Equal(string.Empty, PolicyProviderVersion.Empty.ToString());
}
