// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace OpenTelemetry.OpAmp.Client.Utils;

internal static class SequenceHelper
{
    public static ReadOnlyMemory<byte> GetHeaderSegment(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsEmpty)
        {
            return default;
        }

        if (sequence.First.Length < OpAmpWsHeaderHelper.MaxHeaderLength)
        {
            throw new InvalidOperationException("Sequence is too small to contain a valid header.");
        }

        return sequence.First.Slice(0, OpAmpWsHeaderHelper.MaxHeaderLength);
    }

    public static ReadOnlySequence<byte> CreateSequenceFromBuffers(IReadOnlyList<byte[]> buffers, int endIndex)
    {
        if (buffers == null || buffers.Count == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        BufferSegment segment1 = new BufferSegment(buffers[0]);
        BufferSegment currentSegment = segment1;

        for (int i = 1; i < buffers.Count; i++)
        {
            BufferSegment nextSegment = new BufferSegment(buffers[i]);

            currentSegment.SetNext(nextSegment);
            currentSegment = nextSegment;
        }

        return new ReadOnlySequence<byte>(segment1, 0, currentSegment, endIndex);
    }

    public static ReadOnlySequence<byte> AsSequence(this byte[] message)
    {
        if (message == null || message.Length == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        return new ReadOnlySequence<byte>(message);
    }
}
