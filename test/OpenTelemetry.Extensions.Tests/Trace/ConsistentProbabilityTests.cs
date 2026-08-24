// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.Internal;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class ConsistentProbabilityTests
{
    // The worked example table from the specification, for 1-in-N probability sampling at
    // precision 3, 4 and 5.
    // https://opentelemetry.io/docs/specs/otel/trace/tracestate-probability-sampling/#converting-floating-point-probability-to-threshold-value
    [Theory]

    // 1-in-N, precision 3.
    [InlineData(1, 3, "0")]
    [InlineData(2, 3, "8")]
    [InlineData(3, 3, "aab")]
    [InlineData(4, 3, "c")]
    [InlineData(5, 3, "ccd")]
    [InlineData(8, 3, "e")]
    [InlineData(10, 3, "e66")]
    [InlineData(16, 3, "f")]
    [InlineData(100, 3, "fd71")]
    [InlineData(1000, 3, "ffbe7")]
    [InlineData(10000, 3, "fff972")]
    [InlineData(100000, 3, "ffff584")]
    [InlineData(1000000, 3, "ffffef4")]

    // 1-in-N, precision 4.
    [InlineData(1, 4, "0")]
    [InlineData(2, 4, "8")]
    [InlineData(3, 4, "aaab")]
    [InlineData(4, 4, "c")]
    [InlineData(5, 4, "cccd")]
    [InlineData(8, 4, "e")]
    [InlineData(10, 4, "e666")]
    [InlineData(16, 4, "f")]
    [InlineData(100, 4, "fd70a")]
    [InlineData(1000, 4, "ffbe77")]
    [InlineData(10000, 4, "fff9724")]
    [InlineData(100000, 4, "ffff583a")]
    [InlineData(1000000, 4, "ffffef39")]

    // 1-in-N, precision 5.
    [InlineData(1, 5, "0")]
    [InlineData(2, 5, "8")]
    [InlineData(3, 5, "aaaab")]
    [InlineData(4, 5, "c")]
    [InlineData(5, 5, "ccccd")]
    [InlineData(8, 5, "e")]
    [InlineData(10, 5, "e6666")]
    [InlineData(16, 5, "f")]
    [InlineData(100, 5, "fd70a4")]
    [InlineData(1000, 5, "ffbe76d")]
    [InlineData(10000, 5, "fff97247")]
    [InlineData(100000, 5, "ffff583a5")]
    [InlineData(1000000, 5, "ffffef391")]
    public void EncodeThreshold_MatchesSpecificationTable(int oneInN, int precision, string expected)
    {
        var probability = oneInN == 1 ? 1.0 : 1.0 / oneInN;

        var actual = ConsistentProbability.EncodeThreshold(probability, precision);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(double.PositiveInfinity)]
    public void EncodeThreshold_ReturnsZeroForProbabilityAtOrAboveOne(double probability)
        => Assert.Equal("0", ConsistentProbability.EncodeThreshold(probability, ConsistentProbability.DefaultPrecision));

    [Theory]
    [InlineData(0.5, "8")]
    [InlineData(0.25, "c")]
    [InlineData(0.125, "e")]
    [InlineData(0.0625, "f")]
    public void EncodeThreshold_EncodesExactBinaryFractions(double probability, string expected)
    {
        // Exact binary fractions are precision-independent.
        Assert.Equal(expected, ConsistentProbability.EncodeThreshold(probability, 3));
        Assert.Equal(expected, ConsistentProbability.EncodeThreshold(probability, 4));
        Assert.Equal(expected, ConsistentProbability.EncodeThreshold(probability, 13));
    }

    // Arbitrary-fraction cases verifying exact parity with the collector, including the full-precision
    // rounding quirk where 1/3 ends in "c" rather than a repeating "a".
    // https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/6d20534d0a232acaa8cf7161ddbaeab6915e0c01/pkg/sampling/probability_test.go#L19-L58
    [Theory]
    [InlineData(2.0 / 3.0, 3, "555")]
    [InlineData(1.0 / 3.0, 14, "aaaaaaaaaaaaac")]
    public void EncodeThreshold_MatchesCollectorFractionExamples(double probability, int precision, string expected)
        => Assert.Equal(expected, ConsistentProbability.EncodeThreshold(probability, precision));

    // The collector's full-precision encoding examples, exercising exact encoding for probabilities
    // near 1 (the frexp(1 - probability) precision boost) as well as near 0.
    // https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/6d20534d0a232acaa8cf7161ddbaeab6915e0c01/pkg/sampling/encoding_test.go#L61-L69
    [Theory]
    [InlineData(2.0 / 3.0, 14, "55555555555558")]
    [InlineData(1.0 - (1.0 / 256.0), 14, "01")] // 1 - 0x1p-8, near 1.
    [InlineData(1.0 - (84.0 / 256.0), 14, "54")] // 1 - 0x54p-8.
    [InlineData(1.0 - 2.2204460492503131e-16, 14, "0000000000001")] // 1 - 2^-52, the closest probability below 1.
    [InlineData(256.0 * 1.3877787807814457e-17, 14, "ffffffffffff")] // 0x100 * 2^-56 = 2^-48.
    public void EncodeThreshold_MatchesCollectorEncodingExamples(double probability, int precision, string expected)
        => Assert.Equal(expected, ConsistentProbability.EncodeThreshold(probability, precision));

    [Fact]
    public void ThresholdToProbability_MatchesCollectorExamples()
    {
        // https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/6d20534d0a232acaa8cf7161ddbaeab6915e0c01/pkg/sampling/encoding_test.go#L103-L110
        Assert.Equal(0.5, ConsistentProbability.ThresholdToProbability(ConsistentProbability.DecodeThreshold("8")));
        Assert.Equal(1.0, ConsistentProbability.ThresholdToProbability(ConsistentProbability.DecodeThreshold("0")));
        Assert.Equal(1.0 - (0x444 / 4096.0), ConsistentProbability.ThresholdToProbability(ConsistentProbability.DecodeThreshold("444")));
        Assert.Equal(1.0 - (1.0 / 3.0), ConsistentProbability.ThresholdToProbability(ConsistentProbability.DecodeThreshold("55555554")), 9);
    }

    [Fact]
    public void EncodeThreshold_EncodesVerySmallProbabilityExactly()
    {
        // The exact-integer encoding preserves the precise threshold for very small probabilities,
        // where the floating-point reference method loses precision or rounds down towards "0".
        const double Probability = 1e-15;

        var encoded = ConsistentProbability.EncodeThreshold(Probability, ConsistentProbability.DefaultPrecision);

        // 2^56 - round(1e-15 * 2^56) = 2^56 - 72 = 0xffffffffffffb8.
        Assert.Equal("ffffffffffffb8", encoded);
    }

    // The specification's "very small" example (precision 3), verifying exact parity with the collector.
    // https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/6d20534d0a232acaa8cf7161ddbaeab6915e0c01/pkg/sampling/probability_test.go#L163-L188
    [Theory]
    [InlineData(1, "ffffffffffffff")] // Skip 1 out of 2^56.
    [InlineData(2, "fffffffffffffe")] // Skip 2 out of 2^56.
    [InlineData(3, "fffffffffffffd")]
    [InlineData(4, "fffffffffffffc")]
    [InlineData(8, "fffffffffffff8")]
    [InlineData(16, "fffffffffffff")]
    public void EncodeThreshold_EncodesSmallestProbabilitiesExactly(int numerator, string expected)
    {
        var probability = numerator * Math.Pow(2, -56);

        Assert.Equal(expected, ConsistentProbability.EncodeThreshold(probability, 3));
    }

    [Theory]
    [InlineData(1e-7)]
    [InlineData(1e-9)]
    [InlineData(1e-15)]
    [InlineData(1.3877787807814457e-17)] // 2^-56, the smallest valid sampling probability.
    public void EncodeThreshold_ProducesValidThresholdForSmallProbabilities(double probability)
    {
        // Very small probabilities drive the precision to its maximum (14), exercising that bound.
        var encoded = ConsistentProbability.EncodeThreshold(probability, ConsistentProbability.DefaultPrecision);

        // A th value is 1 to 14 lowercase hexadecimal digits.
        Assert.InRange(encoded.Length, 1, ConsistentProbability.MaxHexDigits);
        Assert.All(encoded, c => Assert.True(c is (>= '0' and <= '9') or (>= 'a' and <= 'f')));

        // The decoded threshold stays within the valid 56-bit range and round-trips.
        var threshold = ConsistentProbability.DecodeThreshold(encoded);
        Assert.InRange(threshold, 1, ConsistentProbability.MaxRandomValue);
        Assert.Equal(encoded, ConsistentProbability.EncodeThresholdInteger(threshold));
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(0x80000000000000L, "8")] // 50%
    [InlineData(0xc0000000000000L, "c")] // 25%
    [InlineData(0xfd70a400000000L, "fd70a4")] // ~1%
    [InlineData(0x00ffffffffffffffL, "ffffffffffffff")]
    public void EncodeThresholdInteger_RemovesTrailingZeros(long threshold, string expected)
        => Assert.Equal(expected, ConsistentProbability.EncodeThresholdInteger(threshold));

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("8", 0x80000000000000L)] // "8" extended to 8000_0000_0000_00
    [InlineData("c", 0xc0000000000000L)]
    [InlineData("fd70a4", 0xfd70a400000000L)]
    [InlineData("ffffffffffffff", 0x00ffffffffffffffL)]
    public void DecodeThreshold_ExtendsWithTrailingZeros(string threshold, long expected)
        => Assert.Equal(expected, ConsistentProbability.DecodeThreshold(threshold));

    [Theory]
    [InlineData("0")]
    [InlineData("8")]
    [InlineData("c")]
    [InlineData("aaab")]
    [InlineData("fd70a")]
    [InlineData("fd70a4")]
    [InlineData("ffffef391")]
    public void EncodeAndDecodeThreshold_RoundTrips(string threshold)
    {
        var value = ConsistentProbability.DecodeThreshold(threshold);

        Assert.Equal(threshold, ConsistentProbability.EncodeThresholdInteger(value));
    }

    // From https://opentelemetry.io/docs/specs/otel/trace/tracestate-handling/#sampling-threshold-value-th
    [Theory]
    [InlineData("0", 1.0, 1.0)] // 100% sampling.
    [InlineData("8", 0.5, 2.0)] // 50% sampling.
    [InlineData("c", 0.25, 4.0)] // 25% sampling.
    public void Threshold_ConvertsToProbabilityAndAdjustedCount(string threshold, double probability, double adjustedCount)
    {
        var value = ConsistentProbability.DecodeThreshold(threshold);

        Assert.Equal(probability, ConsistentProbability.ThresholdToProbability(value));
        Assert.Equal(adjustedCount, ConsistentProbability.ThresholdToAdjustedCount(value));
    }

    // "Actual probability" and "Exact adjusted count" columns from the specification table (precision 5).
    [Theory]
    [InlineData("fd70a4", 0.009999990463256836, 100.00009536752259)]
    [InlineData("ffbe76d", 0.000999998301267624, 1000.0016987352618)]
    public void Threshold_MatchesSpecificationActualProbabilityAndAdjustedCount(string threshold, double probability, double adjustedCount)
    {
        var value = ConsistentProbability.DecodeThreshold(threshold);

        Assert.Equal(probability, ConsistentProbability.ThresholdToProbability(value), 12);
        Assert.Equal(adjustedCount, ConsistentProbability.ThresholdToAdjustedCount(value), 6);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("8", 8L)]
    [InlineData("f", 15L)]
    [InlineData("ff", 255L)]
    [InlineData("6e6d1a75832a2f", 0x6e6d1a75832a2fL)]
    [InlineData("ffffffffffffff", 0x00ffffffffffffffL)]
    public void TryParseHex56_ParsesValidValues(string value, long expected)
    {
        Assert.True(ConsistentProbability.TryParseHex56(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("g")] // Not a hexadecimal digit.
    [InlineData("12345678901234x")]
    [InlineData("123456789012345")] // 15 digits, exceeds 56 bits.
    [InlineData("ABCDEF")] // The specification requires lowercase hexadecimal digits.
    [InlineData("abcdeF")]
    public void TryParseHex56_RejectsInvalidValues(string? value)
    {
        Assert.False(ConsistentProbability.TryParseHex56(value, out var actual));
        Assert.Equal(0L, actual);
    }
}
