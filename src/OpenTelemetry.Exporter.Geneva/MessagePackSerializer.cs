// <copyright file="MessagePackSerializer.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

internal static class MessagePackSerializer
{
    public const byte MIN_FIX_MAP = 0x80;
    public const byte MIN_FIX_ARRAY = 0x90;
    public const byte MIN_FIX_STR = 0xA0;
    public const byte NIL = 0xC0;
    public const byte FALSE = 0xC2;
    public const byte TRUE = 0xC3;
    public const byte BIN8 = 0xC4;
    public const byte BIN16 = 0xC5;
    public const byte BIN32 = 0xC6;
    public const byte TIMESTAMP96 = 0xC7;
    public const byte FLOAT32 = 0xCA;
    public const byte FLOAT64 = 0xCB;
    public const byte UINT8 = 0xCC;
    public const byte UINT16 = 0xCD;
    public const byte UINT32 = 0xCE;
    public const byte UINT64 = 0xCF;
    public const byte INT8 = 0xD0;
    public const byte INT16 = 0xD1;
    public const byte INT32 = 0xD2;
    public const byte INT64 = 0xD3;
    public const byte TIMESTAMP32 = 0xD6;
    public const byte TIMESTAMP64 = 0xD7;
    public const byte STR8 = 0xD9;
    public const byte STR16 = 0xDA;
    public const byte STR32 = 0xDB;
    public const byte ARRAY16 = 0xDC;
    public const byte ARRAY32 = 0xDD;
    public const byte MAP16 = 0xDE;
    public const byte MAP32 = 0xDF;
    public const byte EXT_DATE_TIME = 0xFF;

