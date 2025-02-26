// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.LinuxTracepoints.Provider;
using OpenTelemetry.Exporter.Geneva.Tld;
using OpenTelemetry.Exporter.Geneva.Transports;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva.EventHeader;

internal class EventHeaderLogExporter : EventHeaderExporter, IDisposable
{
    internal const int StringLengthLimit = (1 << 14) - 1; // 16 * 1024 - 1 = 16383
    private const int MaxSanitizedEventNameLength = 50;

    private static readonly ThreadLocal<EventHeaderDynamicBuilder> EventBuilder = new();
    private static readonly ThreadLocal<KeyValuePair<string, object>[]> PartCFields = new(); // This is used to temporarily store the PartC fields from tags
    private static readonly ThreadLocal<List<KeyValuePair<string, object?>>> EnvProperties = new();
    private static readonly Action<LogRecordScope, EventHeaderLogExporter> ProcessScopeForIndividualColumnsAction = OnProcessScopeForIndividualColumns;

    private static readonly string[] LogLevels =
    [
        "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"
    ];

    private readonly ThreadLocal<SerializationDataForScopes> serializationData = new(); // This is used for Scopes
    private readonly byte partAFieldsCount = 1; // At least one field: time
    private readonly bool shouldPassThruTableMappings;
    private readonly string defaultEventName = "Log";
    private readonly HashSet<string>? customFields;
    private readonly Dictionary<string, string>? tableMappings;
    private readonly ValueTuple<byte[], byte[]>? repeatedPartAFields;
    private readonly ExceptionStackExportMode exceptionStackExportMode;

#pragma warning disable CA2213 // Disposable fields should be disposed: it is managed and disposed in the Dispose method of EventHeaderDynamicProvider
    private readonly EventHeaderDynamicTracepoint logsTracepoint;
#pragma warning restore CA2213 // Disposable fields should be disposed

    private bool isDisposed;

