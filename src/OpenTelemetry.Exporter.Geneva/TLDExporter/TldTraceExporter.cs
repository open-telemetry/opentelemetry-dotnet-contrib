// <copyright file="TldTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Text;
using System.Threading;
using OpenTelemetry.Exporter.Geneva.External;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva.TldExporter;

internal sealed class TldTraceExporter : TldExporter, IDisposable
{
    // TODO: Is using a single ThreadLocal a better idea?
    private static readonly ThreadLocal<EventBuilder> eventBuilder = new(() => null);
    private static readonly ThreadLocal<List<KeyValuePair<string, object>>> keyValuePairs = new(() => null);
    private static readonly ThreadLocal<KeyValuePair<string, object>[]> partCFields = new(() => null); // This is used to temporarily store the PartC fields from tags

    private static readonly string INVALID_SPAN_ID = default(ActivitySpanId).ToHexString();

    private static readonly IReadOnlyDictionary<string, string> CS40_PART_B_MAPPING = new Dictionary<string, string>
    {
        ["db.system"] = "dbSystem",
        ["db.name"] = "dbName",
        ["db.statement"] = "dbStatement",

        ["http.method"] = "httpMethod",
        ["http.url"] = "httpUrl",
        ["http.status_code"] = "httpStatusCode",

        ["messaging.system"] = "messagingSystem",
        ["messaging.destination"] = "messagingDestination",
        ["messaging.url"] = "messagingUrl",
    };

    private readonly string partAName = "Span";
    private readonly byte partAFieldsCount = 3; // At least three fields: time, ext_dt_traceId, ext_dt_spanId
    private readonly IReadOnlyDictionary<string, object> m_customFields;
    private readonly Tuple<byte[], byte[]> repeatedPartAFields;

    private readonly EventProvider eventProvider;

    private bool isDisposed;

    public TldTraceExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        var connectionStringBuilder = new ConnectionStringBuilder(options.ConnectionString);
        this.eventProvider = new EventProvider(connectionStringBuilder.EtwSession);

        if (options.TableNameMappings != null
            && options.TableNameMappings.TryGetValue("Span", out var customTableName))
        {
            this.partAName = customTableName;
        }

