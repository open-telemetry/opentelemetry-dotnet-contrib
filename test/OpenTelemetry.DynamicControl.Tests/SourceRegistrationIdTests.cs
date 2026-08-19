// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Tests;

public class SourceRegistrationIdTests
{
    [Fact]
    public void Constructor_WithValidValue_SetsValue()
    {
        var id = new SourceRegistrationId("opamp-1");

        Assert.Equal("opamp-1", id.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankValue_Throws(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => _ = new SourceRegistrationId(value!));

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        var left = new SourceRegistrationId("opamp-1");
        var right = new SourceRegistrationId("opamp-1");

        Assert.True(left.Equals(right), "Typed Equals should be true");
        Assert.True(left.Equals((object)right), "Object Equals should be true");
        Assert.True(left == right, "Equals (==) operator should be true");
        Assert.True(right == left, "Equals (==) operator should be true for swapped operands");
        Assert.False(left != right, "Not equals (!=) operator should be false");
        Assert.False(right != left, "Not equals (!=) operator should be false for swapped operands");
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        var left = new SourceRegistrationId("opamp-1");
        var right = new SourceRegistrationId("opamp-2");

        Assert.False(left.Equals(right), "Typed Equals should be false");
        Assert.True(left != right, "Not equals (!=) operator should be true");
        Assert.False(left == right, "Equals (==) operator should be false");
    }

    [Fact]
    public void Equals_WithValueDifferingOnlyByCase_ReturnsFalse()
    {
        var left = new SourceRegistrationId("opamp-1");
        var right = new SourceRegistrationId("OpAmp-1");

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_WithOtherType_ReturnsFalse()
    {
        var id = new SourceRegistrationId("opamp-1");

        Assert.False(id.Equals("opamp-1"), "Should not be equal to a string");
        Assert.False(id.Equals(null), "Should not be equal to null");
    }

    [Fact]
    public void Default_HasEmptyValueAndIsUsableWithoutThrowing()
    {
        var id = default(SourceRegistrationId);

        Assert.Equal(string.Empty, id.Value);
        Assert.Equal(default, id);
        Assert.Equal(SourceRegistrationId.None, id);
        Assert.Equal(default(SourceRegistrationId).GetHashCode(), id.GetHashCode());
        Assert.NotEqual(new SourceRegistrationId("opamp-1"), id);
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var snapshots = new Dictionary<SourceRegistrationId, string>
        {
            [new SourceRegistrationId("opamp-1")] = "first",
            [new SourceRegistrationId("opamp-2")] = "second",
        };

        snapshots[new SourceRegistrationId("opamp-1")] = "replacement";

        Assert.Equal(2, snapshots.Count);
        Assert.Equal("replacement", snapshots[new SourceRegistrationId("opamp-1")]);
        Assert.Equal("second", snapshots[new SourceRegistrationId("opamp-2")]);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var id = new SourceRegistrationId("opamp-1");
        Assert.Equal("opamp-1", id.ToString());
    }
}
