// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using FsCheck;
using FsCheck.Xunit;
using OpenTelemetry.Extensions.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions;

/// <summary>
/// Property-based (fuzz) tests that assert invariants of the consistent probability sampler and its
/// supporting codec hold across a large number of randomized inputs.
/// </summary>
public static class ConsistentProbabilityFuzzTests
{
    private const int MaxValue = 1_000;

    [Property(MaxTest = MaxValue)]
    public static void EncodeThreshold_ProducesValidRoundTrippableThreshold(double rawProbability, PositiveInt rawPrecision)
    {
        var probability = ToProbability(rawProbability);
        var precision = ((rawPrecision.Get - 1) % 13) + 1; // 1 to 13.

        var encoded = ConsistentProbability.EncodeThreshold(probability, precision);

        // A th value is 1 to 14 lowercase hexadecimal digits.
        Assert.InRange(encoded.Length, 1, ConsistentProbability.MaxHexDigits);
        Assert.All(encoded, c => Assert.True(c is (>= '0' and <= '9') or (>= 'a' and <= 'f')));

        // The decoded threshold is a valid 56-bit value and the encoding is a stable fixed point.
        var threshold = ConsistentProbability.DecodeThreshold(encoded);
        Assert.InRange(threshold, 0L, ConsistentProbability.MaxRandomValue);
        Assert.Equal(encoded, ConsistentProbability.EncodeThresholdInteger(threshold));
    }

    [Property(MaxTest = MaxValue)]
    public static void EncodeThresholdInteger_And_DecodeThreshold_RoundTrip(ulong rawThreshold)
    {
        var threshold = (long)(rawThreshold % (ulong)ConsistentProbability.MaxAdjustedCount); // [0, 2^56).

        var encoded = ConsistentProbability.EncodeThresholdInteger(threshold);

        Assert.InRange(encoded.Length, 1, ConsistentProbability.MaxHexDigits);
        Assert.Equal(threshold, ConsistentProbability.DecodeThreshold(encoded));
    }

    [Property(MaxTest = MaxValue)]
    public static void TryParseHex56_NeverThrowsAndStaysInRange(string? input)
    {
        // A throw would fail the property; the parser must tolerate any input.
        var parsed = ConsistentProbability.TryParseHex56(input, out var value);

        if (parsed)
        {
            Assert.InRange(input!.Length, 1, ConsistentProbability.MaxHexDigits);
            Assert.InRange(value, 0L, ConsistentProbability.MaxRandomValue);
        }
        else
        {
            Assert.Equal(0L, value);
        }
    }

    [Property(MaxTest = MaxValue)]
    public static void OtelTraceState_ParseAndSerialize_AreIdempotent(string? input)
    {
        // Parsing arbitrary input must never throw.
        var first = OtelTraceState.Parse(input);
        var serialized = first.Serialize();

        // Re-parsing the serialized form preserves the th/rv semantics...
        var second = OtelTraceState.Parse(serialized);

        Assert.Equal(first.HasThreshold, second.HasThreshold);
        Assert.Equal(first.HasRandomValue, second.HasRandomValue);
        Assert.Equal(first.Threshold, second.Threshold);
        Assert.Equal(first.RandomValue, second.RandomValue);

        // ...and serialization is a stable fixed point.
        Assert.Equal(serialized, second.Serialize());
    }

    [Property(MaxTest = MaxValue)]
    public static void ShouldSample_DecisionMatchesResolvedRandomness(double rawProbability, ulong rawRandomness, byte modeSelector, bool recorded)
    {
        var probability = ToProbability(rawProbability);
        var expectedThreshold = ConsistentProbability.DecodeThreshold(
            ConsistentProbability.EncodeThreshold(probability, ConsistentProbability.DefaultPrecision));

        // 0 = explicit rv, 1 = random TraceId flag, 2 = generated randomness.
        var mode = modeSelector % 3;
        var explicitRandomness = (long)(rawRandomness % (ulong)ConsistentProbability.MaxAdjustedCount);

        var traceId = ActivityTraceId.CreateRandom();
        _ = ConsistentProbability.TryParseHex56(traceId.ToHexString().AsSpan(18), out var traceIdRandomness);

        var traceState = mode == 0 ? "ot=rv:" + Hex14(explicitRandomness) : null;

        var flags = ActivityTraceFlags.None;
        if (mode == 1)
        {
            flags |= (ActivityTraceFlags)0x2; // W3C "random" flag.
        }

        if (recorded)
        {
            flags |= ActivityTraceFlags.Recorded;
        }

        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), flags, traceState);
        var parameters = new SamplingParameters(parent, traceId, "operation", ActivityKind.Internal, tags: null, links: null);

        var result = new ConsistentProbabilitySampler(probability).ShouldSample(in parameters);

        // The output must always be parseable.
        var outgoing = OtelTraceState.Parse(result.TraceStateString);

        var randomness = mode switch
        {
            0 => explicitRandomness,
            1 => traceIdRandomness,
            _ => outgoing.RandomValue,
        };

        if (mode == 2)
        {
            Assert.True(outgoing.HasRandomValue, "Generated randomness should be recorded as rv.");
        }

        var expected = randomness >= expectedThreshold ? SamplingDecision.RecordAndSample : SamplingDecision.Drop;

        Assert.Equal(expected, result.Decision);

        if (expected == SamplingDecision.RecordAndSample)
        {
            Assert.True(outgoing.HasThreshold);
            Assert.Equal(expectedThreshold, outgoing.Threshold);
        }
        else
        {
            Assert.False(outgoing.HasThreshold);
        }
    }

    [Property(MaxTest = MaxValue)]
    public static void ShouldSample_IsMonotonicInProbabilityForSharedRandomness(double rawProbabilityA, double rawProbabilityB, ulong rawRandomness)
    {
        var lower = Math.Min(ToProbability(rawProbabilityA), ToProbability(rawProbabilityB));
        var higher = Math.Max(ToProbability(rawProbabilityA), ToProbability(rawProbabilityB));

        // A fixed, shared randomness value makes the decisions comparable across probabilities.
        var randomness = (long)(rawRandomness % (ulong)ConsistentProbability.MaxAdjustedCount);
        var traceState = "ot=rv:" + Hex14(randomness);
        var parent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, traceState);
        var parameters = new SamplingParameters(parent, ActivityTraceId.CreateRandom(), "operation", ActivityKind.Internal, tags: null, links: null);

        var lowerDecision = new ConsistentProbabilitySampler(lower).ShouldSample(in parameters).Decision;
        var higherDecision = new ConsistentProbabilitySampler(higher).ShouldSample(in parameters).Decision;

        // A span kept at the lower probability must also be kept at the higher probability.
        if (lowerDecision == SamplingDecision.RecordAndSample)
        {
            Assert.Equal(SamplingDecision.RecordAndSample, higherDecision);
        }
    }

    private static double ToProbability(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1.0;
        }

        // Map any finite double into the valid (0, 1] range so the constructor never rejects it.
        value = Math.Abs(value);
        var fraction = value - Math.Floor(value); // [0, 1).
        var probability = fraction == 0.0 ? 1.0 : fraction;

        return Math.Min(1.0, Math.Max(Math.Pow(2, -56), probability));
    }

    private static string Hex14(long value) => value.ToString("x14", CultureInfo.InvariantCulture);
}
