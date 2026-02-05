// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva;

public static class MetricSerializerTests
{
    private const int MaxValue = 1_000;
    private const int BufferSize = 65_536;

    [Property(MaxTest = MaxValue)]
    public static void SerializeByte_Does_Not_Throw(byte value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = bufferOffset.Get % (BufferSize - sizeof(byte));

        // Act
        MetricSerializer.SerializeByte(buffer, ref offset, value);

        // Assert
        Assert.Equal(value, buffer[bufferOffset.Get % (BufferSize - sizeof(byte))]);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeByte_Increments_Offset_By_One(byte value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeByte(buffer, ref offset, value);

        // Assert
        Assert.Equal(sizeof(byte), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeByte_Buffer_Too_Small_Throws(byte value)
    {
        // Arrange
        var buffer = new byte[1];
        var offset = 1; // At boundary

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeByte(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Does_Not_Throw(ushort value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = bufferOffset.Get % (BufferSize - sizeof(ushort));

        // Act
        MetricSerializer.SerializeUInt16(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Increments_Offset(ushort value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt16(buffer, ref offset, value);

        // Assert
        Assert.Equal(sizeof(ushort), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Little_Endian_Encoding(ushort value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt16(buffer, ref offset, value);

        // Assert
        Assert.Equal((byte)value, buffer[0]);
        Assert.Equal((byte)(value >> 8), buffer[1]);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Buffer_Too_Small_Throws(ushort value)
    {
        // Arrange
        var buffer = new byte[1];
        var offset = 0; // Need 2 bytes but only have 1

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt16(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_At_Buffer_Boundary_Throws(ushort value)
    {
        // Arrange
        var buffer = new byte[2];
        var offset = 2; // At exact boundary

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt16(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt16_Does_Not_Throw(short value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = bufferOffset.Get % (BufferSize - sizeof(short));
        var expected = offset + 2;

        // Act
        MetricSerializer.SerializeInt16(buffer, ref offset, value);

        // Assert
        Assert.Equal(expected, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt16_Increments_Offset_By_Two(short value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt16(buffer, ref offset, value);

        // Assert
        Assert.Equal(sizeof(short), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt16_Buffer_Too_Small_Throws(short value)
    {
        // Arrange
        var buffer = new byte[1];
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt16(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Does_Not_Throw(uint value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = bufferOffset.Get % (BufferSize - sizeof(uint));
        var expected = offset + 4;

        // Act
        MetricSerializer.SerializeUInt32(buffer, ref offset, value);

        // Assert
        Assert.Equal(expected, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Increments_Offset_By_Four(uint value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt32(buffer, ref offset, value);

        // Assert
        Assert.Equal(sizeof(uint), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Little_Endian_Encoding(uint value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt32(buffer, ref offset, value);

        // Assert
        Assert.Equal((byte)value, buffer[0]);
        Assert.Equal((byte)(value >> 8), buffer[1]);
        Assert.Equal((byte)(value >> 0x10), buffer[2]);
        Assert.Equal((byte)(value >> 0x18), buffer[3]);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Buffer_Too_Small_Throws(uint value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 4, 3); // 0-3 bytes (less than needed)
        if (size == 0)
        {
            size = 1;
        }

        var buffer = new byte[size];
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt32(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Does_Not_Throw(ulong value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = bufferOffset.Get % (BufferSize - sizeof(ulong));
        var expected = offset + 8;

        // Act
        MetricSerializer.SerializeUInt64(buffer, ref offset, value);

        // Assert
        Assert.Equal(expected, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Increments_Offset_By_Eight(ulong value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt64(buffer, ref offset, value);

        // Assert
        Assert.Equal(sizeof(ulong), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Buffer_Too_Small_Throws(ulong value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 8, 7); // 0-7 bytes (less than needed)
        if (size == 0)
        {
            size = 1;
        }

        var buffer = new byte[size];
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64_Does_Not_Throw(long value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = bufferOffset.Get % (BufferSize - sizeof(long));
        var expected = offset + 8;

        // Act and Assert
        MetricSerializer.SerializeInt64(buffer, ref offset, value);

        // Assert
        Assert.Equal(expected, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64_Increments_Offset_By_Eight(long value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt64(buffer, ref offset, value);

        // Assert
        Assert.Equal(sizeof(long), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64_Buffer_Too_Small_Throws(long value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 8, 7);
        if (size == 0)
        {
            size = 1;
        }

        var buffer = new byte[size];
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt64(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Does_Not_Throw(NormalFloat value, NonNegativeInt bufferOffset)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = bufferOffset.Get % (BufferSize - sizeof(double));
        var expected = offset + 8;

        // Act and Assert
        MetricSerializer.SerializeFloat64(buffer, ref offset, value.Get);

        // Assert
        Assert.Equal(expected, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Increments_Offset_By_Eight(NormalFloat value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeFloat64(buffer, ref offset, value.Get);

        // Assert
        Assert.Equal(sizeof(double), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Handles_Special_Values()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var specialValues = new[]
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.MinValue,
            double.MaxValue,
            double.Epsilon,
            0.0,
            -0.0,
        };

        // Act and Assert
        foreach (var value in specialValues)
        {
            var offset = 0;
            MetricSerializer.SerializeFloat64(buffer, ref offset, value);
            Assert.Equal(8, offset);
        }
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Buffer_Too_Small_Throws(NormalFloat value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 8, 7);
        if (size == 0)
        {
            size = 1;
        }

        var buffer = new byte[size];
        var offset = 0;

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricSerializer.SerializeFloat64(buffer, ref offset, value.Get));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Null_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeString(buffer, ref offset, null);

        // Assert
        Assert.Equal(sizeof(short), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Empty_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeString(buffer, ref offset, string.Empty);

        // Assert
        Assert.Equal(sizeof(short), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Arbitrary_Input_Does_Not_Throw(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act and Assert
        MetricSerializer.SerializeString(buffer, ref offset, input.Get);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Updates_Offset_Correctly(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var expectedLength = System.Text.Encoding.UTF8.GetByteCount(input.Get);

        // Act
        MetricSerializer.SerializeString(buffer, ref offset, input.Get);

        // Assert
        Assert.Equal(sizeof(short) + expectedLength, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Special_Characters_Do_Not_Throw(char special)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var str = new string(special, 10);

        // Act
        MetricSerializer.SerializeString(buffer, ref offset, str);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Buffer_Too_Small_Throws(NonEmptyString input)
    {
        // Arrange
        var str = input.Get.Substring(0, Math.Min(input.Get.Length, 10)); // Limit string size
        var requiredSize = System.Text.Encoding.UTF8.GetByteCount(str) + sizeof(short);
        var bufferSize = Math.Max(1, requiredSize / 2); // Intentionally too small
        var buffer = new byte[bufferSize];
        var offset = 0;

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => MetricSerializer.SerializeString(buffer, ref offset, str));

        // Assert
        Assert.True(ex is ArgumentException or IndexOutOfRangeException, $"Unexpected exception type {ex.GetType()}.");
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Offset_Near_Buffer_End_Does_Not_Throw(NonEmptyString input, PositiveInt nearEnd)
    {
        // Arrange
        var buffer = new byte[100];
        var offset = Math.Min(nearEnd.Get % 100, 99); // Start near or at the end

        // Act
        MetricSerializer.SerializeString(buffer, ref offset, input.Get);

        // Assert
        Assert.True(offset >= 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeEncodedString_Does_Not_Throw(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var encodedValue = System.Text.Encoding.UTF8.GetBytes(input.Get);

        // Act
        MetricSerializer.SerializeEncodedString(buffer, ref offset, encodedValue);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeEncodedString_Updates_Offset_Correctly(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var encodedValue = System.Text.Encoding.UTF8.GetBytes(input.Get);

        // Act
        MetricSerializer.SerializeEncodedString(buffer, ref offset, encodedValue);

        // Assert
        Assert.Equal(sizeof(short) + encodedValue.Length, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeEncodedString_Buffer_Too_Small_Throws(NonEmptyString input)
    {
        // Arrange
        var encodedValue = System.Text.Encoding.UTF8.GetBytes(input.Get.Substring(0, Math.Min(input.Get.Length, 10)));
        var requiredSize = encodedValue.Length + sizeof(short);
        var bufferSize = Math.Max(1, requiredSize / 2);
        var buffer = new byte[bufferSize];
        var offset = 0;

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => MetricSerializer.SerializeEncodedString(buffer, ref offset, encodedValue));

        // Assert
        Assert.True(ex is ArgumentException or IndexOutOfRangeException, $"Unexpected exception type {ex.GetType()}.");
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBase128String_Null_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeBase128String(buffer, ref offset, null);

        // Assert
        Assert.Equal(2, offset); // Length byte only
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBase128String_Empty_Does_Not_Throw()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeBase128String(buffer, ref offset, string.Empty);

        // Assert
        Assert.Equal(2, offset); // Length byte only
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBase128String_Arbitrary_Input_Does_Not_Throw(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeBase128String(buffer, ref offset, input.Get);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBase128String_Buffer_Too_Small_Throws(NonEmptyString input)
    {
        // Arrange
        var str = input.Get.Substring(0, Math.Min(input.Get.Length, 5));
        var buffer = new byte[1]; // Very small buffer
        var offset = 0;

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => MetricSerializer.SerializeBase128String(buffer, ref offset, str));

        // Assert
        Assert.True(ex is ArgumentException or IndexOutOfRangeException, $"Unexpected exception type {ex.GetType()}.");
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32AsBase128_Does_Not_Throw(uint value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt32AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32AsBase128_Increments_Offset(uint value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt32AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
        Assert.True(offset <= 5); // Max 5 bytes for uint32 in base-128
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32AsBase128_Buffer_Too_Small_Throws(NonZeroInt value)
    {
        // Arrange
        var buffer = Array.Empty<byte>(); // May not be enough for all uint32 values
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt32AsBase128(buffer, ref offset, (uint)value.Get));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64AsBase128_Does_Not_Throw(ulong value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt64AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64AsBase128_Increments_Offset(ulong value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt64AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
        Assert.True(offset <= 10); // Max 10 bytes for uint64 in base-128
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64AsBase128_Buffer_Too_Small_Throws(ulong value)
    {
        // Arrange
        var buffer = Array.Empty<byte>();
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64AsBase128(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt32AsBase128_Does_Not_Throw(int value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt32AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt32AsBase128_Increments_Offset(int value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt32AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
        Assert.True(offset <= 10); // Max 10 bytes for signed values in base-128
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt32AsBase128_Buffer_Too_Small_Throws(int value)
    {
        // Arrange
        var buffer = Array.Empty<byte>();
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt32AsBase128(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64AsBase128_Does_Not_Throw(long value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt64AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64AsBase128_Increments_Offset(long value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt64AsBase128(buffer, ref offset, value);

        // Assert
        Assert.True(offset > 0);
        Assert.True(offset <= 10); // Max 10 bytes for signed int64 in base-128
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64AsBase128_Handles_Negative_Values(NegativeInt value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt64AsBase128(buffer, ref offset, value.Get);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64AsBase128_Buffer_Too_Small_Throws(long value)
    {
        // Arrange
        var buffer = Array.Empty<byte>();
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt64AsBase128(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpanOfBytes_Does_Not_Throw(NonEmptyArray<byte> data)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var bytes = data.Get;

        // Act
        var span = new Span<byte>(bytes);
        MetricSerializer.SerializeSpanOfBytes(buffer, ref offset, span, bytes.Length);

        // Assert
        Assert.Equal(bytes.Length, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpanOfBytes_Updates_Offset_Correctly(NonEmptyArray<byte> data)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var bytes = data.Get;
        var span = new Span<byte>(bytes);

        // Act
        MetricSerializer.SerializeSpanOfBytes(buffer, ref offset, span, bytes.Length);

        // Assert
        Assert.Equal(bytes.Length, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpanOfBytes_Copies_Data_Correctly(NonEmptyArray<byte> data)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var bytes = data.Get;
        var span = new Span<byte>(bytes);

        // Act
        MetricSerializer.SerializeSpanOfBytes(buffer, ref offset, span, bytes.Length);

        // Assert
        for (int i = 0; i < bytes.Length; i++)
        {
            Assert.Equal(bytes[i], buffer[i]);
        }
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpanOfBytes_Buffer_Too_Small_Throws(NonEmptyArray<byte> data)
    {
        // Arrange
        var bytes = data.Get;

        if (bytes.Length is 1)
        {
            bytes = [bytes[0], bytes[0]];
        }

        var bufferSize = Math.Max(1, bytes.Length / 2); // Intentionally too small
        var buffer = new byte[bufferSize];
        var offset = 0;

        // Act
        var ex = Assert.ThrowsAny<Exception>(() =>
        {
            var span = new Span<byte>(bytes);
            MetricSerializer.SerializeSpanOfBytes(buffer, ref offset, span, bytes.Length);
        });

        // Assert
        Assert.True(ex is ArgumentException or IndexOutOfRangeException, $"Unexpected exception type {ex.GetType()}.");
    }

    [Property(MaxTest = MaxValue)]
    public static void Multiple_Serializations_Do_Not_Overlap(byte b1, ushort u16, uint u32, ulong u64)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeByte(buffer, ref offset, b1);
        var offsetAfterByte = offset;
        MetricSerializer.SerializeUInt16(buffer, ref offset, u16);
        var offsetAfterUInt16 = offset;
        MetricSerializer.SerializeUInt32(buffer, ref offset, u32);
        var offsetAfterUInt32 = offset;
        MetricSerializer.SerializeUInt64(buffer, ref offset, u64);

        // Assert
        Assert.Equal(sizeof(byte), offsetAfterByte);
        Assert.Equal(sizeof(byte) + sizeof(ushort), offsetAfterUInt16);
        Assert.Equal(sizeof(byte) + sizeof(ushort) + sizeof(uint), offsetAfterUInt32);
        Assert.Equal(sizeof(byte) + sizeof(ushort) + sizeof(uint) + sizeof(ulong), offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void Multiple_Serializations_Buffer_Too_Small_Throws(byte b1, ushort u16, uint u32)
    {
        // Arrange
        var buffer = new byte[10]; // Not enough for all serializations
        var offset = 0;

        // Act and Assert
        MetricSerializer.SerializeByte(buffer, ref offset, b1);
        MetricSerializer.SerializeUInt16(buffer, ref offset, u16);
        MetricSerializer.SerializeUInt32(buffer, ref offset, u32);

        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64(buffer, ref offset, 12345UL));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64AsBase128_Zero_Uses_One_Byte()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt64AsBase128(buffer, ref offset, 0UL);

        // Assert
        Assert.Equal(1, offset);
        Assert.Equal(0, buffer[0]);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64AsBase128_Zero_Uses_One_Byte()
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;

        // Act
        MetricSerializer.SerializeInt64AsBase128(buffer, ref offset, 0L);

        // Assert
        Assert.Equal(1, offset);
        Assert.Equal(0, buffer[0]);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Unicode_Characters_Do_Not_Throw(NonEmptyString input)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var unicodeString = $"\uD83D\uDD25{input.Get}\uD83C\uDF89";

        // Act
        MetricSerializer.SerializeString(buffer, ref offset, unicodeString);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBase128String_Very_Long_String_Does_Not_Throw(PositiveInt length)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = 0;
        var count = Math.Min(length.Get, 1000);
        var longString = new string('a', count);

        // Act
        MetricSerializer.SerializeBase128String(buffer, ref offset, longString);

        // Assert
        Assert.True(offset > 0);
    }

    [Property(MaxTest = MaxValue)]
    public static void Serialization_With_Varying_Offset_Positions_Does_Not_Throw(PositiveInt startOffset, byte value)
    {
        // Arrange
        var buffer = new byte[BufferSize];
        var offset = startOffset.Get % (BufferSize / 2); // Start at various positions
        var expected = offset + 1;

        // Act
        MetricSerializer.SerializeByte(buffer, ref offset, value);

        // Assert
        Assert.Equal(expected, offset);
    }

    [Property(MaxTest = MaxValue)]
    public static void All_Serialize_Methods_Handle_Zero_Length_Buffer()
    {
        // Arrange
        var buffer = Array.Empty<byte>();
        var offset = 0;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeByte(buffer, ref offset, 1));
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt16(buffer, ref offset, 1));
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt16(buffer, ref offset, 1));
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt32(buffer, ref offset, 1));
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64(buffer, ref offset, 1));
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt64(buffer, ref offset, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricSerializer.SerializeFloat64(buffer, ref offset, 1.0));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeByte_Offset_Beyond_Buffer_Length(byte value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100); // Offset beyond buffer

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeByte(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Offset_Beyond_Buffer_Length(ushort value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt16(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Offset_Beyond_Buffer_Length(uint value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt32(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Offset_Beyond_Buffer_Length(ulong value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64_Offset_Beyond_Buffer_Length(long value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt64(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Offset_Beyond_Buffer_Length(NormalFloat value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricSerializer.SerializeFloat64(buffer, ref offset, value.Get));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Offset_Beyond_Buffer_Length(NonEmptyString input, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);
        var str = input.Get.Substring(0, Math.Min(input.Get.Length, 5));

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => MetricSerializer.SerializeString(buffer, ref offset, str));

        // Assert
        Assert.True(ex is ArgumentException or IndexOutOfRangeException, $"Unexpected exception type {ex.GetType()}.");
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64AsBase128_Offset_Beyond_Buffer_Length(ulong value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64AsBase128(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt64AsBase128_Offset_Beyond_Buffer_Length(long value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt64AsBase128(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeSpanOfBytes_Offset_Beyond_Buffer_Length(NonEmptyArray<byte> data, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);
        var bytes = data.Get;

        // Act
        var ex = Assert.ThrowsAny<Exception>(() =>
        {
            var span = new Span<byte>(bytes);
            MetricSerializer.SerializeSpanOfBytes(buffer, ref offset, span, bytes.Length);
        });

        // Assert
        Assert.True(ex is ArgumentException or IndexOutOfRangeException, $"Unexpected exception type {ex.GetType()}.");
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeByte_Offset_Exactly_At_Buffer_Length(byte value)
    {
        // Arrange
        var buffer = new byte[100];
        var offset = buffer.Length; // Exactly at boundary

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeByte(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt16_Offset_Exactly_At_Buffer_Length(ushort value)
    {
        // Arrange
        var buffer = new byte[100];
        var offset = buffer.Length;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt16(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Offset_Exactly_At_Buffer_Length(uint value)
    {
        // Arrange
        var buffer = new byte[100];
        var offset = buffer.Length;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt32(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Offset_Exactly_At_Buffer_Length(ulong value)
    {
        // Arrange
        var buffer = new byte[100];
        var offset = buffer.Length;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeFloat64_Offset_Exactly_At_Buffer_Length(NormalFloat value)
    {
        // Arrange
        var buffer = new byte[100];
        var offset = buffer.Length;

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricSerializer.SerializeFloat64(buffer, ref offset, value.Get));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeInt16_Offset_One_Byte_Before_Buffer_End(short value)
    {
        // Arrange - offset allows 1 byte but type needs 2
        var buffer = new byte[100];
        var offset = buffer.Length - 1;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeInt16(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32_Offset_Two_Bytes_Before_Buffer_End(uint value)
    {
        // Arrange - offset allows 2 bytes but type needs 4
        var buffer = new byte[100];
        var offset = buffer.Length - 2;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt32(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64_Offset_Four_Bytes_Before_Buffer_End(ulong value)
    {
        // Arrange - offset allows 4 bytes but type needs 8
        var buffer = new byte[100];
        var offset = buffer.Length - 4;

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt64(buffer, ref offset, value));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeString_Offset_Way_Beyond_Buffer_Length(NonEmptyString input, PositiveInt largeOffset)
    {
        // Arrange
        var buffer = new byte[100];
        var offset = 1000 + largeOffset.Get; // Way beyond buffer
        var str = input.Get.Substring(0, Math.Min(input.Get.Length, 3));

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => MetricSerializer.SerializeString(buffer, ref offset, str));

        // Assert
        Assert.True(ex is ArgumentException or IndexOutOfRangeException, $"Unexpected exception type {ex.GetType()}.");
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeEncodedString_Offset_Beyond_Buffer_Length(NonEmptyString input, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);
        var encodedValue = System.Text.Encoding.UTF8.GetBytes(input.Get.Substring(0, Math.Min(input.Get.Length, 5)));

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeEncodedString(buffer, ref offset, encodedValue));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeBase128String_Offset_Beyond_Buffer_Length(NonEmptyString input, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);
        var str = input.Get.Substring(0, Math.Min(input.Get.Length, 3));

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeBase128String(buffer, ref offset, str));
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt32AsBase128_Offset_Beyond_Buffer_Length(uint value, PositiveInt bufferSize)
    {
        // Arrange
        var size = Math.Min(bufferSize.Get % 100, 50);
        if (size == 0)
        {
            size = 10;
        }

        var buffer = new byte[size];
        var offset = size + (bufferSize.Get % 100);

        // Act and Assert
        Assert.Throws<IndexOutOfRangeException>(() => MetricSerializer.SerializeUInt32AsBase128(buffer, ref offset, value));
    }

    [Theory]
    [InlineData(0x80UL)]
    [InlineData(0x4000UL)]
    [InlineData(0x200000UL)]
    [InlineData(0xFFFFFFFUL)]
    [InlineData(ulong.MaxValue)]
    public static void SerializeUInt64AsBase128_SetsHighBit_WhenMoreBytesRequired(ulong value)
    {
        // Arrange
        var buffer = new byte[10];
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt64AsBase128(buffer, ref offset, value);

        // Assert - all bytes except the last should have the high bit (0x80) set
        for (int i = 0; i < offset - 1; i++)
        {
            Assert.NotEqual(0, buffer[i] & 0x80);
        }

        // The last byte should NOT have the high bit set
        Assert.Equal(0, buffer[offset - 1] & 0x80);
    }

    [Property(MaxTest = MaxValue)]
    public static void SerializeUInt64AsBase128_FuzzTest_RandomValues_VerifiesContinuationBit(ulong first, uint second)
    {
        // Arrange
        var buffer = new byte[10];

        var value = (first << 32) | second;
        var offset = 0;

        // Act
        MetricSerializer.SerializeUInt64AsBase128(buffer, ref offset, value);

        // Assert
        // Verify valid encoding length
        Assert.InRange(offset, 1, 10);

        // Verify the continuation bit (b |= 0x80) is set correctly
        if (value >= 128)
        {
            // Multi-byte encoding: all bytes except last must have 0x80 set
            for (int j = 0; j < offset - 1; j++)
            {
                Assert.NotEqual(0, buffer[j] & 0x80);
            }
        }

        // Last byte must never have continuation bit set
        Assert.Equal(0, buffer[offset - 1] & 0x80);
    }
}
