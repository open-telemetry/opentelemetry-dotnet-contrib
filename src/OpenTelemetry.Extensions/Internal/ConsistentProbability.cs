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
    /// This is a direct port of the reference <c>probability_to_threshold_with_precision</c>
    /// algorithm from the specification.
    /// </summary>
    /// <param name="probability">The sampling probability, in the range <c>(0, 1]</c>.</param>
    /// <param name="precision">The number of significant hexadecimal digits, in the range <c>[1, 13]</c>.</param>
    /// <returns>The threshold encoded with trailing zeros removed (for example <c>fd70a</c>).</returns>
    public static string EncodeThreshold(double probability, int precision)
    {
        if (probability >= 1.0)
        {
            // Special case: 100% sampling has a rejection threshold of zero.
            return "0";
        }

        // math.frexp(probability): probability is a positive value less than one, so the
        // exponent is less than or equal to zero.
        var exponent = FrexpExponent(probability);

        // Raise the precision by the number of leading 'f' digits so the configured precision
        // applies to the significant digits of the threshold for probabilities near zero.
        precision = Math.Max(1, Math.Min(13, precision + ((-exponent) / 4)));

        // Change the probability into 1 + rejection probability, mapping (0, 1] into [1, 2).
        var rejectionProbability = 2.0 - probability;

        // Add half of the final digit of precision so the truncation below rounds correctly.
        // math.ldexp(0.5, -4 * precision) == 2^(-4 * precision - 1).
        rejectionProbability += Exp2((-4 * precision) - 1);

        // The mantissa is the 13 hexadecimal digits that Python's float.hex() emits after the
        // leading "0x1." for a value in [1, 2).
        var mantissa = BitConverter.DoubleToInt64Bits(rejectionProbability) & 0xF_FFFF_FFFF_FFFFL;

        const string Format = "x13"; // 13 hex digits, no leading "0x"

#if NET
        Span<char> digits = stackalloc char[13];

        if (rejectionProbability >= 2.0)
        {
            // Compensating for leading 'f' digits can technically never
            // produce a value >= 2.0, but guard against it for safety.
            digits.Fill('f');
        }
        else
        {
            _ = mantissa.TryFormat(digits, out _, Format, CultureInfo.InvariantCulture);
        }

        // Keep the requested precision and drop trailing zeros.
        var threshold = digits.Slice(0, precision).TrimEnd('0');

        return threshold.IsEmpty ? "0" : new string(threshold);
#else
        // Compensating for leading 'f' digits can technically never
        // produce a value >= 2.0, but guard against it for safety.
        var digits = rejectionProbability >= 2.0
            ? "fffffffffffff"
            : mantissa.ToString(Format, CultureInfo.InvariantCulture);

        // Keep the requested precision and drop trailing zeros.
        var threshold = digits.Substring(0, precision).TrimEnd('0');

        return threshold.Length == 0 ? "0" : threshold;
#endif
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
    /// Returns the exponent that <c>math.frexp</c> would produce for a positive value in <c>(0, 1)</c>,
    /// i.e. the value <c>e</c> such that <c>value = m * 2^e</c> with <c>0.5 &lt;= m &lt; 1</c>.
    /// </summary>
    private static int FrexpExponent(double value)
    {
        // value is a positive, normal double less than one.
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

    /// <summary>
    /// Returns <c>2^exponent</c> exactly for the small negative exponents used when encoding
    /// thresholds, avoiding the rounding of <see cref="Math.Pow(double, double)"/> and the
    /// <c>Math.ScaleB</c> API which is unavailable on older target frameworks.
    /// </summary>
    private static double Exp2(int exponent)
    {
        var bits = (long)(exponent + 1023) << 52;
        return BitConverter.Int64BitsToDouble(bits);
    }
}
