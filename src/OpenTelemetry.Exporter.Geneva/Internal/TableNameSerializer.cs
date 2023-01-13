// <copyright file="TableNameSerializer.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class TableNameSerializer
{
    public const int MaxSanitizedCategoryNameLength = 50;
    public const int MaxSanitizedCategoryNameBytes = MaxSanitizedCategoryNameLength + 2;
    private const StringComparison DictionaryKeyComparison = StringComparison.Ordinal;

    private static readonly Tuple<string, byte[]> s_passthroughTableName = new("*", Array.Empty<byte>());
    private static readonly Tuple<string, byte[]> s_invalidTableName = new(string.Empty, Array.Empty<byte>());
    private static readonly StringComparer s_dictionaryKeyComparer = StringComparer.Ordinal;

    private readonly Tuple<string, byte[]> m_defaultTableName;
    private readonly Dictionary<string, Tuple<string, byte[]>> m_tableMappings;
    private readonly bool m_shouldPassThruTableMappings;
    private readonly object m_lockObject = new();
    private Dictionary<string, Tuple<string, byte[]>> m_tableNameCache = new(s_dictionaryKeyComparer);

    public IReadOnlyDictionary<string, Tuple<string, byte[]>> TableNameCache => this.m_tableNameCache;

    public TableNameSerializer(GenevaExporterOptions options, string defaultTableName)
    {
        Debug.Assert(options != null, "options were null");
        Debug.Assert(!string.IsNullOrWhiteSpace(defaultTableName), "defaultEventName was null or whitespace");
        Debug.Assert(IsValidTableName(defaultTableName), "defaultEventName was invalid");

        this.m_defaultTableName = new(defaultTableName, BuildStr8BufferForAsciiString(defaultTableName));

        if (options.TableNameMappings != null)
        {
            var tempTableMappings = new Dictionary<string, Tuple<string, byte[]>>(options.TableNameMappings.Count, s_dictionaryKeyComparer);
            foreach (var kv in options.TableNameMappings)
            {
                if (kv.Key == "*")
                {
                    if (kv.Value == "*")
                    {
                        this.m_shouldPassThruTableMappings = true;
                    }
                    else
                    {
                        this.m_defaultTableName = new(kv.Value, BuildStr8BufferForAsciiString(kv.Value));
                    }
                }
                else if (kv.Value == "*")
                {
                    tempTableMappings[kv.Key] = s_passthroughTableName;
                }
                else
                {
                    tempTableMappings[kv.Key] = new(kv.Value, BuildStr8BufferForAsciiString(kv.Value));
                }
            }

            this.m_tableMappings = tempTableMappings;
        }
    }

    public static bool IsReservedTableName(string tableName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(tableName), "tableName was null or whitespace");

        // TODO: Implement this if needed.

        return false;
    }

    public static bool IsValidTableName(string tableName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(tableName), "tableName was null or whitespace");

        var length = tableName.Length;
        if (length > MaxSanitizedCategoryNameLength)
        {
            return false;
        }

        char firstChar = tableName[0];
        if (firstChar < 'A' || firstChar > 'Z')
        {
            return false;
        }

        for (int i = 1; i < length; i++)
        {
            char cur = tableName[i];
            if ((cur >= 'a' && cur <= 'z') || (cur >= 'A' && cur <= 'Z') || (cur >= '0' && cur <= '9'))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ResolveAndSerializeTableNameForCategoryName(byte[] destination, int offset, string categoryName, out Tuple<string, byte[]> tableName)
    {
        tableName = this.ResolveTableMappingForCategoryName(categoryName);

        Debug.Assert(tableName != null, "tableName was null");

        return MessagePackSerializer.SerializeSpan(destination, offset, tableName.Item2);
    }

    private static byte[] BuildStr8BufferForAsciiString(string value)
    {
        var length = value.Length;

        byte[] buffer = new byte[length + 2];

        Encoding.ASCII.GetBytes(value, 0, length, buffer, 2);

        MessagePackSerializer.WriteStr8Header(buffer, 0, length);

        return buffer;
    }

    // This method would map the logger category to a table name which only contains alphanumeric values with the following additions:
    // Any character that is not allowed will be removed.
    // If the resulting string is longer than 50 characters, only the first 50 characters will be taken.
    // If the first character in the resulting string is a lower-case alphabet, it will be converted to the corresponding upper-case.
    // If the resulting string still does not comply with Rule, the category name will not be serialized.
    private static int WriteSanitizedCategoryNameToSpan(Span<byte> buffer, string categoryName)
    {
        // Reserve 2 bytes for storing LIMIT_MAX_STR8_LENGTH_IN_BYTES and (byte)validNameLength -
        // these 2 bytes will be back filled after iterating through categoryName.
        int cursor = 2;
        int validNameLength = 0;

        // Special treatment for the first character.
        var firstChar = categoryName[0];
        if (firstChar >= 'A' && firstChar <= 'Z')
        {
            buffer[cursor++] = (byte)firstChar;
            ++validNameLength;
        }
        else if (firstChar >= 'a' && firstChar <= 'z')
        {
            // If the first character in the resulting string is a lower-case alphabet,
            // it will be converted to the corresponding upper-case.
            buffer[cursor++] = (byte)(firstChar - 32);
            ++validNameLength;
        }
        else
        {
            // Not a valid name.
            return 0;
        }

        for (int i = 1; i < categoryName.Length; ++i)
        {
            if (validNameLength == MaxSanitizedCategoryNameLength)
            {
                break;
            }

            var cur = categoryName[i];
            if ((cur >= 'a' && cur <= 'z') || (cur >= 'A' && cur <= 'Z') || (cur >= '0' && cur <= '9'))
            {
                buffer[cursor++] = (byte)cur;
                ++validNameLength;
            }
        }

        // Backfilling MessagePack serialization protocol and valid category length to the startIdx of the categoryName byte array.
        MessagePackSerializer.WriteStr8Header(buffer, 0, validNameLength);

        return cursor;
    }

    private static string ConvertSanitizedCategoryNameToString(byte[] tableName)
    {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return string.Create(tableName.Length - 2, tableName, CreateString);

        static void CreateString(Span<char> destination, byte[] source)
        {
            for (int i = 2, c = 0; i < source.Length; i++, c++)
            {
                destination[c] = (char)source[i];
            }
        }
#else
        char[] destination = new char[tableName.Length - 2];

        for (int i = 2, c = 0; i < tableName.Length; i++, c++)
        {
            destination[c] = (char)tableName[i];
        }

        return new(destination);
#endif
    }

    private Tuple<string, byte[]> ResolveTableMappingForCategoryName(string categoryName)
    {
        var tableNameCache = this.m_tableNameCache;

        if (tableNameCache.TryGetValue(categoryName, out Tuple<string, byte[]> tableName))
        {
            return tableName;
        }

        return this.ResolveTableMappingForCategoryNameRare(categoryName);
    }

    private Tuple<string, byte[]> ResolveTableMappingForCategoryNameRare(string categoryName)
    {
        Tuple<string, byte[]> mappedTableName = null;

        // If user configured table name mappings run resolution logic.
        if (this.m_tableMappings != null
            && !this.m_tableMappings.TryGetValue(categoryName, out mappedTableName))
        {
            // Find best match if an exact match was not found.

            string currentKey = null;

            foreach (var mapping in this.m_tableMappings)
            {
                if (!categoryName.StartsWith(mapping.Key, DictionaryKeyComparison))
                {
                    continue;
                }

                if (currentKey == null || mapping.Key.Length >= currentKey.Length)
                {
                    currentKey = mapping.Key;
                    mappedTableName = mapping.Value;
                }
            }
        }

        mappedTableName ??= !this.m_shouldPassThruTableMappings
            ? this.m_defaultTableName
            : s_passthroughTableName;

        Span<byte> sanitizedTableNameStorage = mappedTableName == s_passthroughTableName
            ? stackalloc byte[MaxSanitizedCategoryNameBytes]
            : Array.Empty<byte>();

        if (sanitizedTableNameStorage.Length > 0)
        {
            // We resolved to a wildcard which is pass-through mode.

            int bytesWritten = WriteSanitizedCategoryNameToSpan(sanitizedTableNameStorage, categoryName);
            if (bytesWritten > 0)
            {
                sanitizedTableNameStorage = sanitizedTableNameStorage.Slice(0, bytesWritten);
            }
            else
            {
                mappedTableName = s_invalidTableName;
            }
        }

        lock (this.m_lockObject)
        {
            var tableNameCache = this.m_tableNameCache;

            // Check if another thread added the mapping while we waited on the
            // lock.
            if (tableNameCache.TryGetValue(categoryName, out Tuple<string, byte[]> tableName))
            {
                return tableName;
            }

            if (mappedTableName == s_passthroughTableName)
            {
                byte[] sanitizedTableName = sanitizedTableNameStorage.ToArray();
                mappedTableName = new(ConvertSanitizedCategoryNameToString(sanitizedTableName), sanitizedTableName);
            }

            // Note: This is using copy-on-write pattern to keep the happy
            // path lockless once everything has spun up.
            Dictionary<string, Tuple<string, byte[]>> newTableNameCache = new(tableNameCache, s_dictionaryKeyComparer)
            {
                [categoryName] = mappedTableName,
            };

            this.m_tableNameCache = newTableNameCache;

            return mappedTableName;
        }
    }
}
