// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Parses the textual form of a trace sampling probability.
/// </summary>
/// <remarks>
/// Parsing uses the invariant culture, permits surrounding white space and exponents, and
/// rejects negative values, group separators, and values that underflow to zero.
/// </remarks>
internal static class TraceSamplingRateParser
{
    /// <summary>
    /// Attempts to convert the textual form of a sampling probability into a number.
    /// </summary>
    /// <param name="text">
    /// The text to convert. Surrounding white space is permitted. May be
    /// <see langword="null"/>, which is treated as no number.
    /// </param>
    /// <param name="probability">
    /// When this method returns <see langword="true"/>, the parsed value; otherwise zero.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the text represents a supported non-negative number; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string? text, out double probability)
    {
        probability = default;

        if (text is null)
        {
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out probability))
        {
            return false;
        }

        if (double.IsNaN(probability)
            || double.IsInfinity(probability)
            || double.IsNegative(probability)
#if !NET && !NETSTANDARD2_1_OR_GREATER
            || (probability == 0 && HasNegativeSign(text))
#endif
            || (probability == 0 && HasNonZeroSignificand(text)))
        {
            probability = default;
            return false;
        }

        return true;
    }

#if !NET && !NETSTANDARD2_1_OR_GREATER
    private static bool HasNegativeSign(string text)
    {
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                return character == '-';
            }
        }

        return false;
    }
#endif

    private static bool HasNonZeroSignificand(string text)
    {
        foreach (var character in text)
        {
            if (character is 'e' or 'E')
            {
                return false;
            }

            if (character is >= '1' and <= '9')
            {
                return true;
            }
        }

        return false;
    }
}
