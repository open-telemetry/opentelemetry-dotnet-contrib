// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class MetricSerializerTests
{
#pragma warning disable CA1825 // Avoid zero-length array allocations
    public static TheoryData<double> Float64TestCases =>
    [
        3.14,
        0.0,
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
        6.0,
    ];
#pragma warning restore CA1825 // Avoid zero-length array allocations

    [Theory]
    [MemberData(nameof(Float64TestCases))]
    public void SerializeFloat64_RoundTripsCorrectly(double expectedValue)
    {
        var buffer = new byte[16];
        int index = 0;

        MetricSerializer.SerializeFloat64(buffer, ref index, expectedValue);

        Assert.Equal(8, index);

        long actualBits = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expectedValue), actualBits);

        double actualValue = BitConverter.Int64BitsToDouble(actualBits);

        if (double.IsNaN(expectedValue))
        {
            Assert.True(double.IsNaN(actualValue));
        }
        else
        {
            Assert.Equal(expectedValue, actualValue);
        }
    }

    [Fact]
    public void SerializeFloat64_NegativeZero_PreservesBitPattern()
    {
        var buffer = new byte[16];
        int index = 0;
        double negativeZero = -0.0;

        MetricSerializer.SerializeFloat64(buffer, ref index, negativeZero);

        long actual = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8));
        Assert.Equal(BitConverter.DoubleToInt64Bits(negativeZero), actual);

        // -0.0 and +0.0 compare as equal but have different bit patterns
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(0.0), actual);
    }

    [Fact]
    public void SerializeFloat64_WritesLittleEndian()
    {
        // 1.0 has IEEE 754 bit pattern 0x3FF0000000000000
        // In little-endian: 00 00 00 00 00 00 F0 3F
        var buffer = new byte[16];
        int index = 0;

        MetricSerializer.SerializeFloat64(buffer, ref index, 1.0);

        Assert.Equal(0x00, buffer[0]);
        Assert.Equal(0x00, buffer[1]);
        Assert.Equal(0x00, buffer[2]);
        Assert.Equal(0x00, buffer[3]);
        Assert.Equal(0x00, buffer[4]);
        Assert.Equal(0x00, buffer[5]);
        Assert.Equal(0xF0, buffer[6]);
        Assert.Equal(0x3F, buffer[7]);
    }
}
