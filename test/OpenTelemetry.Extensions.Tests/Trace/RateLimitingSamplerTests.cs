// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class RateLimitingSamplerTests
{
    [Fact]
    public void ShouldSample_ReturnsRecordAndSample_WhenWithinRateLimit()
    {
        // Arrange
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: default,
            name: "TestOperation",
            kind: default,
            tags: null,
            links: null);

        var sampler = new RateLimitingSampler(5); // 5 trace per second
        int sampleIn = 0, sampleOut = 0;

        // Fire in 3 traces with a second, should all be sampled in

        for (var i = 0; i < 3; i++)
        {
            // Act
            var result = sampler.ShouldSample(in samplingParameters);
            switch (result.Decision)
            {
                case SamplingDecision.RecordAndSample:
                    sampleIn++;
                    break;
                case SamplingDecision.RecordOnly:
                    Assert.Fail("Unexpected decision");
                    break;
                case SamplingDecision.Drop:
                    sampleOut++;
                    break;
            }

            Thread.Sleep(333);
        }

        // Assert
        Assert.True(sampleIn == 3);
        Assert.True(sampleOut == 0);
    }

    [Fact]
    public async void ShouldFilter_WhenAboveRateLimit()
    {
        const int SAMPLE_RATE = 5; // 5 trace per second
        const int CYCLES = 500;

        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: default,
            name: "TestOperation",
            kind: default,
            tags: null,
            links: null);
        var sampler = new RateLimitingSampler(SAMPLE_RATE);
        int sampleIn = 0, sampleOut = 0;

        var startTime = DateTime.UtcNow;

        for (var i = 0; i < CYCLES; i++)
        {
            var result = sampler.ShouldSample(in samplingParameters);
            switch (result.Decision)
            {
                case SamplingDecision.RecordAndSample:
                    sampleIn++;
                    break;
                case SamplingDecision.RecordOnly:
                    Assert.Fail("Unexpected decision");
                    break;
                case SamplingDecision.Drop:
                    sampleOut++;
                    break;
            }

            // Task.Delay is limited by the OS Scheduler, so we can't guarantee the exact time
            await Task.Delay(5);
        }

        var timeTakenSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
        var approxSamples = Math.Floor(timeTakenSeconds * SAMPLE_RATE);
        // Assert - We should have sampled in 5 traces per second over duration
        // Adding in a fudge factor
        Assert.True(sampleIn > (approxSamples * 0.9) && sampleIn < (approxSamples * 1.2));
        Assert.True(sampleOut == (CYCLES - sampleIn));
    }
}
