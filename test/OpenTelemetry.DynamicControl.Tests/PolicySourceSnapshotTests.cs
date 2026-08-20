// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Sources;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicySourceSnapshotTests
{
    private static readonly PolicySourceMetadata DefaultMetadata =
        new(new SourceRegistrationId("source-1"), PolicySourceKind.File);

    [Fact]
    public void TryCreate_ZeroPolicies_SucceedsAndIsEmpty()
    {
        var result = TryCreate(DefaultMetadata, 1, [], out var snapshot, out _);

        Assert.True(result, "TryCreate with zero policies should succeed");
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsEmpty, "snapshot should be empty with zero policies");
        Assert.Empty(snapshot.Policies);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void TryCreate_InvalidSequence_ReturnsFalse(long sequence)
    {
        var result = TryCreate(DefaultMetadata, sequence, [], out var snapshot, out var error);

        Assert.False(result, "TryCreate should fail with an invalid sequence");
        Assert.Null(snapshot);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_DefaultMetadata_ReturnsFalse()
    {
        var result = TryCreate(default, 1, [], out var snapshot, out var error);

        Assert.False(result, "TryCreate should fail with default metadata");
        Assert.Null(snapshot);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_NullPoliciesList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PolicySourceSnapshot.TryCreate(DefaultMetadata, 1, PolicySourceVersion.Empty, null!, out _, out _));
    }

    [Fact]
    public void TryCreate_NullElement_ReturnsFalse()
    {
        var result = TryCreate(DefaultMetadata, 1, [null!], out var snapshot, out var error);

        Assert.False(result, "TryCreate should fail when the list contains a null element");
        Assert.Null(snapshot);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_BlankPolicyType_ReturnsFalse(string blankType)
    {
        var result = TryCreate(DefaultMetadata, 1, [new StubPolicy(blankType, "id-1")], out var snapshot, out var error);

        Assert.False(result, "TryCreate should fail with a blank PolicyType");
        Assert.Null(snapshot);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_BlankPolicyId_ReturnsFalse(string blankId)
    {
        var result = TryCreate(DefaultMetadata, 1, [new StubPolicy("type", blankId)], out var snapshot, out var error);

        Assert.False(result, "TryCreate should fail with a blank policy Id");
        Assert.Null(snapshot);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_DuplicatePolicyKey_ReturnsFalseWithKeyInError()
    {
        var policy1 = new StubPolicy("trace-sampling", "policy-1");
        var policy2 = new StubPolicy("trace-sampling", "policy-1");

        var result = TryCreate(DefaultMetadata, 1, [policy1, policy2], out var snapshot, out var error);

        Assert.False(result, "TryCreate should fail when a duplicate policy key is present");
        Assert.Null(snapshot);
        Assert.NotNull(error);
        Assert.Contains("trace-sampling/policy-1", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_SameIdDifferentType_Succeeds()
    {
        // Same Id but different PolicyType is NOT a duplicate. The key is composite.
        var policy1 = new StubPolicy("trace-sampling", "policy-1");
        var policy2 = new StubPolicy("metrics-sampling", "policy-1");

        var result = TryCreate(DefaultMetadata, 1, [policy1, policy2], out var snapshot, out _);

        Assert.True(result, "Same Id but different PolicyType is not a duplicate. TryCreate should succeed");
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Policies.Length);
    }

    [Fact]
    public void TryCreate_PolicySourceVersionEmpty_Succeeds()
    {
        var result = PolicySourceSnapshot.TryCreate(
            DefaultMetadata, 1, PolicySourceVersion.Empty, [], out var snapshot, out _);

        Assert.True(result, "TryCreate with PolicySourceVersion.Empty should succeed");
        Assert.NotNull(snapshot);
        Assert.True(snapshot.Version.IsEmpty, "Snapshot version should be empty when PolicySourceVersion.Empty was passed");
    }

    [Fact]
    public void TryCreate_DefensiveCopy_CallerMutationDoesNotAffectSnapshot()
    {
        var policy = new StubPolicy("trace-sampling", "policy-1");
        var list = new List<TelemetryPolicy> { policy };

        TryCreate(DefaultMetadata, 1, list, out var snapshot, out _);

        list.Add(new StubPolicy("trace-sampling", "policy-2"));

        Assert.Single(snapshot!.Policies);
    }

    [Fact]
    public void TryCreate_PoliciesInShuffledOrder_SortedByPolicyKeyComparer()
    {
        var p1 = new StubPolicy("b-type", "a-id");
        var p2 = new StubPolicy("a-type", "z-id");
        var p3 = new StubPolicy("a-type", "a-id");

        TryCreate(DefaultMetadata, 1, [p1, p2, p3], out var snapshot, out _);

        Assert.NotNull(snapshot);
        Assert.Equal("a-type", snapshot.Policies[0].PolicyType);
        Assert.Equal("a-id", snapshot.Policies[0].Id);
        Assert.Equal("a-type", snapshot.Policies[1].PolicyType);
        Assert.Equal("z-id", snapshot.Policies[1].Id);
        Assert.Equal("b-type", snapshot.Policies[2].PolicyType);
        Assert.Equal("a-id", snapshot.Policies[2].Id);
    }

    [Fact]
    public void TryCreate_DifferentInputOrders_ProduceIdenticalPoliciesSequence()
    {
        var p1 = new StubPolicy("b-type", "id");
        var p2 = new StubPolicy("a-type", "id");

        TryCreate(DefaultMetadata, 1, [p1, p2], out var snap1, out _);
        TryCreate(DefaultMetadata, 2, [p2, p1], out var snap2, out _);

        Assert.NotNull(snap1);
        Assert.NotNull(snap2);
        Assert.Equal(snap1.Policies.Length, snap2.Policies.Length);
        for (var i = 0; i < snap1.Policies.Length; i++)
        {
            Assert.Equal(snap1.Policies[i].PolicyType, snap2.Policies[i].PolicyType);
            Assert.Equal(snap1.Policies[i].Id, snap2.Policies[i].Id);
        }
    }

    [Fact]
    public void TryGetPolicy_Hit_ReturnsTrueAndPolicy()
    {
        var policy = new StubPolicy("trace-sampling", "policy-1");
        TryCreate(DefaultMetadata, 1, [policy], out var snapshot, out _);

        var found = snapshot!.TryGetPolicy(new PolicyKey("trace-sampling", "policy-1"), out var result);

        Assert.True(found, "TryGetPolicy should find a policy that was created");
        Assert.Same(policy, result);
    }

    [Fact]
    public void TryGetPolicy_Miss_ReturnsFalseWithNullOut()
    {
        TryCreate(DefaultMetadata, 1, [], out var snapshot, out _);

        var found = snapshot!.TryGetPolicy(new PolicyKey("trace-sampling", "missing"), out var result);

        Assert.False(found, "TryGetPolicy should return false for a missing policy key");
        Assert.Null(result);
    }

    [Fact]
    public void RegistrationId_MatchesMetadataRegistrationId()
    {
        var id = new SourceRegistrationId("my-source");
        var metadata = new PolicySourceMetadata(id, PolicySourceKind.OpAmp);
        TryCreate(metadata, 1, [], out var snapshot, out _);

        Assert.Equal(id, snapshot!.RegistrationId);
        Assert.Equal(metadata.RegistrationId, snapshot.RegistrationId);
    }

    [Fact]
    public void Policies_CannotBeMutatedThroughIListCast()
    {
        var policy = new StubPolicy("trace-sampling", "a");
        TryCreate(DefaultMetadata, 1, [policy], out var snapshot, out _);

        var asList = Assert.IsAssignableFrom<IList<TelemetryPolicy>>(snapshot!.Policies);

        Assert.True(asList.IsReadOnly, "Policies list must be read-only");
        Assert.Throws<NotSupportedException>(() => { asList[0] = new StubPolicy("trace-sampling", "b"); });
        Assert.Throws<NotSupportedException>(() => asList.Add(new StubPolicy("trace-sampling", "c")));
        Assert.Throws<NotSupportedException>(() => asList.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => asList.Clear());
        Assert.Same(policy, snapshot.Policies[0]);
    }

    [Fact]
    public void Policies_EmptySnapshot_IsAlsoReadOnly()
    {
        TryCreate(DefaultMetadata, 1, [], out var snapshot, out _);

        var asList = Assert.IsAssignableFrom<IList<TelemetryPolicy>>(snapshot!.Policies);
        Assert.True(asList.IsReadOnly, "empty snapshot's Policies must also be read-only");
    }

    private static bool TryCreate(
        PolicySourceMetadata metadata,
        long sequence,
        IReadOnlyList<TelemetryPolicy?> policies,
        out PolicySourceSnapshot? snapshot,
        out string? error)
        => PolicySourceSnapshot.TryCreate(metadata, sequence, PolicySourceVersion.Empty, policies, out snapshot, out error);

    private sealed class StubPolicy : TelemetryPolicy
    {
        public StubPolicy(string policyType, string id)
            : base(id, $"{policyType}/{id}")
        {
            this.PolicyType = policyType;
        }

        public override string PolicyType { get; }
    }
}
