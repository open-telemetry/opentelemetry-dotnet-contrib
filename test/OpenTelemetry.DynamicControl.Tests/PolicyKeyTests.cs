// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using static OpenTelemetry.DynamicControl.Tests.PolicyKeyTestHelper;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyKeyTests
{
    [Fact]
    public void Constructor_WithValidComponents_SetsComponents()
    {
        var key = Key("trace-sampling", "policy-id");

        Assert.Equal(Type("trace-sampling"), key.PolicyType);
        Assert.Equal(Id("policy-id"), key.PolicyId);
    }

    [Fact]
    public void Constructor_WithEmptyPolicyType_Throws() =>
        Assert.Throws<ArgumentException>("policyType", () => _ = new PolicyKey(PolicyType.Empty, Id("policy-id")));

    [Fact]
    public void Constructor_WithEmptyPolicyId_Throws() =>
        Assert.Throws<ArgumentException>("policyId", () => _ = new PolicyKey(Type("trace-sampling"), PolicyId.Empty));

    [Fact]
    public void Equals_WithSameComponents_ReturnsTrue()
    {
        var left = Key("trace-sampling", "policy-id");
        var right = Key("trace-sampling", "policy-id");

        Assert.True(left.Equals(right), "Typed Equals should be true");
        Assert.True(left.Equals((object)right), "Object Equals should be true");
        Assert.True(left == right, "== operator should be true");
        Assert.True(right == left, "== operator should be symmetric");
        Assert.False(left != right, "!= operator should be false");
        Assert.False(right != left, "!= operator should be symmetric");
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_WithSamePolicyIdButDifferentPolicyType_ReturnsFalse()
    {
        // Identity is the composite of both components, so the same identifier used by
        // two policy types occupies two distinct slots.
        var left = Key("trace-sampling", "policy-id");
        var right = Key("log-level", "policy-id");

        Assert.False(left.Equals(right), "Equals should be false");
        Assert.True(left != right, "Not equals (!=) operator should be true");
    }

    [Fact]
    public void Equals_WithSamePolicyTypeButDifferentPolicyId_ReturnsFalse()
    {
        var left = Key("trace-sampling", "policy-id");
        var right = Key("trace-sampling", "other-policy-id");

        Assert.False(left.Equals(right), "Equals should be false");
        Assert.True(left != right, "Not equals (!=) operator should be true");
    }

    [Fact]
    public void Equals_WithComponentsSwapped_ReturnsFalse()
    {
        var left = Key("a", "b");
        var right = Key("b", "a");

        Assert.False(left.Equals(right), "Equals should be false");
    }

    [Fact]
    public void Equals_WithComponentsDifferingOnlyByCase_ReturnsFalse()
    {
        // Both components are compared ordinally; policy identity is not case-insensitive.
        var left = Key("trace-sampling", "policy-id");
        var right = Key("Trace-Sampling", "Policy-Id");

        Assert.False(left.Equals(right), "Equals should be false");
    }

    [Fact]
    public void Equals_WithOtherType_ReturnsFalse()
    {
        var key = Key("trace-sampling", "policy-id");

        Assert.False(key.Equals("trace-sampling/policy-id"), "Should not be equal to a string");
        Assert.False(key.Equals(null), "Should not be equal to null");
    }

    [Fact]
    public void Default_HasEmptyComponentsAndIsUsableWithoutThrowing()
    {
        var key = default(PolicyKey);

        Assert.Equal(PolicyType.Empty, key.PolicyType);
        Assert.Equal(PolicyId.Empty, key.PolicyId);
        Assert.Equal(default, key);
        Assert.Equal(PolicyKey.Empty, key);
        Assert.Equal(default(PolicyKey).GetHashCode(), key.GetHashCode());
        Assert.NotEqual(Key("trace-sampling", "policy-id"), key);
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var policies = new Dictionary<PolicyKey, string>
        {
            [Key("trace-sampling", "policy-id")] = "first",
            [Key("log-level", "policy-id")] = "second",
        };

        // Overwrites rather than adds; an equal key must resolve to the same slot.
        policies[Key("trace-sampling", "policy-id")] = "replacement";

        Assert.Equal(2, policies.Count);
        Assert.Equal("replacement", policies[Key("trace-sampling", "policy-id")]);
        Assert.Equal("second", policies[Key("log-level", "policy-id")]);
    }

    [Fact]
    public void ToString_ReturnsPolicyTypeAndPolicyId()
    {
        var key = Key("trace-sampling", "policy-id");

        Assert.Equal("trace-sampling/policy-id", key.ToString());
    }

    [Fact]
    public void FromPolicy_WithValidatedPolicy_ReturnsCompositeKey()
    {
        var policy = CreatePolicy("policy-id");

        var key = PolicyKey.FromPolicy(policy);

        Assert.Equal(TraceSamplingRatePolicy.PolicyTypeValue, key.PolicyType);
        Assert.Equal(Id("policy-id"), key.PolicyId);
    }

    [Fact]
    public void FromPolicy_WithNullPolicy_Throws() =>
        Assert.Throws<ArgumentNullException>("policy", () => _ = PolicyKey.FromPolicy(null!));

    [Fact]
    public void FromPolicy_WithSameIdAcrossPolicyTypes_ReturnsDistinctKeys()
    {
        // The reason identity is composite: one provider may reuse an identifier for
        // policies of different types.
        TelemetryPolicy samplingPolicy = CreatePolicy("shared-id");
        TelemetryPolicy otherPolicy = new StubPolicy(Id("shared-id"), "Policy name", Type("log-level"));

        var samplingKey = PolicyKey.FromPolicy(samplingPolicy);
        var otherKey = PolicyKey.FromPolicy(otherPolicy);

        Assert.NotEqual(samplingKey, otherKey);
        Assert.Equal(samplingKey.PolicyId, otherKey.PolicyId);
    }

    [Fact]
    public void FromPolicy_IgnoresPolicyContent()
    {
        // A source replacing a policy value must land on the same slot, so the key must
        // not vary with the policy's content.
        var original = CreatePolicy("policy-id", 0.25);
        var updated = CreatePolicy("policy-id", 0.75);

        Assert.Equal(PolicyKey.FromPolicy(original), PolicyKey.FromPolicy(updated));

        var policies = new Dictionary<PolicyKey, TelemetryPolicy>
        {
            [PolicyKey.FromPolicy(original)] = original,
        };

        policies[PolicyKey.FromPolicy(updated)] = updated;

        Assert.Single(policies);
        Assert.Same(updated, policies[PolicyKey.FromPolicy(original)]);
    }

    [Fact]
    public void FromPolicy_WithEmptyPolicyType_Throws()
    {
        TelemetryPolicy policy = new StubPolicy(Id("policy-id"), "Policy name", PolicyType.Empty);

        Assert.Throws<ArgumentException>(() => _ = PolicyKey.FromPolicy(policy));
    }

    private static TraceSamplingRatePolicy CreatePolicy(string id, double samplingProbability = 0.5)
    {
        Assert.True(TraceSamplingRatePolicy.TryCreate(
            Id(id),
            "Policy name",
            samplingProbability,
            out var policy,
            out _));

        return policy;
    }

    private sealed class StubPolicy : TelemetryPolicy
    {
        public StubPolicy(PolicyId id, string name, PolicyType policyType)
            : base(id, name)
        {
            this.PolicyType = policyType;
        }

        public override PolicyType PolicyType { get; }
    }
}
