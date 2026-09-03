// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class ProtobufSerializerHelperTests
{
    [Theory]
    [InlineData(0, 0x80, 0x80, 0x00)]
    [InlineData(1, 0x81, 0x80, 0x00)]
    [InlineData(127, 0xFF, 0x80, 0x00)]
    [InlineData(128, 0x80, 0x81, 0x00)]
    [InlineData(16383, 0xFF, 0xFF, 0x00)]
    [InlineData(16384, 0x80, 0x80, 0x01)]
    [InlineData(2097151, 0xFF, 0xFF, 0x7F)]
    public void WriteLengthCustomWritesFixedWidthVarint(int length, byte first, byte second, byte third)
    {
        var buffer = new byte[3];
        var cursor = 0;

        ProtobufSerializerHelper.WriteLengthCustom(buffer, ref cursor, length);

        Assert.Equal(3, cursor);
        Assert.Equal([first, second, third], buffer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2097152)]
    public void WriteLengthCustomRejectsValuesOutsideThreeByteRange(int length)
    {
        var buffer = new byte[3];
        var cursor = 0;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ProtobufSerializerHelper.WriteLengthCustom(buffer, ref cursor, length));

        Assert.Equal(nameof(length), exception.ParamName);
        Assert.True(GenevaBufferOverflowExceptionHelper.IsMetricSerializerBufferOverflow(exception));
        Assert.Equal(0, cursor);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void WriteLengthCustomRejectsInvalidCursor(int cursor)
    {
        var buffer = new byte[3];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ProtobufSerializerHelper.WriteLengthCustom(buffer, ref cursor, 0));

        Assert.Equal(nameof(cursor), exception.ParamName);
        Assert.True(GenevaBufferOverflowExceptionHelper.IsMetricSerializerBufferOverflow(exception));
        Assert.Equal([0, 0, 0], buffer);
    }
}
