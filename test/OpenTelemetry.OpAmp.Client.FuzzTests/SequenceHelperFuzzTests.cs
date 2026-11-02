// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using FsCheck.Xunit;
using OpenTelemetry.OpAmp.Client.Internal.Utils;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

/// <summary>
/// Property-based (fuzz) tests that assert the sequence a WebSocket message is read from contains exactly the bytes received,
/// however many buffers they were received into.
/// </summary>
public static class SequenceHelperFuzzTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest)]
    public static void CreateSequenceFromBuffers_Contains_Exactly_The_Bytes_Received(byte[] data, int seed)
    {
        var random = new Random(seed);
        var buffers = new List<byte[]>();
        var endIndex = 0;
        var offset = 0;

        // As when receiving, every buffer but the last is full and the last has room to spare.
        while (offset < data.Length)
        {
            var buffer = new byte[random.Next(1, 40)];
            var used = Math.Min(buffer.Length, data.Length - offset);

            Array.Copy(data, offset, buffer, 0, used);
            buffers.Add(buffer);
            offset += used;
            endIndex = used;
        }

        var sequence = buffers.CreateSequenceFromBuffers(endIndex);

        Assert.Equal(data.Length, sequence.Length);
        Assert.Equal(data, sequence.ToArray());
    }

    [Property(MaxTest = MaxTest)]
    public static void AsSequence_Contains_Exactly_The_Bytes_Given(byte[] data)
    {
        Assert.Equal(data, data.AsSequence().ToArray());
    }
}
