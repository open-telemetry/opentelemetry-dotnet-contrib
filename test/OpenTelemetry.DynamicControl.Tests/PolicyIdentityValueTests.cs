// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyIdentityValueTests
{
    [Fact]
    public void PolicyType_WithValidValue_PreservesValue()
    {
        var policyType = new PolicyType("trace-sampling");

        Assert.Equal("trace-sampling", policyType.Value);
        Assert.False(policyType.IsEmpty);
        Assert.Equal("trace-sampling", policyType.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void PolicyType_WithBlankValue_Throws(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => _ = new PolicyType(value!));

    [Fact]
    public void PolicyType_Default_IsEmpty()
    {
        var policyType = default(PolicyType);

        Assert.True(policyType.IsEmpty);
        Assert.Equal(string.Empty, policyType.Value);
        Assert.Equal(PolicyType.Empty, policyType);
    }

    [Fact]
    public void PolicyType_UsesOrdinalValueSemantics()
    {
        var first = new PolicyType("trace-sampling");
        var equal = new PolicyType("trace-sampling");
        var differentCase = new PolicyType("Trace-Sampling");

        Assert.True(first == equal);
        Assert.False(first != equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, differentCase);
        Assert.True(first.CompareTo(differentCase) > 0);
    }

    [Fact]
    public void PolicyId_WithValidValue_PreservesValue()
    {
        var policyId = new PolicyId("policy-1");

        Assert.Equal("policy-1", policyId.Value);
        Assert.False(policyId.IsEmpty);
        Assert.Equal("policy-1", policyId.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void PolicyId_WithBlankValue_Throws(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => _ = new PolicyId(value!));

    [Fact]
    public void PolicyId_Default_IsEmpty()
    {
        var policyId = default(PolicyId);

        Assert.True(policyId.IsEmpty);
        Assert.Equal(string.Empty, policyId.Value);
        Assert.Equal(PolicyId.Empty, policyId);
    }

    [Fact]
    public void PolicyId_UsesOrdinalValueSemantics()
    {
        var first = new PolicyId("policy-1");
        var equal = new PolicyId("policy-1");
        var differentCase = new PolicyId("Policy-1");

        Assert.True(first == equal);
        Assert.False(first != equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, differentCase);
        Assert.True(first.CompareTo(differentCase) > 0);
    }
}
