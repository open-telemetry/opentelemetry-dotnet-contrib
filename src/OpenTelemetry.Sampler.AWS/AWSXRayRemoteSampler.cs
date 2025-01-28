// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.AWS;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

/// <summary>
/// Remote sampler that gets sampling configuration from AWS X-Ray.
/// </summary>
public sealed class AWSXRayRemoteSampler : Trace.Sampler, IDisposable
{
    internal static readonly TimeSpan DefaultTargetInterval = TimeSpan.FromSeconds(10);

    private static readonly Random Random = new();
    private bool isFallBackEventToWriteSwitch = true;

    /// <inheritdoc cref="AWSXRayRemoteSampler"/>
    public AWSXRayRemoteSampler(Resource resource, Action<AWSXRayRemoteSamplerOptions>? configure = null)
        : this(resource, Clock.GetDefault(), configure)
    {
    }

    [SuppressMessage("Performance", "CA5394: Do not use insecure randomness", Justification = "Secure random is not required for jitters.")]
    internal AWSXRayRemoteSampler(Resource resource, Clock clock, Action<AWSXRayRemoteSamplerOptions>? configure = null)
    {
        var options = new AWSXRayRemoteSamplerOptions();

        if (configure != null)
        {
            configure(options);
        }

        if (options.PollingInterval < TimeSpan.Zero)
        {
            throw new ArgumentException("Polling interval must be non-negative.");
        }

        this.Resource = resource;
        this.PollingInterval = options.PollingInterval;
        this.Endpoint = options.Endpoint;
        this.Clock = clock;
        this.ClientId = GenerateClientId();
        this.Client = new AWSXRaySamplerClient(this.Endpoint);
        this.FallbackSampler = new FallbackSampler(this.Clock);
        this.AWSSemanticConventions = new AWSSemanticConventions(options.SemanticConventionVersion);
        this.RulesCache = new RulesCache(this.AWSSemanticConventions, this.Clock, this.ClientId, this.Resource, this.FallbackSampler);

        // upto 5 seconds of jitter for rule polling
        this.RulePollerJitter = TimeSpan.FromMilliseconds(Random.Next(1, 5000));

        // upto 100 milliseconds of jitter for target polling
        this.TargetPollerJitter = TimeSpan.FromMilliseconds(Random.Next(1, 100));

        // execute the first update right away and schedule subsequent update later.
        this.RulePollerTimer = new Timer(this.GetAndUpdateRules, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);

        // set up the target poller to go off once after the default interval. We will update the timer later.
        this.TargetPollerTimer = new Timer(this.GetAndUpdateTargets, null, DefaultTargetInterval, Timeout.InfiniteTimeSpan);
    }

    internal TimeSpan RulePollerJitter { get; set; }

    internal TimeSpan TargetPollerJitter { get; set; }

    internal Clock Clock { get; set; }

    internal string ClientId { get; set; }

    internal AWSSemanticConventions AWSSemanticConventions { get; set; }

    internal Resource Resource { get; set; }

    internal string Endpoint { get; set; }

    internal AWSXRaySamplerClient Client { get; set; }

    internal RulesCache RulesCache { get; set; }

    internal Timer RulePollerTimer { get; set; }

    internal Timer TargetPollerTimer { get; set; }

    internal TimeSpan PollingInterval { get; set; }

    internal Trace.Sampler FallbackSampler { get; set; }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (this.RulesCache.Expired())
        {
            if (this.isFallBackEventToWriteSwitch)
            {
                this.isFallBackEventToWriteSwitch = false;

                // could be expensive operation, conditionally call once
                AWSSamplerEventSource.Log.InfoUsingFallbackSampler();
            }

            return this.FallbackSampler.ShouldSample(in samplingParameters);
        }

        this.isFallBackEventToWriteSwitch = true;
        return this.RulesCache.ShouldSample(in samplingParameters);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    [SuppressMessage(
        "Usage",
        "CA5394: Do not use insecure randomness",
        Justification = "using insecure random is fine here since clientId doesn't need to be secure.")]
    private static string GenerateClientId()
    {
        char[] hex = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'];
        var clientIdChars = new char[24];
        for (var i = 0; i < clientIdChars.Length; i++)
        {
            clientIdChars[i] = hex[Random.Next(hex.Length)];
        }

        return new string(clientIdChars);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.RulePollerTimer?.Dispose();
            this.Client?.Dispose();
            this.RulesCache?.Dispose();
        }
    }

    private async void GetAndUpdateRules(object? state)
    {
        var rules = await this.Client.GetSamplingRules().ConfigureAwait(false);

        this.RulesCache.UpdateRules(rules);

        // schedule the next rule poll.
        this.RulePollerTimer.Change(this.PollingInterval.Add(this.RulePollerJitter), Timeout.InfiniteTimeSpan);
    }

    private async void GetAndUpdateTargets(object? state)
    {
        var statistics = this.RulesCache.Snapshot(this.Clock.Now());

        var request = new GetSamplingTargetsRequest(statistics);
        var response = await this.Client.GetSamplingTargets(request).ConfigureAwait(false);
        if (response != null)
        {
            Dictionary<string, SamplingTargetDocument> targets = [];
            foreach (var target in response.SamplingTargetDocuments)
            {
                if (target.RuleName != null)
                {
                    targets[target.RuleName] = target;
                }
            }

            this.RulesCache.UpdateTargets(targets);

            if (response.LastRuleModification > 0)
            {
                var lastRuleModificationTime = this.Clock.ToDateTime(response.LastRuleModification);

                if (lastRuleModificationTime > this.RulesCache.GetUpdatedAt())
                {
                    // rules have been updated. fetch the new ones right away.
                    this.RulePollerTimer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
                }
            }
        }

        // schedule next target poll
        var nextTargetFetchTime = this.RulesCache.NextTargetFetchTime();
        var nextTargetFetchInterval = nextTargetFetchTime.Subtract(this.Clock.Now());
        if (nextTargetFetchInterval < TimeSpan.Zero)
        {
            nextTargetFetchInterval = DefaultTargetInterval;
        }

        this.TargetPollerTimer.Change(nextTargetFetchInterval.Add(this.TargetPollerJitter), Timeout.InfiniteTimeSpan);
    }
}
