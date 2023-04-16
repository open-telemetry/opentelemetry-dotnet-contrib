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

using System.Collections.Generic;
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
            resourceArn: "*",
            serviceName: "myServiceName",
            serviceType: "AWS::EC2::Instance",
            urlPath: "/helloworld",
            version: 1,
            attributes: new Dictionary<string, string>());

        var activityTags = new Dictionary<string, string>()
        {
            { "http.host", "localhost" },
            { "http.method", "GET" },
            { "http.url", @"http://127.0.0.1:5000/helloworld" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.True(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_ec2")));
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

        var activityTags = new Dictionary<string, string>()
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

        var activityTags = new Dictionary<string, string>()
        {
            { "http.target", "/helloworld" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.True(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", string.Empty)));
    }

    [Fact]
    public void TestAttributeMatching()
    {
        var ruleAttributes = new Dictionary<string, string>()
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

        var activityTags = new Dictionary<string, string>()
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
        var ruleAttributes = new Dictionary<string, string>()
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

        var activityTags = new Dictionary<string, string>()
        {
            { "http.target", "/helloworld" },
            { "dog", "bark" },
        };

        var applier = new SamplingRuleApplier("clientId", new TestClock(), rule, new Statistics());
        Assert.False(applier.Matches(Utils.CreateSamplingParametersWithTags(activityTags), Utils.CreateResource("myServiceName", "aws_ecs")));
    }

    // TODO: Add more test cases for ShouldSample once the sampling logic is added.
}
