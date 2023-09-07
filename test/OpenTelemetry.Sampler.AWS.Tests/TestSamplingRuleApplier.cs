// <copyright file="TestSamplingRuleApplier.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class TestSamplingRuleApplier
{
    [Fact]
    public void TestRuleMatchesWithAllAttributes()
    {
        var rule = new SamplingRule(
            ruleName: "testRule",
            priority: 1,
            fixedRate: 0.05,
            reservoirSize: 1,
            host: "localhost",
            httpMethod: "GET",
            resourceArn: "arn:aws:lambda:us-west-2:123456789012:function:my-function",
            serviceName: "myServiceName",
            serviceType: "AWS::Lambda::Function",
            urlPath: "/helloworld",
            version: 1,
            attributes: new Dictionary<string, string>());

        var activityTags = new Dictionary<string, string>
        {
            { "http.host", "localhost" },
            { "http.method", "GET" },
            { "http.url", @"http://127.0.0.1:5000/helloworld" },
            { "faas.id", "arn:aws:lambda:us-west-2:123456789012:function:my-function" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.True(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_lambda")));
    }

    [Fact]
    public void TestRuleMatchesWithWildcardAttributes()
    {
        var rule = new SamplingRule(
            ruleName: "testRule",
            priority: 1,
            fixedRate: 0.05,
            reservoirSize: 1,
            host: "*",
            httpMethod: "*",
            resourceArn: "*",
            serviceName: "myServiceName",
            serviceType: "*",
            urlPath: "/helloworld",
            version: 1,
            attributes: new Dictionary<string, string>());

        var activityTags = new Dictionary<string, string>
        {
            { "http.host", "localhost" },
            { "http.method", "GET" },
            { "http.url", @"http://127.0.0.1:5000/helloworld" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.True(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_ec2")));
    }

    [Fact]
    public void TestRuleMatchesWithNoActivityAttributes()
    {
        var rule = new SamplingRule(
            ruleName: "testRule",
            priority: 1,
            fixedRate: 0.05,
            reservoirSize: 1,
            host: "*",
            httpMethod: "*",
            resourceArn: "*",
            serviceName: "myServiceName",
            serviceType: "*",
            urlPath: "/helloworld",
            version: 1,
            attributes: new Dictionary<string, string>());

        var activityTags = new Dictionary<string, string>();

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.False(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_ec2")));
    }

    [Fact]
    public void TestRuleMatchesWithNoActivityAttributesAndWildcardRules()
    {
        var rule = new SamplingRule(
            ruleName: "testRule",
            priority: 1,
            fixedRate: 0.05,
            reservoirSize: 1,
            host: "*",
            httpMethod: "*",
            resourceArn: "*",
            serviceName: "myServiceName",
            serviceType: "*",
            urlPath: "*",
            version: 1,
            attributes: new Dictionary<string, string>());

        var activityTags = new Dictionary<string, string>();

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.True(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_ec2")));
    }

    [Fact]
    public void TestRuleMatchesWithHttpTarget()
    {
        var rule = new SamplingRule(
            ruleName: "testRule",
            priority: 1,
            fixedRate: 0.05,
            reservoirSize: 1,
            host: "*",
            httpMethod: "*",
            resourceArn: "*",
            serviceName: "*",
            serviceType: "*",
            urlPath: "/hello*",
            version: 1,
            attributes: new Dictionary<string, string>());

        var activityTags = new Dictionary<string, string>
        {
            { "http.target", "/helloworld" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.True(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", string.Empty)));
    }

    [Fact]
    public void TestAttributeMatching()
    {
        var ruleAttributes = new Dictionary<string, string>
        {
            { "dog", "bark" },
            { "cat", "meow" },
        };

        var rule = new SamplingRule(
            ruleName: "testRule",
            priority: 1,
            fixedRate: 0.05,
            reservoirSize: 1,
            host: "*",
            httpMethod: "*",
            resourceArn: "*",
            serviceName: "*",
            serviceType: "*",
            urlPath: "*",
            version: 1,
            attributes: ruleAttributes);

        var activityTags = new Dictionary<string, string>
        {
            { "http.target", "/helloworld" },
            { "dog", "bark" },
            { "cat", "meow" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.True(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_ecs")));
    }

    [Fact]
    public void TestAttributeMatchingWithLessActivityTags()
    {
        var ruleAttributes = new Dictionary<string, string>
        {
            { "dog", "bark" },
            { "cat", "meow" },
        };

        var rule = new SamplingRule(
            ruleName: "testRule",
            priority: 1,
            fixedRate: 0.05,
            reservoirSize: 1,
            host: "*",
            httpMethod: "*",
            resourceArn: "*",
            serviceName: "*",
            serviceType: "*",
            urlPath: "*",
            version: 1,
            attributes: ruleAttributes);

        var activityTags = new Dictionary<string, string>
        {
            { "http.target", "/helloworld" },
            { "dog", "bark" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.False(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_ecs")));
    }

    // fixed rate is 1.0 and reservoir is 0
    [Fact]
    public void TestFixedRateAlwaysSample()
    {
        TestClock clock = new TestClock();
        SamplingRule rule = new SamplingRule(
            "rule1",
            1,
            1.0, // fixed rate
            0, // reservoir
            "*",
            "*",
            "*",
            "*",
            "*",
            "*",
            1,
            new Dictionary<string, string>());

        var applier = new SamplingRuleApplier("clientId", clock, rule, new Statistics());

        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);

        // test if the snapshot was correctly captured
        var statistics = applier.Snapshot(clock.Now());
        Assert.Equal("clientId", statistics.ClientID);
        Assert.Equal("rule1", statistics.RuleName);
        Assert.Equal(clock.ToDouble(clock.Now()), statistics.Timestamp);
        Assert.Equal(1, statistics.RequestCount);
        Assert.Equal(1, statistics.SampledCount);
        Assert.Equal(0, statistics.BorrowCount);

        // reset statistics
        statistics = applier.Snapshot(clock.Now());
        Assert.Equal(0, statistics.RequestCount);
        Assert.Equal(0, statistics.SampledCount);
        Assert.Equal(0, statistics.BorrowCount);
    }

    // fixed rate is 0.0 and reservoir is 0
    [Fact]
    public void TestFixedRateNeverSample()
    {
        TestClock clock = new TestClock();
        SamplingRule rule = new SamplingRule(
            "rule1",
            1,
            0.0, // fixed rate
            0, // reservoir
            "*",
            "*",
            "*",
            "*",
            "*",
            "*",
            1,
            new Dictionary<string, string>());

        var applier = new SamplingRuleApplier("clientId", clock, rule, new Statistics());

        Assert.Equal(SamplingDecision.Drop, applier.ShouldSample(default).Decision);

        // test if the snapshot was correctly captured
        var statistics = applier.Snapshot(clock.Now());
        Assert.Equal("clientId", statistics.ClientID);
        Assert.Equal("rule1", statistics.RuleName);
        Assert.Equal(clock.ToDouble(clock.Now()), statistics.Timestamp);
        Assert.Equal(1, statistics.RequestCount);
        Assert.Equal(0, statistics.SampledCount);
        Assert.Equal(0, statistics.BorrowCount);
    }

    [Fact]
    public void TestBorrowFromReservoir()
    {
        TestClock clock = new TestClock();
        SamplingRule rule = new SamplingRule(
           "rule1",
           1,
           0.0, // fixed rate
           100, // reservoir
           "*",
           "*",
           "*",
           "*",
           "*",
           "*",
           1,
           new Dictionary<string, string>());

        var applier = new SamplingRuleApplier("clientId", clock, rule, new Statistics());

        // sampled by borrowing
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);

        // can only borrow 1 req/sec
        Assert.Equal(SamplingDecision.Drop, applier.ShouldSample(default).Decision);

        // test if the snapshot was correctly captured
        var statistics = applier.Snapshot(clock.Now());
        Assert.Equal("clientId", statistics.ClientID);
        Assert.Equal("rule1", statistics.RuleName);
        Assert.Equal(clock.ToDouble(clock.Now()), statistics.Timestamp);
        Assert.Equal(2, statistics.RequestCount);
        Assert.Equal(1, statistics.SampledCount);
        Assert.Equal(1, statistics.BorrowCount);
    }

    [Fact]
    public void TestWithTarget()
    {
        TestClock clock = new TestClock();
        SamplingRule rule = new SamplingRule(
           "rule1",
           1,
           0.0, // fixed rate
           100, // reservoir
           "*",
           "*",
           "*",
           "*",
           "*",
           "*",
           1,
           new Dictionary<string, string>());

        var applier = new SamplingRuleApplier("clientId", clock, rule, new Statistics());

        // no target assigned yet. so borrow 1 from reservoir every second
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);
        Assert.Equal(SamplingDecision.Drop, applier.ShouldSample(default).Decision);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);
        Assert.Equal(SamplingDecision.Drop, applier.ShouldSample(default).Decision);

        // get the target
        SamplingTargetDocument target = new SamplingTargetDocument
        {
            FixedRate = 0.0,
            Interval = 10,
            ReservoirQuota = 2,
            ReservoirQuotaTTL = clock.ToDouble(clock.Now().Add(TimeSpan.FromSeconds(10))),
            RuleName = "rule1",
        };

        applier = applier.WithTarget(target, clock.Now());

        // 2 req/sec quota
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);
        Assert.Equal(SamplingDecision.Drop, applier.ShouldSample(default).Decision);
    }

    [Fact]
    public void TestWithTargetWithoutQuota()
    {
        TestClock clock = new TestClock();
        SamplingRule rule = new SamplingRule(
           "rule1",
           1,
           0.0, // fixed rate
           100, // reservoir
           "*",
           "*",
           "*",
           "*",
           "*",
           "*",
           1,
           new Dictionary<string, string>());

        var applier = new SamplingRuleApplier("clientId", clock, rule, new Statistics());

        // no target assigned yet. so borrow 1 from reservoir every second
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);
        Assert.Equal(SamplingDecision.Drop, applier.ShouldSample(default).Decision);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);
        Assert.Equal(SamplingDecision.Drop, applier.ShouldSample(default).Decision);

        var statistics = applier.Snapshot(clock.Now());
        Assert.Equal(4, statistics.RequestCount);
        Assert.Equal(2, statistics.SampledCount);
        Assert.Equal(2, statistics.BorrowCount);

        // get the target
        SamplingTargetDocument target = new SamplingTargetDocument
        {
            FixedRate = 1.0,
            Interval = 10,
            ReservoirQuota = null,
            ReservoirQuotaTTL = null,
            RuleName = "rule1",
        };
        applier = applier.WithTarget(target, clock.Now());

        // no reservoir, sample using fixed rate (100% sample)
        Assert.Equal(SamplingDecision.RecordAndSample, applier.ShouldSample(default).Decision);
        statistics = applier.Snapshot(clock.Now());
        Assert.Equal(1, statistics.RequestCount);
        Assert.Equal(1, statistics.SampledCount);
        Assert.Equal(0, statistics.BorrowCount);
    }
}
