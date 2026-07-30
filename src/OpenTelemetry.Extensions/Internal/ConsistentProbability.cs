// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Extensions.Internal;

/// <summary>
/// Helpers for converting between sampling probabilities, 56-bit rejection thresholds and their
/// hexadecimal <c>th</c>/<c>rv</c> encodings, following the OpenTelemetry
/// <see href="https://opentelemetry.io/docs/specs/otel/trace/tracestate-probability-sampling/">
/// probability sampling</see> and
/// <see href="https://opentelemetry.io/docs/specs/otel/trace/tracestate-handling/">tracestate handling</see>
/// specifications.
/// </summary>
internal static class ConsistentProbability
{
    /// <summary>
    /// The maximum number of hexadecimal digits used to encode a 56-bit value.
    /// </summary>
    public const int MaxHexDigits = 14;

    /// <summary>
    /// The default encoding precision recommended by the specification.
    /// </summary>
    public const int DefaultPrecision = 4;

    /// <summary>
    /// <c>2^56</c>, the number of distinct 56-bit values (the maximum adjusted count).
    /// </summary>
    public const long MaxAdjustedCount = 1L << 56;

    /// <summary>
    /// The largest valid randomness value, <c>2^56 - 1</c>.
    /// </summary>
    public const long MaxRandomValue = MaxAdjustedCount - 1;

    /// <summary>
    /// Encodes a sampling probability as a <c>th</c> value using the specified precision.
    /// </summary>
    /// <param name="probability">The sampling probability, in the range <c>(0, 1]</c>.</param>
    /// <param name="precision">The number of significant hexadecimal digits, in the range <c>[1, 14]</c>.</param>
    /// <returns>The threshold encoded with trailing zeros removed (for example <c>fd70a</c>).</returns>
    /// <remarks>
    /// This computes the exact 56-bit rejection threshold directly from the probability, matching the
    /// OpenTelemetry Collector implementation rather than the (less accurate) floating-point reference
    /// pseudocode in the specification, so that values near <c>0</c> and <c>1</c> are encoded exactly:
    /// <see href="https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/6d20534d0a232acaa8cf7161ddbaeab6915e0c01/pkg/sampling/probability.go#L33-L77">
    /// ProbabilityToThresholdWithPrecision</see>.
    /// </remarks>
    public static string EncodeThreshold(double probability, int precision)
    {
        if (probability >= 1.0)
        {
            // Special case: 100% sampling has a rejection threshold of zero.
            return "0";
        }

        // Raise the precision by the number of leading '0' or 'f' digits so the configured precision
        // applies to the significant digits of the threshold near both 0 and 1. frexp returns an
        // exponent <= 0; every multiple of -4 corresponds to another leading '0' or 'f' hex digit.
        var exponentFraction = FrexpExponent(probability);
        var exponentRejection = FrexpExponent(1.0 - probability);

        precision = Math.Min(MaxHexDigits, Math.Max(precision + (exponentFraction / -4), precision + (exponentRejection / -4)));

        // Compute the rejection threshold as a 56-bit integer: T = 2^56 - round(probability * 2^56).
        var scaled = (long)Math.Round(probability * MaxAdjustedCount, MidpointRounding.AwayFromZero);
        var threshold = MaxAdjustedCount - scaled;

        // Round to the requested precision by dropping the low hex digits, rounding to nearest.
        var shift = 4 * (MaxHexDigits - precision);

        if (shift > 0)
        {
            var half = 1L << (shift - 1);
            threshold = ((threshold + half) >> shift) << shift;
        }

        return EncodeThresholdInteger(threshold);
    }

    /// <summary>
    /// Encodes a 56-bit integer rejection threshold as a <c>th</c> value, with trailing zeros removed.
    /// </summary>
    /// <param name="threshold">The rejection threshold, in the range <c>[0, 2^56)</c>.</param>
    /// <returns>The encoded threshold (for example <c>8</c> for 50% sampling).</returns>
    public static string EncodeThresholdInteger(long threshold)
    {
        if (threshold <= 0)
        {
            return "0";
        }

        const string Format = "x14"; // 14 hex digits, no leading "0x"

#if NET
        Span<char> buffer = stackalloc char[MaxHexDigits];

        _ = threshold.TryFormat(buffer, out var written, Format, CultureInfo.InvariantCulture);

        var trimmed = buffer.Slice(0, written).TrimEnd('0');

        return trimmed.IsEmpty ? "0" : new string(trimmed);
#else
        var hex = threshold.ToString(Format, CultureInfo.InvariantCulture).TrimEnd('0');
        return hex.Length == 0 ? "0" : hex;
#endif
    }

