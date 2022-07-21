// <copyright file="TLDTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.TraceLoggingDynamic;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva
{
    internal class TLDTraceExporter : GenevaBaseExporter<Activity>
    {
        public TLDTraceExporter(GenevaExporterOptions options)
        {
            Guard.ThrowIfNull(options);
            Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

            if (options.TableNameMappings != null
                && options.TableNameMappings.TryGetValue("Span", out var customTableName))
            {
                if (string.IsNullOrWhiteSpace(customTableName))
                {
                    throw new ArgumentException("TableName mapping for Span is invalid.");
                }

                if (Encoding.UTF8.GetByteCount(customTableName) != customTableName.Length)
                {
                    throw new ArgumentException("The \"{customTableName}\" provided for TableNameMappings option contains non-ASCII characters", customTableName);
                }

                this.partAName = customTableName;
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
                var dedicatedFields = new Dictionary<string, object>(StringComparer.Ordinal);

                // Seed customFields with Span PartB
                customFields["azureResourceProvider"] = true;
                dedicatedFields["azureResourceProvider"] = true;
                foreach (var name in CS40_PART_B_MAPPING.Values)
                {
                    customFields[name] = true;
                    dedicatedFields[name] = true;
                }

                foreach (var name in options.CustomFields)
                {
                    customFields[name] = true;
                    dedicatedFields[name] = true;
                }

                this.m_customFields = customFields;

                foreach (var name in CS40_PART_B_MAPPING.Keys)
                {
                    dedicatedFields[name] = true;
                }

                dedicatedFields["otel.status_code"] = true;
                dedicatedFields["otel.status_description"] = true;
                this.m_dedicatedFields = dedicatedFields;
            }

            var fields = options.PrepopulatedFields.ToDictionary(item => item.Key, item => item.Value);
            fields.Remove(".ver");
            this.prepopulatedFields = options.PrepopulatedFields;
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            // Note: The MessagePackSerializer takes way less time / memory than creating the activity itself.
            //       This makes the short-circuit check less useful.
            //       On the other side, running the serializer could help to catch error in the early phase of development lifecycle.
            //
            // if (!m_dataTransport.IsEnabled())
            // {
            //     return ExportResult.Success;
            // }

            var result = ExportResult.Success;
            if (this.eventProvider.IsEnabled())
            {
                foreach (var activity in batch)
                {
                    try
                    {
                        this.SerializeActivity(activity);
                        this.eventProvider.Write(this.eventBuilder.Value);
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
                    this.eventProvider?.Dispose();
                    this.eventBuilder.Dispose();
                    this.keyValuePairs.Dispose();
                }
                catch (Exception ex)
                {
                    ExporterEventSource.Log.ExporterException("GenevaTraceExporter Dispose failed.", ex);
                }
            }

            this.isDisposed = true;
            base.Dispose(disposing);
        }

        internal void SerializeActivity(Activity activity)
        {
            var eb = this.eventBuilder.Value;

            eb.Reset(this.partAName);
            eb.AddUInt16("__csver__", 1024, EventOutType.Hex);

            var partAFieldsCount = this.prepopulatedFields.Count + 4; // Four fields: name, time, ext_dt_traceId, ext_dt_spanId

            var dtBegin = activity.StartTimeUtc;
            var tsBegin = dtBegin.Ticks;
            var tsEnd = tsBegin + activity.Duration.Ticks;
            var dtEnd = new DateTime(tsEnd);

            eb.AddStruct("PartA", (byte)partAFieldsCount);
            eb.AddCountedString("name", this.partAName);
            eb.AddFileTime("time", dtEnd, EventOutType.DateTimeUtc);
            eb.AddCountedString("ext_dt_traceId", activity.Context.TraceId.ToHexString());
            eb.AddCountedString("ext_dt_spanId", activity.Context.SpanId.ToHexString());

            foreach (var entry in this.prepopulatedFields)
            {
                V40_PART_A_TLD_MAPPING.TryGetValue(entry.Key, out string replacementKey);
                var key = replacementKey ?? entry.Key;
                this.Serialize(eb, key, entry.Value);
            }

            int hasValidParentId = 0;
            var strParentId = activity.ParentSpanId.ToHexString();
            if (!ReferenceEquals(strParentId, INVALID_SPAN_ID))
            {
                hasValidParentId++;
            }

            var links = activity.Links;
            var hasLinks = links.Any() ? 1 : 0;

            int cntPartBFieldsFromTags = 0;
            int cntPartCFieldsFromTags = 0;
            int hasEnvProperties = 0;
            int isStatusSuccess = 1;
            string statusDescription = string.Empty;

            foreach (var entry in activity.TagObjects)
            {
                // TODO: check name collision
                if (CS40_PART_B_MAPPING.TryGetValue(entry.Key, out string replacementKey))
                {
                    cntPartBFieldsFromTags++;
                }
                else if (string.Equals(entry.Key, "otel.status_code", StringComparison.Ordinal))
                {
                    if (string.Equals(entry.Value.ToString(), "ERROR", StringComparison.Ordinal))
                    {
                        isStatusSuccess = 0;
                    }

                    continue;
                }
                else if (string.Equals(entry.Key, "otel.status_description", StringComparison.Ordinal))
                {
                    statusDescription = entry.Value.ToString();
                    continue;
                }
                else if (this.m_customFields == null || this.m_customFields.ContainsKey(entry.Key))
                {
                    // TODO: the above null check can be optimized and avoided inside foreach.
                    cntPartCFieldsFromTags++;
                }
                else
                {
                    hasEnvProperties = 1;
                    continue;
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
                    statusDescription = activity.StatusDescription;
                }
            }

            var partBFieldsCount = 5 + hasValidParentId + hasLinks + cntPartBFieldsFromTags; // Five fields: _typeName, name, kind, startTime, success
            if (isStatusSuccess == 0)
            {
                partBFieldsCount++; // we export statusMessage only when status is not successful
            }

            eb.AddStruct("PartB", (byte)partBFieldsCount);
            eb.AddCountedString("_typeName", "Span");
            eb.AddCountedString("name", activity.DisplayName);
            eb.AddInt32("kind", (int)activity.Kind);
            eb.AddFileTime("startTime", dtBegin, EventOutType.DateTimeUtc);
            eb.AddBool32("success", isStatusSuccess, EventOutType.Boolean);

            if (hasValidParentId == 1)
            {
                eb.AddCountedString("parentId", strParentId);
            }

            if (isStatusSuccess == 0)
            {
                eb.AddCountedString("statusMessage", statusDescription);
            }

            if (hasLinks == 1)
            {
                var keyValuePairs = this.keyValuePairs.Value;
                keyValuePairs.Clear();
                foreach (var link in links)
                {
                    keyValuePairs.Add(new("toTraceId", link.Context.TraceId.ToHexString()));
                    keyValuePairs.Add(new("toSpanId", link.Context.SpanId.ToHexString()));
                }

                eb.AddCountedString("links", JsonSerializer.SerializeMap(keyValuePairs));
            }

            if (cntPartBFieldsFromTags > 0)
            {
                foreach (var entry in activity.TagObjects)
                {
                    // TODO: check name collision
                    if (CS40_PART_B_MAPPING.TryGetValue(entry.Key, out string replacementKey))
                    {
                        this.Serialize(eb, entry.Key, entry.Value);
                    }
                }
            }

            var partCFieldsCount = cntPartCFieldsFromTags + hasEnvProperties;
            eb.AddStruct("PartC", (byte)partCFieldsCount);

            foreach (var entry in activity.TagObjects)
            {
                // TODO: check name collision
                if (CS40_PART_B_MAPPING.TryGetValue(entry.Key, out string replacementKey))
                {
                    continue;
                }
                else if (string.Equals(entry.Key, "otel.status_code", StringComparison.Ordinal))
                {
                    continue;
                }
                else if (string.Equals(entry.Key, "otel.status_description", StringComparison.Ordinal))
                {
                    continue;
                }
                else if (this.m_customFields == null || this.m_customFields.ContainsKey(entry.Key))
                {
                    // TODO: the above null check can be optimized and avoided inside foreach.
                    this.Serialize(eb, entry.Key, entry.Value);
                }
            }

            if (hasEnvProperties == 1)
            {
                // Iteration #2 - Get all "other" fields and collapse them into single field
                // named "env_properties".
                var keyValuePairs = this.keyValuePairs.Value;
                keyValuePairs.Clear();
                foreach (var entry in activity.TagObjects)
                {
                    // TODO: check name collision
                    if (this.m_dedicatedFields.ContainsKey(entry.Key))
                    {
                        continue;
                    }
                    else
                    {
                        keyValuePairs.Add(new(entry.Key, entry.Value));
                    }
                }

                eb.AddCountedString("env_properties", JsonSerializer.SerializeMap(keyValuePairs));
            }
        }

        private readonly string partAName = "Span";

        private readonly IReadOnlyDictionary<string, object> m_customFields;

        private readonly IReadOnlyDictionary<string, object> m_dedicatedFields;

        private readonly IReadOnlyDictionary<string, object> prepopulatedFields;

        private static readonly string INVALID_SPAN_ID = default(ActivitySpanId).ToHexString();

        private readonly EventProvider eventProvider;

        private readonly ThreadLocal<EventBuilder> eventBuilder = new ThreadLocal<EventBuilder>(() => new());

        private readonly ThreadLocal<List<KeyValuePair<string, object>>> keyValuePairs = new(() => new());

        private static readonly IReadOnlyDictionary<string, string> V40_PART_A_TLD_MAPPING = new Dictionary<string, string>
        {
            // Part A
            [Schema.V40.PartA.IKey] = "iKey",
            [Schema.V40.PartA.Name] = "name",
            [Schema.V40.PartA.Time] = "time",

            // Part A Application Extension
            [Schema.V40.PartA.Extensions.App.Id] = "ext_app_id",
            [Schema.V40.PartA.Extensions.App.Ver] = "ext_app_ver",

            // Part A Cloud Extension
            [Schema.V40.PartA.Extensions.Cloud.Role] = "ext_cloud_role",
            [Schema.V40.PartA.Extensions.Cloud.RoleInstance] = "ext_cloud_roleInstance",
            [Schema.V40.PartA.Extensions.Cloud.RoleVer] = "ext_cloud_roleVer",

            // Part A Os extension
            [Schema.V40.PartA.Extensions.Os.Name] = "ext_os_name",
            [Schema.V40.PartA.Extensions.Os.Ver] = "ext_os_ver",
        };

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

        private bool isDisposed;

        private void Serialize(EventBuilder eb, string key, object value)
        {
            switch (value)
            {
                case bool vb:
                    eb.AddBool32(key, vb ? 1 : 0, EventOutType.Boolean);
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
                    eb.AddCountedString(key, vs);
                    break;
                case DateTime vdt:
                    eb.AddFileTime(key, vdt, EventOutType.DateTimeUtc);
                    break;
                case IEnumerable<KeyValuePair<string, object>> vmap:
                    eb.AddCountedString(key, JsonSerializer.SerializeMap(vmap));
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
                    eb.AddFileTimeArray(key, vdtarray, EventOutType.DateTimeUtc);
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

                    eb.AddCountedString(key, repr);
                    break;
            }
        }
    }
}
