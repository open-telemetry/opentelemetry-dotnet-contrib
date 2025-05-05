// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Extensions.Trace;

/// <summary>
/// Activity processor that exports LogRecord on Activity start, during it's lifetime as a heartbeat and on end.
/// </summary>
/// <remarks>
/// Add this processor *before* exporter related Activity processors.
/// </remarks>
public class PartialActivityProcessor : BaseProcessor<Activity>
{
    private static MethodInfo writeTraceDataMethod = null!;
    private static ConstructorInfo logRecordConstructor = null!;
    private static object sdkLimitOptions = null!;
    private readonly ManualResetEvent shutdownTrigger;
    private readonly BaseExporter<LogRecord> logExporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartialActivityProcessor"/> class.
    /// </summary>
    /// <param name="logExporter">Log exporter to be used.</param>
    public PartialActivityProcessor(BaseExporter<LogRecord> logExporter)
    {
        this.logExporter = logExporter ?? throw new ArgumentNullException(nameof(logExporter));

        this.shutdownTrigger = new ManualResetEvent(false);

        // Access OpenTelemetry internals as soon as possible to fail fast rather than waiting for the first heartbeat
        AccessOpenTelemetryInternals(
            out writeTraceDataMethod,
            out logRecordConstructor,
            out sdkLimitOptions);
    }

    /// <inheritdoc />
    public override void OnStart(Activity data)
    {
        if (data == null)
        {
            return;
        }

        var logRecord = GetLogRecord(data, GetStartLogRecordAttributes());
        this.logExporter.Export(new Batch<LogRecord>(logRecord));
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (data == null)
        {
            return;
        }

        var logRecord = GetLogRecord(data, GetEndLogRecordAttributes());
        this.logExporter.Export(new Batch<LogRecord>(logRecord));
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        try
        {
            this.shutdownTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        switch (timeoutMilliseconds)
        {
            case Timeout.Infinite:
                return this.logExporter.Shutdown();
            case 0:
                return this.logExporter.Shutdown(0);
        }

        var sw = Stopwatch.StartNew();
        var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
        return this.logExporter.Shutdown((int)Math.Max(timeout, 0));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.shutdownTrigger.Dispose();
    }

    private static LogRecord GetLogRecord(
        Activity data,
        List<KeyValuePair<string, object?>> logRecordAttributesToBeAdded)
    {
        var buffer = new byte[750000];

        var result = writeTraceDataMethod.Invoke(
            null,
            [buffer, 0, sdkLimitOptions, null!, new Batch<Activity>(data)]);
        var writePosition = result as int? ?? 0; // Use a default value if null

        var logRecord = (LogRecord)logRecordConstructor.Invoke(null);
        logRecord.Timestamp = DateTime.UtcNow;
        logRecord.TraceId = data.TraceId;
        logRecord.SpanId = data.SpanId;
        logRecord.TraceFlags = ActivityTraceFlags.None;
        logRecord.Body = Convert.ToBase64String(buffer, 0, writePosition);

        // Severity = LogRecordSeverity.Info,
        // SeverityText = "Info",

        var logRecordAttributes = GetLogRecordAttributes();
        logRecordAttributes.AddRange(logRecordAttributesToBeAdded);
        logRecord.Attributes = logRecordAttributes;

        return logRecord;
    }

    private static void AccessOpenTelemetryInternals(
        out MethodInfo writeTraceDataMethodParam,
        out ConstructorInfo logRecordConstructorParam,
        out object sdkLimitOptionsParam)
    {
        var sdkLimitOptionsType = Type.GetType(
            "OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.SdkLimitOptions, OpenTelemetry.Exporter.OpenTelemetryProtocol",
            true);

        if (sdkLimitOptionsType == null)
        {
            throw new InvalidOperationException("Failed to get the type 'SdkLimitOptions'.");
        }

        sdkLimitOptionsParam = Activator.CreateInstance(sdkLimitOptionsType, nonPublic: true) ??
                          throw new InvalidOperationException(
                              "Failed to create an instance of 'SdkLimitOptions'.");

        var protobufOtlpTraceSerializerType = Type.GetType(
            "OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpTraceSerializer, OpenTelemetry.Exporter.OpenTelemetryProtocol",
            true);

        if (protobufOtlpTraceSerializerType == null)
        {
            throw new InvalidOperationException(
                "Failed to get the type 'ProtobufOtlpTraceSerializer'.");
        }

        writeTraceDataMethodParam =
            protobufOtlpTraceSerializerType.GetMethod(
                "WriteTraceData",
                BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("Failed to get the method 'WriteTraceData'.");

        var logRecordType = Type.GetType("OpenTelemetry.Logs.LogRecord, OpenTelemetry", true);

        if (logRecordType == null)
        {
            throw new InvalidOperationException("Failed to get the type 'LogRecord'.");
        }

        logRecordConstructorParam = logRecordType.GetConstructor(
                                   BindingFlags.NonPublic | BindingFlags.Instance,
                                   null,
                                   Type.EmptyTypes,
                                   null) ??
                               throw new InvalidOperationException(
                                   "Failed to get the constructor of 'LogRecord'.");
    }

    private static List<KeyValuePair<string, object?>> GetLogRecordAttributes() =>
    [
        new("telemetry.logs.cluster", "partial"),
        new("telemetry.logs.project", "span"),
    ];

    private static List<KeyValuePair<string, object?>> GetEndLogRecordAttributes() =>
    [
        new("span.state", "ended")
    ];

    private static List<KeyValuePair<string, object?>> GetStartLogRecordAttributes() =>
    [
        new("span.state", "started")
    ];
}