    /// <summary>
    /// Decodes a <c>th</c> value into a 56-bit integer rejection threshold by extending it with
    /// trailing zeros to 14 digits and parsing the result.
    /// </summary>
    /// <param name="threshold">The encoded threshold (1 to 14 hexadecimal digits).</param>
    /// <returns>The rejection threshold, in the range <c>[0, 2^56)</c>.</returns>
    public static long DecodeThreshold(string threshold)
    {
        _ = TryDecodeThreshold(threshold.AsSpan(), out var value);
        return value;
    }

    /// <summary>
    /// Attempts to decode a <c>th</c> value into a 56-bit integer rejection threshold.
    /// </summary>
    /// <param name="threshold">The encoded threshold (1 to 14 hexadecimal digits).</param>
    /// <param name="value">The rejection threshold when successful; otherwise zero.</param>
    /// <returns><see langword="true"/> if the value was decoded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecodeThreshold(ReadOnlySpan<char> threshold, out long value)
    {
        if (threshold.IsEmpty || threshold.Length > MaxHexDigits || !TryParseHex56(threshold, out var parsed))
        {
            value = 0;
            return false;
        }

        // Extend the value with trailing zeros to 14 digits, i.e. shift left by 4 bits per omitted digit.
        var shift = 4 * (MaxHexDigits - threshold.Length);
        value = shift > 0 ? parsed << shift : parsed;

        return true;
    }

    /// <summary>
    /// Parses a hexadecimal string of 1 to 14 digits into its integer value.
    /// </summary>
    /// <param name="value">The hexadecimal string.</param>
    /// <param name="result">The parsed value when successful; otherwise zero.</param>
    /// <returns><see langword="true"/> if the value was parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseHex56(string? value, out long result)
        => TryParseHex56(value.AsSpan(), out result);

    /// <summary>
    /// Parses a hexadecimal span of 1 to 14 digits into its integer value.
    /// </summary>
    /// <param name="value">The hexadecimal characters.</param>
    /// <param name="result">The parsed value when successful; otherwise zero.</param>
    /// <returns><see langword="true"/> if the value was parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseHex56(ReadOnlySpan<char> value, out long result)
    {
        result = 0;

        if (value.IsEmpty || value.Length > MaxHexDigits)
        {
            return false;
        }

        long parsed = 0;

        foreach (var ch in value)
        {
            var digit = ch switch
            {
                >= '0' and <= '9' => ch - '0',
                >= 'a' and <= 'f' => ch - 'a' + 10,
                >= 'A' and <= 'F' => ch - 'A' + 10,
                _ => -1,
            };

            if (digit < 0)
            {
                return false;
            }

            parsed = (parsed << 4) | (long)digit;
        }

        result = parsed;
        return true;
    }

    /// <summary>
    /// Calculates the sampling probability represented by a rejection threshold.
    /// </summary>
    /// <param name="threshold">The rejection threshold, in the range <c>[0, 2^56)</c>.</param>
    /// <returns>
    /// The sampling probability, in the range <c>(0, 1]</c>.
    /// </returns>
    /// <remarks>
    /// Per the specification: <c>Probability = (MaxAdjustedCount - Threshold) / MaxAdjustedCount</c>.
    /// </remarks>
    public static double ThresholdToProbability(long threshold)
        => (double)(MaxAdjustedCount - threshold) / MaxAdjustedCount;

    /// <summary>
    /// Calculates the adjusted count (inverse sampling probability) for a rejection threshold.
    /// </summary>
    /// <param name="threshold">The rejection threshold, in the range <c>[0, 2^56)</c>.</param>
    /// <returns>
    /// The adjusted count.
    /// </returns>
    /// <remarks>
    /// Per the specification: <c>AdjustedCount = MaxAdjustedCount / (MaxAdjustedCount - Threshold)</c>.
    /// </remarks>
    public static double ThresholdToAdjustedCount(long threshold)
        => (double)MaxAdjustedCount / (MaxAdjustedCount - threshold);

    /// <summary>
    /// Returns the exponent that <c>math.frexp</c> would produce for a positive value in <c>(0, 1]</c>,
    /// i.e. the value <c>e</c> such that <c>value = m * 2^e</c> with <c>0.5 &lt;= m &lt; 1</c>.
    /// </summary>
    private static int FrexpExponent(double value)
    {
        // value is a positive, normal double in (0, 1] (1.0 arises when 1 - probability rounds up).
#if NET
        return Math.ILogB(value) + 1;
#else
        var bits = BitConverter.DoubleToInt64Bits(value);
        var biasedExponent = (int)((bits >> 52) & 0x7FF);

        // frexp normalises the mantissa to [0.5, 1) rather than [1, 2), so its exponent is one
        // greater than the unbiased IEEE-754 exponent (biasedExponent - 1023).
        return biasedExponent - 1022;
#endif
    }
}
