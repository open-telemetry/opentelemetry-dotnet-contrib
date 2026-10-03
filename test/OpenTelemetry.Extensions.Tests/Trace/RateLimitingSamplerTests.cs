// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class RateLimitingSamplerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenMaxTracesPerSecondIsNotPositive(int maxTracesPerSecond)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimitingSampler(maxTracesPerSecond));

        Assert.Equal("maxTracesPerSecond", exception.ParamName);
    }

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
                default:
                    Assert.Fail("Unexpected value");
                    break;
            }

            Thread.Sleep(333);
        }

        // Assert
        Assert.Equal(3, sampleIn);
        Assert.Equal(0, sampleOut);
    }

    [Fact]
    public void ShouldFilter_WhenAboveRateLimit()
    {
        const int SampleRate = 5; // 5 traces per second
        const int Cycles = 500;
        const long IntervalMilliseconds = 5;

        var clock = new FakeClock();
        var sampler = CreateSampler(SampleRate, clock);
        int sampleIn = 0, sampleOut = 0;

        for (var i = 0; i < Cycles; i++)
        {
            switch (ShouldSample(sampler))
            {
                case SamplingDecision.RecordAndSample:
                    sampleIn++;
                    break;
                case SamplingDecision.Drop:
                    sampleOut++;
                    break;
                default:
                    Assert.Fail("Unexpected decision");
                    break;
            }

            clock.Advance(IntervalMilliseconds);
        }

        // The initial balance of SampleRate traces are all sampled in, then one more
        // trace is sampled in for every 1/SampleRate seconds that elapse between the
        // first and last sampling decision ((Cycles - 1) * 5ms = 2.495s => 12 traces).
        var expected = SampleRate + (int)((Cycles - 1) * IntervalMilliseconds * SampleRate / 1000);

        Assert.Equal(expected, sampleIn);
        Assert.Equal(Cycles - sampleIn, sampleOut);
    }

    [Fact]
    public void ShouldNotAccumulateBalanceAboveRateLimit_WhenIdle()
    {
        const int SampleRate = 5; // 5 traces per second

        var clock = new FakeClock();
        var sampler = CreateSampler(SampleRate, clock);

        // Spend the initial balance
        for (var i = 0; i < SampleRate; i++)
        {
            Assert.Equal(SamplingDecision.RecordAndSample, ShouldSample(sampler));
        }

        Assert.Equal(SamplingDecision.Drop, ShouldSample(sampler));

        // Being idle for longer than one second only replenishes the maximum balance
        clock.Advance(10_000);

        for (var i = 0; i < SampleRate; i++)
        {
            Assert.Equal(SamplingDecision.RecordAndSample, ShouldSample(sampler));
        }

        Assert.Equal(SamplingDecision.Drop, ShouldSample(sampler));

        // One more trace is allowed once 1/SampleRate seconds have elapsed
        clock.Advance((1000 / SampleRate) - 1);
        Assert.Equal(SamplingDecision.Drop, ShouldSample(sampler));

        clock.Advance(1);
        Assert.Equal(SamplingDecision.RecordAndSample, ShouldSample(sampler));
        Assert.Equal(SamplingDecision.Drop, ShouldSample(sampler));
    }

    private static RateLimitingSampler CreateSampler(int maxTracesPerSecond, FakeClock clock)
        => new(maxTracesPerSecond, (creditsPerSecond, maxBalance) => new RateLimiter(creditsPerSecond, maxBalance, clock.GetElapsedTicks, FakeClock.TicksPerSecond));

    private static SamplingDecision ShouldSample(RateLimitingSampler sampler)
    {
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: default,
            name: "TestOperation",
            kind: default,
            tags: null,
            links: null);

        return sampler.ShouldSample(in samplingParameters).Decision;
    }

    private sealed class FakeClock
    {
        public const long TicksPerSecond = 1000; // 1 tick = 1 millisecond

        private long elapsedTicks;

        public long GetElapsedTicks() => this.elapsedTicks;

        public void Advance(long milliseconds) => this.elapsedTicks += milliseconds;
    }
}
