// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MsgPackTraceExporter : MsgPackExporter, IDisposable
{
    internal static readonly Dictionary<string, string> CS40_PART_B_MAPPING_DICTIONARY = new()
    {
        ["db.system"] = "dbSystem",
        ["db.name"] = "dbName",
        ["db.statement"] = "dbStatement",

        ["http.method"] = "httpMethod",
        ["http.request.method"] = "httpMethod",
        ["http.url"] = "httpUrl",
        ["url.full"] = "httpUrl",
        ["http.status_code"] = "httpStatusCode",
        ["http.response.status_code"] = "httpStatusCode",

        ["messaging.system"] = "messagingSystem",
        ["messaging.destination"] = "messagingDestination",
        ["messaging.url"] = "messagingUrl",
    };

#if NET8_0_OR_GREATER
    internal static readonly FrozenDictionary<string, string> CS40_PART_B_MAPPING = CS40_PART_B_MAPPING_DICTIONARY.ToFrozenDictionary();
#else
    internal static readonly Dictionary<string, string> CS40_PART_B_MAPPING = CS40_PART_B_MAPPING_DICTIONARY;
#endif

    internal readonly ThreadLocal<byte[]> Buffer = new();

#if NET8_0_OR_GREATER
    internal readonly FrozenSet<string> CustomFields;

    internal readonly FrozenSet<string> DedicatedFields;
#else
    internal readonly HashSet<string> CustomFields;

    internal readonly HashSet<string> DedicatedFields;
#endif

    private const int BUFFER_SIZE = 65360; // the maximum ETW payload (inclusive)

    private static readonly string INVALID_SPAN_ID = default(ActivitySpanId).ToHexString();

    private readonly byte[] bufferPrologue;
    private readonly byte[] bufferEpilogue;
    private readonly ushort prepopulatedFieldsCount;
    private readonly int timestampPatchIndex;
    private readonly int mapSizePatchIndex;
    private readonly IDataTransport dataTransport;
    private readonly bool shouldIncludeTraceState;

    private bool isDisposed;

    public MsgPackTraceExporter(GenevaExporterOptions options)
    {
        var partAName = "Span";
        if (options.TableNameMappings != null
            && options.TableNameMappings.TryGetValue("Span", out var customTableName))
        {
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

                this.dataTransport = new EtwDataTransport(connectionStringBuilder.EtwSession);
                break;
            case TransportProtocol.Unix:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("Unix domain socket should not be used on Windows.");
                }

                var unixDomainSocketPath = connectionStringBuilder.ParseUnixDomainSocketPath();
                this.dataTransport = new UnixDomainSocketDataTransport(unixDomainSocketPath);
                break;
            default:
                throw new NotSupportedException($"Protocol '{connectionStringBuilder.Protocol}' is not supported");
        }

        // TODO: Validate custom fields (reserved name? etc).
        if (options.CustomFields != null)
        {
            var customFields = new HashSet<string>(StringComparer.Ordinal);
            var dedicatedFields = new HashSet<string>(StringComparer.Ordinal);

            // Seed customFields with Span PartB
            customFields.Add("azureResourceProvider");
            dedicatedFields.Add("azureResourceProvider");

            foreach (var name in CS40_PART_B_MAPPING.Values)
            {
                customFields.Add(name);
                dedicatedFields.Add(name);
            }

            foreach (var name in options.CustomFields)
            {
                customFields.Add(name);
                dedicatedFields.Add(name);
            }

#if NET8_0_OR_GREATER
            this.CustomFields = customFields.ToFrozenSet(StringComparer.Ordinal);
#else
            this.CustomFields = customFields;
#endif

            foreach (var name in CS40_PART_B_MAPPING.Keys)
            {
                dedicatedFields.Add(name);
            }

            dedicatedFields.Add("otel.status_code");
            dedicatedFields.Add("otel.status_description");

#if NET8_0_OR_GREATER
            this.DedicatedFields = dedicatedFields.ToFrozenSet(StringComparer.Ordinal);
#else
            this.DedicatedFields = dedicatedFields;
#endif
        }

        this.shouldIncludeTraceState = options.IncludeTraceStateForSpan;

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
        this.timestampPatchIndex = cursor;
        cursor += 12; // reserve 12 bytes for the timestamp

        cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, ushort.MaxValue); // Note: always use Map16 for perf consideration
        this.mapSizePatchIndex = cursor - 2;

        this.prepopulatedFieldsCount = 0;

        // TODO: Do we support PartB as well?
        // Part A - core envelope
        cursor = AddPartAField(buffer, cursor, Schema.V40.PartA.Name, partAName);
        this.prepopulatedFieldsCount += 1;

        foreach (var entry in options.PrepopulatedFields)
        {
            var value = entry.Value;
            cursor = AddPartAField(buffer, cursor, entry.Key, value);
            this.prepopulatedFieldsCount += 1;
        }

        this.bufferPrologue = new byte[cursor - 0];
        System.Buffer.BlockCopy(buffer, 0, this.bufferPrologue, 0, cursor - 0);

        cursor = MessagePackSerializer.Serialize(buffer, 0, new Dictionary<string, object> { { "TimeFormat", "DateTime" } });

        this.bufferEpilogue = new byte[cursor - 0];
        System.Buffer.BlockCopy(buffer, 0, this.bufferEpilogue, 0, cursor - 0);
    }

    internal bool IsUsingUnixDomainSocket => this.dataTransport is UnixDomainSocketDataTransport;

    public ExportResult Export(in Batch<Activity> batch)
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
                this.dataTransport.Send(this.Buffer.Value, cursor - 0);
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.FailedToSendTraceData(ex); // TODO: preallocate exception or no exception
                result = ExportResult.Failure;
            }
        }

        ExporterEventSource.Log.ExportCompleted(nameof(MsgPackTraceExporter));

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
            (this.dataTransport as IDisposable)?.Dispose();
            this.Buffer.Dispose();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("MsgPackTraceExporter Dispose failed.", ex);
        }

        this.isDisposed = true;
    }

    internal int SerializeActivity(Activity activity)
    {
        var buffer = this.Buffer.Value;
        if (buffer == null)
        {
            buffer = new byte[BUFFER_SIZE]; // TODO: handle OOM
            System.Buffer.BlockCopy(this.bufferPrologue, 0, buffer, 0, this.bufferPrologue.Length);
            this.Buffer.Value = buffer;
        }

        var cursor = this.bufferPrologue.Length;
        var cntFields = this.prepopulatedFieldsCount;
        var dtBegin = activity.StartTimeUtc;
        var tsBegin = dtBegin.Ticks;
        var tsEnd = tsBegin + activity.Duration.Ticks;
        var dtEnd = new DateTime(tsEnd);

        MessagePackSerializer.WriteTimestamp96(buffer, this.timestampPatchIndex, tsEnd);

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

        if (this.shouldIncludeTraceState)
        {
            var traceStateString = activity.TraceStateString;
            if (!string.IsNullOrEmpty(traceStateString))
            {
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "traceState");
                cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, traceStateString);
                cntFields += 1;
            }
        }

        var linkEnumerator = activity.EnumerateLinks();
        if (linkEnumerator.MoveNext())
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "links");
            cursor = MessagePackSerializer.WriteArrayHeader(buffer, cursor, ushort.MaxValue); // Note: always use Array16 for perf consideration
            var idxLinkPatch = cursor - 2;

            ushort cntLink = 0;
            do
            {
                ref readonly var link = ref linkEnumerator.Current;

                cursor = MessagePackSerializer.WriteMapHeader(buffer, cursor, 2);
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "toTraceId");
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, link.Context.TraceId.ToHexString());
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, "toSpanId");
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, link.Context.SpanId.ToHexString());
                cntLink += 1;
            }
            while (linkEnumerator.MoveNext());

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

        foreach (ref readonly var entry in activity.EnumerateTagObjects())
        {
            // TODO: check name collision
            if (CS40_PART_B_MAPPING.TryGetValue(entry.Key, out string replacementKey))
            {
                cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, replacementKey);
            }
            else if (string.Equals(entry.Key, "otel.status_code", StringComparison.Ordinal))
            {
                if (string.Equals(Convert.ToString(entry.Value, CultureInfo.InvariantCulture), "ERROR", StringComparison.Ordinal))
                {
                    isStatusSuccess = false;
                }

                continue;
            }
            else if (string.Equals(entry.Key, "otel.status_description", StringComparison.Ordinal))
            {
                statusDescription = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
                continue;
            }
            else if (this.CustomFields == null || this.CustomFields.Contains(entry.Key))
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

            foreach (ref readonly var entry in activity.EnumerateTagObjects())
            {
                // TODO: check name collision
                if (this.DedicatedFields.Contains(entry.Key))
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

        MessagePackSerializer.WriteUInt16(buffer, this.mapSizePatchIndex, cntFields);

        System.Buffer.BlockCopy(this.bufferEpilogue, 0, buffer, cursor, this.bufferEpilogue.Length);
        cursor += this.bufferEpilogue.Length;

        return cursor;
    }
}
