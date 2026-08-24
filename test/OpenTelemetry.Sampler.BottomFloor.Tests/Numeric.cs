// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor.Tests;

/// <summary>
/// Numeric helpers the tests rely on that are not available on every target
/// framework the package supports. The library itself targets down to
/// <c>netstandard2.0</c> and <c>net462</c>, so the tests run there too; these
/// wrappers keep the assertions identical across all of them.
/// </summary>
internal static class Numeric
{
    /// <summary>
    /// Returns whether a value is neither NaN nor infinite.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when the value is finite.</returns>
    public static bool IsFinite(double value)
#if NET
        => double.IsFinite(value);
#else
        => !double.IsNaN(value) && !double.IsInfinity(value);
#endif

    /// <summary>
    /// Returns the largest value that compares less than <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to step down from.</param>
    /// <returns>The next representable value toward negative infinity.</returns>
    public static double BitDecrement(double value)
#if NET
        => Math.BitDecrement(value);
#else
        => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) - 1);
#endif
}
