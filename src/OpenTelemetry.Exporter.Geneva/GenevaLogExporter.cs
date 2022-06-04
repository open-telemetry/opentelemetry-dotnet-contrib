// <copyright file="GenevaLogExporter.cs" company="OpenTelemetry Authors">
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

#if NETSTANDARD2_0 || NET461
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaLogExporter : GenevaBaseExporter<LogRecord>
{
    private const int BUFFER_SIZE = 65360; // the maximum ETW payload (inclusive)
    private const int MaxSanitizedEventNameLength = 50;

    private readonly IReadOnlyDictionary<string, object> m_customFields;
    private readonly string m_defaultEventName = "Log";
    private readonly IReadOnlyDictionary<string, object> m_prepopulatedFields;
    private readonly List<string> m_prepopulatedFieldKeys;
    private static readonly ThreadLocal<byte[]> m_buffer = new ThreadLocal<byte[]>(() => null);
    private readonly byte[] m_bufferEpilogue;
    private static readonly string[] logLevels = new string[7]
    {
        "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None",
    };

    private readonly IDataTransport m_dataTransport;
    private readonly bool shouldPassThruTableMappings;
    private bool isDisposed;
    private Func<object, string> convertToJson;

    public GenevaLogExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        // TODO: Validate mappings for reserved tablenames etc.
        if (options.TableNameMappings != null)
        {
            var tempTableMappings = new Dictionary<string, string>(options.TableNameMappings.Count, StringComparer.Ordinal);
            foreach (var kv in options.TableNameMappings)
            {
                if (Encoding.UTF8.GetByteCount(kv.Value) != kv.Value.Length)
                {
                    throw new ArgumentException("The value: \"{tableName}\" provided for TableNameMappings option contains non-ASCII characters", kv.Value);
                }

                if (kv.Key == "*")
                {
                    if (kv.Value == "*")
                    {
                        this.shouldPassThruTableMappings = true;
                    }
                    else
                    {
                        this.m_defaultEventName = kv.Value;
                    }
                }
                else
                {
                    tempTableMappings[kv.Key] = kv.Value;
                }
            }

            this.m_tableMappings = tempTableMappings;
        }

        var connectionStringBuilder = new ConnectionStringBuilder(options.ConnectionString);
        switch (connectionStringBuilder.Protocol)
        {
            case TransportProtocol.Etw:
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("ETW cannot be used on non-Windows operating systems.");
                }

                this.m_dataTransport = new EtwDataTransport(connectionStringBuilder.EtwSession);
                break;
            case TransportProtocol.Unix:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("Unix domain socket should not be used on Windows.");
                }

                var unixDomainSocketPath = connectionStringBuilder.ParseUnixDomainSocketPath();
                this.m_dataTransport = new UnixDomainSocketDataTransport(unixDomainSocketPath);
                break;
            case TransportProtocol.Tcp:
                throw new ArgumentException("TCP transport is not supported yet.");
            case TransportProtocol.Udp:
                throw new ArgumentException("UDP transport is not supported yet.");
            default:
                throw new ArgumentOutOfRangeException(nameof(connectionStringBuilder.Protocol));
        }

        this.convertToJson = options.ConvertToJson;

        if (options.PrepopulatedFields != null)
        {
            this.m_prepopulatedFieldKeys = new List<string>();
            var tempPrepopulatedFields = new Dictionary<string, object>(options.PrepopulatedFields.Count, StringComparer.Ordinal);
            foreach (var kv in options.PrepopulatedFields)
            {
                tempPrepopulatedFields[kv.Key] = kv.Value;
                this.m_prepopulatedFieldKeys.Add(kv.Key);
            }

            this.m_prepopulatedFields = tempPrepopulatedFields;
        }

        // TODO: Validate custom fields (reserved name? etc).
        if (options.CustomFields != null)
        {
            var customFields = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var name in options.CustomFields)
            {
                customFields[name] = true;
            }

            this.m_customFields = customFields;
        }

        var buffer = new byte[BUFFER_SIZE];
        var cursor = MessagePackSerializer.Serialize(buffer, 0, new Dictionary<string, object> { { "TimeFormat", "DateTime" } });
        this.m_bufferEpilogue = new byte[cursor - 0];
        Buffer.BlockCopy(buffer, 0, this.m_bufferEpilogue, 0, cursor - 0);
    }

    private readonly IReadOnlyDictionary<string, string> m_tableMappings;

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        var result = ExportResult.Success;
        foreach (var logRecord in batch)
        {
            try
            {
                var cursor = this.SerializeLogRecord(logRecord);
                this.m_dataTransport.Send(m_buffer.Value, cursor - 0);
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.ExporterException(ex); // TODO: preallocate exception or no exception
                result = ExportResult.Failure;
            }
        }

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            // DO NOT Dispose m_buffer as it is a static type
            try
            {
                (this.m_dataTransport as IDisposable)?.Dispose();
                this.m_prepopulatedFieldKeys.Clear();
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.ExporterException(ex);
            }
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }

    internal bool IsUsingUnixDomainSocket
    {
        get => this.m_dataTransport is UnixDomainSocketDataTransport;
    }

    internal int SerializeLogRecord(LogRecord logRecord)
    {
        IReadOnlyList<KeyValuePair<string, object>> listKvp;
        if (logRecord.State == null)
        {
            // When State is null, OTel SDK guarantees StateValues is populated
            // TODO: Debug.Assert?
            listKvp = logRecord.StateValues;
        }
        else
        {
            // Attempt to see if State could be ROL_KVP.
            listKvp = logRecord.State as IReadOnlyList<KeyValuePair<string, object>>;
        }

        var buffer = m_buffer.Value;
        if (buffer == null)
        {
            buffer = new byte[BUFFER_SIZE]; // TODO: handle OOM
            m_buffer.Value = buffer;
        }

        /* Fluentd Forward Mode:
        [
            "Log",
            [
                [ <timestamp>, { "env_ver": "4.0", ... } ]
            ],
            { "TimeFormat": "DateTime" }
        ]
        */

        // Structured log.
        // 2 scenarios.
        // 1. Structured logging with template
        // eg:
        // body
        // "Hello from {food} {price}."
        // part c
        // food = onion
        // price = 100
        // TODO: 2. Structured with strongly typed logging.
        var timestamp = logRecord.Timestamp;
        var cursor = 0;
        cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, 3);

        var categoryName = logRecord.CategoryName;
        string eventName = null;

        Span<byte> sanitizedEventName = default;

        // If user configured explicit TableName, use it.
        if (this.m_tableMappings != null && this.m_tableMappings.TryGetValue(categoryName, out eventName))
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, eventName);
        }
        else if (!this.shouldPassThruTableMappings)
        {
            eventName = this.m_defaultEventName;
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, eventName);
        }
        else
        {
            int cursorStartIdx = cursor;

            if (categoryName.Length > 0)
            {
                cursor = SerializeSanitizedCategoryName(buffer, cursor, categoryName);
            }

            if (cursor == cursorStartIdx)
            {
                // Serializing null as categoryName could not be sanitized into a valid string.
                cursor = MessagePackSerializer.SerializeNull(buffer, cursor);
            }
            else
            {
                // Sanitized category name has been serialized.
                sanitizedEventName = buffer.AsSpan().Slice(cursorStartIdx, cursor - cursorStartIdx);
            }
        }

        cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, 1);
        cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, 2);
        cursor = MessagePackSerializer.SerializeUtcDateTime(buffer, cursor, timestamp);
        cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, ushort.MaxValue); // Note: always use Map16 for perf consideration
        ushort cntFields = 0;
        var idxMapSizePatch = cursor - 2;

        if (this.m_prepopulatedFieldKeys != null)
        {
            for (int i = 0; i < this.m_prepopulatedFieldKeys.Count; i++)
            {
                var key = this.m_prepopulatedFieldKeys[i];
                var value = this.m_prepopulatedFields[key];
                switch (value)
                {
                    case bool vb:
                    case byte vui8:
                    case sbyte vi8:
                    case short vi16:
                    case ushort vui16:
                    case int vi32:
                    case uint vui32:
                    case long vi64:
                    case ulong vui64:
                    case float vf:
                    case double vd:
                    case string vs:
                        break;
                    default:
                        value = this.convertToJson(value);
                        break;
                }

                cursor = AddPartAField(buffer, cursor, key, value);
                cntFields += 1;
            }
        }

        // Part A - core envelope
        if (sanitizedEventName.Length != 0)
        {
            cursor = AddPartAField(buffer, cursor, Schema.V40.PartA.Name, sanitizedEventName);
        }
        else
        {
            cursor = AddPartAField(buffer, cursor, Schema.V40.PartA.Name, eventName);
        }

        cntFields += 1;

        cursor = AddPartAField(buffer, cursor, Schema.V40.PartA.Time, timestamp);
        cntFields += 1;

        // Part A - dt extension
        if (logRecord.TraceId != default)
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_dt_traceId");

            // Note: ToHexString returns the pre-calculated hex representation without allocation
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, logRecord.TraceId.ToHexString());
            cntFields += 1;
        }

        if (logRecord.SpanId != default)
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_dt_spanId");
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, logRecord.SpanId.ToHexString());
            cntFields += 1;
        }

        // Part A - ex extension
        if (logRecord.Exception != null)
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_ex_type");
            cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, logRecord.Exception.GetType().FullName);
            cntFields += 1;

            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_ex_msg");
            cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, logRecord.Exception.Message);
            cntFields += 1;
        }

        // Part B
        var logLevel = logRecord.LogLevel;

        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "severityText");
        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, logLevels[(int)logLevel]);
        cntFields += 1;

        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "severityNumber");
        cursor = MessagePackSerializer.SerializeUInt8(buffer, cursor, GetSeverityNumber(logLevel));
        cntFields += 1;

        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "name");
        cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, categoryName);
        cntFields += 1;

        bool hasEnvProperties = false;
        bool bodyPopulated = false;
        for (int i = 0; i < listKvp?.Count; i++)
        {
            var entry = listKvp[i];

            // Iteration #1 - Get those fields which become dedicated columns
            // i.e all Part B fields and opt-in Part C fields.
            if (entry.Key == "{OriginalFormat}")
            {
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "body");
                cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, logRecord.FormattedMessage ?? Convert.ToString(entry.Value, CultureInfo.InvariantCulture));
                cntFields += 1;
                bodyPopulated = true;
                continue;
            }
            else if (this.m_customFields == null || this.m_customFields.ContainsKey(entry.Key))
            {
                // TODO: the above null check can be optimized and avoided inside foreach.
                if (entry.Value != null)
                {
                    // null is not supported.
                    cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, entry.Key);
                    cursor = MessagePackSerializer.Serialize(buffer, cursor, entry.Value);
                    cntFields += 1;
                }
            }
            else
            {
                hasEnvProperties = true;
                continue;
            }
        }

        if (!bodyPopulated && logRecord.FormattedMessage != null)
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "body");
            cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, logRecord.FormattedMessage);
            cntFields += 1;
        }

        ushort scopeDepth = 0;
        int indexArrayLength = 0;
        logRecord.ForEachScope(ProcessScope, (object)null);
        void ProcessScope(LogRecordScope scope, object state)
        {
            if (++scopeDepth == 1)
            {
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "scopes");
                cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, ushort.MaxValue);
                indexArrayLength = cursor - 2;
            }

            cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, ushort.MaxValue);
            int indexMapSizeScope = cursor - 2;
            ushort keysCount = 0;

            foreach (KeyValuePair<string, object> scopeItem in scope)
            {
                string key = "scope";
                if (!string.IsNullOrEmpty(scopeItem.Key))
                {
                    key = scopeItem.Key;
                }

                cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, key);
                cursor = MessagePackSerializer.Serialize(buffer, cursor, scopeItem.Value);
                keysCount++;
            }

            MessagePackSerializer.WriteUInt16(buffer, indexMapSizeScope, keysCount);
        }

        if (scopeDepth > 0)
        {
            MessagePackSerializer.WriteUInt16(buffer, indexArrayLength, scopeDepth);
            cntFields += 1;
        }

        if (hasEnvProperties)
        {
            // Iteration #2 - Get all "other" fields and collapse them into single field
            // named "env_properties".
            ushort envPropertiesCount = 0;
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_properties");
            cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, ushort.MaxValue);
            int idxMapSizeEnvPropertiesPatch = cursor - 2;
            for (int i = 0; i < listKvp.Count; i++)
            {
                var entry = listKvp[i];
                if (entry.Key == "{OriginalFormat}" || this.m_customFields.ContainsKey(entry.Key))
                {
                    continue;
                }
                else
                {
                    cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, entry.Key);
                    cursor = MessagePackSerializer.Serialize(buffer, cursor, entry.Value);
                    envPropertiesCount += 1;
                }
            }

            cntFields += 1;
            MessagePackSerializer.WriteUInt16(buffer, idxMapSizeEnvPropertiesPatch, envPropertiesCount);
        }

        var eventId = logRecord.EventId;
        if (eventId != default)
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "eventId");
            cursor = MessagePackSerializer.SerializeInt32(buffer, cursor, eventId.Id);
            cntFields += 1;
        }

        MessagePackSerializer.WriteUInt16(buffer, idxMapSizePatch, cntFields);
        Buffer.BlockCopy(this.m_bufferEpilogue, 0, buffer, cursor, this.m_bufferEpilogue.Length);
        cursor += this.m_bufferEpilogue.Length;
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetSeverityNumber(LogLevel logLevel)
    {
        // Maps the Ilogger LogLevel to OpenTelemetry logging level.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/data-model.md#mapping-of-severitynumber
        // TODO: for improving perf simply do ((int)loglevel * 4) + 1
        // or ((int)logLevel << 2) + 1
        switch (logLevel)
        {
            case LogLevel.Trace:
                return 1;
            case LogLevel.Debug:
                return 5;
            case LogLevel.Information:
                return 9;
            case LogLevel.Warning:
                return 13;
            case LogLevel.Error:
                return 17;
            case LogLevel.Critical:
                return 21;

            // we reach default only for LogLevel.None
            // but that is filtered out anyway.
            // should we throw here then?
            default:
                return 1;
        }
    }

    // This method would map the logger category to a table name which only contains alphanumeric values with the following additions:
    // Any character that is not allowed will be removed.
    // If the resulting string is longer than 50 characters, only the first 50 characters will be taken.
    // If the first character in the resulting string is a lower-case alphabet, it will be converted to the corresponding upper-case.
    // If the resulting string still does not comply with Rule, the category name will not be serialized.
    private static int SerializeSanitizedCategoryName(byte[] buffer, int cursor, string categoryName)
    {
        int cursorStartIdx = cursor;

        // Reserve 2 bytes for storing LIMIT_MAX_STR8_LENGTH_IN_BYTES and (byte)validNameLength -
        // these 2 bytes will be back filled after iterating through categoryName.
        cursor += 2;
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
            return cursor -= 2;
        }

        for (int i = 1; i < categoryName.Length; ++i)
        {
            if (validNameLength == MaxSanitizedEventNameLength)
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
        MessagePackSerializer.WriteStr8Header(buffer, cursorStartIdx, validNameLength);

        return cursor;
    }
}
#endif
