// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicySourceMetadataTests
{
    [Fact]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        var registrationId = new SourceRegistrationId("opamp-1");

        var metadata = new PolicySourceMetadata(registrationId, PolicySourceKind.OpAmp, 10);

        Assert.Equal(registrationId, metadata.RegistrationId);
        Assert.Equal(PolicySourceKind.OpAmp, metadata.Kind);
        Assert.Equal(10, metadata.Priority);
    }

    [Fact]
    public void Constructor_WithoutPriority_UsesDefaultPriority()
    {
        var metadata = new PolicySourceMetadata(new SourceRegistrationId("file-1"), PolicySourceKind.File);

        Assert.Equal(int.MaxValue, metadata.Priority);
    }

    [Fact]
    public void Constructor_WithDefaultRegistrationId_Throws() =>
        Assert.Throws<ArgumentException>(
            "registrationId",
            () => _ = new PolicySourceMetadata(SourceRegistrationId.Empty, PolicySourceKind.OpAmp));

    [Theory]
    [InlineData(0)] // The zero/default sentinel
    [InlineData(99)] // undefined integer cast
    public void Constructor_WithInvalidKind_Throws(int kindValue) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            "kind",
            () => _ = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), (PolicySourceKind)kindValue));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_WithNonNegativePriority_IsAllowed(int priority)
    {
        var metadata = new PolicySourceMetadata(new SourceRegistrationId("file-1"), PolicySourceKind.File, priority);

        Assert.Equal(priority, metadata.Priority);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WithNegativePriority_Throws(int priority) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            nameof(priority),
            () => _ = new PolicySourceMetadata(new SourceRegistrationId("file-1"), PolicySourceKind.File, priority));

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var left = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp, 10);
        var right = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp, 10);

        Assert.True(left.Equals(right), "Typed Equals should be true");
        Assert.True(left.Equals((object)right), "Object Equals should be true");
        Assert.True(left == right, "Equals (==) operator should be true");
        Assert.True(right == left, "Equals (==) operator should be true for swapped operands");
        Assert.False(left != right, "Not equals (!=) operator should be false");
        Assert.False(right != left, "Not equals (!=) operator should be false for swapped operands");
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentRegistrationId_ReturnsFalse()
    {
        var left = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp, 10);
        var right = new PolicySourceMetadata(new SourceRegistrationId("opamp-2"), PolicySourceKind.OpAmp, 10);

        Assert.False(left.Equals(right), "Equals should be false");
        Assert.True(left != right, "Not equals (!=) operator should be true");
    }

    [Fact]
    public void Equals_WithDifferentKind_ReturnsFalse()
    {
        var left = new PolicySourceMetadata(new SourceRegistrationId("source-1"), PolicySourceKind.OpAmp, 10);
        var right = new PolicySourceMetadata(new SourceRegistrationId("source-1"), PolicySourceKind.File, 10);

        Assert.False(left.Equals(right), "Equals should be false");
    }

    [Fact]
    public void Equals_WithDifferentPriority_ReturnsFalse()
    {
        var left = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp, 10);
        var right = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp, 20);

        Assert.False(left.Equals(right), "Equals should be false");
    }

    [Fact]
    public void Equals_WithOtherType_ReturnsFalse()
    {
        var metadata = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp);

        Assert.False(metadata.Equals(metadata.RegistrationId), "Should not be equal to a SourceRegistrationId");
        Assert.False(metadata.Equals(null), "Should not be equal to null");
    }

    [Fact]
    public void Priority_IsLowerForExplicitlyPrioritizedSource()
    {
        // Per the Telemetry Policy OTEP's provider-priority convention, a lower value wins
        // (e.g. OpAmp=1 outranks a File source using the default, unprioritized value).
        // Aggregation rules are defined with aggregation itself; this only pins the
        // direction of the ordering that metadata expresses.
        var opAmp = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp, 1);
        var file = new PolicySourceMetadata(new SourceRegistrationId("file-1"), PolicySourceKind.File);

        Assert.True(opAmp.Priority < file.Priority, "OpAmp source should have a lower (higher-precedence) priority value than the default-priority File source");
    }

    [Fact]
    public void Default_HasEmptyRegistrationIdUnknownKindAndZeroPriority()
    {
        var metadata = default(PolicySourceMetadata);

        Assert.Equal(default, metadata.RegistrationId);
        Assert.Equal(SourceRegistrationId.Empty, metadata.RegistrationId);
        Assert.Equal(PolicySourceKind.Unknown, metadata.Kind);
        Assert.Equal(0, metadata.Priority);
        Assert.Equal(default(PolicySourceMetadata).GetHashCode(), metadata.GetHashCode());
        Assert.NotEqual(
            new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp),
            metadata);
    }

    [Fact]
    public void ToString_ReturnsRegistrationIdKindAndPriority()
    {
        var metadata = new PolicySourceMetadata(new SourceRegistrationId("opamp-1"), PolicySourceKind.OpAmp, 10);

        Assert.Equal("opamp-1/OpAmp/10", metadata.ToString());
    }
}
