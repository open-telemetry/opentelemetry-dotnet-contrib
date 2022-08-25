// <copyright file="GenevaTraceExporter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaTraceExporter : GenevaBaseExporter<Activity>
{
    public GenevaTraceExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        var partAName = "Span";
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

            partAName = customTableName;
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
        cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, 3);
        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, partAName);
        cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, 1);
        cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, 2);

        // timestamp
        cursor = MessagePackSerializer.WriteTimestamp96Header(buffer, cursor);
        this.m_idxTimestampPatch = cursor;
        cursor += 12; // reserve 12 bytes for the timestamp

        cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, ushort.MaxValue); // Note: always use Map16 for perf consideration
        this.m_idxMapSizePatch = cursor - 2;

        this.m_cntPrepopulatedFields = 0;

        // TODO: Do we support PartB as well?
        // Part A - core envelope
        cursor = AddPartAField(buffer, cursor, Schema.V40.PartA.Name, partAName);
        this.m_cntPrepopulatedFields += 1;

        foreach (var entry in options.PrepopulatedFields)
        {
            var value = entry.Value;
            cursor = AddPartAField(buffer, cursor, entry.Key, value);
            this.m_cntPrepopulatedFields += 1;
        }

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
        foreach (var activity in batch)
        {
            try
            {
                var cursor = this.SerializeActivity(activity);
                this.m_dataTransport.Send(this.m_buffer.Value, cursor - 0);
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.FailedToSendTraceData(ex); // TODO: preallocate exception or no exception
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
            try
            {
                (this.m_dataTransport as IDisposable)?.Dispose();
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

    internal bool IsUsingUnixDomainSocket
    {
        get => this.m_dataTransport is UnixDomainSocketDataTransport;
    }

    internal int SerializeActivity(Activity activity)
    {
        var buffer = this.m_buffer.Value;
        if (buffer == null)
        {
            buffer = new byte[BUFFER_SIZE]; // TODO: handle OOM
            Buffer.BlockCopy(this.m_bufferPrologue, 0, buffer, 0, this.m_bufferPrologue.Length);
            this.m_buffer.Value = buffer;
        }

        var cursor = this.m_bufferPrologue.Length;
        var cntFields = this.m_cntPrepopulatedFields;
        var dtBegin = activity.StartTimeUtc;
        var tsBegin = dtBegin.Ticks;
        var tsEnd = tsBegin + activity.Duration.Ticks;
        var dtEnd = new DateTime(tsEnd);

        MessagePackSerializer.WriteTimestamp96(buffer, this.m_idxTimestampPatch, tsEnd);

        #region Part A - core envelope
        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_time");
        cursor = MessagePackSerializer.SerializeUtcDateTime(buffer, cursor, dtEnd);
        cntFields += 1;
        #endregion

        #region Part A - dt extension
        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_dt_traceId");

        // Note: ToHexString returns the pre-calculated hex representation without allocation
        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, activity.Context.TraceId.ToHexString());
        cntFields += 1;

        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "env_dt_spanId");
        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, activity.Context.SpanId.ToHexString());
        cntFields += 1;
        #endregion

        #region Part B Span - required fields
        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "name");
        cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, activity.DisplayName);
        cntFields += 1;

        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "kind");
        cursor = MessagePackSerializer.SerializeInt32(buffer, cursor, (int)activity.Kind);
        cntFields += 1;

        cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "startTime");
        cursor = MessagePackSerializer.SerializeUtcDateTime(buffer, cursor, dtBegin);
        cntFields += 1;

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
    }

    private const int BUFFER_SIZE = 65360; // the maximum ETW payload (inclusive)

    private readonly ThreadLocal<byte[]> m_buffer = new ThreadLocal<byte[]>(() => null);

    private readonly byte[] m_bufferPrologue;

    private readonly byte[] m_bufferEpilogue;

    private readonly ushort m_cntPrepopulatedFields;

    private readonly int m_idxTimestampPatch;

    private readonly int m_idxMapSizePatch;

    private readonly IDataTransport m_dataTransport;

    private readonly IReadOnlyDictionary<string, object> m_customFields;

    private readonly IReadOnlyDictionary<string, object> m_dedicatedFields;

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

    private bool isDisposed;
}
