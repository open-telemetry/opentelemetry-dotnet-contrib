// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using OpenTelemetry.DynamicControl.Internal.Aggregation;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using static OpenTelemetry.DynamicControl.Tests.PolicyAggregationTestHelper;

namespace OpenTelemetry.DynamicControl.Tests;

public class ResolvedPolicySnapshotTests
{
    [Fact]
    public void Empty_HasStoreRevisionZeroAndNoPolicies()
    {
        var empty = ResolvedPolicySnapshot.Empty;

        Assert.Equal(0, empty.StoreRevision);
        Assert.Empty(empty.Policies);
        Assert.True(empty.IsEmpty, "The empty snapshot must report itself as empty.");
    }

    [Fact]
    public void Empty_IsSameReference() => Assert.Same(ResolvedPolicySnapshot.Empty, ResolvedPolicySnapshot.Empty);

    [Fact]
    public void Empty_Policies_IsReadOnly()
    {
        var asList = Assert.IsType<IList<ResolvedPolicy>>(ResolvedPolicySnapshot.Empty.Policies, exactMatch: false);

        Assert.True(asList.IsReadOnly, "The empty snapshot's Policies must also be read-only.");
    }

    [Fact]
    public void Policies_UnsortedInput_IsOrderedByPolicyKey()
    {
        var snapshot = new ResolvedPolicySnapshot(
            storeRevision: 7,
            [
                Resolved("trace-sampling", "trace-sampling"),
                Resolved("log-level", "log-level"),
                Resolved("log-level", "audit"),
            ]);

        Assert.Equal(7, snapshot.StoreRevision);
        Assert.Equal(
            ["log-level/audit", "log-level/log-level", "trace-sampling/trace-sampling"],
            snapshot.Policies.Select(static policy => policy.Key.ToString()));
    }

    [Fact]
    public void Policies_CannotBeMutatedThroughIListCast()
    {
        var snapshot = new ResolvedPolicySnapshot(
            storeRevision: 1,
            [Resolved("log-level", "log-level"), Resolved("trace-sampling", "trace-sampling")]);

        var first = snapshot.Policies[0];

        var asList = Assert.IsType<IList<ResolvedPolicy>>(snapshot.Policies, exactMatch: false);

        Assert.True(asList.IsReadOnly, "Policies must be read-only.");
        Assert.Throws<NotSupportedException>(() => { asList[0] = snapshot.Policies[1]; });
        Assert.Throws<NotSupportedException>(() => asList.RemoveAt(0));
        Assert.Throws<NotSupportedException>(asList.Clear);
        Assert.Same(first, snapshot.Policies[0]);
    }

    [Fact]
    public void TryGetPolicy_Hit_ReturnsTrueAndTheResolvedPolicy()
    {
        var resolved = Resolved("trace-sampling", "trace-sampling");
        var snapshot = new ResolvedPolicySnapshot(storeRevision: 1, [resolved]);

        var found = snapshot.TryGetPolicy(PolicyKeyTestHelper.Key("trace-sampling", "trace-sampling"), out var policy);

        Assert.True(found, "TryGetPolicy should find the resolved key.");
        Assert.Same(resolved, policy);
    }

    [Fact]
    public void TryGetPolicy_Miss_ReturnsFalseWithNullOut()
    {
        var snapshot = new ResolvedPolicySnapshot(storeRevision: 1, [Resolved("trace-sampling", "trace-sampling")]);

        var found = snapshot.TryGetPolicy(PolicyKeyTestHelper.Key("log-level", "log-level"), out var policy);

        Assert.False(found, "TryGetPolicy should return false for an unresolved key.");
        Assert.Null(policy);
    }

    [Fact]
    public void ResolvedPolicy_ToString_ReportsKeyAndWinningProvider()
    {
        var resolved = Resolved("trace-sampling", "trace-sampling", "opamp-provider");

        Assert.Equal("trace-sampling/trace-sampling/opamp-provider", resolved.ToString());
    }

    [Fact]
    public void OutrankedPolicy_ToString_ReportsKeyProviderAndOutcome()
    {
        var outranked = new OutrankedPolicy(
            new ProviderRegistrationId("file-provider"),
            Policy("log-level", "log-level"),
            PolicyAggregationReason.Superseded);

        Assert.Equal("log-level/log-level/file-provider/Superseded", outranked.ToString());
    }

    private static ResolvedPolicy Resolved(string policyType, string policyId, string winningProvider = "p-1")
    {
        var policy = Policy(policyType, policyId);

        return new ResolvedPolicy(
            PolicyKey.FromPolicy(policy),
            policy,
            new ProviderRegistrationId(winningProvider),
            ImmutableArray<OutrankedPolicy>.Empty);
    }
}
