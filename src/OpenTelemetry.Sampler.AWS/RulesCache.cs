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

    public RulesCache()
    {
        this.Rules = new Dictionary<string, SamplingRule>();
        this.rwLock = new ReaderWriterLockSlim();
        this.Clock = Clock.GetInstance;
    }

    public Dictionary<string, SamplingRule> Rules { get; internal set; }

    public Clock Clock { get; internal set; }

    public DateTime? UpdatedAt { get; internal set; }

    public void UpdateRules(List<SamplingRule> newRules)
    {
        newRules.Sort((x, y) => x.CompareTo(y));

        var oldRulesCopy = this.DeepCopyRules();

        foreach (var newRule in newRules)
        {
            if (oldRulesCopy.TryGetValue(newRule.RuleName, out SamplingRule? oldRule))
            {
                if (oldRule is not null)
                {
                    newRule.Reservoir = oldRule.Reservoir;
                    newRule.Statistics = oldRule.Statistics;
                }
            }
        }

        this.rwLock.EnterWriteLock();
        try
        {
            this.Rules = newRules.ToDictionary(x => x.RuleName, x => x);
            this.UpdatedAt = Clock.Now();
        }
        finally
        {
            this.rwLock.ExitWriteLock();
        }
    }

    public bool Expired()
    {
        this.rwLock.EnterReadLock();
        try
        {
            if (this.UpdatedAt is null)
            {
                return true;
            }

            return Clock.Now() > this.UpdatedAt.Value.AddSeconds(CacheTTL);
        }
        finally
        {
            this.rwLock.ExitReadLock();
        }
    }

    public SamplingRule? MatchRule(SamplingParameters samplingParameters, Resource resource)
    {
        SamplingRule? matchedRule = null;

        this.rwLock.EnterReadLock();
        try
        {
            foreach (var ruleKeyValue in this.Rules)
            {
                if (ruleKeyValue.Value.Matches(samplingParameters, resource) ||
                    string.Equals("Default", ruleKeyValue.Key, StringComparison.Ordinal))
                {
                    matchedRule = ruleKeyValue.Value;
                    break;
                }
            }
        }
        finally
        {
            this.rwLock.ExitReadLock();
        }

        return matchedRule;
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.rwLock.Dispose();
        }
    }

    private Dictionary<string, SamplingRule> DeepCopyRules()
    {
        Dictionary<string, SamplingRule> copy = new Dictionary<string, SamplingRule>();

        this.rwLock.EnterReadLock();
        try
        {
            foreach (var ruleKeyValue in this.Rules)
            {
                copy.Add(ruleKeyValue.Key, ruleKeyValue.Value.DeepCopy());
            }
        }
        finally
        {
            this.rwLock.ExitReadLock();
        }

        return copy;
    }
}
