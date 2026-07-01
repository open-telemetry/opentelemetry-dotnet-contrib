// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Extensions.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class ConsistentProbabilitySamplerTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.0000001)]
    [InlineData(2.0)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenProbabilityIsInvalid(double samplingProbability)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ConsistentProbabilitySampler(samplingProbability));

        Assert.Equal("samplingProbability", exception.ParamName);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.0001)]
    public void Constructor_AcceptsValidProbability(double samplingProbability)
    {
        var sampler = new ConsistentProbabilitySampler(samplingProbability);

        Assert.NotNull(sampler);
    }

    [Fact]
    public void Constructor_AcceptsSmallestValidProbability()
    {
        var sampler = new ConsistentProbabilitySampler(Math.Pow(2, -56));

        Assert.NotNull(sampler);
    }

    [Theory]
    [InlineData(0.5, "ConsistentProbabilitySampler{0.5}")]
    [InlineData(0.25, "ConsistentProbabilitySampler{0.25}")]
    [InlineData(1.0, "ConsistentProbabilitySampler{1}")]
    public void Description_DescribesTheProbability(double samplingProbability, string expected)
    {
        var sampler = new ConsistentProbabilitySampler(samplingProbability);

        Assert.Equal(expected, sampler.Description);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(0x80000000000000L)]
    [InlineData(0x00ffffffffffffffL)]
    public void ShouldSample_AlwaysSamplesWhenProbabilityIsOne(long randomness)
    {
        var parameters = CreateRootParameters();
        var sampler = new ConsistentProbabilitySampler(1.0, new FixedRandom(randomness));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Contains("th:0", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_SamplesWhenRandomnessEqualsThreshold()
    {
        // At 50% the rejection threshold is exactly 2^55.
        var parameters = CreateRootParameters();
        var sampler = new ConsistentProbabilitySampler(0.5, new FixedRandom(0x80000000000000L));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:8;rv:80000000000000", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_DropsWhenRandomnessBelowThreshold()
    {
        var parameters = CreateRootParameters();
        var sampler = new ConsistentProbabilitySampler(0.5, new FixedRandom(0x7fffffffffffffL));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.Drop, result.Decision);

        // The threshold is erased for an unsampled span, but the generated randomness is retained.
        Assert.Equal("ot=rv:7fffffffffffff", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_UsesExplicitRandomValueInsteadOfGenerator()
    {
        // The generator would drop (randomness 0), but the explicit rv is the maximum value.
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            traceState: "ot=rv:ffffffffffffff");

        var parameters = CreateParameters(parent);
        var sampler = new ConsistentProbabilitySampler(0.5, new FixedRandom(0L));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:8;rv:ffffffffffffff", result.TraceStateString);
    }

    [Theory]
    [InlineData("ffffffffffffff", SamplingDecision.RecordAndSample)]
    [InlineData("00000000000000", SamplingDecision.Drop)]
    public void ShouldSample_UsesTraceIdWhenRandomFlagIsSet(string trailing, SamplingDecision expected)
    {
        var traceId = ActivityTraceId.CreateFromString((new string('f', 18) + trailing).AsSpan());

        // 0x02 is the W3C Trace Context "random" flag.
        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), (ActivityTraceFlags)0x02);
        var parameters = CreateParameters(parent, traceId);

        // The generator would produce the opposite decision, proving the TraceId was used.
        var randomness = expected == SamplingDecision.RecordAndSample ? 0L : 0x00ffffffffffffffL;
        var sampler = new ConsistentProbabilitySampler(0.5, new FixedRandom(randomness));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(expected, result.Decision);

        if (expected == SamplingDecision.RecordAndSample)
        {
            // Randomness comes from the TraceID, so no explicit rv is added.
            Assert.Equal("ot=th:8", result.TraceStateString);
        }
    }

    [Fact]
    public void ShouldSample_GeneratesAndRecordsRandomValueForRootSpan()
    {
        var parameters = CreateRootParameters();
        var sampler = new ConsistentProbabilitySampler(0.5, new FixedRandom(0x90000000000000L));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:8;rv:90000000000000", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_PreservesOtherTraceStateMembers()
    {
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            traceState: "ot=rv:ffffffffffffff,vendor=abc");

        var parameters = CreateParameters(parent);
        var sampler = new ConsistentProbabilitySampler(0.5, new FixedRandom(0L));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal("ot=th:8;rv:ffffffffffffff,vendor=abc", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_IgnoresParentThresholdAndEncodesItsOwn()
    {
        // "A consistent probability sampling decision ignores the parent's sampling threshold (if
        // any)." The parent was sampled at 50% (th:8), but this sampler applies its own 25% (th:c).
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: "ot=th:8;rv:ffffffffffffff");

        var parameters = CreateParameters(parent);
        var sampler = new ConsistentProbabilitySampler(0.25, new FixedRandom(0L));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);

        // The outgoing threshold is this sampler's (th:c), not the parent's (th:8).
        Assert.Equal("ot=th:c;rv:ffffffffffffff", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_DropsIndependentlyOfParentThreshold()
    {
        // The parent was sampled at 100% (th:0), but an independent decision based on the shared
        // randomness (R = 0) drops the span at 50%, rather than inheriting the parent's decision.
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: "ot=th:0;rv:00000000000000");

        var parameters = CreateParameters(parent);
        var sampler = new ConsistentProbabilitySampler(0.5, new FixedRandom(0x00ffffffffffffffL));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.Drop, result.Decision);

        // The parent's threshold is erased because this span is not sampled here.
        Assert.Equal("ot=rv:00000000000000", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_IsConsistentAcrossProbabilities()
    {
        // A span kept at probability p1 must also be kept at any probability p2 >= p1, given the
        // same randomness value.
        const long Randomness = 0x90000000000000L; // ~56.25% into the range.
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            traceState: FormattableString.Invariant($"ot=rv:{Randomness:x14}"));

        var parameters = CreateParameters(parent);

        // The randomness comes from rv, so the generator value is irrelevant.
        Assert.Equal(SamplingDecision.RecordAndSample, Sample(0.5));
        Assert.Equal(SamplingDecision.RecordAndSample, Sample(0.75));
        Assert.Equal(SamplingDecision.RecordAndSample, Sample(1.0));

        // A much lower probability drops it.
        Assert.Equal(SamplingDecision.Drop, Sample(0.1));

        SamplingDecision Sample(double probability)
            => new ConsistentProbabilitySampler(probability, new FixedRandom(0L)).ShouldSample(in parameters).Decision;
    }

    [Fact]
    public void ShouldSample_ApproximatesConfiguredProbabilityWithDefaultGenerator()
    {
        const int Iterations = 100_000;
        const double Probability = 0.25;

        var parameters = CreateRootParameters();
        var sampler = new ConsistentProbabilitySampler(Probability, new Random(12345));

        var sampled = 0;
        for (var i = 0; i < Iterations; i++)
        {
            if (sampler.ShouldSample(in parameters).Decision == SamplingDecision.RecordAndSample)
            {
                sampled++;
            }
        }

        var fraction = (double)sampled / Iterations;

        Assert.InRange(fraction, Probability - 0.01, Probability + 0.01);
    }

    [Fact]
    public void ShouldSample_GeneratesRandomnessWithinValidRange()
    {
        var parameters = CreateRootParameters();
        var sampler = new ConsistentProbabilitySampler(0.5, new Random(98765));

        for (var i = 0; i < 10_000; i++)
        {
            var result = sampler.ShouldSample(in parameters);

            // The generated randomness is recorded as rv, which the parser only accepts when it is
            // exactly 14 hexadecimal digits, i.e. a valid 56-bit value in [0, 2^56).
            var state = OtelTraceState.Parse(result.TraceStateString);

            Assert.True(state.HasRandomValue);
            Assert.InRange(state.RandomValue, 0L, ConsistentProbability.MaxRandomValue);
        }
    }

    private static SamplingParameters CreateRootParameters()
        => CreateParameters(default, ActivityTraceId.CreateRandom());

    private static SamplingParameters CreateParameters(ActivityContext parentContext)
        => CreateParameters(parentContext, ActivityTraceId.CreateRandom());

    private static SamplingParameters CreateParameters(ActivityContext parentContext, ActivityTraceId traceId)
        => new(parentContext, traceId, "TestOperation", ActivityKind.Internal, tags: null, links: null);

    private sealed class FixedRandom : Random
    {
        private readonly long value;

        public FixedRandom(long value)
        {
            this.value = value;
        }

        public override void NextBytes(byte[] buffer) => this.Fill(buffer);

#if NET
        public override long NextInt64(long minValue, long maxValue) => this.value;

        public override void NextBytes(Span<byte> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = i < 7 ? (byte)((this.value >> (8 * (6 - i))) & 0xFF) : (byte)0;
            }
        }
#endif

        private void Fill(byte[] buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = i < 7 ? (byte)((this.value >> (8 * (6 - i))) & 0xFF) : (byte)0;
            }
        }
    }
}
