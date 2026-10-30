// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck.Xunit;
using Google.Protobuf;
using OpenTelemetry.OpAmp.Client.Internal.Utils;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

/// <summary>
/// Property-based (fuzz) tests that assert the variable length integer which starts an OpAMP WebSocket message
/// is read and written as Protocol Buffers does, wherever the bytes of it are split between the buffers it is read from.
/// </summary>
public static class Varint64FuzzTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest)]
    public static void Encode_Matches_The_Protobuf_Encoding(uint high, uint low, byte shift)
    {
        var value = FuzzValue.UInt64(high, low, shift);
        using var stream = new MemoryStream();

        using (var output = new CodedOutputStream(stream, leaveOpen: true))
        {
            output.WriteUInt64(value);
        }

        Assert.Equal(stream.ToArray(), Varint64.Encode(value));
    }

    [Property(MaxTest = MaxTest)]
    public static void Encode_Then_TryDecode_Round_Trips_Whatever_The_Segmentation(uint high, uint low, byte shift, int seed)
    {
        var value = FuzzValue.UInt64(high, low, shift);
        var encoded = Varint64.Encode(value);

        var decoded = Varint64.TryDecode(TestSegment.Split(encoded, seed), out var bytesRead, out var result, out var errorMessage);

        Assert.True(decoded, errorMessage);
        Assert.Equal(value, result);
        Assert.Equal(encoded.Length, bytesRead);
    }

    [Property(MaxTest = MaxTest)]
    public static void TryDecode_Agrees_With_The_Protobuf_Reader(byte[] data, int seed, bool forceContinuation)
    {
        if (forceContinuation && data.Length > 0)
        {
            // Random bytes only rarely have a value of 10 bytes or more, which is where the limit is.
            for (var i = 0; i < data.Length - 1; i++)
            {
                data[i] |= 0x80;
            }
        }

        var (expectedOk, expectedValue, expectedRead) = Reference(data);

        var actualOk = Varint64.TryDecode(TestSegment.Split(data, seed), out var bytesRead, out var result, out var errorMessage);

        Assert.Equal(expectedOk, actualOk);

        if (actualOk)
        {
            Assert.Equal(expectedValue, result);
            Assert.Equal(expectedRead, bytesRead);
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(errorMessage));
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void TryDecode_Never_Reads_More_Than_There_Is(byte[] data, int seed)
    {
        var sequence = TestSegment.Split(data, seed);

        var decoded = Varint64.TryDecode(sequence, out var bytesRead, out _, out _);

        Assert.InRange(bytesRead, 0, (int)sequence.Length);

        if (decoded)
        {
            Assert.InRange(bytesRead, 1, 10);
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void TryVerifyHeader_Accepts_Only_A_Header_Of_Zero(byte[] data, int seed, bool forceContinuation)
    {
        if (forceContinuation && data.Length > 0)
        {
            for (var i = 0; i < data.Length - 1; i++)
            {
                data[i] |= 0x80;
            }
        }

        var (expectedOk, expectedValue, expectedRead) = Reference(data);

        var verified = OpAmpWsHeaderHelper.TryVerifyHeader(TestSegment.Split(data, seed), out var headerSize, out var errorMessage);

        Assert.Equal(expectedOk && expectedValue == 0, verified);

        if (verified)
        {
            Assert.Equal(expectedRead, headerSize);
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(errorMessage));
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void WriteHeader_Writes_A_Header_Which_Is_Verified(byte[] padding, int seed)
    {
        var buffer = new byte[OpAmpWsHeaderHelper.MaxHeaderLength + padding.Length];

        var written = OpAmpWsHeaderHelper.WriteHeader(new ArraySegment<byte>(buffer, 0, buffer.Length));

        var verified = OpAmpWsHeaderHelper.TryVerifyHeader(TestSegment.Split(buffer.Take(written).ToArray(), seed), out var headerSize, out var errorMessage);

        Assert.True(verified, errorMessage);
        Assert.Equal(written, headerSize);
    }

    private static (bool Ok, ulong Value, int Read) Reference(byte[] data)
    {
        try
        {
            var input = new CodedInputStream(data);
            var value = input.ReadUInt64();

            return (true, value, (int)input.Position);
        }
        catch (InvalidProtocolBufferException)
        {
            return (false, 0, 0);
        }
    }
}
