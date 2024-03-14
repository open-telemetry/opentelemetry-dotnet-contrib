// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

internal static class ProtobufSerializerHelper
{
    private const int Fixed64Size = 8;

    internal static Encoding Utf8Encoding => Encoding.UTF8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteStringTag(byte[] buffer, ref int currentPosition, string value, int fieldNumber)
    {
        int stringSize = Utf8Encoding.GetByteCount(value);

        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.LengthDelimited);

        WriteLength(buffer, ref currentPosition, stringSize);

        _ = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, currentPosition);

        currentPosition += stringSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteEnumWithTag(byte[] buffer, ref int currentPosition, int fieldNumber, int value)
    {
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Varint);

        // Assuming 1 byte which matches the intended use.
        // Otherwise, need to first calculate the bytes needed.
        WriteRawByte(buffer, ref currentPosition, (byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteBoolWithTag(byte[] buffer, ref int currentPosition, int fieldNumber, bool value)
    {
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Varint);
        WriteRawByte(buffer, ref currentPosition, value ? (byte)1 : (byte)0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteFixed64WithTag(byte[] buffer, ref int currentPosition, int fieldNumber, ulong value)
    {
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Fixed64);
        WriteRawLittleEndian64(buffer, ref currentPosition, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteInt64WithTag(byte[] buffer, ref int currentPosition, int fieldNumber, ulong value)
    {
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Varint);
        WriteRawVarint64(buffer, ref currentPosition, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteDoubleWithTag(byte[] buffer, ref int currentPosition, int fieldNumber, double value)
    {
        WriteTag(buffer, ref currentPosition, fieldNumber, WireFormat.WireType.Fixed64);
        WriteRawLittleEndian64(buffer, ref currentPosition, (ulong)BitConverter.DoubleToInt64Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteLengthCustom(byte[] buffer, ref int currentPosition, int length)
    {
        WriteRawVarintCustom(buffer, ref currentPosition, (uint)length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteLength(byte[] buffer, ref int currentPosition, int length)
    {
        WriteRawVarint32(buffer, ref currentPosition, (uint)length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawVarintCustom(byte[] buffer, ref int currentPosition, uint value)
    {
        int index = 0;

        // Loop until all 7 bits from the integer value have been encoded
        while (value > 0)
        {
            byte chunk = (byte)(value & 0x7F); // Extract the least significant 7 bits
            value >>= 7; // Right shift the value by 7 bits to process the next chunk

            // If there are more bits to encode, set the most significant bit to 1
            if (index < 3)
            {
                chunk |= 0x80;
            }

            buffer[currentPosition++] = chunk; // Store the encoded chunk
            index++;
        }

        // If fewer than 3 bytes were used, pad with zeros
        while (index < 2)
        {
            buffer[currentPosition++] = 0x80;
            index++;
        }

        while (index < 3)
        {
            buffer[currentPosition++] = 0x00;
            index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawVarint32(byte[] buffer, ref int currentPosition, uint value)
    {
        // Optimize for the common case of a single byte value
        if (value < 128 && currentPosition < buffer.Length)
        {
            buffer[currentPosition++] = (byte)value;
            return;
        }

        // Fast path when capacity is available
        while (currentPosition < buffer.Length)
        {
            if (value > 127)
            {
                buffer[currentPosition++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }
            else
            {
                buffer[currentPosition++] = (byte)value;
                return;
            }
        }

        // Write byte individually
        // We dont refresh the buffer but this could be used when the buffer is refreshed.
        // Right now, it would simply fail.
        while (value > 127)
        {
            WriteRawByte(buffer, ref currentPosition, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        WriteRawByte(buffer, ref currentPosition, (byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawVarint64(byte[] buffer, ref int currentPosition, ulong value)
    {
        // Optimize for the common case of a single byte value
        if (value < 128 && currentPosition < buffer.Length)
        {
            buffer[currentPosition++] = (byte)value;
            return;
        }

        // Fast path when capacity is available
        while (currentPosition < buffer.Length)
        {
            if (value > 127)
            {
                buffer[currentPosition++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }
            else
            {
                buffer[currentPosition++] = (byte)value;
                return;
            }
        }

        // Write byte individually
        // We dont refresh the buffer but this could be used when the buffer is refreshed.
        // Right now it would simply fail.
        while (value > 127)
        {
            WriteRawByte(buffer, ref currentPosition, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        WriteRawByte(buffer, ref currentPosition, (byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawByte(byte[] buffer, ref int currentPosition, byte value)
    {
        if (currentPosition < 0)
        {
            // TODO: handle insufficient space.
        }

        buffer[currentPosition++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTag(byte[] buffer, ref int currentPosition, int fieldNumber, WireFormat.WireType type)
    {
        // Assuming 1 length here for our use case.
        // Otherwise, first need to calculate the size of the tag.
        WriteRawVarint32(buffer, ref currentPosition, WireFormat.MakeTag(fieldNumber, type));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTagAndLengthPrefix(byte[] buffer, ref int currentPosition, int contentLength, int fieldNumber, WireFormat.WireType type)
    {
        WriteTag(buffer, ref currentPosition, fieldNumber, type);
        WriteLengthCustom(buffer, ref currentPosition, contentLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRawLittleEndian64(byte[] buffer, ref int currentPosition, ulong value)
    {
        if (currentPosition < buffer.Length)
        {
            Span<byte> span = new Span<byte>(buffer, currentPosition, Fixed64Size);

            BinaryPrimitives.WriteUInt64LittleEndian(span, value);

            currentPosition += Fixed64Size;
        }
        else
        {
            // TODO: handle insufficient space.
            // Write manually byte by byte.
        }
    }
}
