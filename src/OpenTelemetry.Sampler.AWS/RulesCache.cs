// <copyright file="RulesCache.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

internal class RulesCache : IDisposable
{
    private const int CacheTTL = 60 * 60; // cache expires 1 hour after the refresh (in sec)

    private readonly ReaderWriterLockSlim rwLock;

    public RulesCache(Clock clock, string clientId, Resource resource, Trace.Sampler fallbackSampler)
    {
        this.rwLock = new ReaderWriterLockSlim();
        this.Clock = clock;
        this.ClientId = clientId;
        this.Resource = resource;
        this.FallbackSampler = fallbackSampler;
        this.RuleAppliers = new List<SamplingRuleApplier>();
        this.UpdatedAt = this.Clock.Now();
    }

    internal Clock Clock { get; set; }

    internal string ClientId { get; set; }

    internal Resource Resource { get; set; }

    internal Trace.Sampler FallbackSampler { get; set; }

    internal List<SamplingRuleApplier> RuleAppliers { get; set; }

    internal DateTime UpdatedAt { get; set; }

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

        List<SamplingRuleApplier> newRuleAppliers = new List<SamplingRuleApplier>();
        foreach (var rule in newRules)
        {
            var currentStatistics = this.RuleAppliers
                .FirstOrDefault(currentApplier => currentApplier.RuleName == rule.RuleName)
                ?.Statistics ?? new Statistics();

            var ruleApplier = new SamplingRuleApplier(this.ClientId, this.Clock, rule, currentStatistics);
            newRuleAppliers.Add(ruleApplier);
        }

        this.rwLock.EnterWriteLock();
        try
        {
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
                return ruleApplier.ShouldSample(in samplingParameters);
            }
        }

        // ideally the default rule should have matched.
        // if we are here then likely due to a bug.
        AWSSamplerEventSource.Log.InfoUsingFallbackSampler();
        return this.FallbackSampler.ShouldSample(in samplingParameters);
    }

    public List<SamplingStatisticsDocument> Snapshot(DateTime now)
    {
        List<SamplingStatisticsDocument> snapshots = new List<SamplingStatisticsDocument>();
        foreach (var ruleApplier in this.RuleAppliers)
        {
            snapshots.Add(ruleApplier.Snapshot(now));
        }

        return snapshots;
    }

    public void UpdateTargets(Dictionary<string, SamplingTargetDocument> targets)
    {
        List<SamplingRuleApplier> newRuleAppliers = new List<SamplingRuleApplier>();
        foreach (var ruleApplier in this.RuleAppliers)
        {
            targets.TryGetValue(ruleApplier.RuleName, out SamplingTargetDocument? target);
            if (target != null)
            {
                newRuleAppliers.Add(ruleApplier.WithTarget(target, this.Clock.Now()));
            }
            else
            {
                // did not get target for this rule. Will be updated in future target poll.
                newRuleAppliers.Add(ruleApplier);
            }
        }

        this.rwLock.EnterWriteLock();
        try
        {
            this.RuleAppliers = newRuleAppliers;
        }
        finally
        {
            this.rwLock.ExitWriteLock();
        }
    }

    public DateTime NextTargetFetchTime()
    {
        var defaultPollingTime = this.Clock.Now().AddSeconds(AWSXRayRemoteSampler.DefaultTargetInterval.TotalSeconds);

        if (this.RuleAppliers.Count == 0)
        {
            return defaultPollingTime;
        }

        var minPollingTime = this.RuleAppliers
            .Select(r => r.NextSnapshotTime)
            .Min();

        if (minPollingTime < this.Clock.Now())
        {
            return defaultPollingTime;
        }

        return minPollingTime;
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal DateTime GetUpdatedAt()
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
