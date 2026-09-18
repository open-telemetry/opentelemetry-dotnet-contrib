// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Aggregation;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;
using static OpenTelemetry.DynamicControl.Tests.PolicyAggregationTestHelper;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyAggregatorTests
{
    private static readonly PolicyKey SamplingKey = new(
        TraceSamplingRatePolicy.PolicyTypeValue,
        new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value));

    [Fact]
    public void Aggregate_NullStoreSnapshot_Throws()
        => Assert.Throws<ArgumentNullException>("storeSnapshot", () => PolicyAggregator.Aggregate(null!));

    [Fact]
    public void Aggregate_EmptyStore_ReturnsSharedEmptyInstance()
    {
        var resolved = PolicyAggregator.Aggregate(PolicyStoreSnapshot.Empty);

        Assert.Same(ResolvedPolicySnapshot.Empty, resolved);
        Assert.True(resolved.IsEmpty, "An empty store resolves to no effective policies.");
        Assert.Equal(0, resolved.StoreRevision);
    }

    [Fact]
    public void Aggregate_ProvidersWithoutPolicies_IsEmptyAndPreservesRevision()
    {
        var storeSnapshot = Store(
            Provider("p-1", PolicyProviderKind.OpAmp),
            Provider("p-2", PolicyProviderKind.File));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        Assert.NotSame(ResolvedPolicySnapshot.Empty, resolved);
        Assert.True(resolved.IsEmpty, "Providers supplying nothing resolve to no effective policies.");
        Assert.Equal(2, resolved.StoreRevision);
    }

    [Fact]
    public void Aggregate_StoreEmptiedByRemovingEveryProvider_IsEmptyAndPreservesRevision()
    {
        using var store = new PolicyStore();
        store.ReplaceProvider(Provider("p-1", PolicyProviderKind.OpAmp, SamplingPolicy(0.5)));
        store.RemoveProvider(new ProviderRegistrationId("p-1"));

        var resolved = PolicyAggregator.Aggregate(store.Current);

        Assert.NotSame(ResolvedPolicySnapshot.Empty, resolved);
        Assert.True(resolved.IsEmpty, "A store with no providers resolves to no effective policies.");
        Assert.Equal(2, resolved.StoreRevision);
    }

    [Fact]
    public void Aggregate_SingleProviderSeveralKeys_AllUncontestedAndSortedByKey()
    {
        var storeSnapshot = Store(
            Provider(
                "p-1",
                PolicyProviderKind.File,
                Policy("trace-sampling", "trace-sampling"),
                Policy("log-level", "log-level"),
                Policy("log-level", "audit")));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        Assert.Equal(
            ["log-level/audit", "log-level/log-level", "trace-sampling/trace-sampling"],
            resolved.Policies.Select(static policy => policy.Key.ToString()));

        Assert.All(
            resolved.Policies,
            static policy =>
            {
                Assert.Empty(policy.OutrankedPolicies);
                Assert.Equal("p-1", policy.Provider.Value);
            });
    }

    [Fact]
    public void Aggregate_TwoProvidersWithDisjointKeys_ResolvesBothUncontested()
    {
        var storeSnapshot = Store(
            Provider("p-1", PolicyProviderKind.OpAmp, Policy("trace-sampling", "trace-sampling")),
            Provider("p-2", PolicyProviderKind.File, Policy("log-level", "log-level")));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        Assert.Equal(2, resolved.Policies.Length);
        Assert.All(resolved.Policies, static policy => Assert.Empty(policy.OutrankedPolicies));

        Assert.True(
            resolved.TryGetPolicy(PolicyKeyTestHelper.Key("log-level", "log-level"), out var logLevel),
            "The log-level key should resolve.");
        Assert.Equal("p-2", logLevel.Provider.Value);

        Assert.True(
            resolved.TryGetPolicy(PolicyKeyTestHelper.Key("trace-sampling", "trace-sampling"), out var sampling),
            "The trace-sampling key should resolve.");
        Assert.Equal("p-1", sampling.Provider.Value);
    }

    [Fact]
    public void Aggregate_SameKeyFromProvidersOfDifferentKinds_HigherPrecedenceKindWins()
    {
        var remote = SamplingPolicy(0.25);
        var local = SamplingPolicy(1.0);

        var storeSnapshot = Store(
            Provider("file-provider", PolicyProviderKind.File, local),
            Provider("opamp-provider", PolicyProviderKind.OpAmp, remote));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        var policy = Assert.Single(resolved.Policies);
        Assert.Same(remote, policy.Policy);
        Assert.Equal("opamp-provider", policy.Provider.Value);

        var outranked = Assert.Single(policy.OutrankedPolicies);
        Assert.Equal("file-provider", outranked.Provider.Value);
        Assert.Same(local, outranked.Policy);
        Assert.Equal(PolicyAggregationReason.Superseded, outranked.Reason);
    }

    [Fact]
    public void Aggregate_ExplicitPriority_TakesPrecedenceOverKindDefaults()
    {
        var remote = SamplingPolicy(0.25);
        var local = SamplingPolicy(1.0);

        var storeSnapshot = Store(
            Provider("file-provider", PolicyProviderKind.File, priority: 0, local),
            Provider("opamp-provider", PolicyProviderKind.OpAmp, remote));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        var policy = Assert.Single(resolved.Policies);
        Assert.Same(local, policy.Policy);
        Assert.Equal("file-provider", policy.Provider.Value);
        Assert.Equal(PolicyAggregationReason.Superseded, Assert.Single(policy.OutrankedPolicies).Reason);
    }

    [Fact]
    public void Aggregate_EqualPriority_LowestOrdinalRegistrationIdWins()
    {
        var first = SamplingPolicy(0.1);
        var second = SamplingPolicy(0.2);

        var storeSnapshot = Store(
            Provider("provider-b", PolicyProviderKind.OpAmp, second),
            Provider("provider-a", PolicyProviderKind.OpAmp, first));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        var policy = Assert.Single(resolved.Policies);
        Assert.Same(first, policy.Policy);
        Assert.Equal("provider-a", policy.Provider.Value);

        var outranked = Assert.Single(policy.OutrankedPolicies);
        Assert.Equal("provider-b", outranked.Provider.Value);
        Assert.Equal(PolicyAggregationReason.Conflicting, outranked.Reason);
    }

    [Fact]
    public void Aggregate_EqualPriority_ComparesRegistrationIdsOrdinallyNotNumerically()
    {
        var storeSnapshot = Store(
            Provider("p-2", PolicyProviderKind.OpAmp, SamplingPolicy(0.2)),
            Provider("p-10", PolicyProviderKind.OpAmp, SamplingPolicy(0.1)));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        var policy = Assert.Single(resolved.Policies);
        Assert.Equal("p-10", policy.Provider.Value);
        Assert.Equal("p-2", Assert.Single(policy.OutrankedPolicies).Provider.Value);
    }

    [Fact]
    public void Aggregate_ThreeContenders_RecordsOutrankedInPrecedenceOrderWithPerProviderOutcomes()
    {
        var winner = SamplingPolicy(0.1);
        var tied = SamplingPolicy(0.2);
        var lowest = SamplingPolicy(0.3);

        var storeSnapshot = Store(
            Provider("provider-c", PolicyProviderKind.File, lowest),
            Provider("provider-b", PolicyProviderKind.OpAmp, tied),
            Provider("provider-a", PolicyProviderKind.OpAmp, winner));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        var policy = Assert.Single(resolved.Policies);
        Assert.Same(winner, policy.Policy);
        Assert.Equal("provider-a", policy.Provider.Value);

        Assert.Equal(2, policy.OutrankedPolicies.Length);

        Assert.Equal("provider-b", policy.OutrankedPolicies[0].Provider.Value);
        Assert.Same(tied, policy.OutrankedPolicies[0].Policy);
        Assert.Equal(PolicyAggregationReason.Conflicting, policy.OutrankedPolicies[0].Reason);

        Assert.Equal("provider-c", policy.OutrankedPolicies[1].Provider.Value);
        Assert.Same(lowest, policy.OutrankedPolicies[1].Policy);
        Assert.Equal(PolicyAggregationReason.Superseded, policy.OutrankedPolicies[1].Reason);
    }

    [Fact]
    public void Aggregate_ProviderSupplyingNothing_ContributesNoContender()
    {
        var only = SamplingPolicy(0.5);

        var storeSnapshot = Store(
            Provider("opamp-provider", PolicyProviderKind.OpAmp),
            Provider("file-provider", PolicyProviderKind.File, only));

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        var policy = Assert.Single(resolved.Policies);
        Assert.Same(only, policy.Policy);
        Assert.Equal("file-provider", policy.Provider.Value);
        Assert.Empty(policy.OutrankedPolicies);
    }

    [Fact]
    public void Aggregate_NonEmptyStore_CopiesStoreRevision()
    {
        using var store = new PolicyStore();
        store.ReplaceProvider(Provider("p-1", PolicyProviderKind.OpAmp, SamplingPolicy(0.1)));
        store.ReplaceProvider(Provider(Metadata("p-1", PolicyProviderKind.OpAmp), sequence: 2, SamplingPolicy(0.2)));

        var resolved = PolicyAggregator.Aggregate(store.Current);

        Assert.Equal(2, resolved.StoreRevision);
    }

    [Fact]
    public void Aggregate_DoesNotReorderTheStoreSnapshotProviders()
    {
        var storeSnapshot = Store(
            Provider("a-provider", PolicyProviderKind.File, SamplingPolicy(0.1)),
            Provider("z-provider", PolicyProviderKind.OpAmp, SamplingPolicy(0.2)));

        PolicyAggregator.Aggregate(storeSnapshot);

        Assert.Equal(
            ["a-provider", "z-provider"],
            storeSnapshot.Providers.Select(static provider => provider.RegistrationId.Value));
    }

    [Fact]
    public void Aggregate_AfterWinnerRetractsWithAnEmptySnapshot_PromotesTheRunnerUp()
    {
        var remote = SamplingPolicy(0.25);
        var local = SamplingPolicy(1.0);

        using var store = new PolicyStore();
        store.ReplaceProvider(Provider("file-provider", PolicyProviderKind.File, local));
        store.ReplaceProvider(Provider("opamp-provider", PolicyProviderKind.OpAmp, remote));

        Assert.Same(remote, Assert.Single(PolicyAggregator.Aggregate(store.Current).Policies).Policy);

        store.ReplaceProvider(Provider(Metadata("opamp-provider", PolicyProviderKind.OpAmp), sequence: 2));

        var promoted = Assert.Single(PolicyAggregator.Aggregate(store.Current).Policies);

        Assert.Same(local, promoted.Policy);
        Assert.Equal("file-provider", promoted.Provider.Value);
        Assert.Empty(promoted.OutrankedPolicies);
    }

    [Fact]
    public void Aggregate_AfterWinnerIsRemoved_PromotesTheRunnerUp()
    {
        var remote = SamplingPolicy(0.25);
        var local = SamplingPolicy(1.0);

        using var store = new PolicyStore();
        store.ReplaceProvider(Provider("file-provider", PolicyProviderKind.File, local));
        store.ReplaceProvider(Provider("opamp-provider", PolicyProviderKind.OpAmp, remote));

        store.RemoveProvider(new ProviderRegistrationId("opamp-provider"));

        var promoted = Assert.Single(PolicyAggregator.Aggregate(store.Current).Policies);

        Assert.Same(local, promoted.Policy);
        Assert.Equal("file-provider", promoted.Provider.Value);
    }

    [Fact]
    public void Aggregate_AfterTheLastProviderOfAKeyRetracts_KeyIsAbsent()
    {
        using var store = new PolicyStore();
        store.ReplaceProvider(Provider("file-provider", PolicyProviderKind.File, SamplingPolicy(1.0)));

        store.ReplaceProvider(Provider(Metadata("file-provider", PolicyProviderKind.File), sequence: 2));

        var resolved = PolicyAggregator.Aggregate(store.Current);

        Assert.True(resolved.IsEmpty, "No provider supplies the key any more.");
        Assert.False(resolved.TryGetPolicy(SamplingKey, out var missing), "A retracted key must not resolve.");
        Assert.Null(missing);
    }

    [Fact]
    public void Aggregate_SameStoreSnapshotTwice_ProducesContentEqualResults()
    {
        var storeSnapshot = Store(
            Provider("provider-a", PolicyProviderKind.OpAmp, SamplingPolicy(0.1)),
            Provider("provider-b", PolicyProviderKind.OpAmp, SamplingPolicy(0.2)),
            Provider("provider-c", PolicyProviderKind.File, Policy("log-level", "log-level")));

        var first = PolicyAggregator.Aggregate(storeSnapshot);
        var second = PolicyAggregator.Aggregate(storeSnapshot);

        Assert.NotSame(first, second);
        AssertContentEqual(first, second);
    }

    private static void AssertContentEqual(ResolvedPolicySnapshot expected, ResolvedPolicySnapshot actual)
    {
        Assert.Equal(expected.StoreRevision, actual.StoreRevision);
        Assert.Equal(expected.Policies.Length, actual.Policies.Length);

        for (var i = 0; i < expected.Policies.Length; i++)
        {
            var expectedPolicy = expected.Policies[i];
            var actualPolicy = actual.Policies[i];

            Assert.Equal(expectedPolicy.Key, actualPolicy.Key);
            Assert.Same(expectedPolicy.Policy, actualPolicy.Policy);
            Assert.Equal(expectedPolicy.Provider, actualPolicy.Provider);
            Assert.Equal(expectedPolicy.OutrankedPolicies.Length, actualPolicy.OutrankedPolicies.Length);

            for (var j = 0; j < expectedPolicy.OutrankedPolicies.Length; j++)
            {
                Assert.Equal(expectedPolicy.OutrankedPolicies[j].Provider, actualPolicy.OutrankedPolicies[j].Provider);
                Assert.Same(expectedPolicy.OutrankedPolicies[j].Policy, actualPolicy.OutrankedPolicies[j].Policy);
                Assert.Equal(expectedPolicy.OutrankedPolicies[j].Reason, actualPolicy.OutrankedPolicies[j].Reason);
            }
        }
    }
}
