// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

internal sealed class TestSegment : ReadOnlySequenceSegment<byte>
{
    public TestSegment(ReadOnlyMemory<byte> memory)
    {
        this.Memory = memory;
    }

    /// <summary>
    /// Splits the data into a sequence of segments of random lengths, some of them empty, so that anything
    /// which reads the sequence has to cope with a value that straddles any boundary between segments.
    /// </summary>
    /// <param name="data">The data to split.</param>
    /// <param name="seed">The seed of the random lengths.</param>
    /// <returns>A sequence of the data.</returns>
    public static ReadOnlySequence<byte> Split(byte[] data, int seed)
    {
        if (data.Length == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        var random = new Random(seed);
        var offset = 0;
        TestSegment? first = null;
        TestSegment? last = null;

        while (offset < data.Length)
        {
            var length = random.Next(0, 5) == 0 ? 0 : random.Next(1, Math.Min(data.Length - offset, 12) + 1);
            var segment = new TestSegment(new ReadOnlyMemory<byte>(data, offset, length));

            if (last is null)
            {
                first = segment;
            }
            else
            {
                last.Append(segment);
            }

            last = segment;
            offset += length;
        }

        return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
    }

    private void Append(TestSegment next)
    {
        next.RunningIndex = this.RunningIndex + this.Memory.Length;
        this.Next = next;
    }
}
