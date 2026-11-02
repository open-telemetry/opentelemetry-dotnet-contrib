// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

/// <summary>
/// A 64 bit value of any magnitude, built from a seed so that a failure can be replayed.
/// </summary>
/// <remarks>
/// The default generators of FsCheck are bounded by the size of the test, which is 100 by default, so they only make
/// small values. Values which take seven to ten bytes to encode as a variable length integer would never be made.
/// The values here are spread evenly across the number of bits they have, and include the values either side of
/// each of the seven bit boundaries at which the number of bytes of the encoding changes.
/// </remarks>
public sealed class FuzzValue
{
    public FuzzValue(int seed)
    {
        this.Seed = seed;
        this.Value = Build(new Random(seed));
    }

    public int Seed { get; }

    public ulong Value { get; }

    public override string ToString() => $"Seed: {this.Seed}, Value: {this.Value} (0x{this.Value:X})";

    private static ulong Build(Random random)
    {
        if (random.Next(0, 4) == 0)
        {
            // Each byte of a variable length integer holds seven bits, so the length changes at 2^7, 2^14 ... 2^63.
            var boundary = 1UL << (7 * random.Next(1, 10));

            return random.Next(0, 3) switch
            {
                0 => boundary - 1,
                1 => boundary,
                _ => random.Next(0, 2) == 0 ? ulong.MaxValue : 0,
            };
        }

        var bytes = new byte[sizeof(ulong)];
        random.NextBytes(bytes);

        var value = BitConverter.ToUInt64(bytes, 0);
        var bits = random.Next(0, 65);

        return bits == 64 ? value : value & ((1UL << bits) - 1);
    }
}
