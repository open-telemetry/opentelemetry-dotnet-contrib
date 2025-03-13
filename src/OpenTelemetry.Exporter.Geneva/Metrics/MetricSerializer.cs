// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

#pragma warning disable SA1649 // File name should match first type name

internal enum MetricEventType
{
    ULongMetric = 50,
    DoubleMetric = 55,
    ExternallyAggregatedULongDistributionMetric = 56,
    TLV = 70,
}

internal enum PayloadType
{
    AccountName = 1,
    Namespace = 2,
    MetricName = 3,
    Dimensions = 4,
    ULongMetric = 5,
    DoubleMetric = 6,
    ExternallyAggregatedULongDistributionMetric = 8,
    HistogramULongValueCountPairs = 12,
    Exemplars = 15,
}

[Flags]
internal enum ExemplarFlags : byte
{
    None = 0x0,
    IsMetricValueDoubleStoredAsLong = 0x1,
    IsTimestampAvailable = 0x2,
    SpanIdExists = 0x4,
    TraceIdExists = 0x8,
    SampleCountExists = 0x10,
}

/// <summary>
/// Represents the binary header for non-ETW transmitted metrics.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct BinaryHeader
{
    /// <summary>
    /// The event ID that represents how it will be processed.
    /// </summary>
    [FieldOffset(0)]
    public ushort EventId;

    /// <summary>
    /// The length of the body following the header.
    /// </summary>
    [FieldOffset(2)]
    public ushort BodyLength;
}

internal static class MetricSerializer
{
    /// <summary>
    /// Writes the string to buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferIndex">Index of the buffer.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeString(byte[] buffer, ref int bufferIndex, string? value)
    {
        if (value?.Length > 0)
        {
            // Advanced the buffer to account for the length, we will write it back after encoding.
            var lengthWritten = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, bufferIndex += 2);
            nuint idx = (uint)bufferIndex;
            bufferIndex = (int)idx + lengthWritten;

            // Write the length now that it is known
            SerializeUInt16Length(buffer, idx - 2, lengthWritten);
        }
        else
        {
            SerializeUInt16(buffer, ref bufferIndex, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeUInt16Length(byte[] buffer, nuint lengthIndex, int value)
    {
#if NET8_0_OR_GREATER
        Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(buffer), lengthIndex), (ushort)value);
#else
        Unsafe.WriteUnaligned(ref buffer[(int)lengthIndex], (ushort)value);
#endif
    }

    /// <summary>
    /// Writes the byte to buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferIndex">Index of the buffer.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeByte(byte[] buffer, ref int bufferIndex, byte value)
        => buffer[bufferIndex++] = value;

    /// <summary>
    /// Writes the unsigned short to buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferIndex">Index of the buffer.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeUInt16(byte[] buffer, ref int bufferIndex, ushort value)
        => Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref buffer[bufferIndex += sizeof(ushort)], (nint)(-sizeof(ushort))), value);

    /// <summary>
    /// Writes the unsigned int to buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferIndex">Index of the buffer.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeUInt32(byte[] buffer, ref int bufferIndex, uint value)
        => Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref buffer[bufferIndex += sizeof(uint)], (nint)(-sizeof(uint))), value);

    /// <summary>
    /// Writes the ulong to buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferIndex">Index of the buffer.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeUInt64(byte[] buffer, ref int bufferIndex, ulong value)
        => Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref buffer[bufferIndex += sizeof(ulong)], (nint)(-sizeof(ulong))), value);

    /// <summary>
    /// Writes the double to buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferIndex">Index of the buffer.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void SerializeFloat64(byte[] buffer, ref int bufferIndex, double value)
        => SerializeUInt64(buffer, ref bufferIndex, (ulong)BitConverter.DoubleToInt64Bits(value));

    /// <summary>
    /// Writes the base128 string to buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferIndex">Index of the buffer.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeBase128String(byte[] buffer, ref int bufferIndex, string? value)
    {
        if (value?.Length > 0)
        {
            // reserve 2 bytes for length
            var lengthWritten = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, bufferIndex += 2);
            nuint idx = (uint)bufferIndex;
            bufferIndex = (int)idx + lengthWritten;

            // length is base-128 encoded in 2 bytes as [7bits of length + 0x80, 7bits of length]
            SerializeUInt16Length(buffer, idx - 2, (byte)lengthWritten | 0x80 | ((lengthWritten & 0b_1111111_0000000) << 1));
        }
        else
        {
            SerializeByte(buffer, ref bufferIndex, 0);
        }
    }

    /// <summary>
    /// Writes unsigned int value Base-128 encoded.
    /// </summary>
    /// <param name="buffer">Buffer used for writing.</param>
    /// <param name="offset">Offset to start with. Will be moved to the next byte after written.</param>
    /// <param name="value">Value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeUInt32AsBase128(byte[] buffer, ref int offset, uint value)
    {
        while (value > 0x7f)
        {
            buffer[offset++] = (byte)(0x80 | value);
            value >>= 7;
        }

        buffer[offset++] = (byte)value;
    }

    /// <summary>
    /// Writes long value Base-128 encoded.
    /// </summary>
    /// <param name="buffer">Buffer used for writing.</param>
    /// <param name="offset">Offset to start with. Will be moved to the next byte after written.</param>
    /// <param name="value">Value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeInt64AsBase128(byte[] buffer, ref int offset, long value)
    {
        var negative = value < 0;
        var t = negative ? -value : value;

        uint b = (uint)t & 0x3f;
        t >>= 6;
        if (negative)
        {
            b |= 0x40;
        }

        if (t > 0)
        {
            b |= 0x80;
        }

        buffer[offset++] = (byte)b;

        while (t > 0)
        {
            b = (uint)t;
            t >>= 7;

            if (t > 0)
            {
                b |= 0x80;
            }

            buffer[offset++] = (byte)b;
        }
    }
}
