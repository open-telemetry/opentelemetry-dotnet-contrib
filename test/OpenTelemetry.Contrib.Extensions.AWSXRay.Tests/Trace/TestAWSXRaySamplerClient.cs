// <copyright file="TestAWSXRaySamplerClient.cs" company="OpenTelemetry Authors">
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
using System.IO;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Tests.Trace;

public class TestAWSXRaySamplerClient : IDisposable
{
    private WireMockServer mockServer;

    private AWSXRaySamplerClient client;

    public TestAWSXRaySamplerClient()
    {
        this.mockServer = WireMockServer.Start();
        this.client = new AWSXRaySamplerClient(this.mockServer.Url);
    }

    public void Dispose()
    {
        this.mockServer.Dispose();
        this.client.Dispose();
    }

    [Fact]
    public void TestGetSamplingRules()
    {
        this.CreateResponse("/GetSamplingRules", "Trace/Data/GetSamplingRulesResponse.json");

        var responseTask = this.client.GetSamplingRules();
        responseTask.Wait();

        List<SamplingRule> rules = responseTask.Result;

        Assert.Equal(3, rules.Count);

        Assert.Equal("Rule1", rules[0].RuleName);
        Assert.Equal(1000, rules[0].Priority);
        Assert.Equal(0.05, rules[0].FixedRate);
        Assert.Equal(10, rules[0].ReservoirSize);
        Assert.Equal("*", rules[0].Host);
        Assert.Equal("*", rules[0].HttpMethod);
        Assert.Equal("*", rules[0].ResourceArn);
        Assert.Equal("*", rules[0].ServiceName);
        Assert.Equal("*", rules[0].UrlPath);
        Assert.Equal(1, rules[0].Version);
        Assert.Equal(2, rules[0].Attributes.Count);
        Assert.Equal("bar", rules[0].Attributes["foo"]);
        Assert.Equal("baz", rules[0].Attributes["doo"]);

        Assert.Equal("Default", rules[1].RuleName);
        Assert.Equal(10000, rules[1].Priority);
        Assert.Equal(0.05, rules[1].FixedRate);
        Assert.Equal(1, rules[1].ReservoirSize);
        Assert.Equal("*", rules[1].Host);
        Assert.Equal("*", rules[1].HttpMethod);
        Assert.Equal("*", rules[1].ResourceArn);
        Assert.Equal("*", rules[1].ServiceName);
        Assert.Equal("*", rules[1].UrlPath);
        Assert.Equal(1, rules[1].Version);
        Assert.Empty(rules[1].Attributes);

        Assert.Equal("Rule2", rules[2].RuleName);
        Assert.Equal(1, rules[2].Priority);
        Assert.Equal(0.2, rules[2].FixedRate);
        Assert.Equal(10, rules[2].ReservoirSize);
        Assert.Equal("*", rules[2].Host);
        Assert.Equal("GET", rules[2].HttpMethod);
        Assert.Equal("*", rules[2].ResourceArn);
        Assert.Equal("FooBar", rules[2].ServiceName);
        Assert.Equal("/foo/bar", rules[2].UrlPath);
        Assert.Equal(1, rules[2].Version);
        Assert.Empty(rules[2].Attributes);
    }

    [Fact]
    public void TestGetSamplingRulesMalformed()
    {
        this.mockServer
            .Given(Request.Create().WithPath("/GetSamplingRules").UsingPost())
            .RespondWith(
                Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("notJson"));

        var responseTask = this.client.GetSamplingRules();
        responseTask.Wait();

        List<SamplingRule> rules = responseTask.Result;

        Assert.Empty(rules);
    }

    private void CreateResponse(string endpoint, string filePath)
    {
        string mockResponse = File.ReadAllText(filePath);
        this.mockServer
            .Given(Request.Create().WithPath(endpoint).UsingPost())
            .RespondWith(
                Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(mockResponse));
    }
}
