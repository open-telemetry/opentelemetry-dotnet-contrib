// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

internal static class ProtobufSerializerHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteStringTag(byte[] buffer, ref int cursor, int fieldNumber, string value)
        => cursor = WriteStringTag(buffer, cursor, GetTagValue(fieldNumber, WireType.LEN), value);

    internal static int WriteStringTag(byte[] buffer, int cursor, uint tagValue, string value)
    {
        int prefixLen = value.Length <= 0x7f / 3 ? 2 : 4;
        var stringSize = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, cursor += prefixLen);

        nuint idx = (uint)cursor;
        if (prefixLen == 2)
        {
#if NET8_0_OR_GREATER
            Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(buffer), idx - 2) = (byte)tagValue;
            Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(buffer), idx - 1) = (byte)stringSize;
#else
            buffer[(int)idx - 2] = (byte)tagValue;
            buffer[(int)idx - 1] = (byte)stringSize;
#endif
        }
        else
        {
            WriteTagAndLengthPrefixImpl(buffer, idx - 4, stringSize, tagValue);
        }

        return (int)idx + stringSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteEnumWithTag(byte[] buffer, ref int cursor, int fieldNumber, int value)
    {
        Debug.Assert((uint)value <= 0x7f, "Enum value must fit within 1-byte varint encoding");
        ref var target = ref buffer[cursor += 2];
        Unsafe.AddByteOffset(ref target, (nint)(-2)) = (byte)GetTagValue(fieldNumber, WireType.VARINT);
        Unsafe.AddByteOffset(ref target, (nint)(-1)) = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteBoolWithTag(byte[] buffer, ref int cursor, int fieldNumber, bool value)
        => WriteEnumWithTag(buffer, ref cursor, fieldNumber, value ? 1 : 0);

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
    internal static void WriteVarint32(byte[] buffer, ref int cursor, uint value)
    {
        while (value > 0x7f)
        {
            buffer[cursor++] = (byte)(0x80 | value);
            value >>= 7;
        }

        buffer[cursor++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteVarint64(byte[] buffer, ref int cursor, ulong value)
    {
        while (value > 0x7f)
        {
            buffer[cursor++] = (byte)(0x80 | (uint)value);
            value >>= 7;
        }

        buffer[cursor++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTag(byte[] buffer, ref int cursor, int fieldNumber, WireType type)
    {
        buffer[cursor++] = (byte)GetTagValue(fieldNumber, type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTagAndLengthPrefix(byte[] buffer, int tagAndLengthIndex, int cursor, int fieldNumber, WireType type)
        => WriteTagAndLengthPrefixImpl(buffer, (uint)tagAndLengthIndex, cursor - 4 - tagAndLengthIndex, GetTagValue(fieldNumber, type));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTagAndLengthPrefixImpl(byte[] buffer, nuint tagAndLengthIndex, int contentLength, uint tagValue)
    {
        var value = (uint)contentLength;
        Debug.Assert(value <= 0b_1111111_1111111_1111111, "Length must fit within 3-byte varint encoding");

        // The tag and length are encoded in 4 bytes as [tag, 7bits of length + 0x80, 7bits of length + 0x80, 7bits of length]
#if NET8_0_OR_GREATER
        if (System.Runtime.Intrinsics.X86.Bmi2.IsSupported)
        {
            value = tagValue | 0x00808000 | System.Runtime.Intrinsics.X86.Bmi2.ParallelBitDeposit(value, 0x7f7f7f00);
        }
        else
#endif
        {
            value = tagValue | 0x00808000 | ((value & 0b_1111111) << 8) | ((value & 0b_1111111_0000000) << 9) | ((value & 0b_1111111_0000000_0000000) << 10);
        }

        if (!BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

#if NET8_0_OR_GREATER
        Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(buffer), tagAndLengthIndex), value);
#else
        Unsafe.WriteUnaligned(ref buffer[(int)tagAndLengthIndex], value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteFixed64LittleEndianFormat(byte[] buffer, ref int cursor, ulong value)
    {
        if (!BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref buffer[cursor += sizeof(ulong)], (nint)(-sizeof(ulong))), value);
    }

    internal static uint GetTagValue(int fieldNumber, WireType wireType)
    {
        return ((uint)(fieldNumber << 3)) | (uint)wireType;
    }
}
