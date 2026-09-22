// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck.Xunit;
using OpenTelemetry.DynamicControl.Internal.Aggregation;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class PolicyAggregatorTests
{
    private const int MaxTest = 500;

    private static readonly DiagnosticLogLevel[] LogLevels =
    [
        DiagnosticLogLevel.Debug,
        DiagnosticLogLevel.Error,
        DiagnosticLogLevel.Information,
        DiagnosticLogLevel.None,
        DiagnosticLogLevel.Trace,
        DiagnosticLogLevel.Warning,
    ];

    private static readonly PolicyProviderKind[] Kinds =
    [
        PolicyProviderKind.Custom,
        PolicyProviderKind.File,
        PolicyProviderKind.Http,
        PolicyProviderKind.OpAmp,
    ];

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void Aggregate_IsIndependentOfProviderSubmissionRecency(
        FuzzedProviderSet providerSet,
        int resubmissionMask)
    {
        var providers = Materialize(providerSet);

        var expected = PolicyAggregator.Aggregate(Store(providers));
        var actual = PolicyAggregator.Aggregate(Store(providers, resubmissionMask));

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

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void Aggregate_ResolvesEveryKeyOnceAndAccountsForEveryContender(FuzzedProviderSet providerSet)
    {
        var providers = Materialize(providerSet);
        var storeSnapshot = Store(providers);

        var resolved = PolicyAggregator.Aggregate(storeSnapshot);

        Assert.Equal(storeSnapshot.Revision, resolved.StoreRevision);

        var supplied = new HashSet<PolicyKey>();
        foreach (var provider in providers)
        {
            foreach (var policy in provider.Policies)
            {
                supplied.Add(PolicyKey.FromPolicy(policy));
            }
        }

        Assert.Equal(supplied.Count, resolved.Policies.Length);

        for (var i = 0; i < resolved.Policies.Length; i++)
        {
            var policy = resolved.Policies[i];

            Assert.Contains(policy.Key, supplied);

            if (i > 0)
            {
                Assert.True(
                    PolicyKeyComparer.Default.Compare(resolved.Policies[i - 1].Key, policy.Key) < 0,
                    $"Policy key {resolved.Policies[i - 1].Key} must sort before {policy.Key}.");
            }

            Assert.True(
                storeSnapshot.TryGetProvider(policy.Provider, out var winner),
                $"The winning provider {policy.Provider} must be in the store.");
            Assert.True(
                winner.TryGetPolicy(policy.Key, out var winningPolicy),
                $"Provider {policy.Provider} must supply {policy.Key}.");
            Assert.Same(winningPolicy, policy.Policy);

            var contenders = 0;
            foreach (var provider in providers)
            {
                if (provider.TryGetPolicy(policy.Key, out _))
                {
                    contenders++;
                }
            }

            Assert.Equal(contenders - 1, policy.OutrankedPolicies.Length);

            AssertOutrankedAccountedFor(storeSnapshot, policy, winner.Metadata);
        }
    }

    private static void AssertOutrankedAccountedFor(
        PolicyStoreSnapshot storeSnapshot,
        ResolvedPolicy policy,
        PolicyProviderMetadata winner)
    {
        var previousPriority = winner.Priority;
        var previousProvider = winner.RegistrationId;

        foreach (var outranked in policy.OutrankedPolicies)
        {
            Assert.NotEqual(policy.Provider, outranked.Provider);

            Assert.True(
                storeSnapshot.TryGetProvider(outranked.Provider, out var loser),
                $"The outranked provider {outranked.Provider} must be in the store.");
            Assert.True(
                loser.TryGetPolicy(policy.Key, out var lostPolicy),
                $"Provider {outranked.Provider} must supply {policy.Key}.");
            Assert.Same(lostPolicy, outranked.Policy);

            var priority = loser.Metadata.Priority;

            Assert.True(
                priority >= previousPriority,
                $"Outranked policies must be ordered by precedence, but {priority} follows {previousPriority}.");

            var expectedReason = priority == winner.Priority
                ? PolicyAggregationReason.Conflicting
                : PolicyAggregationReason.Superseded;

            Assert.Equal(expectedReason, outranked.Reason);

            if (priority == previousPriority)
            {
                Assert.True(
                    string.CompareOrdinal(previousProvider.Value, outranked.Provider.Value) < 0,
                    $"Provider {previousProvider} must take precedence over {outranked.Provider} at equal priority.");
            }

            previousPriority = priority;
            previousProvider = outranked.Provider;
        }
    }

    private static PolicyProviderSnapshot[] Materialize(FuzzedProviderSet providerSet)
    {
        var providers = new PolicyProviderSnapshot[providerSet.Providers.Length];

        for (var i = 0; i < providers.Length; i++)
        {
            var fuzzed = providerSet.Providers[i];

            var metadata = new PolicyProviderMetadata(
                new ProviderRegistrationId($"p-{i}"),
                Kinds[fuzzed.KindSelector % Kinds.Length],
                fuzzed.Priority);

            var created = PolicyProviderSnapshot.TryCreate(
                metadata,
                sequence: 1,
                PolicyProviderVersion.Empty,
                Policies(fuzzed),
                out var snapshot,
                out var error);

            Assert.True(created, $"Provider snapshot creation failed: {error}.");
            Assert.NotNull(snapshot);

            providers[i] = snapshot;
        }

        return providers;
    }

    private static List<TelemetryPolicy> Policies(FuzzedProvider fuzzed)
    {
        var policies = new List<TelemetryPolicy>(3);

        if ((fuzzed.PolicyMask & 1) != 0)
        {
            var created = TraceSamplingRatePolicy.TryCreate(
                new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value),
                "trace sampling",
                fuzzed.SamplingProbability,
                out var policy,
                out var error);

            Assert.True(created, $"Sampling policy creation failed: {error}.");
            Assert.NotNull(policy);

            policies.Add(policy);
        }

        if ((fuzzed.PolicyMask & 2) != 0)
        {
            policies.Add(LogLevel(LogLevelPolicy.PolicyTypeValue.Value, fuzzed.LogLevelSelector));
        }

        if ((fuzzed.PolicyMask & 4) != 0)
        {
            policies.Add(LogLevel("audit", fuzzed.LogLevelSelector));
        }

        return policies;
    }

    private static LogLevelPolicy LogLevel(string policyId, byte selector)
    {
        var level = LogLevels[selector % LogLevels.Length];

        var created = LogLevelPolicy.TryCreate(
            new PolicyId(policyId),
            "log level",
            level,
            out var policy,
            out var error);

        Assert.True(created, $"Log level policy creation failed: {error}.");
        Assert.NotNull(policy);

        return policy;
    }

    private static PolicyStoreSnapshot Store(PolicyProviderSnapshot[] providers, int resubmissionMask = 0)
    {
        using var store = new PolicyStore();

        for (var i = 0; i < providers.Length; i++)
        {
            var provider = providers[i];

            // Retracting and resubmitting the same set leaves it behind a higher sequence number,
            // so a selected provider ends up as the most recent submission without its effective
            // set differing.
            if (((resubmissionMask >> i) & 1) != 0)
            {
                Submit(store, Resubmit(provider, sequence: 1, retract: true));
                Submit(store, Resubmit(provider, sequence: 2, retract: false));
                continue;
            }

            Submit(store, provider);
        }

        return store.Current;
    }

    private static PolicyProviderSnapshot Resubmit(PolicyProviderSnapshot provider, long sequence, bool retract)
    {
        var created = PolicyProviderSnapshot.TryCreate(
            provider.Metadata,
            sequence,
            PolicyProviderVersion.Empty,
            retract ? [] : provider.Policies,
            out var snapshot,
            out var error);

        Assert.True(created, $"Provider snapshot creation failed: {error}.");
        Assert.NotNull(snapshot);

        return snapshot;
    }

    private static void Submit(PolicyStore store, PolicyProviderSnapshot provider)
    {
        var result = store.ReplaceProvider(provider);

        Assert.True(result.Applied, $"Provider {provider.RegistrationId} was not applied: {result.Status}.");
    }
}