        // TODO: Validate custom fields (reserved name? etc).
        if (options.CustomFields != null)
        {
            var customFields = new Dictionary<string, object>(StringComparer.Ordinal);

            // Seed customFields with Span PartB
            customFields["azureResourceProvider"] = true;
            foreach (var name in CS40_PART_B_MAPPING.Values)
            {
                customFields[name] = true;
            }

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
            if (eb == null)
            {
                eb = new EventBuilder(UncheckedASCIIEncoding.SharedInstance);
                eventBuilder.Value = eb;
            }

            eb.Reset(this.partAName);

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
                Serialize(eb, keyToSerialize, value);

                this.repeatedPartAFields = eb.GetRawFields();
            }
        }
    }

    public ExportResult Export(in Batch<Activity> batch)
    {
        var result = ExportResult.Success;
        if (this.eventProvider.IsEnabled())
        {
            foreach (var activity in batch)
            {
                try
                {
                    this.SerializeActivity(activity);
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

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        try
        {
            // DO NOT Dispose eventBuilder, keyValuePairs, and partCFields as they are static
            this.eventProvider?.Dispose();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("TldTraceExporter Dispose failed.", ex);
        }

        this.isDisposed = true;
    }

    internal void SerializeActivity(Activity activity)
    {
        var eb = eventBuilder.Value;
        if (eb == null)
        {
            eb = new EventBuilder(UncheckedASCIIEncoding.SharedInstance);
            eventBuilder.Value = eb;
        }

        eb.Reset(this.partAName);
        eb.AddUInt16("__csver__", 1024, EventOutType.Hex);

        var dtBegin = activity.StartTimeUtc;
        var tsBegin = dtBegin.Ticks;
        var tsEnd = tsBegin + activity.Duration.Ticks;
        var dtEnd = new DateTime(tsEnd);

        eb.AddStruct("PartA", this.partAFieldsCount);
        eb.AddFileTime("time", dtEnd);
        eb.AddCountedString("ext_dt_traceId", activity.Context.TraceId.ToHexString());
        eb.AddCountedString("ext_dt_spanId", activity.Context.SpanId.ToHexString());
        if (this.repeatedPartAFields != null)
        {
            eb.AppendRawFields(this.repeatedPartAFields);
        }

        byte partBFieldsCount = 5;
        var partBFieldsCountPatch = eb.AddStruct("PartB", partBFieldsCount); // We at least have five fields in PartB: _typeName, name, kind, startTime, success
        eb.AddCountedString("_typeName", "Span");
        eb.AddCountedAnsiString("name", activity.DisplayName, Encoding.UTF8);
        eb.AddUInt8("kind", (byte)activity.Kind);
        eb.AddFileTime("startTime", dtBegin);

        var strParentId = activity.ParentSpanId.ToHexString();

        // Note: this should be blazing fast since Object.ReferenceEquals(strParentId, INVALID_SPAN_ID) == true
        if (!string.Equals(strParentId, INVALID_SPAN_ID, StringComparison.Ordinal))
        {
            eb.AddCountedString("parentId", strParentId);
            partBFieldsCount++;
        }

        var linkEnumerator = activity.EnumerateLinks();
        if (linkEnumerator.MoveNext())
        {
            var keyValuePairsForLinks = keyValuePairs.Value;
            if (keyValuePairsForLinks == null)
            {
                keyValuePairsForLinks = new List<KeyValuePair<string, object>>();
                keyValuePairs.Value = keyValuePairsForLinks;
            }

            keyValuePairsForLinks.Clear();

            do
            {
                ref readonly var link = ref linkEnumerator.Current;

                // TODO: This could lead to unbounded memory usage.
                keyValuePairsForLinks.Add(new("toTraceId", link.Context.TraceId.ToHexString()));
                keyValuePairsForLinks.Add(new("toSpanId", link.Context.SpanId.ToHexString()));
            }
            while (linkEnumerator.MoveNext());

            var serializedLinksStringAsBytes = JsonSerializer.SerializeKeyValuePairsListAsBytes(keyValuePairsForLinks, out var count);
            eb.AddCountedAnsiString("links", serializedLinksStringAsBytes, 0, count);

            partBFieldsCount++;
        }

        byte hasEnvProperties = 0;
        byte isStatusSuccess = 1;
        string statusDescription = string.Empty;

        int partCFieldsCountFromTags = 0;
        var kvpArrayForPartCFields = partCFields.Value;
        if (kvpArrayForPartCFields == null)
        {
            kvpArrayForPartCFields = new KeyValuePair<string, object>[120];
            partCFields.Value = kvpArrayForPartCFields;
        }

        List<KeyValuePair<string, object>> envPropertiesList = null;

        foreach (ref readonly var entry in activity.EnumerateTagObjects())
        {
            // TODO: check name collision
            if (CS40_PART_B_MAPPING.TryGetValue(entry.Key, out string replacementKey))
            {
                Serialize(eb, replacementKey, entry.Value);
                partBFieldsCount++;
            }
            else if (string.Equals(entry.Key, "otel.status_code", StringComparison.Ordinal))
            {
                if (string.Equals(Convert.ToString(entry.Value, CultureInfo.InvariantCulture), "ERROR", StringComparison.Ordinal))
                {
                    isStatusSuccess = 0;
                }

                continue;
            }
            else if (string.Equals(entry.Key, "otel.status_description", StringComparison.Ordinal))
            {
                statusDescription = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
                continue;
            }
            else if (this.m_customFields == null || this.m_customFields.ContainsKey(entry.Key))
            {
                // TODO: the above null check can be optimized and avoided inside foreach.
                kvpArrayForPartCFields[partCFieldsCountFromTags] = new(entry.Key, entry.Value);
                partCFieldsCountFromTags++;
            }
            else
            {
                if (hasEnvProperties == 0)
                {
                    hasEnvProperties = 1;
                    envPropertiesList = keyValuePairs.Value;
                    if (envPropertiesList == null)
                    {
                        envPropertiesList = new List<KeyValuePair<string, object>>();
                        keyValuePairs.Value = envPropertiesList;
                    }

                    envPropertiesList.Clear();
                }

                // TODO: This could lead to unbounded memory usage.
                envPropertiesList.Add(new(entry.Key, entry.Value));
            }
        }

        if (activity.Status != ActivityStatusCode.Unset)
        {
            if (activity.Status == ActivityStatusCode.Error)
            {
                isStatusSuccess = 0;
            }

            if (!string.IsNullOrEmpty(activity.StatusDescription))
            {
                eb.AddCountedAnsiString("statusMessage", statusDescription, Encoding.UTF8);
                partBFieldsCount++;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(activity.StatusDescription))
            {
                eb.AddCountedAnsiString("statusMessage", statusDescription, Encoding.UTF8);
                partBFieldsCount++;
            }
        }

        // Do not increment partBFieldsCount here as the field "success" has already been accounted for
        eb.AddUInt8("success", isStatusSuccess, EventOutType.Boolean);

        eb.SetStructFieldCount(partBFieldsCountPatch, partBFieldsCount);

        var partCFieldsCount = partCFieldsCountFromTags + hasEnvProperties;
        eb.AddStruct("PartC", (byte)partCFieldsCount);

        for (int i = 0; i < partCFieldsCountFromTags; i++)
        {
            Serialize(eb, kvpArrayForPartCFields[i].Key, kvpArrayForPartCFields[i].Value);
        }

        if (hasEnvProperties == 1)
        {
            // Get all "other" fields and collapse them into single field
            // named "env_properties".

            var serializedEnvPropertiesStringAsBytes = JsonSerializer.SerializeKeyValuePairsListAsBytes(envPropertiesList, out var count);
            eb.AddCountedAnsiString("env_properties", serializedEnvPropertiesStringAsBytes, 0, count);
        }
    }
}
