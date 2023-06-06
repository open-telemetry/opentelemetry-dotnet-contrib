// <copyright file="TestRateLimitingSampler.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class TestRateLimitingSampler
{
    [Fact]
    public void TestLimitsRate()
    {
        TestClock clock = new TestClock();
        Trace.Sampler sampler = new RateLimitingSampler(1, clock);

        Assert.Equal(SamplingDecision.RecordAndSample, sampler.ShouldSample(Utils.CreateSamplingParameters()).Decision);

        // balance used up
        Assert.Equal(SamplingDecision.Drop, sampler.ShouldSample(Utils.CreateSamplingParameters()).Decision);

        // balance restore after 1 second, not yet
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(SamplingDecision.Drop, sampler.ShouldSample(Utils.CreateSamplingParameters()).Decision);

        // balance restored
        clock.Advance(TimeSpan.FromMilliseconds(900));
        Assert.Equal(SamplingDecision.RecordAndSample, sampler.ShouldSample(Utils.CreateSamplingParameters()).Decision);
    }
}