    public EventHeaderLogExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);

        this.logsTracepoint = UnixUserEventsDataTransport.Instance.RegisterUserEventProviderForLogs();

        this.exceptionStackExportMode = options.ExceptionStackExportMode;

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
                        this.defaultEventName = kv.Value;
                    }
                }
                else
                {
                    tempTableMappings[kv.Key] = kv.Value;
                }
            }

            this.tableMappings = tempTableMappings;
        }

        // TODO: Validate custom fields (reserved name? etc).
        if (options.CustomFields != null)
        {
            var customFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in options.CustomFields)
            {
                customFields.Add(name);
            }

            this.customFields = customFields;
        }

        if (options.PrepopulatedFields != null)
        {
            var prePopulatedFieldsCount = (byte)(options.PrepopulatedFields.Count - 1); // PrepopulatedFields option has the key ".ver" added to it which is not needed for TLD
            this.partAFieldsCount += prePopulatedFieldsCount;

            var eb = EventBuilder.Value ??= new EventHeaderDynamicBuilder();

            eb.Reset("_"); // EventName does not matter here as we only need the serialized key-value pairs

            foreach (var entry in options.PrepopulatedFields)
            {
                var key = entry.Key;
                var value = entry.Value;

                if (entry.Key == Schema.V40.PartA.Ver)
                {
                    continue;
                }

                V40_PART_A_EVENT_HEADER_MAPPING.TryGetValue(key, out var replacementKey);
                var keyToSerialize = replacementKey ?? key;
                Serialize(eb, keyToSerialize, value);
            }

            this.repeatedPartAFields = eb.GetRawFields();
        }
    }

    public ExportResult Export(in Batch<LogRecord> batch)
    {
        if (this.logsTracepoint.IsEnabled)
        {
            var result = ExportResult.Success;
            foreach (var logRecord in batch)
            {
                try
                {
                    var eventBuilder = this.SerializeLogRecord(logRecord);
                    eventBuilder.Write(this.logsTracepoint);
                }
                catch (Exception ex)
                {
                    ExporterEventSource.Log.FailedToSendLogData(ex);
                    result = ExportResult.Failure;
                }
            }

            return result;
        }

        return ExportResult.Failure;
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        try
        {
            // DO NOT Dispose eventBuilder, keyValuePairs, and partCFields as they are static
            this.serializationData?.Dispose();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("EventHeaderLogExporter Dispose failed.", ex);
        }

        this.isDisposed = true;
    }

    internal EventHeaderDynamicBuilder SerializeLogRecord(LogRecord logRecord)
    {
        IReadOnlyList<KeyValuePair<string, object?>>? listKvp;

        // `LogRecord.State` and `LogRecord.StateValues` were marked Obsolete in https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334
#pragma warning disable 0618
        if (logRecord.StateValues != null)
        {
            listKvp = logRecord.StateValues;
        }
        else
        {
            // Attempt to see if State could be ROL_KVP.
            listKvp = logRecord.State as IReadOnlyList<KeyValuePair<string, object?>>;
        }
#pragma warning restore 0618

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

        var categoryName = logRecord.CategoryName ?? this.defaultEventName;

        // If user configured explicit TableName, use it.
        if (this.tableMappings?.TryGetValue(categoryName, out var eventName) != true)
        {
            if (!this.shouldPassThruTableMappings)
            {
                eventName = this.defaultEventName;
            }
            else
            {
                // TODO: Avoid allocation
                eventName = GetSanitizedCategoryName(categoryName);
            }
        }

        var eb = EventBuilder.Value ??= new EventHeaderDynamicBuilder(); // TODO: make sure it can be reused with a reset

        var timestamp = logRecord.Timestamp;

        eb.Reset(eventName!);
        eb.AddUInt16("__csver__", 1024, Microsoft.LinuxTracepoints.EventHeaderFieldFormat.HexInt);

        eb.AddStructWithMetadataPosition("partA", out var partAFieldsCountMetadataPosition);
        Serialize(eb, "time", timestamp); // TODO: this is different from TldLogExporter due to lack of AddFileTime method.
        byte partAFieldsCount = 1; // TODO: time field is not counted as PartA fields in TldLogExporter. Is it missing? Should time be counted as PartA field?
        if (this.repeatedPartAFields != null)
        {
            eb.AddRawFields(this.repeatedPartAFields.Value);
        }

        partAFieldsCount += this.partAFieldsCount;

        // Part A - dt extension
        if (logRecord.TraceId != default)
        {
            eb.AddString16("ext_dt_traceId", logRecord.TraceId.ToHexString());
            partAFieldsCount++;
        }

        if (logRecord.SpanId != default)
        {
            eb.AddString16("ext_dt_spanId", logRecord.SpanId.ToHexString());
            partAFieldsCount++;
        }

        // Part A - ex extension
        if (logRecord.Exception != null)
        {
            var fullName = logRecord.Exception.GetType().FullName;
            if (!string.IsNullOrEmpty(fullName))
            {
                eb.AddString16("ext_ex_type", fullName);
            }

            eb.AddString16("ext_ex_msg", logRecord.Exception.Message);

            partAFieldsCount += 2;

            if (this.exceptionStackExportMode == ExceptionStackExportMode.ExportAsString)
            {
                // The current approach relies on the existing trim
                // capabilities which trims string in excess of STRING_SIZE_LIMIT_CHAR_COUNT
                // TODO: Revisit this:
                // 1. Trim it off based on how much more bytes are available
                // before running out of limit instead of STRING_SIZE_LIMIT_CHAR_COUNT.
                // 2. Trim smarter, by trimming the middle of stack, an
                // keep top and bottom.
                var exceptionStack = logRecord.Exception.ToInvariantString();
                if (exceptionStack.Length > StringLengthLimit)
                {
                    exceptionStack = exceptionStack[..StringLengthLimit];
                }

                eb.AddString16("ext_ex_stack", exceptionStack);
                partAFieldsCount++;
            }
            else if (this.exceptionStackExportMode == ExceptionStackExportMode.ExportAsStackTraceString)
            {
                var stackTrace = logRecord.Exception.StackTrace;
                if (stackTrace != null)
                {
                    if (stackTrace.Length > StringLengthLimit)
                    {
                        stackTrace = stackTrace[..StringLengthLimit];
                    }

                    eb.AddString16("ext_ex_stack", stackTrace);
                    partAFieldsCount++;
                }
            }
        }

        eb.SetStructFieldCount(partAFieldsCountMetadataPosition, partAFieldsCount);

        // Part B

        byte partBFieldsCount = 4; // We at least have three fields in Part B: _typeName, severityText, severityNumber, name
        eb.AddStructWithMetadataPosition("PartB", out var partBFieldsCountMetadataPosition);
        eb.AddString16("_typeName", "Log");

        // `LogRecord.LogLevel` was marked Obsolete in https://github.com/open-telemetry/opentelemetry-dotnet/pull/4568
#pragma warning disable 0618
        var logLevel = logRecord.LogLevel;
#pragma warning restore 0618

        eb.AddString16("severityText", LogLevels[(int)logLevel]);
        eb.AddUInt8("severityNumber", GetSeverityNumber(logLevel));

        var eventId = logRecord.EventId;
        if (eventId != default)
        {
            eb.AddInt32("eventId", eventId.Id);
            partBFieldsCount++;
        }

        byte hasEnvProperties = 0;
        var bodyPopulated = false;
        var namePopulated = false;

        byte partCFieldsCountFromState = 0;
        var kvpArrayForPartCFields = PartCFields.Value ??= new KeyValuePair<string, object>[120];

        List<KeyValuePair<string, object?>>? envPropertiesList = null;

        for (var i = 0; i < listKvp?.Count; i++)
        {
            var entry = listKvp[i];

            // Iteration #1 - Get those fields which become dedicated columns
            // i.e all Part B fields and opt-in Part C fields.
            if (entry.Key == "{OriginalFormat}")
            {
                eb.AddString16(
                    "body",
                    logRecord.FormattedMessage ?? Convert.ToString(entry.Value, CultureInfo.InvariantCulture) ?? string.Empty);
                partBFieldsCount++;
                bodyPopulated = true;
                continue;
            }
            else if (this.customFields == null || this.customFields.Contains(entry.Key))
            {
                // TODO: the above null check can be optimized and avoided inside foreach.
                if (entry.Value != null)
                {
                    // null is not supported.
                    if (string.Equals(entry.Key, "name", StringComparison.Ordinal))
                    {
                        if (entry.Value is string nameValue)
                        {
                            // name must be string according to Part B in Common Schema. Skip serializing this field otherwise
                            eb.AddString16("name", nameValue);
                            namePopulated = true;
                        }
                    }
                    else
                    {
                        kvpArrayForPartCFields[partCFieldsCountFromState] = new(entry.Key, entry.Value);
                        partCFieldsCountFromState++;
                    }
                }
            }
            else
            {
                if (hasEnvProperties == 0)
                {
                    hasEnvProperties = 1;
                    envPropertiesList = EnvProperties.Value ??= [];

                    envPropertiesList.Clear();
                }

                // TODO: This could lead to unbounded memory usage.
                envPropertiesList!.Add(new(entry.Key, entry.Value));
            }
        }

        if (!namePopulated)
        {
            eb.AddString16("name", categoryName);
        }

        if (!bodyPopulated && logRecord.FormattedMessage != null)
        {
            eb.AddString16("body", logRecord.FormattedMessage);
            partBFieldsCount++;
        }

        eb.SetStructFieldCount(partBFieldsCountMetadataPosition, partBFieldsCount);

        // Part C

        // Prepare state for scopes
        var dataForScopes = this.serializationData.Value ??= new();

        dataForScopes.HasEnvProperties = hasEnvProperties;
        dataForScopes.PartCFieldsCountFromState = partCFieldsCountFromState;

        logRecord.ForEachScope(ProcessScopeForIndividualColumnsAction, this);

        // Update the variables that could have been modified in ProcessScopeForIndividualColumns
        hasEnvProperties = dataForScopes.HasEnvProperties;
        partCFieldsCountFromState = dataForScopes.PartCFieldsCountFromState;

        var partCFieldsCount = (byte)(partCFieldsCountFromState + hasEnvProperties); // We at least have these many fields in Part C

        if (partCFieldsCount > 0)
        {
            eb.AddStructWithMetadataPosition("PartC", out var partCFieldsCountMetadataPosition);

            for (var i = 0; i < partCFieldsCountFromState; i++)
            {
                Serialize(eb, kvpArrayForPartCFields[i].Key, kvpArrayForPartCFields[i].Value);
            }

            if (hasEnvProperties == 1)
            {
                // Get all "other" fields and collapse them into single field
                // named "env_properties".
                var serializedEnvPropertiesStringAsBytes = JsonSerializer.SerializeKeyValuePairsListAsBytes(envPropertiesList, out var count);
                eb.AddString8("env_properties", serializedEnvPropertiesStringAsBytes);
            }

            eb.SetStructFieldCount(partCFieldsCountMetadataPosition, partCFieldsCount);
        }

        return eb;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetSeverityNumber(LogLevel logLevel)
    {
        // Maps the Ilogger LogLevel to OpenTelemetry logging level.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/data-model.md#mapping-of-severitynumber
        // TODO: for improving perf simply do ((int)loglevel * 4) + 1
        // or ((int)logLevel << 2) + 1
        return logLevel switch
        {
            LogLevel.Trace => 1,
            LogLevel.Debug => 5,
            LogLevel.Information => 9,
            LogLevel.Warning => 13,
            LogLevel.Error => 17,
            LogLevel.Critical => 21,

            // we reach default only for LogLevel.None
            // but that is filtered out anyway.
            // should we throw here then?
            LogLevel.None => 1,
            _ => 1,
        };
    }

    // This method would map the logger category to a table name which only contains alphanumeric values with the following additions:
    // Any character that is not allowed will be removed.
    // If the resulting string is longer than 50 characters, only the first 50 characters will be taken.
    // If the first character in the resulting string is a lower-case alphabet, it will be converted to the corresponding upper-case.
    // If the resulting string still does not comply with Rule, the category name will not be serialized.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetSanitizedCategoryName(string categoryName)
    {
        var validNameLength = 0;
        Span<char> result = stackalloc char[MaxSanitizedEventNameLength];

        // Special treatment for the first character.
        var firstChar = categoryName[0];
        if (firstChar is >= 'A' and <= 'Z')
        {
            result[0] = firstChar;
            ++validNameLength;
        }
        else if (firstChar is >= 'a' and <= 'z')
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

        for (var i = 1; i < categoryName.Length; i++)
        {
            if (validNameLength == MaxSanitizedEventNameLength)
            {
                break;
            }

            var cur = categoryName[i];
            if (cur is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                result[validNameLength] = cur;
                ++validNameLength;
            }
        }

        return result.Slice(0, validNameLength).ToString();
    }

    private static void OnProcessScopeForIndividualColumns(LogRecordScope scope, EventHeaderLogExporter state)
    {
        Debug.Assert(state.serializationData.Value != null, "state.serializationData was null");
        Debug.Assert(PartCFields.Value != null, "PartCFields.Value was null");

        var stateData = state.serializationData.Value!;
        var customFields = state.customFields;
        var kvpArrayForPartCFields = PartCFields.Value!;

        List<KeyValuePair<string, object?>>? envPropertiesList = null;

        foreach (var scopeItem in scope)
        {
            if (string.IsNullOrEmpty(scopeItem.Key) || scopeItem.Key == "{OriginalFormat}")
            {
                continue;
            }

            if (customFields == null || customFields.Contains(scopeItem.Key))
            {
                if (scopeItem.Value != null)
                {
                    kvpArrayForPartCFields[stateData.PartCFieldsCountFromState] = new(scopeItem.Key, scopeItem.Value);
                    stateData.PartCFieldsCountFromState++;
                }
            }
            else
            {
                if (stateData.HasEnvProperties == 0)
                {
                    stateData.HasEnvProperties = 1;

                    envPropertiesList = EnvProperties.Value ??= [];

                    envPropertiesList.Clear();
                }

                // TODO: This could lead to unbounded memory usage.
                envPropertiesList!.Add(new(scopeItem.Key, scopeItem.Value));
            }
        }
    }

    private sealed class SerializationDataForScopes
    {
        public byte HasEnvProperties;
        public byte PartCFieldsCountFromState;
    }
}

#endif
