// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

internal static class OtlpProtobufSerializerHelper
{
    private const int Fixed64Size = 8;

    internal static Encoding Utf8Encoding => Encoding.UTF8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteStringTag(byte[] buffer, ref int currentPosition, string value, int fieldNumber)
    {
        int stringSize = Utf8Encoding.GetByteCount(value);

        currentPosition -= stringSize;

        _ = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, currentPosition);

        WriteLength(buffer, ref currentPosition, stringSize);

        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.LengthDelimited);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteEnumWithTag(byte[] buffer, ref int currentPosition, int fieldNumber, int value)
    {
        // Assuming 1 byte which matches the intended use.
        // Otherwise, need to first calculte the bytes needed.
        currentPosition--;
        WriteRawByte(buffer, currentPosition, (byte)value);
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Varint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteBoolWithTag(byte[] buffer, ref int currentPosition, int fieldNumber, bool value)
    {
        currentPosition--;
        WriteRawByte(buffer, currentPosition, value ? (byte)1 : (byte)0);
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Varint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteFixed64WithTag(byte[] buffer, ref int currentPosition, int fieldNumber, ulong value)
    {
        WriteRawLittleEndian64(buffer, ref currentPosition, value);
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Fixed64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteInt64WithTag(byte[] buffer, ref int currentPosition, int fieldNumber, ulong value)
    {
        WriteRawVarint64(buffer, ref currentPosition, value);
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Varint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteDoubleWithTag(byte[] buffer, ref int currentPosition, int fieldNumber, double value)
    {
        WriteRawLittleEndian64(buffer, ref currentPosition, (ulong)BitConverter.DoubleToInt64Bits(value));
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Fixed64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteLength(byte[] buffer, ref int currentPosition, int length)
    {
        WriteRawVarint32(buffer, ref currentPosition, length, (uint)length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawVarint32(byte[] buffer, ref int currentPosition, int length, uint value)
    {
        currentPosition -= ComputeLengthSize(length);

        var tempPosition = currentPosition;

        // Optimize for the common case of a single byte value
        if (value < 128 && tempPosition >= 0)
        {
            buffer[tempPosition] = (byte)value;
            return;
        }

        // Fast path when capacity is available
        while (tempPosition >= 0)
        {
            if (value > 127)
            {
                buffer[tempPosition++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }
            else
            {
                buffer[tempPosition++] = (byte)value;
                return;
            }
        }

        // Write byte individually
        // We dont refresh the buffer but this could be used when the buffer is refreshed.
        // Right now, it would simply fail.
        while (value > 127)
        {
            WriteRawByte(buffer, tempPosition, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        WriteRawByte(buffer, tempPosition, (byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawVarint64(byte[] buffer, ref int currentPosition, ulong value)
    {
        currentPosition -= ComputeRawVarint64Size(value);

        var tempPosition = currentPosition;

        // Optimize for the common case of a single byte value
        if (value < 128 && tempPosition >= 0)
        {
            buffer[tempPosition] = (byte)value;
            return;
        }

        // Fast path when capacity is available
        while (tempPosition >= 0)
        {
            if (value > 127)
            {
                buffer[tempPosition++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }
            else
            {
                buffer[tempPosition++] = (byte)value;
                return;
            }
        }

        // Write byte individually
        // We dont refresh the buffer but this could be used when the buffer is refreshed.
        // Right now, it would simply fail.
        while (value > 127)
        {
            WriteRawByte(buffer, tempPosition, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        WriteRawByte(buffer, tempPosition, (byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawByte(byte[] buffer, int currentPosition, byte value)
    {
        if (currentPosition < 0)
        {
            // TODO: handle insufficient space.
            // Refresh buffer?
        }

        buffer[currentPosition] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTag(byte[] buffer, ref int currentPosition, int fieldNumber, WireFormat.WireType type)
    {
        // Assuming 1 length here for our use case.
        // Otherwise, first need to calculate the size of the tag.
        WriteRawVarint32(buffer, ref currentPosition, 1, WireFormat.MakeTag(fieldNumber, type));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTagAndLengthPrefix(byte[] buffer, ref int currentPosition, int contentLength, int fieldNumber, WireFormat.WireType type)
    {
        WriteLength(buffer, ref currentPosition, contentLength);
        WriteTag(buffer, ref currentPosition, fieldNumber, type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawLittleEndian64(byte[] buffer, ref int currentPosition, ulong value)
    {
        currentPosition -= Fixed64Size;

        if (currentPosition >= 0)
        {
            Span<byte> span = new Span<byte>(buffer, currentPosition, Fixed64Size);

            BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        }
        else
        {
            // TODO: handle insufficient space.
            // Write manually byte by byte.
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeLengthSize(int length)
    {
        return ComputeRawVarint32Size((uint)length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeRawVarint32Size(uint value)
    {
        if ((value & (0xffffffff << 7)) == 0)
        {
            return 1;
        }

        if ((value & (0xffffffff << 14)) == 0)
        {
            return 2;
        }

        if ((value & (0xffffffff << 21)) == 0)
        {
            return 3;
        }

        if ((value & (0xffffffff << 28)) == 0)
        {
            return 4;
        }

        return 5;
    }

    public static int ComputeRawVarint64Size(ulong value)
    {
        if ((value & (0xffffffffffffffffL << 7)) == 0)
        {
            return 1;
        }

        if ((value & (0xffffffffffffffffL << 14)) == 0)
        {
            return 2;
        }

        if ((value & (0xffffffffffffffffL << 21)) == 0)
        {
            return 3;
        }

        if ((value & (0xffffffffffffffffL << 28)) == 0)
        {
            return 4;
        }

        if ((value & (0xffffffffffffffffL << 35)) == 0)
        {
            return 5;
        }

        if ((value & (0xffffffffffffffffL << 42)) == 0)
        {
            return 6;
        }

        if ((value & (0xffffffffffffffffL << 49)) == 0)
        {
            return 7;
        }

        if ((value & (0xffffffffffffffffL << 56)) == 0)
        {
            return 8;
        }

        if ((value & (0xffffffffffffffffL << 63)) == 0)
        {
            return 9;
        }

        return 10;
    }

    public static int ComputeTagSize(int fieldNumber)
    {
        return ComputeRawVarint32Size(WireFormat.MakeTag(fieldNumber, 0));
    }
}
