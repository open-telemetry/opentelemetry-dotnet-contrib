// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicySourceVersionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Constructor_WithNullEmptyOrWhitespace_Throws(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => _ = new PolicySourceVersion(value!));

    [Fact]
    public void Empty_IsEmpty_True() => Assert.True(PolicySourceVersion.Empty.IsEmpty, "Empty.IsEmpty should be true");

    [Fact]
    public void Empty_Value_ReturnsEmptyString() => Assert.Equal(string.Empty, PolicySourceVersion.Empty.Value);

    [Fact]
    public void Empty_IsDefaultStruct() => Assert.Equal(default, PolicySourceVersion.Empty);

    [Fact]
    public void Constructor_IsEmpty_False()
    {
        var version = new PolicySourceVersion("abc");
        Assert.False(version.IsEmpty, "a constructed version should not be empty");
    }

    [Fact]
    public void Constructor_Value_ReturnsSuppliedValue()
    {
        var version = new PolicySourceVersion("etag-123");
        Assert.Equal("etag-123", version.Value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("hash-xyz-456")]
    public void EqualValues_AreEqual_WithSameHashCode(string raw)
    {
        var x = new PolicySourceVersion(raw);
        var y = new PolicySourceVersion(raw);

        Assert.True(x.Equals(y), "Typed Equals should be true");
        Assert.True(x.Equals((object)y), "Object Equals should be true");
        Assert.True(x == y, "== operator should be true");
        Assert.False(x != y, "!= operator should be false");
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void DifferentCase_NotEqual_OrdinalCaseSensitive()
    {
        var lower = new PolicySourceVersion("abc");
        var upper = new PolicySourceVersion("ABC");

        Assert.False(lower.Equals(upper), "'abc' and 'ABC' should not be equal");
        Assert.True(lower != upper, "!= operator should be true");
    }

    [Fact]
    public void Empty_NotEqualToConstructed()
    {
        var version = new PolicySourceVersion("anything");
        Assert.False(PolicySourceVersion.Empty.Equals(version), "Empty should not equal a constructed version");
        Assert.False(version.Equals(PolicySourceVersion.Empty), "a constructed version should not equal Empty");
        Assert.True(version != PolicySourceVersion.Empty, "!= operator should be true for non-equal versions");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var version = new PolicySourceVersion("etag-abc");
        Assert.Equal("etag-abc", version.ToString());
    }

    [Fact]
    public void Empty_ToString_ReturnsEmptyString() => Assert.Equal(string.Empty, PolicySourceVersion.Empty.ToString());
}
