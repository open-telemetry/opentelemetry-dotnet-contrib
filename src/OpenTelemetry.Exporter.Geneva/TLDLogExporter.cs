// <copyright file="TLDLogExporter.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.TraceLoggingDynamic;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class TLDLogExporter : TLDBaseExporter<LogRecord>
{
    private const int StringLengthLimit = (1 << 14) - 1; // 16 * 1024 - 1 = 16383
    private const int MaxSanitizedEventNameLength = 50;

    private static readonly string[] logLevels = new string[7]
    {
        "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None",
    };

    private readonly byte partAFieldsCount = 1; // At least one field: time
    private readonly bool shouldPassThruTableMappings;
    private readonly string m_defaultEventName = "Log";
    private readonly IReadOnlyDictionary<string, object> m_customFields;
    private readonly IReadOnlyDictionary<string, string> m_tableMappings;
    private readonly Tuple<byte[], byte[]> repeatedPartAFields;

    private readonly EventProvider eventProvider;
    private static readonly ThreadLocal<EventBuilder> eventBuilder = new(() => new(UncheckedASCIIEncoding.SharedInstance));
    private static readonly ThreadLocal<List<KeyValuePair<string, object>>> envProperties = new(() => new());
    private static readonly ThreadLocal<KeyValuePair<string, object>[]> partCFields = new(() => new KeyValuePair<string, object>[120]); // This is used to temporarily store the PartC fields from tags

    private bool isDisposed;

    public TLDLogExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        // TODO: Validate mappings for reserved tablenames etc.
        if (options.TableNameMappings != null)
        {
            var tempTableMappings = new Dictionary<string, string>(options.TableNameMappings.Count, StringComparer.Ordinal);
            foreach (var kv in options.TableNameMappings)
            {
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

                this.eventProvider = new EventProvider(connectionStringBuilder.EtwSession);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(connectionStringBuilder.Protocol));
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

        if (options.PrepopulatedFields != null)
        {
            var prePopulatedFieldsCount = (byte)(options.PrepopulatedFields.Count - 1); // PrepopulatedFields option has the key ".ver" added to it which is not needed for TLD
            this.partAFieldsCount += prePopulatedFieldsCount;

            var eb = eventBuilder.Value;
            eb.Reset("_"); // EventName does not matter here as we only need the serialized key-value pairs

            foreach (var entry in options.PrepopulatedFields)
            {
                var key = entry.Key;
                var value = entry.Value;

                if (entry.Key == Schema.V40.PartA.Ver)
                {
                    continue;
                }

                V40_PART_A_TLD_MAPPING.TryGetValue(key, out string replacementKey);
                var keyToSerialize = replacementKey ?? key;
                this.Serialize(eb, keyToSerialize, value);

                this.repeatedPartAFields = eb.GetRawFields();
            }
        }
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        var result = ExportResult.Success;
        if (this.eventProvider.IsEnabled())
        {
            foreach (var activity in batch)
            {
                try
                {
                    this.SerializeLogRecord(activity);
                    this.eventProvider.Write(eventBuilder.Value);
                }
                catch (Exception ex)
                {
                    ExporterEventSource.Log.FailedToSendTraceData(ex); // TODO: preallocate exception or no exception
                    result = ExportResult.Failure;
                }
            }
        }
        else
        {
            return ExportResult.Failure;
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
            try
            {
                // DO NOT Dispose eventBuilder, keyValuePairs, and partCFields as they are static
                this.eventProvider?.Dispose();
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.ExporterException("GenevaTraceExporter Dispose failed.", ex);
            }
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }

    internal void SerializeLogRecord(LogRecord logRecord)
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

        string eventName;
        var categoryName = logRecord.CategoryName;

        // If user configured explicit TableName, use it.
        if (this.m_tableMappings != null && this.m_tableMappings.TryGetValue(categoryName, out eventName))
        {
        }
        else if (!this.shouldPassThruTableMappings)
        {
            eventName = this.m_defaultEventName;
        }
        else
        {
            // TODO: Avoid allocation
            eventName = GetSanitizedCategoryName(categoryName);
        }

        var eb = eventBuilder.Value;
        var timestamp = logRecord.Timestamp;

        eb.Reset(eventName);
        eb.AddUInt16("__csver__", 1024, EventOutType.Hex);

        var partAFieldsCountPatch = eb.AddStruct("PartA", this.partAFieldsCount);
        eb.AddFileTime("time", timestamp);
        if (this.repeatedPartAFields != null)
        {
            eb.AppendRawFields(this.repeatedPartAFields);
        }

        byte partAFieldsCount = this.partAFieldsCount;

        // Part A - dt extension
        if (logRecord.TraceId != default)
        {
            eb.AddCountedString("ext_dt_traceId", logRecord.TraceId.ToHexString());
            partAFieldsCount++;
        }

        if (logRecord.SpanId != default)
        {
            eb.AddCountedString("ext_dt_spanId", logRecord.TraceId.ToHexString());
            partAFieldsCount++;
        }

        // Part A - ex extension
        if (logRecord.Exception != null)
        {
            eb.AddCountedAnsiString("ext_ex_type", logRecord.Exception.GetType().FullName, Encoding.UTF8);
            eb.AddCountedAnsiString("ext_ex_msg", logRecord.Exception.Message, Encoding.UTF8);
            partAFieldsCount += 2;
        }

        eb.SetStructFieldCount(partAFieldsCountPatch, partAFieldsCount);

        // Part B

        byte partBFieldsCount = 3;
        var partBFieldsCountPatch = eb.AddStruct("PartB", partBFieldsCount); // We at least have three fields in Part B: severityText, severityNumber, name

        var logLevel = logRecord.LogLevel;
        eb.AddCountedString("severityText", logLevels[(int)logLevel]);
        eb.AddUInt8("severityNumber", GetSeverityNumber(logLevel));
        eb.AddCountedAnsiString("name", categoryName, Encoding.UTF8);

        byte hasEnvProperties = 0;
        bool bodyPopulated = false;

        int partCFieldsCountFromState = 0;
        var kvpArrayForPartCFields = partCFields.Value;
        List<KeyValuePair<string, object>> envPropertiesList = null;

        for (int i = 0; i < listKvp?.Count; i++)
        {
            var entry = listKvp[i];

            // Iteration #1 - Get those fields which become dedicated columns
            // i.e all Part B fields and opt-in Part C fields.
            if (entry.Key == "{OriginalFormat}")
            {
                eb.AddCountedAnsiString("body", logRecord.FormattedMessage ?? Convert.ToString(entry.Value, CultureInfo.InvariantCulture), Encoding.UTF8);
                partBFieldsCount++;
                bodyPopulated = true;
                continue;
            }
            else if (this.m_customFields == null || this.m_customFields.ContainsKey(entry.Key))
            {
                // TODO: the above null check can be optimized and avoided inside foreach.
                if (entry.Value != null)
                {
                    // null is not supported.
                    kvpArrayForPartCFields[partCFieldsCountFromState] = new(entry.Key, entry.Value);
                    partCFieldsCountFromState++;
                }
            }
            else
            {
                if (hasEnvProperties == 0)
                {
                    hasEnvProperties = 1;
                    envPropertiesList = envProperties.Value;
                    envPropertiesList.Clear();
                }

                envPropertiesList.Add(new(entry.Key, entry.Value));
            }
        }

        if (!bodyPopulated && logRecord.FormattedMessage != null)
        {
            eb.AddCountedAnsiString("body", logRecord.FormattedMessage, Encoding.UTF8);
            partBFieldsCount++;
        }

        eb.SetStructFieldCount(partBFieldsCountPatch, partBFieldsCount);

        // Part C

        var partCFieldsCount = partCFieldsCountFromState + hasEnvProperties; // We at least have these many fields in Part C
        var partCFieldsCountPatch = eb.AddStruct("PartC", (byte)partCFieldsCount);

        for (int i = 0; i < partCFieldsCountFromState; i++)
        {
            this.Serialize(eb, kvpArrayForPartCFields[i].Key, kvpArrayForPartCFields[i].Value);
        }

        if (hasEnvProperties == 1)
        {
            // Get all "other" fields and collapse them into single field
            // named "env_properties".
            var serializedEnvProperties = JsonSerializer.SerializeMap(envPropertiesList);
            eb.AddCountedAnsiString("env_properties", serializedEnvProperties, Encoding.UTF8, 0, Math.Min(serializedEnvProperties.Length, StringLengthLimit));
        }

        var eventId = logRecord.EventId;
        if (eventId != default)
        {
            eb.AddInt32("eventId", eventId.Id);
            partCFieldsCount++;
        }

        eb.SetStructFieldCount(partCFieldsCountPatch, (byte)partCFieldsCount);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetSanitizedCategoryName(string categoryName)
    {
        int validNameLength = 0;
        Span<char> result = stackalloc char[MaxSanitizedEventNameLength];

        // Special treatment for the first character.
        var firstChar = categoryName[0];
        if (firstChar >= 'A' && firstChar <= 'Z')
        {
            result[0] = firstChar;
            ++validNameLength;
        }
        else if (firstChar >= 'a' && firstChar <= 'z')
        {
            // If the first character in the resulting string is a lower-case alphabet,
            // it will be converted to the corresponding upper-case.
            result[0] = (char)(firstChar - 32);
            ++validNameLength;
        }
        else
        {
            // Not a valid name.
            return string.Empty;
        }

        for (int i = 1; i < categoryName.Length; i++)
        {
            if (validNameLength == MaxSanitizedEventNameLength)
            {
                break;
            }

            var cur = categoryName[i];
            if ((cur >= 'a' && cur <= 'z') || (cur >= 'A' && cur <= 'Z') || (cur >= '0' && cur <= '9'))
            {
                result[i] = cur;
                ++validNameLength;
            }
        }

        return result.Slice(0, validNameLength).ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Serialize(EventBuilder eb, string key, object value)
    {
        switch (value)
        {
            case bool vb:
                eb.AddUInt8(key, (byte)(vb ? 1 : 0), EventOutType.Boolean);
                break;
            case byte vui8:
                eb.AddUInt8(key, vui8);
                break;
            case sbyte vi8:
                eb.AddInt8(key, vi8);
                break;
            case short vi16:
                eb.AddInt16(key, vi16);
                break;
            case ushort vui16:
                eb.AddUInt16(key, vui16);
                break;
            case int vi32:
                eb.AddInt32(key, vi32);
                break;
            case uint vui32:
                eb.AddUInt32(key, vui32);
                break;
            case long vi64:
                eb.AddInt64(key, vi64);
                break;
            case ulong vui64:
                eb.AddUInt64(key, vui64);
                break;
            case float vf:
                eb.AddFloat32(key, vf);
                break;
            case double vd:
                eb.AddFloat64(key, vd);
                break;
            case string vs:
                eb.AddCountedAnsiString(key, vs, Encoding.UTF8, 0, Math.Min(vs.Length, StringLengthLimit));
                break;
            case DateTime vdt:
                eb.AddFileTime(key, vdt.ToUniversalTime());
                break;

            // TODO: case bool[]
            // TODO: case obj[]
            case byte[] vui8array:
                eb.AddUInt8Array(key, vui8array);
                break;
            case sbyte[] vi8array:
                eb.AddInt8Array(key, vi8array);
                break;
            case short[] vi16array:
                eb.AddInt16Array(key, vi16array);
                break;
            case ushort[] vui16array:
                eb.AddUInt16Array(key, vui16array);
                break;
            case int[] vi32array:
                eb.AddInt32Array(key, vi32array);
                break;
            case uint[] vui32array:
                eb.AddUInt32Array(key, vui32array);
                break;
            case long[] vi64array:
                eb.AddInt64Array(key, vi64array);
                break;
            case ulong[] vui64array:
                eb.AddUInt64Array(key, vui64array);
                break;
            case float[] vfarray:
                eb.AddFloat32Array(key, vfarray);
                break;
            case double[] vdarray:
                eb.AddFloat64Array(key, vdarray);
                break;
            case string[] vsarray:
                eb.AddCountedStringArray(key, vsarray);
                break;
            case DateTime[] vdtarray:
                for (int i = 0; i < vdtarray.Length; i++)
                {
                    vdtarray[i] = vdtarray[i].ToUniversalTime();
                }

                eb.AddFileTimeArray(key, vdtarray);
                break;
            default:
                string repr;
                try
                {
                    repr = Convert.ToString(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    repr = $"ERROR: type {value.GetType().FullName} is not supported";
                }

                eb.AddCountedAnsiString(key, repr, Encoding.UTF8, 0, Math.Min(repr.Length, StringLengthLimit));
                break;
        }
    }
}
