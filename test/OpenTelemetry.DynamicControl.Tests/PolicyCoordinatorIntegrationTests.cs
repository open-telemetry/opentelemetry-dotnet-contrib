// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text;
using OpenTelemetry.DynamicControl.Internal.Aggregation;
using OpenTelemetry.DynamicControl.Internal.Coordination;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

/// <summary>
/// End-to-end integration tests that use real <see cref="PolicyStore"/>,
/// <see cref="JsonKeyValuePolicyParser"/>, and <see cref="PolicyAggregator"/> instances.
/// Only the provider is a fake.
/// </summary>
public class PolicyCoordinatorIntegrationTests
{
    [Fact]
    public async Task FullChain_OneProvider_ResolvesSamplingRatePolicy()
    {
        // Fake provider -> RefreshAsync -> real store -> real aggregator -> assert resolved value.
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.75}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync();

        var resolved = PolicyAggregator.Aggregate(store.Current);

        Assert.False(resolved.IsEmpty);
        var samplingKey = new PolicyKey(
            TraceSamplingRatePolicy.PolicyTypeValue,
            new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value));
        Assert.True(resolved.TryGetPolicy(samplingKey, out var policy), "The sampling rate policy should resolve as effective.");
        var samplingPolicy = Assert.IsType<TraceSamplingRatePolicy>(policy!.Policy);
        Assert.Equal(0.75, samplingPolicy.SamplingProbability);
    }

    [Fact]
    public async Task PriorityResolution_TwoProvidersWithSameKey_HigherPriorityWins()
    {
        // OpAmp (priority 1) and File (priority 3) both supply the same policy type.
        // The OpAmp value should win; the File value should be Superseded.
        using var store = new PolicyStore();
        var opAmp = new FakeProvider(
            "opamp",
            PolicyProviderKind.OpAmp,
            Json("""{"sampling_rate": 0.9}"""));
        var file = new FakeProvider(
            "file",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.1}"""));
        var coordinator = new PolicyCoordinator(store, [opAmp, file]);

        await coordinator.RefreshAsync();

        var resolved = PolicyAggregator.Aggregate(store.Current);

        var samplingKey = new PolicyKey(
            TraceSamplingRatePolicy.PolicyTypeValue,
            new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value));
        Assert.True(resolved.TryGetPolicy(samplingKey, out var effective), "The higher-priority provider's sampling rate policy should resolve as effective.");
        var samplingPolicy = Assert.IsType<TraceSamplingRatePolicy>(effective!.Policy);
        Assert.Equal(0.9, samplingPolicy.SamplingProbability);
        Assert.Equal("opamp", effective.Provider.Value);

        Assert.Single(effective.OutrankedPolicies);
        Assert.Equal(PolicyAggregationReason.Superseded, effective.OutrankedPolicies[0].Reason);
        Assert.Equal("file", effective.OutrankedPolicies[0].Provider.Value);
    }

    [Fact]
    public async Task RetainAndReResolve_HigherPriorityProviderRetracts_LowerPriorityBecomesEffective()
    {
        // With both providers live, the higher-priority one retracts by submitting a
        // decoded-empty payload. The lower-priority provider's policy becomes effective
        // without that provider resubmitting.
        using var store = new PolicyStore();
        var opAmp = new FakeProvider(
            "opamp",
            PolicyProviderKind.OpAmp,
            Json("""{"sampling_rate": 0.9}"""));
        var file = new FakeProvider(
            "file",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.1}"""));
        var coordinator = new PolicyCoordinator(store, [opAmp, file]);

        await coordinator.RefreshAsync();

        var samplingKey = new PolicyKey(
            TraceSamplingRatePolicy.PolicyTypeValue,
            new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value));
        PolicyAggregator.Aggregate(store.Current).TryGetPolicy(samplingKey, out var before);
        Assert.Equal(0.9, Assert.IsType<TraceSamplingRatePolicy>(before!.Policy).SamplingProbability);

        // OpAmp submits an all-invalid payload - decoded-empty, so it's a retraction.
        opAmp.NextPayload = Json("""{"sampling_rate": "bad-value"}""");

        // File provider returns null - it doesn't need to resubmit for its value to resolve.
        file.NextPayload = null;

        await coordinator.RefreshAsync();

        var resolved = PolicyAggregator.Aggregate(store.Current);
        Assert.True(resolved.TryGetPolicy(samplingKey, out var after), "The lower-priority provider's sampling rate policy should resolve as effective after the higher-priority provider retracts.");

        Assert.Equal(0.1, Assert.IsType<TraceSamplingRatePolicy>(after!.Policy).SamplingProbability);
        Assert.Equal("file", after.Provider.Value);
        Assert.Empty(after.OutrankedPolicies);
    }

    [Fact]
    public async Task EqualPriorityTieBreak_OrdinalRegistrationIdDecides()
    {
        // Two providers with equal explicit priority: ordinal RegistrationId decides.
        // "alpha" < "beta" lexicographically, so "alpha" wins.
        using var store = new PolicyStore();
        var alpha = new FakeProvider(
            "alpha",
            PolicyProviderKind.Custom,
            priority: 500,
            Json("""{"sampling_rate": 0.3}"""));
        var beta = new FakeProvider(
            "beta",
            PolicyProviderKind.Custom,
            priority: 500,
            Json("""{"sampling_rate": 0.8}"""));
        var coordinator = new PolicyCoordinator(store, [alpha, beta]);

        await coordinator.RefreshAsync();

        var resolved = PolicyAggregator.Aggregate(store.Current);
        var samplingKey = new PolicyKey(
            TraceSamplingRatePolicy.PolicyTypeValue,
            new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value));
        Assert.True(resolved.TryGetPolicy(samplingKey, out var effective), "The sampling rate policy should resolve as effective after the ordinal RegistrationId tie-break.");
        Assert.Equal("alpha", effective!.Provider.Value);
        Assert.Single(effective.OutrankedPolicies);
        Assert.Equal(PolicyAggregationReason.Conflicting, effective.OutrankedPolicies[0].Reason);
    }

    [Fact]
    public async Task SubscriptionWiring_FiringOnApplied_NotFiringOnSuppressed()
    {
        // store.Subscribe fires once per applied refresh, not for a suppressed one.
        using var store = new PolicyStore();
        var delivered = new ConcurrentQueue<PolicyStoreSnapshot>();

        using var subscription = store.Subscribe(delivered.Enqueue);
        await WaitHelper.WaitUntil(() => !delivered.IsEmpty); // initial replay

        var version = new PolicyProviderVersion("hash-abc");
        var content = Encoding.UTF8.GetBytes("""{"sampling_rate": 0.5}""");
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            new PolicyProviderPayload(content, version));
        var coordinator = new PolicyCoordinator(store, [provider]);

        // First refresh: applied, subscription should fire.
        await coordinator.RefreshAsync();
        await WaitHelper.WaitUntil(() => delivered.Count >= 2);

        var countAfterFirst = delivered.Count;

        // Second refresh: same version - suppressed, subscription should NOT fire again.
        provider.NextPayload = new PolicyProviderPayload(content, version);
        await coordinator.RefreshAsync();
        await Task.Delay(50);

        Assert.Equal(countAfterFirst, delivered.Count);
    }

    private static byte[] Utf8Bytes(string json)
        => Encoding.UTF8.GetBytes(json);

    private static PolicyProviderPayload Json(string json)
        => new(Utf8Bytes(json));

    private sealed class FakeProvider : ITelemetryPolicyProvider
    {
        public FakeProvider(string registrationId, PolicyProviderKind kind, PolicyProviderPayload? payload = null)
        {
            this.Metadata = new PolicyProviderMetadata(new ProviderRegistrationId(registrationId), kind);
            this.NextPayload = payload;
        }

        public FakeProvider(string registrationId, PolicyProviderKind kind, int priority, PolicyProviderPayload? payload = null)
        {
            this.Metadata = new PolicyProviderMetadata(new ProviderRegistrationId(registrationId), kind, priority);
            this.NextPayload = payload;
        }

        public PolicyProviderMetadata Metadata { get; }

        public PolicyProviderPayload? NextPayload { get; set; }

        public Task<PolicyProviderPayload?> FetchAsync(CancellationToken cancellationToken)
            => Task.FromResult(this.NextPayload);
    }
}
