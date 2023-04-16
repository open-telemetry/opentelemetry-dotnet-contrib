// <copyright file="TestRulesCache.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class TestRulesCache
{
    [Fact]
    public void TestExpiredRulesCache()
    {
        var testClock = new TestClock(new DateTime(2023, 4, 15));
        var rulesCache = new RulesCache(testClock, "testId", ResourceBuilder.CreateEmpty().Build(), new AlwaysOnSampler());

        // advance the clock by 2 hours
        testClock.Advance(TimeSpan.FromHours(2));
        Assert.True(rulesCache.Expired());
    }

    [Fact]
    public void TestUpdateRules()
    {
        var clock = new TestClock();

        // set up rules cache with a default rule
        var defaultRule = this.CreateDefaultRule(1, 0.05);
        var stats = new Statistics()
        {
            RequestCount = 10,
            SampleCount = 5,
            BorrowCount = 5,
        };

        var cache = new RulesCache(clock, "test", ResourceBuilder.CreateEmpty().Build(), new AlwaysOffSampler())
        {
            RuleAppliers = new List<SamplingRuleApplier>()
            {
                { new SamplingRuleApplier("testId", clock, defaultRule, stats) },
            },
        };

        // update the default rule
        var newDefaultRule = this.CreateDefaultRule(10, 0.20);
        cache.UpdateRules(new List<SamplingRule> { newDefaultRule });

        // asserts
        Assert.Single(cache.RuleAppliers);
        var rule = cache.RuleAppliers[0];
        Assert.Equal("Default", rule.RuleName);

        // assert that the statistics has been copied over to new rule
        Assert.Equal(10, rule.Statistics.RequestCount);
        Assert.Equal(5, rule.Statistics.BorrowCount);
        Assert.Equal(5, rule.Statistics.SampleCount);
    }

    [Fact]
    public void TestUpdateRulesRemovesOlderRule()
    {
        var clock = new TestClock();

        // set up rule cache with 2 rules
        var rulesCache = new RulesCache(clock, "test", ResourceBuilder.CreateEmpty().Build(), new AlwaysOffSampler())
        {
            RuleAppliers = new List<SamplingRuleApplier>()
            {
                { new SamplingRuleApplier("testId", clock, this.CreateDefaultRule(1, 0.05), null) },
                { new SamplingRuleApplier("testId", clock, this.CreateRule("Rule1", 5, 0.20, 1), null) },
            },
        };

        // the update contains only the default rule
        var newDefaultRule = this.CreateDefaultRule(10, 0.20);
        rulesCache.UpdateRules(new List<SamplingRule> { newDefaultRule });

        // assert that Rule1 doesn't exist in rules cache
        Assert.Single(rulesCache.RuleAppliers);
        Assert.Single(rulesCache.RuleAppliers);
        Assert.Equal("Default", rulesCache.RuleAppliers[0].RuleName);
    }

    // TODO: Add tests for matching sampling rules once the reservoir and fixed rate samplers are added.

    private SamplingRule CreateDefaultRule(int reservoirSize, double fixedRate)
    {
        return this.CreateRule("Default", reservoirSize, fixedRate, 10000);
    }

    private SamplingRule CreateRule(string name, int reservoirSize, double fixedRate, int priority)
    {
        return new SamplingRule(
           ruleName: name,
           priority: priority,
           fixedRate: fixedRate,
           reservoirSize: reservoirSize,
           host: "*",
           httpMethod: "*",
           resourceArn: "*",
           serviceName: "*",
           serviceType: "*",
           urlPath: "*",
           version: 1,
           attributes: new Dictionary<string, string>());
    }
}
