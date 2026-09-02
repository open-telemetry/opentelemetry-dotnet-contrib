// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyProviderMetadataTests
{
    [Fact]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        var registrationId = new ProviderRegistrationId("opamp-1");

        var metadata = new PolicyProviderMetadata(registrationId, PolicyProviderKind.OpAmp, 10);

        Assert.Equal(registrationId, metadata.RegistrationId);
        Assert.Equal(PolicyProviderKind.OpAmp, metadata.Kind);
        Assert.Equal(10, metadata.Priority);
    }

    [Theory]
    [InlineData((int)PolicyProviderKind.OpAmp, 1)]
    [InlineData((int)PolicyProviderKind.Http, 2)]
    [InlineData((int)PolicyProviderKind.File, 3)]
    [InlineData((int)PolicyProviderKind.Custom, 1000)]
    public void Constructor_WithoutPriority_UsesKindDerivedDefault(int kindValue, int expectedPriority)
    {
        var kind = (PolicyProviderKind)kindValue;
        var metadata = new PolicyProviderMetadata(new ProviderRegistrationId("provider-1"), kind);

        Assert.Equal(expectedPriority, metadata.Priority);
    }

    [Fact]
    public void Constructor_WithDefaultRegistrationId_Throws() =>
        Assert.Throws<ArgumentException>(
            "registrationId",
            () => _ = new PolicyProviderMetadata(ProviderRegistrationId.Empty, PolicyProviderKind.OpAmp));

    [Theory]
    [InlineData(0)] // The zero/default sentinel
    [InlineData(99)] // undefined integer cast
    public void Constructor_WithInvalidKind_Throws(int kindValue) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            "kind",
            () => _ = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), (PolicyProviderKind)kindValue));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_WithNonNegativePriority_IsAllowed(int priority)
    {
        var metadata = new PolicyProviderMetadata(new ProviderRegistrationId("file-1"), PolicyProviderKind.File, priority);

        Assert.Equal(priority, metadata.Priority);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WithNegativePriority_Throws(int priority) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            nameof(priority),
            () => _ = new PolicyProviderMetadata(new ProviderRegistrationId("file-1"), PolicyProviderKind.File, priority));

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var left = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp, 10);
        var right = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp, 10);

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
        var left = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp, 10);
        var right = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-2"), PolicyProviderKind.OpAmp, 10);

        Assert.False(left.Equals(right), "Equals should be false");
        Assert.True(left != right, "Not equals (!=) operator should be true");
    }

    [Fact]
    public void Equals_WithDifferentKind_ReturnsFalse()
    {
        var left = new PolicyProviderMetadata(new ProviderRegistrationId("provider-1"), PolicyProviderKind.OpAmp, 10);
        var right = new PolicyProviderMetadata(new ProviderRegistrationId("provider-1"), PolicyProviderKind.File, 10);

        Assert.False(left.Equals(right), "Equals should be false");
    }

    [Fact]
    public void Equals_WithDifferentPriority_ReturnsFalse()
    {
        var left = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp, 10);
        var right = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp, 20);

        Assert.False(left.Equals(right), "Equals should be false");
    }

    [Fact]
    public void Equals_WithOtherType_ReturnsFalse()
    {
        var metadata = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp);

        Assert.False(metadata.Equals(metadata.RegistrationId), "Should not be equal to a ProviderRegistrationId");
        Assert.False(metadata.Equals(null), "Should not be equal to null");
    }

    [Fact]
    public void Priority_DefaultedOpAmpOutranksDefaultedFile()
    {
        // Per the Telemetry Policy OTEP, a defaulted OpAmp provider (priority 1) outranks a
        // defaulted File provider (priority 3) without either caller specifying a priority
        // explicitly. This is the conformance test for kind-derived defaults.
        var opAmp = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp);
        var file = new PolicyProviderMetadata(new ProviderRegistrationId("file-1"), PolicyProviderKind.File);

        Assert.True(opAmp.Priority < file.Priority, "A defaulted OpAmp provider should have a lower (higher-precedence) priority value than a defaulted File provider");
    }

    [Fact]
    public void Constructor_ExplicitPriorityEqualToKindDerivedDefault_EqualsDefaulted()
    {
        // OpAmp's kind-derived default is 1. An explicit priority=1 and the defaulted
        // form must compare equal, because they express the same thing.
        var explicit1 = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp, 1);
        var defaulted = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp);

        Assert.Equal(explicit1, defaulted);
        Assert.Equal(explicit1.GetHashCode(), defaulted.GetHashCode());
    }

    [Fact]
    public void Default_HasEmptyRegistrationIdUnknownKindAndZeroPriority()
    {
        var metadata = default(PolicyProviderMetadata);

        Assert.Equal(default, metadata.RegistrationId);
        Assert.Equal(ProviderRegistrationId.Empty, metadata.RegistrationId);
        Assert.Equal(PolicyProviderKind.Unknown, metadata.Kind);
        Assert.Equal(0, metadata.Priority);
        Assert.Equal(default(PolicyProviderMetadata).GetHashCode(), metadata.GetHashCode());
        Assert.NotEqual(
            new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp),
            metadata);
    }

    [Fact]
    public void ToString_ReturnsRegistrationIdKindAndPriority()
    {
        var metadata = new PolicyProviderMetadata(new ProviderRegistrationId("opamp-1"), PolicyProviderKind.OpAmp, 10);

        Assert.Equal("opamp-1/OpAmp/10", metadata.ToString());
    }
}
