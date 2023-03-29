// <copyright file="TestAWSXRayRemoteSampler.cs" company="OpenTelemetry Authors">
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

public class TestAWSXRayRemoteSampler
{
    [Fact]
    public void TestSamplerWithConfiguration()
    {
        TimeSpan pollingInterval = TimeSpan.FromSeconds(5);
        string endpoint = "http://localhost:3000";

        AWSXRayRemoteSampler sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(pollingInterval)
            .SetEndpoint(endpoint)
            .Build();

        Assert.Equal(pollingInterval, sampler.PollingInterval);
        Assert.Equal(endpoint, sampler.Endpoint);
        Assert.NotNull(sampler.RulePollerTimer);
        Assert.NotNull(sampler.Client);
    }

    [Fact]
    public void TestSamplerWithDefaults()
    {
        AWSXRayRemoteSampler sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build()).Build();

        Assert.Equal(TimeSpan.FromMinutes(5), sampler.PollingInterval);
        Assert.Equal("http://localhost:2000", sampler.Endpoint);
        Assert.NotNull(sampler.RulePollerTimer);
        Assert.NotNull(sampler.Client);
    }

    [Fact]
    public void TestSamplerShouldSample()
    {
        Trace.Sampler sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build()).Build();

        // for now the fallback sampler should be making the sampling decision
        Assert.Equal(
            SamplingDecision.RecordAndSample,
            sampler.ShouldSample(Utils.CreateSamplingParametersWithTags(new Dictionary<string, string>())).Decision);
    }
}
