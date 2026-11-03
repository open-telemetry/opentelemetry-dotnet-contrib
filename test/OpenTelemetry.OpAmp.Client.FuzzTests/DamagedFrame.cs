// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

/// <summary>
/// The bytes of a message from the server which have been damaged in a way a faulty or malicious server might:
/// truncated, bytes changed, inserted or repeated, or replaced altogether, built from a seed so that a failure can be replayed.
/// </summary>
public sealed class DamagedFrame
{
    public DamagedFrame(int seed)
    {
        this.Seed = seed;

        var random = new Random(seed);
        var bytes = new List<byte>(new ServerFrame(random.Next()).Bytes);

        switch (random.Next(6))
        {
            case 0:
                // Truncated at any point.
                bytes = [.. bytes.Take(random.Next(0, bytes.Count + 1))];
                break;
            case 1:
                for (var i = random.Next(1, 6); i > 0 && bytes.Count > 0; i--)
                {
                    bytes[random.Next(bytes.Count)] = (byte)random.Next(256);
                }

                break;
            case 2:
                bytes.InsertRange(random.Next(0, bytes.Count + 1), ServerFrame.RandomBytes(random, 16));
                break;
            case 3:
                if (bytes.Count > 0)
                {
                    var start = random.Next(bytes.Count);
                    var length = random.Next(1, bytes.Count - start + 1);
                    bytes.InsertRange(random.Next(0, bytes.Count + 1), bytes.GetRange(start, length));
                }

                break;
            case 4:
                // Lengths which claim far more than there is.
                bytes.InsertRange(0, [0x0A, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F]);
                break;
            default:
                bytes = [.. ServerFrame.RandomBytes(random, 64)];
                break;
        }

        this.Bytes = [.. bytes];
        this.SplitSeed = random.Next();
    }

    public int Seed { get; }

    public int SplitSeed { get; }

    internal byte[] Bytes { get; }

    public override string ToString() => $"Seed: {this.Seed}, Bytes: {BitConverter.ToString(this.Bytes)}";
}
