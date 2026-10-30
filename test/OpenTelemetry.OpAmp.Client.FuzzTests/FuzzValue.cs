// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

internal static class FuzzValue
{
    /// <summary>
    /// Makes a 64 bit value of any magnitude, which the default generator of FsCheck does not: it makes small values.
    /// </summary>
    /// <param name="high">The upper 32 bits.</param>
    /// <param name="low">The lower 32 bits.</param>
    /// <param name="shift">The number of bits to shift the value right by, so that values of every length are made.</param>
    /// <returns>The value.</returns>
    public static ulong UInt64(uint high, uint low, byte shift) =>
        ((((ulong)high) << 32) | low) >> (shift % 64);
}
