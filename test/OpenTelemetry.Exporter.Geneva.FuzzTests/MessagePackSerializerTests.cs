// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva;

public static class MessagePackSerializerTests
{
    private const int MaxValue = 1_000;
    private const int BufferSize = 65_536;

    [Property(MaxTest = MaxValue)]
    public static void SerializeNull_Does_Not_Throw(NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 1);

        // Act
        var actual = MessagePackSerializer.SerializeNull(buffer, cursor);

        // Assert
        Assert.Equal(cursor + 1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeNull_Returns_Incremented_Cursor()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeNull(buffer, cursor);

        // Assert
        Assert.Equal(1, actual);
        Assert.Equal(MessagePackSerializer.NIL, buffer[0]);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBool_Does_Not_Throw(bool value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 1);

        // Act
        var actual = MessagePackSerializer.SerializeBool(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBool_Returns_Incremented_Cursor(bool value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeBool(buffer, cursor, value);

        // Assert
        Assert.Equal(1, actual);
        Assert.Equal(value ? MessagePackSerializer.TRUE : MessagePackSerializer.FALSE, buffer[cursor]);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt8_Does_Not_Throw(sbyte value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 2);

        // Act
        var actual = MessagePackSerializer.SerializeInt8(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 2);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt8_Increments_Cursor_Correctly(sbyte value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeInt8(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 2);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt16_Does_Not_Throw(short value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 3);

        // Act
        var actual = MessagePackSerializer.SerializeInt16(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 3);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt16_Increments_Cursor_Correctly(short value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeInt16(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 3);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt32_Does_Not_Throw(int value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 5);

        // Act
        var actual = MessagePackSerializer.SerializeInt32(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt32_Increments_Cursor_Correctly(int value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeInt32(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64_Does_Not_Throw(long value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 9);

        // Act
        var actual = MessagePackSerializer.SerializeInt64(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 9);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64_Increments_Cursor_Correctly(long value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeInt64(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 9);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt8_Does_Not_Throw(byte value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 2);

        // Act
        var actual = MessagePackSerializer.SerializeUInt8(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 2);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt8_Increments_Cursor_Correctly(byte value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeUInt8(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 2);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Does_Not_Throw(ushort value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 3);

        // Act
        var actual = MessagePackSerializer.SerializeUInt16(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 3);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Increments_Cursor_Correctly(ushort value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeUInt16(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 3);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Does_Not_Throw(uint value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 5);

        // Act
        var actual = MessagePackSerializer.SerializeUInt32(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Increments_Cursor_Correctly(uint value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeUInt32(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Does_Not_Throw(ulong value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 9);

        // Act
        var actual = MessagePackSerializer.SerializeUInt64(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 9);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Increments_Cursor_Correctly(ulong value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeUInt64(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 9);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat32_Does_Not_Throw(float value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 5);

        // Act
        var actual = MessagePackSerializer.SerializeFloat32(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 5, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat32_Increments_Cursor(float value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeFloat32(buffer, cursor, value);

        // Assert
        Assert.Equal(5, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Does_Not_Throw(NormalFloat value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 9);

        // Act
        var actual = MessagePackSerializer.SerializeFloat64(buffer, cursor, value.Get);

        // Assert
        Assert.Equal(cursor + 9, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Increments_Cursor(NormalFloat value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeFloat64(buffer, cursor, value.Get);

        // Assert
        Assert.Equal(9, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Handles_Special_Values()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        double[] specialValues =
        [
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.MinValue,
            double.MaxValue,
            double.Epsilon,
            0.0,
            -0.0,
        ];

        // Act and Assert
        foreach (var value in specialValues)
        {
            var cursor = 0;
            var actual = MessagePackSerializer.SerializeFloat64(buffer, cursor, value);
            Assert.Equal(9, actual);
        }
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeAsciiString_Null_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeAsciiString(buffer, cursor, null);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeAsciiString_Empty_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeAsciiString(buffer, cursor, string.Empty);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeAsciiString_Arbitrary_Input_With_Valid_Buffer(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var asciiString = new string([.. input.Get.Where(c => c < 128)]);

        if (string.IsNullOrEmpty(asciiString))
        {
            asciiString = "test";
        }

        // Act
        var actual = MessagePackSerializer.SerializeAsciiString(buffer, cursor, asciiString);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeAsciiString_Updates_Cursor_Correctly(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var asciiString = new string([.. input.Get.Where(c => c < 128).Take(100)]);

        if (string.IsNullOrEmpty(asciiString))
        {
            asciiString = "test";
        }

        // Act
        var actual = MessagePackSerializer.SerializeAsciiString(buffer, cursor, asciiString);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUnicodeString_Null_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, (string?)null);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUnicodeString_Empty_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, string.Empty);

        // Assert
        Assert.Equal(3, actual); // STR16 header (3 bytes) + 0 content bytes
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUnicodeString_Arbitrary_Input_Does_Not_Throw(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, input.Get);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUnicodeString_Updates_Cursor_Correctly(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var str = input.Get.Substring(0, Math.Min(input.Get.Length, 100));

        // Act
        var actual = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, str);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUnicodeString_Unicode_Characters_Do_Not_Throw(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var unicodeString = $"\uD83D\uDD25{input.Get}\uD83C\uDF89";

        // Act
        var actual = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, unicodeString);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteArrayHeader_Does_Not_Throw(NonNegativeInt length)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var arrayLength = length.Get % 10_000;

        // Act
        var actual = MessagePackSerializer.WriteArrayHeader(buffer, cursor, arrayLength);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteArrayHeader_Increments_Cursor_Correctly(NonNegativeInt length)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var arrayLength = length.Get % 10_000;

        // Act
        var actual = MessagePackSerializer.WriteArrayHeader(buffer, cursor, arrayLength);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteMapHeader_Does_Not_Throw(NonNegativeInt count)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var mapCount = count.Get % 10_000;

        // Act
        var actual = MessagePackSerializer.WriteMapHeader(buffer, cursor, mapCount);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteMapHeader_Increments_Cursor_Correctly(NonNegativeInt count)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var mapCount = count.Get % 10_000;

        // Act
        var actual = MessagePackSerializer.WriteMapHeader(buffer, cursor, mapCount);

        // Assert
        Assert.True(actual > cursor);
        Assert.True(actual <= cursor + 5);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeArray_Null_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeArray(buffer, cursor, (object[]?)null);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeArray_Empty_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var emptyArray = Array.Empty<object>();

        // Act
        var actual = MessagePackSerializer.SerializeArray(buffer, cursor, emptyArray);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeArray_With_Values_Does_Not_Throw(PositiveInt length)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var arrayLength = Math.Min(length.Get % 10, 5);
        var array = Enumerable.Range(0, arrayLength).Select(i => (object)i).ToArray();

        // Act
        var actual = MessagePackSerializer.SerializeArray(buffer, cursor, array);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeMap_Null_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeMap(buffer, cursor, null);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeMap_Empty_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var emptyMap = new Dictionary<string, object>();

        // Act
        var actual = MessagePackSerializer.SerializeMap(buffer, cursor, emptyMap);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeMap_With_Values_Does_Not_Throw(PositiveInt count)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var mapCount = Math.Min(count.Get % 10, 5);
        var map = Enumerable.Range(0, mapCount)
            .ToDictionary(i => $"key{i}", i => (object)i);

        // Act
        var actual = MessagePackSerializer.SerializeMap(buffer, cursor, map);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeTimestamp96_Does_Not_Throw(long ticks)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.SerializeTimestamp96(buffer, cursor, ticks);

        // Assert
        Assert.Equal(15, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeTimestamp96_Increments_Cursor()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        var actual = MessagePackSerializer.SerializeTimestamp96(buffer, cursor, ticks);

        // Assert
        Assert.Equal(15, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUtcDateTime_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var utc = DateTime.UtcNow;

        // Act
        var actual = MessagePackSerializer.SerializeUtcDateTime(buffer, cursor, utc);

        // Assert
        Assert.Equal(15, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Null_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, null);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Bool_Does_Not_Throw(bool value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Byte_Does_Not_Throw(byte value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_SByte_Does_Not_Throw(sbyte value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Int16_Does_Not_Throw(short value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_UInt16_Does_Not_Throw(ushort value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Int32_Does_Not_Throw(int value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_UInt32_Does_Not_Throw(uint value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Int64_Does_Not_Throw(long value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_UInt64_Does_Not_Throw(ulong value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Float_Does_Not_Throw(float value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_Double_Does_Not_Throw(NormalFloat value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, value.Get);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_String_Does_Not_Throw(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, input.Get);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_DateTime_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var dateTime = DateTime.UtcNow;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, dateTime);

        // Assert
        Assert.Equal(15, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialize_DateTimeOffset_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var dateTimeOffset = DateTimeOffset.UtcNow;

        // Act
        var actual = MessagePackSerializer.Serialize(buffer, cursor, dateTimeOffset);

        // Assert
        Assert.Equal(15, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpan_Empty_Span_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        var emptySpan = ReadOnlySpan<byte>.Empty;
        var actual = MessagePackSerializer.SerializeSpan(buffer, cursor, emptySpan);

        // Assert
        Assert.Equal(1, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpan_Does_Not_Throw(NonEmptyArray<byte> data)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var bytes = data.Get;

        // Act
        var span = new ReadOnlySpan<byte>(bytes);
        var actual = MessagePackSerializer.SerializeSpan(buffer, cursor, span);

        // Assert
        Assert.Equal(bytes.Length, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpan_Updates_Cursor_Correctly(NonEmptyArray<byte> data)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var bytes = data.Get;
        var span = new ReadOnlySpan<byte>(bytes);

        // Act
        var actual = MessagePackSerializer.SerializeSpan(buffer, cursor, span);

        // Assert
        Assert.Equal(bytes.Length, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteInt16_Does_Not_Throw(short value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 2);

        // Act
        var actual = MessagePackSerializer.WriteInt16(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 2, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteInt32_Does_Not_Throw(int value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 4);

        // Act
        var actual = MessagePackSerializer.WriteInt32(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 4, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteInt64_Does_Not_Throw(long value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 8);

        // Act
        var actual = MessagePackSerializer.WriteInt64(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 8, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteUInt16_Does_Not_Throw(ushort value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 2);

        // Act
        var actual = MessagePackSerializer.WriteUInt16(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 2, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteUInt32_Does_Not_Throw(uint value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 4);

        // Act
        var actual = MessagePackSerializer.WriteUInt32(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 4, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteUInt64_Does_Not_Throw(ulong value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = bufferOffset.Get % (BufferSize - 8);

        // Act
        var actual = MessagePackSerializer.WriteUInt64(buffer, cursor, value);

        // Assert
        Assert.Equal(cursor + 8, actual);
    }

    [Property(MaxTest = MaxValue)]
    public static void Multiple_Serializations_Do_Not_Overlap(byte b, short s, int i, long l)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;

        // Act
        cursor = MessagePackSerializer.SerializeUInt8(buffer, cursor, b);
        var cursorAfterByte = cursor;
        cursor = MessagePackSerializer.SerializeInt16(buffer, cursor, s);
        var cursorAfterShort = cursor;
        cursor = MessagePackSerializer.SerializeInt32(buffer, cursor, i);
        var cursorAfterInt = cursor;
        cursor = MessagePackSerializer.SerializeInt64(buffer, cursor, l);

        // Assert
        Assert.True(cursorAfterByte > 0);
        Assert.True(cursorAfterShort > cursorAfterByte);
        Assert.True(cursorAfterInt > cursorAfterShort);
        Assert.True(cursor > cursorAfterInt);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeNull_Cursor_Beyond_Buffer_Length(PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var cursor = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeNull(buffer, cursor));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBool_Cursor_Beyond_Buffer_Length(bool value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var cursor = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeBool(buffer, cursor, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt32_Cursor_Beyond_Buffer_Length(int value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var cursor = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeInt32(buffer, cursor, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Cursor_Beyond_Buffer_Length(ulong value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var cursor = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeUInt64(buffer, cursor, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Cursor_Beyond_Buffer_Length(NormalFloat value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var cursor = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeFloat64(buffer, cursor, value.Get));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUnicodeString_Cursor_Beyond_Buffer_Length(NonEmptyString input, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var cursor = size + (bufferSize.Get % 100);
        var str = input.Get.Substring(0, Math.Min(input.Get.Length, 5));

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeUnicodeString(buffer, cursor, str));
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteArrayHeader_Cursor_Exactly_At_Buffer_Length(NonNegativeInt length)
    {
        // Arrange
        var buffer = new byte[100];
        var cursor = buffer.Length;
        var arrayLength = length.Get % 100;

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.WriteArrayHeader(buffer, cursor, arrayLength));
    }

    [Property(MaxTest = MaxValue)]
    public static void WriteMapHeader_Cursor_Exactly_At_Buffer_Length(NonNegativeInt count)
    {
        // Arrange
        var buffer = new byte[100];
        var cursor = buffer.Length;
        var mapCount = count.Get % 100;

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.WriteMapHeader(buffer, cursor, mapCount));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeTimestamp96_Cursor_Beyond_Buffer_Length(PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var cursor = size + (bufferSize.Get % 100);
        var ticks = DateTime.UtcNow.Ticks;

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeTimestamp96(buffer, cursor, ticks));
    }

    [Property(MaxTest = MaxValue)]
    public static void All_Serialize_Methods_Handle_Zero_Length_Buffer()
    {
        // Arrange
        var buffer = Array.Empty<byte>();
        var cursor = 0;

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeNull(buffer, cursor));
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeBool(buffer, cursor, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeInt32(buffer, cursor, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeUInt64(buffer, cursor, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagePackSerializer.SerializeFloat64(buffer, cursor, 1.0));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeAsciiString_Very_Long_String_Does_Not_Throw(PositiveInt length)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var count = Math.Min(length.Get, 1_000);
        var longString = new string('a', count);

        // Act
        var actual = MessagePackSerializer.SerializeAsciiString(buffer, cursor, longString);

        // Assert
        Assert.True(actual > cursor);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUnicodeString_Very_Long_String_Does_Not_Throw(PositiveInt length)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var cursor = 0;
        var count = Math.Min(length.Get, 1_000);
        var longString = new string('a', count);

        // Act
        var actual = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, longString);

        // Assert
        Assert.True(actual > cursor);
    }
}
