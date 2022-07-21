// <copyright file="JsonSerializer.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace OpenTelemetry.Exporter.Geneva
{
    internal static class JsonSerializer
    {
        private const byte ASCII_QUOTATION_MARK = 0x22;
        private const byte ASCII_REVERSE_SOLIDUS = 0x5C;
        private const byte ASCII_SOLIDUS = 0x2F;
        private const byte ASCII_BACKSPACE = 0x08;
        private const byte ASCII_FORMFEED = 0x0C;
        private const byte ASCII_LINEFEED = 0x0A;
        private const byte ASCII_CARRIAGE_RETURN = 0x0D;
        private const byte ASCII_HORIZONTAL_TAB = 0x09;

        private static readonly byte[] HEX_CODE;
        private static readonly ThreadLocal<byte[]> buffer = new(() => new byte[65360]);

        static JsonSerializer()
        {
            var mapping = new byte[]
            {
                0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                0x61, 0x62, 0x63, 0x64, 0x65, 0x66,
            };
            HEX_CODE = new byte[512];
            for (int i = 0; i < 256; i++)
            {
                HEX_CODE[i] = mapping[i >> 4];
                HEX_CODE[i + 256] = mapping[i & 0x0F];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SerializeNull()
        {
            var count = WriteString(buffer.Value, 0, "null");
            return Encoding.UTF8.GetString(buffer.Value, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SerializeNull(byte[] buffer, int cursor)
        {
            return WriteString(buffer, cursor, "null");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SerializeString(string value)
        {
            var count = SerializeString(buffer.Value, 0, value);
            return Encoding.UTF8.GetString(buffer.Value, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SerializeString(byte[] buffer, int cursor, string value)
        {
            if (value == null)
            {
                return SerializeNull(buffer, cursor);
            }

            buffer[cursor++] = ASCII_QUOTATION_MARK;
            cursor = WriteString(buffer, cursor, value);
            buffer[cursor++] = ASCII_QUOTATION_MARK;
            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SerializeArray<T>(T[] array)
        {
            var count = SerializeArray(buffer.Value, 0, array);
            return Encoding.UTF8.GetString(buffer.Value, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SerializeArray<T>(byte[] buffer, int cursor, T[] array)
        {
            if (array == null)
            {
                return SerializeNull(buffer, cursor);
            }

            buffer[cursor++] = unchecked((byte)'[');
            var length = array.Length;
            if (length >= 1)
            {
                cursor = Serialize(buffer, cursor, array[0]);
                for (int i = 1; i < length; i++)
                {
                    buffer[cursor++] = unchecked((byte)',');
                    cursor = Serialize(buffer, cursor, array[i]);
                }
            }

            buffer[cursor++] = unchecked((byte)']');
            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SerializeMap(IEnumerable<KeyValuePair<string, object>> map)
        {
            var count = SerializeMap(buffer.Value, 0, map);
            return Encoding.UTF8.GetString(buffer.Value, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SerializeMap(byte[] buffer, int cursor, IEnumerable<KeyValuePair<string, object>> map)
        {
            if (map == null)
            {
                return SerializeNull(buffer, cursor);
            }

            buffer[cursor++] = unchecked((byte)'{');
            int count = 0;
            foreach (var entry in map)
            {
                if (count > 0)
                {
                    buffer[cursor++] = unchecked((byte)',');
                }

                cursor = SerializeString(buffer, cursor, entry.Key);
                buffer[cursor++] = unchecked((byte)':');
                cursor = Serialize(buffer, cursor, entry.Value);
                count++;
            }

            buffer[cursor++] = unchecked((byte)'}');
            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Serialize(object obj)
        {
            var count = Serialize(buffer.Value, 0, obj);
            return Encoding.UTF8.GetString(buffer.Value, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Serialize(byte[] buffer, int cursor, object obj)
        {
            if (obj == null)
            {
                return SerializeNull(buffer, cursor);
            }

            switch (obj)
            {
                case bool v:
                    return WriteString(buffer, cursor, v ? "true" : "false");

                // case byte v:
                // case sbyte v:
                case short v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case ushort v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case int v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case uint v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case long v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case ulong v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case float v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case double v:
                    return WriteString(buffer, cursor, v.ToString(CultureInfo.InvariantCulture));
                case string v:
                    return SerializeString(buffer, cursor, v);
                case IDictionary<string, object> v:
                    return SerializeMap(buffer, cursor, v);
                case object[] v:
                    return SerializeArray(buffer, cursor, v);

                // case DateTime v:
                default:
                    return SerializeString(buffer, cursor, $"ERROR: type {obj.GetType().FullName} is not supported");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteString(byte[] buffer, int cursor, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                var ordinal = (ushort)value[i];
                switch (ordinal)
                {
                    case ASCII_QUOTATION_MARK:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = ASCII_QUOTATION_MARK;
                        break;
                    case ASCII_REVERSE_SOLIDUS:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        break;
                    case ASCII_SOLIDUS:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = ASCII_SOLIDUS;
                        break;
                    case ASCII_BACKSPACE:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = unchecked((byte)'b');
                        break;
                    case ASCII_FORMFEED:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = unchecked((byte)'f');
                        break;
                    case ASCII_LINEFEED:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = unchecked((byte)'n');
                        break;
                    case ASCII_CARRIAGE_RETURN:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = unchecked((byte)'r');
                        break;
                    case ASCII_HORIZONTAL_TAB:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = unchecked((byte)'t');
                        break;
                    default:
                        // ASCII printable characters
                        if ((ordinal >= 32) && (ordinal < 127))
                        {
                            buffer[cursor++] = unchecked((byte)ordinal);
                        }

                        // ASCII control characters, extended ASCII codes or UNICODE
                        else
                        {
                            buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                            var high = unchecked(ordinal >> 8);
                            var low = ordinal & 0xFF;
                            buffer[cursor++] = unchecked((byte)'u');
                            buffer[cursor++] = HEX_CODE[high];
                            buffer[cursor++] = HEX_CODE[high + 256];
                            buffer[cursor++] = HEX_CODE[low];
                            buffer[cursor++] = HEX_CODE[low + 256];
                        }

                        break;
                }
            }

            return cursor;
        }
    }
}
