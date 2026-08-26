// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Parses the textual form of a trace sampling probability.
/// </summary>
/// <remarks>
/// Parsing uses the invariant culture and rejects values that underflow to zero.
/// </remarks>
internal static class TraceSamplingRateParser
{
    private const NumberStyles ProbabilityStyles = NumberStyles.Float & ~NumberStyles.AllowLeadingSign;

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
    /// <see langword="true"/> if the text represents a finite, non-negative number that does
    /// not underflow to zero; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string? text, out double probability)
    {
        probability = default;

        if (text is null)
        {
            return false;
        }

        if (!double.TryParse(text, ProbabilityStyles, CultureInfo.InvariantCulture, out probability))
        {
            return false;
        }

        // The NaN and infinity symbols are recognized whatever the number styles, and an
        // overflowing exponent yields infinity rather than a failed parse.
        if (double.IsNaN(probability)
            || double.IsInfinity(probability)
            || (probability == 0 && HasNonZeroSignificand(text)))
        {
            probability = default;
            return false;
        }

        return true;
    }

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
