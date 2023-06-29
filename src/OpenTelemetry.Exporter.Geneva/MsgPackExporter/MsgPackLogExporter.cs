// <copyright file="MsgPackLogExporter.cs" company="OpenTelemetry Authors">
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
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MsgPackLogExporter : MsgPackExporter, IDisposable
{
    private const int BUFFER_SIZE = 65360; // the maximum ETW payload (inclusive)

    private static readonly ThreadLocal<byte[]> m_buffer = new ThreadLocal<byte[]>(() => null);
    private static readonly string[] logLevels = new string[7]
    {
        "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None",
    };

    private readonly bool m_shouldExportEventName;
    private readonly TableNameSerializer m_tableNameSerializer;
    private readonly Dictionary<string, object> m_customFields;
    private readonly Dictionary<string, object> m_prepopulatedFields;
    private readonly ExceptionStackExportMode m_exportExceptionStack;
    private readonly List<string> m_prepopulatedFieldKeys;
    private readonly byte[] m_bufferEpilogue;
    private readonly IDataTransport m_dataTransport;

    // This is used for Scopes
    private readonly ThreadLocal<SerializationDataForScopes> m_serializationData = new(() => null);
    private bool isDisposed;

    public MsgPackLogExporter(GenevaExporterOptions options)
    {
        this.m_tableNameSerializer = new(options, defaultTableName: "Log");
        this.m_exportExceptionStack = options.ExceptionStackExportMode;

        this.m_shouldExportEventName = (options.EventNameExportMode & EventNameExportMode.ExportAsPartAName) != 0;

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
            default:
                throw new ArgumentOutOfRangeException(nameof(connectionStringBuilder.Protocol));
        }

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

    public ExportResult Export(in Batch<LogRecord> batch)
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
                ExporterEventSource.Log.FailedToSendLogData(ex); // TODO: preallocate exception or no exception
                result = ExportResult.Failure;
            }
        }

        return result;
    }

    internal bool IsUsingUnixDomainSocket
    {
        get => this.m_dataTransport is UnixDomainSocketDataTransport;
    }

    internal int SerializeLogRecord(LogRecord logRecord)
    {
        // `LogRecord.State` and `LogRecord.StateValues` were marked Obsolete in https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334
#pragma warning disable 0618
        IReadOnlyList<KeyValuePair<string, object>> listKvp;
        if (logRecord.StateValues != null)
        {
            listKvp = logRecord.StateValues;
        }
        else
        {
            // Attempt to see if State could be ROL_KVP.
            listKvp = logRecord.State as IReadOnlyList<KeyValuePair<string, object>>;
        }
#pragma warning restore 0618

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

        cursor = this.m_tableNameSerializer.ResolveAndSerializeTableNameForCategoryName(buffer, cursor, categoryName, out ReadOnlySpan<byte> eventName);

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
                cursor = AddPartAField(buffer, cursor, key, value);
                cntFields += 1;
            }
        }

        // Part A - core envelope

        var eventId = logRecord.EventId;
        bool hasEventId = eventId != default;

        if (hasEventId && this.m_shouldExportEventName && !string.IsNullOrWhiteSpace(eventId.Name))
        {
            // Export `eventId.Name` as the value for `env_name`
            cursor = AddPartAField(buffer, cursor, Schema.V40.PartA.Name, eventId.Name);
            cntFields += 1;
        }
        else
        {
            // Export the table name as the value for `env_name`
            cursor = AddPartAField(buffer, cursor, Schema.V40.PartA.Name, eventName);
            cntFields += 1;
        }

        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_time");
        cursor = MessagePackSerializer.SerializeUtcDateTime(buffer, cursor, timestamp); // LogRecord.Timestamp should already be converted to UTC format in the SDK
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

        // Prepare state for scopes
        var dataForScopes = this.m_serializationData.Value;
        if (dataForScopes == null)
        {
            dataForScopes = new SerializationDataForScopes
            {
                Buffer = buffer,
            };

            this.m_serializationData.Value = dataForScopes;
        }

        dataForScopes.Cursor = cursor;
        dataForScopes.FieldsCount = cntFields;
        dataForScopes.HasEnvProperties = hasEnvProperties;

        logRecord.ForEachScope(ProcessScopeForIndividualColumns, this);

        // Update the variables that could have been modified in ProcessScopeForIndividualColumns
        hasEnvProperties = dataForScopes.HasEnvProperties;
        cursor = dataForScopes.Cursor;
        cntFields = dataForScopes.FieldsCount;

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

            // Prepare state for scopes
            dataForScopes.Cursor = cursor;
            dataForScopes.EnvPropertiesCount = envPropertiesCount;

            logRecord.ForEachScope(ProcessScopeForEnvProperties, this);

            // Update the variables that could have been modified in ProcessScopeForEnvProperties
            cursor = dataForScopes.Cursor;
            envPropertiesCount = dataForScopes.EnvPropertiesCount;

            cntFields += 1;
            MessagePackSerializer.WriteUInt16(buffer, idxMapSizeEnvPropertiesPatch, envPropertiesCount);
        }

        if (hasEventId)
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "eventId");
            cursor = MessagePackSerializer.SerializeInt32(buffer, cursor, eventId.Id);
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

            if (this.m_exportExceptionStack == ExceptionStackExportMode.ExportAsString)
            {
                // The current approach relies on the existing trim
                // capabilities which trims string in excess of STRING_SIZE_LIMIT_CHAR_COUNT
                // TODO: Revisit this:
                // 1. Trim it off based on how much more bytes are available
                // before running out of limit instead of STRING_SIZE_LIMIT_CHAR_COUNT.
                // 2. Trim smarter, by trimming the middle of stack, an
                // keep top and bottom.
                var exceptionStack = logRecord.Exception.ToInvariantString();
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_ex_stack");
                cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, exceptionStack);
                cntFields += 1;
            }
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

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        // DO NOT Dispose m_buffer as it is a static type
        try
        {
            (this.m_dataTransport as IDisposable)?.Dispose();
            this.m_serializationData.Dispose();
            this.m_prepopulatedFieldKeys.Clear();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("MsgPackLogExporter Dispose failed.", ex);
        }

        this.isDisposed = true;
    }

    private static readonly Action<LogRecordScope, MsgPackLogExporter> ProcessScopeForIndividualColumns = (scope, state) =>
    {
        var stateData = state.m_serializationData.Value;
        var customFields = state.m_customFields;

        foreach (KeyValuePair<string, object> scopeItem in scope)
        {
            if (string.IsNullOrEmpty(scopeItem.Key) || scopeItem.Key == "{OriginalFormat}")
            {
                continue;
            }

            if (customFields == null || customFields.ContainsKey(scopeItem.Key))
            {
                if (scopeItem.Value != null)
                {
                    // null is not supported.
                    stateData.Cursor = MessagePackSerializer.SerializeUnicodeString(stateData.Buffer, stateData.Cursor, scopeItem.Key);
                    stateData.Cursor = MessagePackSerializer.Serialize(stateData.Buffer, stateData.Cursor, scopeItem.Value);
                    stateData.FieldsCount += 1;
                }
            }
            else
            {
                stateData.HasEnvProperties = true;
            }
        }
    };

    private static readonly Action<LogRecordScope, MsgPackLogExporter> ProcessScopeForEnvProperties = (scope, state) =>
    {
        var stateData = state.m_serializationData.Value;
        var customFields = state.m_customFields;

        foreach (KeyValuePair<string, object> scopeItem in scope)
        {
            if (string.IsNullOrEmpty(scopeItem.Key) || scopeItem.Key == "{OriginalFormat}")
            {
                continue;
            }

            if (!customFields.ContainsKey(scopeItem.Key))
            {
                stateData.Cursor = MessagePackSerializer.SerializeUnicodeString(stateData.Buffer, stateData.Cursor, scopeItem.Key);
                stateData.Cursor = MessagePackSerializer.Serialize(stateData.Buffer, stateData.Cursor, scopeItem.Value);
                stateData.EnvPropertiesCount += 1;
            }
        }
    };

    private sealed class SerializationDataForScopes
    {
        public bool HasEnvProperties;
        public ushort EnvPropertiesCount;

        public int Cursor;
        public byte[] Buffer;
        public ushort FieldsCount;
    }
}