    private const int LIMIT_MIN_FIX_NEGATIVE_INT = -32;
    private const int LIMIT_MAX_FIX_STRING_LENGTH_IN_BYTES = 31;
    private const int LIMIT_MAX_STR8_LENGTH_IN_BYTES = (1 << 8) - 1; // str8 stores 2^8 - 1 bytes
    private const int LIMIT_MAX_FIX_MAP_COUNT = 15;
    private const int LIMIT_MAX_FIX_ARRAY_LENGTH = 15;
    private const int STRING_SIZE_LIMIT_CHAR_COUNT = (1 << 14) - 1; // 16 * 1024 - 1 = 16383

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeNull(byte[] buffer, int cursor)
    {
        buffer[cursor++] = NIL;
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeBool(byte[] buffer, int cursor, bool value)
    {
        buffer[cursor++] = value ? TRUE : FALSE;
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeInt8(byte[] buffer, int cursor, sbyte value)
    {
        if (value >= 0)
        {
            return SerializeUInt8(buffer, cursor, unchecked((byte)value));
        }

        if (value < LIMIT_MIN_FIX_NEGATIVE_INT)
        {
            buffer[cursor++] = INT8;
        }

        buffer[cursor++] = unchecked((byte)value);
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeInt16(byte[] buffer, int cursor, short value)
    {
        if (value >= 0)
        {
            return SerializeUInt16(buffer, cursor, unchecked((ushort)value));
        }

        if (value >= sbyte.MinValue)
        {
            return SerializeInt8(buffer, cursor, unchecked((sbyte)value));
        }

        buffer[cursor++] = INT16;
        return WriteInt16(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeInt32(byte[] buffer, int cursor, int value)
    {
        if (value >= 0)
        {
            return SerializeUInt32(buffer, cursor, unchecked((uint)value));
        }

        if (value >= short.MinValue)
        {
            return SerializeInt16(buffer, cursor, unchecked((short)value));
        }

        buffer[cursor++] = INT32;
        return WriteInt32(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeInt64(byte[] buffer, int cursor, long value)
    {
        if (value >= 0)
        {
            return SerializeUInt64(buffer, cursor, unchecked((ulong)value));
        }

        if (value >= int.MinValue)
        {
            return SerializeInt32(buffer, cursor, unchecked((int)value));
        }

        buffer[cursor++] = INT64;
        return WriteInt64(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeUInt8(byte[] buffer, int cursor, byte value)
    {
        if (value > 127)
        {
            buffer[cursor++] = UINT8;
        }

        buffer[cursor++] = value;
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeUInt16(byte[] buffer, int cursor, ushort value)
    {
        if (value <= byte.MaxValue)
        {
            return SerializeUInt8(buffer, cursor, unchecked((byte)value));
        }

        buffer[cursor++] = UINT16;
        return WriteUInt16(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeUInt32(byte[] buffer, int cursor, uint value)
    {
        if (value <= ushort.MaxValue)
        {
            return SerializeUInt16(buffer, cursor, unchecked((ushort)value));
        }

        buffer[cursor++] = UINT32;
        return WriteUInt32(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeUInt64(byte[] buffer, int cursor, ulong value)
    {
        if (value <= uint.MaxValue)
        {
            return SerializeUInt32(buffer, cursor, unchecked((uint)value));
        }

        buffer[cursor++] = UINT64;
        return WriteUInt64(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt16(byte[] buffer, int cursor, short value)
    {
        unchecked
        {
            buffer[cursor++] = (byte)(value >> 8);
            buffer[cursor++] = (byte)value;
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt32(byte[] buffer, int cursor, int value)
    {
        unchecked
        {
            buffer[cursor++] = (byte)(value >> 24);
            buffer[cursor++] = (byte)(value >> 16);
            buffer[cursor++] = (byte)(value >> 8);
            buffer[cursor++] = (byte)value;
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt64(byte[] buffer, int cursor, long value)
    {
        unchecked
        {
            buffer[cursor++] = (byte)(value >> 56);
            buffer[cursor++] = (byte)(value >> 48);
            buffer[cursor++] = (byte)(value >> 40);
            buffer[cursor++] = (byte)(value >> 32);
            buffer[cursor++] = (byte)(value >> 24);
            buffer[cursor++] = (byte)(value >> 16);
            buffer[cursor++] = (byte)(value >> 8);
            buffer[cursor++] = (byte)value;
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt16(byte[] buffer, int cursor, ushort value)
    {
        unchecked
        {
            buffer[cursor++] = (byte)(value >> 8);
            buffer[cursor++] = (byte)value;
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt32(byte[] buffer, int cursor, uint value)
    {
        unchecked
        {
            buffer[cursor++] = (byte)(value >> 24);
            buffer[cursor++] = (byte)(value >> 16);
            buffer[cursor++] = (byte)(value >> 8);
            buffer[cursor++] = (byte)value;
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt64(byte[] buffer, int cursor, ulong value)
    {
        unchecked
        {
            buffer[cursor++] = (byte)(value >> 56);
            buffer[cursor++] = (byte)(value >> 48);
            buffer[cursor++] = (byte)(value >> 40);
            buffer[cursor++] = (byte)(value >> 32);
            buffer[cursor++] = (byte)(value >> 24);
            buffer[cursor++] = (byte)(value >> 16);
            buffer[cursor++] = (byte)(value >> 8);
            buffer[cursor++] = (byte)value;
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeFloat32(byte[] buffer, int cursor, float value)
    {
        buffer[cursor++] = FLOAT32;
        return WriteInt32(buffer, cursor, Float32ToInt32(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int Float32ToInt32(float value)
    {
        return *(int*)&value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeFloat64(byte[] buffer, int cursor, double value)
    {
        buffer[cursor++] = FLOAT64;
        return WriteInt64(buffer, cursor, Float64ToInt64(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe long Float64ToInt64(double value)
    {
        return *(long*)&value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BackFill(byte[] buffer, int nameStartIdx, int validNameLength)
    {
        buffer[nameStartIdx] = STR8;
        buffer[nameStartIdx + 1] = unchecked((byte)validNameLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeAsciiString(byte[] buffer, int cursor, string value)
    {
        if (value == null)
        {
            return SerializeNull(buffer, cursor);
        }

        int start = cursor;
        var cch = value.Length;
        int cb;
        if (cch <= LIMIT_MAX_FIX_STRING_LENGTH_IN_BYTES)
        {
            cursor += 1;
            cb = Encoding.ASCII.GetBytes(value, 0, cch, buffer, cursor);
            if (cb <= LIMIT_MAX_FIX_STRING_LENGTH_IN_BYTES)
            {
                cursor += cb;
                buffer[start] = unchecked((byte)(MIN_FIX_STR | cb));
                return cursor;
            }
            else
            {
                throw new ArgumentException("The input string: \"{inputString}\" has non-ASCII characters in it.", value);
            }
        }

        if (cch <= LIMIT_MAX_STR8_LENGTH_IN_BYTES)
        {
            cursor += 2;
            cb = Encoding.ASCII.GetBytes(value, 0, cch, buffer, cursor);
            cursor += cb;
            if (cb <= LIMIT_MAX_STR8_LENGTH_IN_BYTES)
            {
                buffer[start] = STR8;
                buffer[start + 1] = unchecked((byte)cb);
                return cursor;
            }
            else
            {
                throw new ArgumentException("The input string: \"{inputString}\" has non-ASCII characters in it.", value);
            }
        }

        cursor += 3;
        if (cch <= STRING_SIZE_LIMIT_CHAR_COUNT)
        {
            cb = Encoding.ASCII.GetBytes(value, 0, cch, buffer, cursor);
            cursor += cb;
        }
        else
        {
            cb = Encoding.ASCII.GetBytes(value, 0, STRING_SIZE_LIMIT_CHAR_COUNT - 3, buffer, cursor);
            cursor += cb;
            cb += 3;

            // append "..." to indicate the string truncation
            buffer[cursor++] = 0x2E;
            buffer[cursor++] = 0x2E;
            buffer[cursor++] = 0x2E;
        }

        buffer[start] = STR16;
        buffer[start + 1] = unchecked((byte)(cb >> 8));
        buffer[start + 2] = unchecked((byte)cb);
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeUnicodeString(byte[] buffer, int cursor, string value)
    {
        if (value == null)
        {
            return SerializeNull(buffer, cursor);
        }

        int start = cursor;
        var cch = value.Length;
        int cb;
        cursor += 3;
        if (cch <= STRING_SIZE_LIMIT_CHAR_COUNT)
        {
            cb = Encoding.UTF8.GetBytes(value, 0, cch, buffer, cursor);
            cursor += cb;
        }
        else
        {
            cb = Encoding.UTF8.GetBytes(value, 0, STRING_SIZE_LIMIT_CHAR_COUNT - 3, buffer, cursor);
            cursor += cb;
            cb += 3;

            // append "..." to indicate the string truncation
            buffer[cursor++] = 0x2E;
            buffer[cursor++] = 0x2E;
            buffer[cursor++] = 0x2E;
        }

        buffer[start] = STR16;
        buffer[start + 1] = unchecked((byte)(cb >> 8));
        buffer[start + 2] = unchecked((byte)cb);
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteArrayHeader(byte[] buffer, int cursor, int length)
    {
        if (length <= LIMIT_MAX_FIX_ARRAY_LENGTH)
        {
            buffer[cursor++] = unchecked((byte)(MIN_FIX_ARRAY | length));
        }
        else if (length <= ushort.MaxValue)
        {
            buffer[cursor++] = ARRAY16;
            cursor = WriteUInt16(buffer, cursor, unchecked((ushort)length));
        }
        else
        {
            buffer[cursor++] = ARRAY32;
            cursor = WriteUInt32(buffer, cursor, unchecked((uint)length));
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeArray<T>(byte[] buffer, int cursor, T[] array)
    {
        if (array == null)
        {
            return SerializeNull(buffer, cursor);
        }

        cursor = WriteArrayHeader(buffer, cursor, array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            cursor = Serialize(buffer, cursor, array[i]);
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMapHeader(byte[] buffer, int cursor, int count)
    {
        if (count <= LIMIT_MAX_FIX_MAP_COUNT)
        {
            buffer[cursor++] = unchecked((byte)(MIN_FIX_MAP | count));
        }
        else if (count <= ushort.MaxValue)
        {
            buffer[cursor++] = MAP16;
            cursor = WriteUInt16(buffer, cursor, unchecked((ushort)count));
        }
        else
        {
            buffer[cursor++] = MAP32;
            cursor = WriteUInt32(buffer, cursor, unchecked((uint)count));
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeMap(byte[] buffer, int cursor, IDictionary<string, object> map)
    {
        if (map == null)
        {
            return SerializeNull(buffer, cursor);
        }

        cursor = WriteMapHeader(buffer, cursor, map.Count);
        foreach (var entry in map)
        {
            cursor = SerializeUnicodeString(buffer, cursor, entry.Key);
            cursor = Serialize(buffer, cursor, entry.Value);
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTimestamp96Header(byte[] buffer, int cursor)
    {
        buffer[cursor++] = TIMESTAMP96;
        buffer[cursor++] = 12;
        buffer[cursor++] = EXT_DATE_TIME;
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTimestamp96(byte[] buffer, int cursor, long ticks)
    {
        cursor = WriteUInt32(buffer, cursor, unchecked((uint)((ticks % TimeSpan.TicksPerSecond) * 100)));
        cursor = WriteInt64(buffer, cursor, (ticks / TimeSpan.TicksPerSecond) - 62135596800L);
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeTimestamp96(byte[] buffer, int cursor, long ticks)
    {
        cursor = WriteTimestamp96Header(buffer, cursor);
        cursor = WriteTimestamp96(buffer, cursor, ticks);
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SerializeUtcDateTime(byte[] buffer, int cursor, DateTime utc)
    {
        return SerializeTimestamp96(buffer, cursor, utc.Ticks);
    }

    public static int Serialize(byte[] buffer, int cursor, object obj)
    {
        if (obj == null)
        {
            return SerializeNull(buffer, cursor);
        }

        switch (obj)
        {
            case bool v:
                return SerializeBool(buffer, cursor, v);
            case byte v:
                return SerializeUInt8(buffer, cursor, v);
            case sbyte v:
                return SerializeInt8(buffer, cursor, v);
            case short v:
                return SerializeInt16(buffer, cursor, v);
            case ushort v:
                return SerializeUInt16(buffer, cursor, v);
            case int v:
                return SerializeInt32(buffer, cursor, v);
            case uint v:
                return SerializeUInt32(buffer, cursor, v);
            case long v:
                return SerializeInt64(buffer, cursor, v);
            case ulong v:
                return SerializeUInt64(buffer, cursor, v);
            case float v:
                return SerializeFloat32(buffer, cursor, v);
            case double v:
                return SerializeFloat64(buffer, cursor, v);
            case string v:
                return SerializeUnicodeString(buffer, cursor, v);
            case IDictionary<string, object> v:
                return SerializeMap(buffer, cursor, v);
            case object[] v:
                return SerializeArray(buffer, cursor, v);
            case DateTime v:
                return SerializeUtcDateTime(buffer, cursor, v.ToUniversalTime());
            default:
                string repr = null;

                try
                {
                    repr = Convert.ToString(obj, CultureInfo.InvariantCulture);
                }
                catch
                {
                    repr = $"ERROR: type {obj.GetType().FullName} is not supported";
                }

                return SerializeUnicodeString(buffer, cursor, repr);
        }
    }
}
