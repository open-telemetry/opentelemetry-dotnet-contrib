// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

internal class RulesCache : IDisposable
{
    private const int CacheTTL = 60 * 60; // cache expires 1 hour after the refresh (in sec)

    private readonly ReaderWriterLockSlim rwLock;
    private bool isFallBackEventToWriteSwitch = true;

    public RulesCache(Clock clock, string clientId, Resource resource, Trace.Sampler fallbackSampler)
    {
        this.rwLock = new ReaderWriterLockSlim();
        this.Clock = clock;
        this.ClientId = clientId;
        this.Resource = resource;
        this.FallbackSampler = fallbackSampler;
        this.RuleAppliers = [];
        this.UpdatedAt = this.Clock.Now();
    }

    internal Clock Clock { get; set; }

    internal string ClientId { get; set; }

    internal Resource Resource { get; set; }

    internal Trace.Sampler FallbackSampler { get; set; }

    internal List<SamplingRuleApplier> RuleAppliers { get; set; }

    internal DateTimeOffset UpdatedAt { get; set; }

    public bool Expired()
    {
        this.rwLock.EnterReadLock();
        try
        {
            return this.Clock.Now() > this.UpdatedAt.AddSeconds(CacheTTL);
        }
        finally
        {
            this.rwLock.ExitReadLock();
        }
    }

    public void UpdateRules(List<SamplingRule> newRules)
    {
        // sort the new rules
        newRules.Sort((x, y) => x.CompareTo(y));

        // Build and swap the appliers under the write lock so that a concurrent
        // UpdateTargets (target poller) cannot have its result discarded by the
        // stale snapshot of RuleAppliers this method reuses appliers from.
        this.rwLock.EnterWriteLock();
        try
        {
            Dictionary<string, SamplingRuleApplier> existingAppliers = new(this.RuleAppliers.Count);

            foreach (var applier in this.RuleAppliers)
            {
                existingAppliers[applier.RuleName] = applier;
            }

            List<SamplingRuleApplier> newRuleAppliers = [];
            foreach (var rule in newRules)
            {
                // If the ruleApplier already exists in the current list of appliers, then we reuse it.
                var ruleApplier = existingAppliers.TryGetValue(rule.RuleName, out var currentApplier)
                    ? currentApplier
                    : new SamplingRuleApplier(this.ClientId, this.Clock, rule, new Statistics());

                // update the rule in the applier in case rule attributes have changed
                ruleApplier.Rule = rule;

                newRuleAppliers.Add(ruleApplier);
            }

            this.RuleAppliers = newRuleAppliers;
            this.UpdatedAt = this.Clock.Now();
        }
        finally
        {
            this.rwLock.ExitWriteLock();
        }
    }

    public SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        foreach (var ruleApplier in this.RuleAppliers)
        {
            if (ruleApplier.Matches(samplingParameters, this.Resource))
            {
                this.isFallBackEventToWriteSwitch = true;
                return ruleApplier.ShouldSample(in samplingParameters);
            }
        }

        // ideally the default rule should have matched.
        // if we are here then likely due to a bug.
        if (this.isFallBackEventToWriteSwitch)
        {
            this.isFallBackEventToWriteSwitch = false;
            AWSSamplerEventSource.Log.InfoUsingFallbackSampler();
        }

        return this.FallbackSampler.ShouldSample(in samplingParameters);
    }

    public List<SamplingStatisticsDocument> Snapshot(DateTimeOffset now)
    {
        List<SamplingStatisticsDocument> snapshots = [];
        foreach (var ruleApplier in this.RuleAppliers)
        {
            snapshots.Add(ruleApplier.Snapshot(now));
        }

        return snapshots;
    }

    public void UpdateTargets(Dictionary<string, SamplingTargetDocument> targets)
    {
        // Build and swap the appliers under the write lock so that a concurrent
        // UpdateRules (rule poller) cannot discard the targets applied here by
        // swapping in a list rebuilt from a stale snapshot of RuleAppliers.
        this.rwLock.EnterWriteLock();
        try
        {
            var now = this.Clock.Now();

            List<SamplingRuleApplier> newRuleAppliers = [];
            foreach (var ruleApplier in this.RuleAppliers)
            {
                targets.TryGetValue(ruleApplier.RuleName, out var target);
                if (target != null)
                {
                    newRuleAppliers.Add(ruleApplier.WithTarget(target, now));
                }
                else
                {
                    // did not get target for this rule. Will be updated in future target poll.
                    newRuleAppliers.Add(ruleApplier);
                }
            }

            this.RuleAppliers = newRuleAppliers;
        }
        finally
        {
            this.rwLock.ExitWriteLock();
        }
    }

    public DateTimeOffset NextTargetFetchTime()
    {
        var defaultPollingTime = this.Clock.Now().AddSeconds(AWSXRayRemoteSampler.DefaultTargetInterval.TotalSeconds);

        if (this.RuleAppliers.Count == 0)
        {
            return defaultPollingTime;
        }

        var minPollingTime = this.RuleAppliers.Min(r => r.NextSnapshotTime);

        return minPollingTime < this.Clock.Now() ? defaultPollingTime : minPollingTime;
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal DateTimeOffset GetUpdatedAt()
    {
        this.rwLock.EnterReadLock();
        try
        {
            return this.UpdatedAt;
        }
        finally
        {
            this.rwLock.ExitReadLock();
        }
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.rwLock.Dispose();
        }
    }
}
