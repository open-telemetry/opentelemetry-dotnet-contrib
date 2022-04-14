// <copyright file="MetricSerializer.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva
{
    internal static class MetricSerializer
    {
        /// <summary>
        /// Writes the string to buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="bufferIndex">Index of the buffer.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeString(byte[] buffer, ref int bufferIndex, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (bufferIndex + value.Length + sizeof(short) >= buffer.Length)
                {
                    // TODO: What should we do when the data is invalid?
                }

#if NETSTANDARD2_1
                Span<byte> bufferSpan = new Span<byte>(buffer);
                bufferSpan = bufferSpan.Slice(bufferIndex);
                Span<byte> stringSpan = bufferSpan.Slice(2);
                var lengthWritten = (short)Encoding.UTF8.GetBytes(value, stringSpan);
                MemoryMarshal.Write(bufferSpan, ref lengthWritten);
                bufferIndex += sizeof(short) + lengthWritten;
#else
                // Advanced the buffer to account for the length, we will write it back after encoding.
                var currentIndex = bufferIndex;
                bufferIndex += sizeof(short);
                var lengthWritten = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, bufferIndex);
                bufferIndex += lengthWritten;

                // Write the length now that it is known
                SerializeInt16(buffer, ref currentIndex, (short)lengthWritten);
#endif
            }
            else
            {
                SerializeInt16(buffer, ref bufferIndex, 0);
            }
        }

        /// <summary>
        /// Writes the encoded string to buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="bufferIndex">Index of the buffer.</param>
        /// <param name="encodedValue">The encoded value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeEncodedString(byte[] buffer, ref int bufferIndex, byte[] encodedValue)
        {
            if (bufferIndex + encodedValue.Length + sizeof(short) >= buffer.Length)
            {
                // TODO: What should we do when the data is invalid?
            }

#if NETSTANDARD2_1
            Span<byte> sourceSpan = new Span<byte>(encodedValue);
            Span<byte> bufferSpan = new Span<byte>(buffer);
            bufferSpan = bufferSpan.Slice(bufferIndex);
            sourceSpan.CopyTo(bufferSpan.Slice(2));
            short encodedLength = (short)encodedValue.Length;
            MemoryMarshal.Write(bufferSpan, ref encodedLength);
            bufferIndex += sizeof(short) + encodedLength;
#else
            SerializeInt16(buffer, ref bufferIndex, (short)encodedValue.Length);
            Array.Copy(encodedValue, 0, buffer, bufferIndex, encodedValue.Length);
            bufferIndex += encodedValue.Length;
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
        {
            if (bufferIndex + sizeof(byte) >= buffer.Length)
            {
                // TODO: What should we do when the data is invalid?
            }

            buffer[bufferIndex] = value;
            bufferIndex += sizeof(byte);
        }

        /// <summary>
        /// Writes the unsigned short to buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="bufferIndex">Index of the buffer.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeUInt16(byte[] buffer, ref int bufferIndex, ushort value)
        {
            if (bufferIndex + sizeof(ushort) >= buffer.Length)
            {
                // TODO: What should we do when the data is invalid?
            }

            buffer[bufferIndex] = (byte)value;
            buffer[bufferIndex + 1] = (byte)(value >> 8);
            bufferIndex += sizeof(ushort);
        }

        /// <summary>
        /// Writes the short to buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="bufferIndex">Index of the buffer.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeInt16(byte[] buffer, ref int bufferIndex, short value)
        {
            if (bufferIndex + sizeof(short) >= buffer.Length)
            {
                // TODO: What should we do when the data is invalid?
            }

            buffer[bufferIndex] = (byte)value;
            buffer[bufferIndex + 1] = (byte)(value >> 8);
            bufferIndex += sizeof(short);
        }

        /// <summary>
        /// Writes the unsigned int to buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="bufferIndex">Index of the buffer.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeUInt32(byte[] buffer, ref int bufferIndex, uint value)
        {
            if (bufferIndex + sizeof(uint) >= buffer.Length)
            {
                // TODO: What should we do when the data is invalid?
            }

            buffer[bufferIndex] = (byte)value;
            buffer[bufferIndex + 1] = (byte)(value >> 8);
            buffer[bufferIndex + 2] = (byte)(value >> 0x10);
            buffer[bufferIndex + 3] = (byte)(value >> 0x18);
            bufferIndex += sizeof(uint);
        }

        /// <summary>
        /// Writes the ulong to buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="bufferIndex">Index of the buffer.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeUInt64(byte[] buffer, ref int bufferIndex, ulong value)
        {
            if (bufferIndex + sizeof(ulong) >= buffer.Length)
            {
                // TODO: What should we do when the data is invalid?
            }

            buffer[bufferIndex] = (byte)value;
            buffer[bufferIndex + 1] = (byte)(value >> 8);
            buffer[bufferIndex + 2] = (byte)(value >> 0x10);
            buffer[bufferIndex + 3] = (byte)(value >> 0x18);
            buffer[bufferIndex + 4] = (byte)(value >> 0x20);
            buffer[bufferIndex + 5] = (byte)(value >> 0x28);
            buffer[bufferIndex + 6] = (byte)(value >> 0x30);
            buffer[bufferIndex + 7] = (byte)(value >> 0x38);
            bufferIndex += sizeof(ulong);
        }
    }

    internal enum MetricEventType
    {
        ULongMetric = 50,
        DoubleMetric = 55,
        ExternallyAggregatedULongDistributionMetric = 56,
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

    /// <summary>
    /// Represents the fixed payload of a standard metric.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct MetricPayload
    {
        /// <summary>
        /// The dimension count.
        /// </summary>
        [FieldOffset(0)]
        public ushort CountDimension;

        /// <summary>
        /// Reserved for alignment.
        /// </summary>
        [FieldOffset(2)]
        public ushort ReservedWord; // for 8-byte aligned

        /// <summary>
        /// Reserved for alignment.
        /// </summary>
        [FieldOffset(4)]
        public uint ReservedDword;

        /// <summary>
        /// The UTC timestamp of the metric.
        /// </summary>
        [FieldOffset(8)]
        public ulong TimestampUtc;

        /// <summary>
        /// The value of the metric.
        /// </summary>
        [FieldOffset(16)]
        public MetricData Data;
    }

    /// <summary>
    /// Represents the fixed payload of an externally aggregated metric.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct ExternalPayload
    {
        /// <summary>
        /// The dimension count.
        /// </summary>
        [FieldOffset(0)]
        public ushort CountDimension;

        /// <summary>
        /// Reserved for alignment.
        /// </summary>
        [FieldOffset(2)]
        public ushort ReservedWord; // for alignment

        /// <summary>
        /// The number of samples produced in the period.
        /// </summary>
        [FieldOffset(4)]
        public uint Count;

        /// <summary>
        /// The UTC timestamp of the metric.
        /// </summary>
        [FieldOffset(8)]
        public ulong TimestampUtc;

        /// <summary>
        /// The sum of the samples produced in the period.
        /// </summary>
        [FieldOffset(16)]
        public MetricData Sum;

        /// <summary>
        /// The minimum value of the samples produced in the period.
        /// </summary>
        [FieldOffset(24)]
        public MetricData Min;

        /// <summary>
        /// The maximum value of the samples produced in the period.
        /// </summary>
        [FieldOffset(32)]
        public MetricData Max;
    }

    /// <summary>
    /// Represents the value of a metric.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct MetricData
    {
        /// <summary>
        /// The value represented as an integer.
        /// </summary>
        [FieldOffset(0)]
        public ulong UInt64Value;

        /// <summary>
        /// The value represented as a double.
        /// </summary>
        [FieldOffset(0)]
        public double DoubleValue;
    }
}
