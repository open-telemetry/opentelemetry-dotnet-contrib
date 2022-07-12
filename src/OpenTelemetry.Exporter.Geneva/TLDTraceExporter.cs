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

            var buffer = new byte[BUFFER_SIZE];

            var cursor = 0;

            /* Fluentd Forward Mode:
            [
                "Span",
                [
                    [ <timestamp>, { "env_ver": "4.0", ... } ]
                ],
                { "TimeFormat": "DateTime" }
            ]
            */

            var fields = options.PrepopulatedFields.ToDictionary(item => item.Key, item => item.Value);
            fields.Remove(".ver");
            this.prepopulatedFields = options.PrepopulatedFields;

            this.m_bufferPrologue = new byte[cursor - 0];
            Buffer.BlockCopy(buffer, 0, this.m_bufferPrologue, 0, cursor - 0);

            cursor = MessagePackSerializer.Serialize(buffer, 0, new Dictionary<string, object> { { "TimeFormat", "DateTime" } });

            this.m_bufferEpilogue = new byte[cursor - 0];
            Buffer.BlockCopy(buffer, 0, this.m_bufferEpilogue, 0, cursor - 0);
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
                    this.eventProvider.Dispose();
                    this.eventBuilder.Dispose();
                    this.m_buffer.Dispose();
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
                var value = entry.Value;
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
                    default:
                        eb.AddCountedString(key, value.ToString());
                        break;
                }
            }

            int hasValidParentId = 0;
            var strParentId = activity.ParentSpanId.ToHexString();
            if (!ReferenceEquals(strParentId, INVALID_SPAN_ID))
            {
                hasValidParentId++;
            }

            var links = activity.Links;
            var cntLinks = links.Count();

            int cntPartBFieldsFromTags = 0;
            int cntPartCFieldsFromTags = 0;
            bool hasEnvProperties = false;
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
                    hasEnvProperties = true;
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
            else
            {
                if (isStatusSuccess == 0)
                {
                    MessagePackSerializer.SerializeBool(buffer, idxSuccessPatch, false);
                    if (!string.IsNullOrEmpty(statusDescription))
                    {
                        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "statusMessage");
                        cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, statusDescription);
                        cntFields += 1;
                    }
                }
            }

            var partBFieldsCount = 5 + hasValidParentId + cntLinks + cntPartBFieldsFromTags; // Five fields: _typeName, name, kind, startTime, success
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

            if (links.Any())
            {
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "links");
                cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, ushort.MaxValue); // Note: always use Array16 for perf consideration
                var idxLinkPatch = cursor - 2;
                ushort cntLink = 0;
                foreach (var link in links)
                {
                    cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, 2);
                    cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "toTraceId");
                    cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, link.Context.TraceId.ToHexString());
                    cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "toSpanId");
                    cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, link.Context.SpanId.ToHexString());
                    cntLink += 1;
                }

                MessagePackSerializer.WriteUInt16(buffer, idxLinkPatch, cntLink);
                cntFields += 1;
            }

            /*
                        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "success");
                        cursor = MessagePackSerializer.SerializeBool(buffer, cursor, true);
                        var idxSuccessPatch = cursor - 1;
                        cntFields += 1;
                        #endregion

                        #region Part B Span optional fields and Part C fields
                        var strParentId = activity.ParentSpanId.ToHexString();

                        // Note: this should be blazing fast since Object.ReferenceEquals(strParentId, INVALID_SPAN_ID) == true
                        if (!string.Equals(strParentId, INVALID_SPAN_ID, StringComparison.Ordinal))
                        {
                            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "parentId");
                            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, strParentId);
                            cntFields += 1;
                        }

                        var links = activity.Links;
                        if (links.Any())
                        {
                            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "links");
                            cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, ushort.MaxValue); // Note: always use Array16 for perf consideration
                            var idxLinkPatch = cursor - 2;
                            ushort cntLink = 0;
                            foreach (var link in links)
                            {
                                cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, 2);
                                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "toTraceId");
                                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, link.Context.TraceId.ToHexString());
                                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "toSpanId");
                                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, link.Context.SpanId.ToHexString());
                                cntLink += 1;
                            }

                            MessagePackSerializer.WriteUInt16(buffer, idxLinkPatch, cntLink);
                            cntFields += 1;
                        }

                        // TODO: The current approach is to iterate twice over TagObjects so that all
                        // env_properties can be added the very end. This avoids speculating the size
                        // and preallocating a separate buffer for it.
                        // Alternates include static allocation and iterate once.
                        // The TODO: here is to measure perf and change to alternate, if required.

                        // Iteration #1 - Get those fields which become dedicated column
                        // i.e all PartB fields and opt-in part c fields.
                        bool hasEnvProperties = false;
                        bool isStatusSuccess = true;
                        string statusDescription = string.Empty;

                        foreach (var entry in activity.TagObjects)
                        {
                            // TODO: check name collision
                            if (CS40_PART_B_MAPPING.TryGetValue(entry.Key, out string replacementKey))
                            {
                                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, replacementKey);
                            }
                            else if (string.Equals(entry.Key, "otel.status_code", StringComparison.Ordinal))
                            {
                                if (string.Equals(entry.Value.ToString(), "ERROR", StringComparison.Ordinal))
                                {
                                    isStatusSuccess = false;
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
                                cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, entry.Key);
                            }
                            else
                            {
                                hasEnvProperties = true;
                                continue;
                            }

                            cursor = MessagePackSerializer.Serialize(buffer, cursor, entry.Value);
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

                            foreach (var entry in activity.TagObjects)
                            {
                                // TODO: check name collision
                                if (this.m_dedicatedFields.ContainsKey(entry.Key))
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

                        if (activity.Status != ActivityStatusCode.Unset)
                        {
                            if (activity.Status == ActivityStatusCode.Error)
                            {
                                MessagePackSerializer.SerializeBool(buffer, idxSuccessPatch, false);
                            }

                            if (!string.IsNullOrEmpty(activity.StatusDescription))
                            {
                                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "statusMessage");
                                cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, activity.StatusDescription);
                                cntFields += 1;
                            }
                        }
                        else
                        {
                            if (!isStatusSuccess)
                            {
                                MessagePackSerializer.SerializeBool(buffer, idxSuccessPatch, false);
                                if (!string.IsNullOrEmpty(statusDescription))
                                {
                                    cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "statusMessage");
                                    cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, statusDescription);
                                    cntFields += 1;
                                }
                            }
                        }
                        #endregion

                        MessagePackSerializer.WriteUInt16(buffer, this.m_idxMapSizePatch, cntFields);

                        Buffer.BlockCopy(this.m_bufferEpilogue, 0, buffer, cursor, this.m_bufferEpilogue.Length);
                        cursor += this.m_bufferEpilogue.Length;

                        return cursor;
            */
        }

        private const int BUFFER_SIZE = 65360; // the maximum ETW payload (inclusive)

        private readonly ThreadLocal<byte[]> m_buffer = new ThreadLocal<byte[]>(() => null);

        private readonly byte[] m_bufferPrologue;

        private readonly byte[] m_bufferEpilogue;

        private readonly byte m_cntPrepopulatedFields;

        private readonly int m_idxTimestampPatch;

        private readonly int m_idxMapSizePatch;

        private readonly string partAName = "Span";

        private readonly IReadOnlyDictionary<string, object> m_customFields;

        private readonly IReadOnlyDictionary<string, object> m_dedicatedFields;

        private readonly IReadOnlyDictionary<string, object> prepopulatedFields;

        private static readonly string INVALID_SPAN_ID = default(ActivitySpanId).ToHexString();

        private readonly EventProvider eventProvider;

        private readonly ThreadLocal<EventBuilder> eventBuilder = new ThreadLocal<EventBuilder>(() => new());

        internal static readonly IReadOnlyDictionary<string, string> V40_PART_A_TLD_MAPPING = new Dictionary<string, string>
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
    }
}
