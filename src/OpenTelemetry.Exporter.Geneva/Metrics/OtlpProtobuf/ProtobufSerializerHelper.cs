// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

internal static class ProtobufSerializerHelper
{
    private const int Fixed64Size = 8;

    private const int MaxCustomLength = 0x1FFFFF;

    private const ulong Ulong128 = 128;

    private const uint Uint128 = 128;

    internal static Encoding Utf8Encoding => Encoding.UTF8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteStringTag(byte[] buffer, ref int cursor, int fieldNumber, string value)
    {
        var stringSize = Utf8Encoding.GetByteCount(value);

        WriteTag(buffer, ref cursor, fieldNumber, WireType.LEN);

        WriteLength(buffer, ref cursor, stringSize);

        _ = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, cursor);

        cursor += stringSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteEnumWithTag(byte[] buffer, ref int cursor, int fieldNumber, int value)
    {
        WriteTag(buffer, ref cursor, fieldNumber, WireType.VARINT);

        // Assuming 1 byte which matches the intended use.
        buffer[cursor++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteBoolWithTag(byte[] buffer, ref int cursor, int fieldNumber, bool value)
    {
        WriteTag(buffer, ref cursor, fieldNumber, WireType.VARINT);
        buffer[cursor++] = value ? (byte)1 : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteFixed64WithTag(byte[] buffer, ref int cursor, int fieldNumber, ulong value)
    {
        WriteTag(buffer, ref cursor, fieldNumber, WireType.I64);
        WriteFixed64LittleEndianFormat(buffer, ref cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteInt64WithTag(byte[] buffer, ref int cursor, int fieldNumber, ulong value)
    {
        WriteTag(buffer, ref cursor, fieldNumber, WireType.VARINT);
        WriteVarint64(buffer, ref cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteSInt32WithTag(byte[] buffer, ref int cursor, int fieldNumber, int value)
    {
        WriteTag(buffer, ref cursor, fieldNumber, WireType.VARINT);

        // https://protobuf.dev/programming-guides/encoding/#signed-ints
        WriteVarint32(buffer, ref cursor, (uint)((value << 1) ^ (value >> 31)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteDoubleWithTag(byte[] buffer, ref int cursor, int fieldNumber, double value)
    {
        WriteTag(buffer, ref cursor, fieldNumber, WireType.I64);
        WriteFixed64LittleEndianFormat(buffer, ref cursor, (ulong)BitConverter.DoubleToInt64Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteLengthCustom(byte[] buffer, ref int cursor, int length)
    {
        if ((uint)length > MaxCustomLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must fit in a three-byte varint.");
        }

        buffer[cursor++] = (byte)(0x80 | (length & 0x7F));
        buffer[cursor++] = (byte)(0x80 | ((length >> 7) & 0x7F));
        buffer[cursor++] = (byte)((length >> 14) & 0x7F);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteLength(byte[] buffer, ref int cursor, int length)
        => WriteVarint32(buffer, ref cursor, (uint)length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteVarint32(byte[] buffer, ref int cursor, uint value)
    {
        while (value >= Uint128)
        {
            buffer[cursor++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        buffer[cursor++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteVarint64(byte[] buffer, ref int cursor, ulong value)
    {
        while (value >= Ulong128)
        {
            buffer[cursor++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        buffer[cursor++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTag(byte[] buffer, ref int cursor, int fieldNumber, WireType type)
        => WriteVarint32(buffer, ref cursor, GetTagValue(fieldNumber, type));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTagAndLengthPrefix(byte[] buffer, ref int cursor, int contentLength, int fieldNumber, WireType type)
    {
        WriteTag(buffer, ref cursor, fieldNumber, type);
        WriteLengthCustom(buffer, ref cursor, contentLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteFixed64LittleEndianFormat(byte[] buffer, ref int cursor, ulong value)
    {
        if (cursor + Fixed64Size > buffer.Length)
        {
            // Surface the overflow with the shared message instead of silently dropping the value
            // so it can be detected and reported by GenevaBufferOverflowExceptionHelper.
            throw new ArgumentOutOfRangeException(nameof(cursor), cursor, GenevaBufferOverflowExceptionHelper.MetricBufferTooSmallMessage);
        }

        var span = new Span<byte>(buffer, cursor, Fixed64Size);

        BinaryPrimitives.WriteUInt64LittleEndian(span, value);

        cursor += Fixed64Size;
    }

    internal static uint GetTagValue(int fieldNumber, WireType wireType)
        => ((uint)(fieldNumber << 3)) | (uint)wireType;
}
