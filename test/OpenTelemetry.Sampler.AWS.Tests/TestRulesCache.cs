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

using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class TestRulesCache
{
    [Fact]
    public void TestUpdateRules()
    {
        // set up rules cache with a default rule
        var defaultRule = this.CreateDefaultRule(1, 0.05);
        defaultRule.Reservoir = new Reservoir(20);
        defaultRule.Statistics = new Statistics()
        {
            RequestCount = 10,
            BorrowCount = 5,
            SampleCount = 5,
        };

        var rulesCache = new RulesCache()
        {
            Rules = new Dictionary<string, SamplingRule>()
            {
                { "Default", defaultRule },
            },
        };

        // update the default rule
        var newDefaultRule = this.CreateDefaultRule(10, 0.20);
        rulesCache.UpdateRules(new List<SamplingRule> { newDefaultRule });

        // assert that the default rule has been updated
        Assert.True(rulesCache.Rules.TryGetValue("Default", out var rule));
        Assert.Equal("Default", rule.RuleName);
        Assert.Equal(10, rule.ReservoirSize);
        Assert.Equal(0.20, rule.FixedRate);

        // assert that the reservoir and statistics has been copied over to new rule
        Assert.Equal(20, rule.Reservoir.Quota);
        Assert.Equal(10, rule.Statistics.RequestCount);
        Assert.Equal(5, rule.Statistics.BorrowCount);
        Assert.Equal(5, rule.Statistics.SampleCount);
    }

    [Fact]
    public void TestUpdateRulesRemovesOlderRule()
    {
        // set up rule cache with 2 rules
        var rulesCache = new RulesCache()
        {
            Rules = new Dictionary<string, SamplingRule>()
            {
                { "Default", this.CreateDefaultRule(1, 0.05) },
                { "Rule1", this.CreateRule("Rule1", 5, 0.20, 1) },
            },
        };

        // the update contains only the default rule
        var newDefaultRule = this.CreateDefaultRule(10, 0.20);
        rulesCache.UpdateRules(new List<SamplingRule> { newDefaultRule });

        // assert that Rule1 doesn't exist in rules cache
        Assert.Single(rulesCache.Rules);
        Assert.False(rulesCache.Rules.ContainsKey("Rule1"));
        Assert.True(rulesCache.Rules.ContainsKey("Default"));
    }

    [Fact]
    public void TestMatchesWithHigherPriority()
    {
        // set up rule cache with 3 rules
        var rulesCache = new RulesCache();

        var rules = new List<SamplingRule>
        {
            this.CreateDefaultRule(1, 0.05),
            this.CreateRule("Rule1", 5, 0.20, 100),
            this.CreateRule("Rule2", 10, 0.20, 1),
        };

        rulesCache.UpdateRules(rules);

        var samplingParameters = Utils.CreateSamplingParametersWithTags(new Dictionary<string, string>());
        var resource = Utils.CreateResource("myServiceName", "aws_ec2");

        var matchedRule = rulesCache.MatchRule(samplingParameters, resource);

        // assert that the rule with higher priority matched
        Assert.Equal("Rule2", matchedRule.RuleName);
    }

    [Fact]
    public void TestMatchWithSamePriority()
    {
        // set up rule cache with 3 rules with 2 rules at same priority
        var rulesCache = new RulesCache();

        var rules = new List<SamplingRule>
        {
            this.CreateDefaultRule(1, 0.05),
            this.CreateRule("Rule1", 5, 0.20, 1),
            this.CreateRule("Rule2", 10, 0.20, 1),
        };

        rulesCache.UpdateRules(rules);

        var samplingParameters = Utils.CreateSamplingParametersWithTags(new Dictionary<string, string>());
        var resource = Utils.CreateResource("myServiceName", "aws_ec2");

        var matchedRule = rulesCache.MatchRule(samplingParameters, resource);

        // assert that the rule is matched in alphabetical order
        Assert.Equal("Rule1", matchedRule.RuleName);
    }

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
