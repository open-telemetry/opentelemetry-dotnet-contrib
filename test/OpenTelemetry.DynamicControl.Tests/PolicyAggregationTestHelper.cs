// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

internal static class PolicyAggregationTestHelper
{
    public static PolicyProviderSnapshot Provider(
        string registrationId,
        PolicyProviderKind kind,
        params TelemetryPolicy[] policies)
        => Provider(Metadata(registrationId, kind), policies);

    public static PolicyProviderSnapshot Provider(
        string registrationId,
        PolicyProviderKind kind,
        int priority,
        params TelemetryPolicy[] policies)
        => Provider(Metadata(registrationId, kind, priority), policies);

    public static PolicyProviderSnapshot Provider(
        PolicyProviderMetadata metadata,
        params TelemetryPolicy[] policies)
        => Provider(metadata, sequence: 1, policies);

    public static PolicyProviderSnapshot Provider(
        PolicyProviderMetadata metadata,
        long sequence,
        params TelemetryPolicy[] policies)
    {
        var created = PolicyProviderSnapshot.TryCreate(
            metadata,
            sequence,
            PolicyProviderVersion.Empty,
            policies,
            out var snapshot,
            out var error);

        Assert.True(created, $"provider snapshot creation failed: {error}");
        Assert.NotNull(snapshot);

        return snapshot;
    }

    public static PolicyProviderMetadata Metadata(string registrationId, PolicyProviderKind kind, int? priority = null)
        => new(new ProviderRegistrationId(registrationId), kind, priority);

    public static PolicyStoreSnapshot Store(params PolicyProviderSnapshot[] providers)
    {
        using var store = new PolicyStore();

        foreach (var provider in providers)
        {
            var result = store.ReplaceProvider(provider);
            Assert.True(result.Applied, $"provider {provider.RegistrationId} was not applied: {result.Status}");
        }

        return store.Current;
    }

    public static TelemetryPolicy Policy(string policyType, string policyId, string? name = null)
        => new StubPolicy(policyType, policyId, name);

    public static TraceSamplingRatePolicy SamplingPolicy(double samplingProbability)
    {
        var created = TraceSamplingRatePolicy.TryCreate(
            new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value),
            "trace sampling",
            samplingProbability,
            out var policy,
            out var error);

        Assert.True(created, $"sampling policy creation failed: {error}");
        Assert.NotNull(policy);

        return policy;
    }

    private sealed class StubPolicy : TelemetryPolicy
    {
        public StubPolicy(string policyType, string policyId, string? name)
            : base(new PolicyId(policyId), name ?? $"{policyType}/{policyId}")
        {
            this.PolicyType = new PolicyType(policyType);
        }

        public override PolicyType PolicyType { get; }
    }
}
