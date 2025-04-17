// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
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
    private const int DefaultScheduledDelayMilliseconds = 5000;
    private static MethodInfo writeTraceDataMethod = null!;
    private static ConstructorInfo logRecordConstructor = null!;
    private static object sdkLimitOptions = null!;
    private readonly Thread exporterThread;
    private readonly ManualResetEvent shutdownTrigger;
    private readonly int scheduledDelayMilliseconds;
    private readonly BaseExporter<LogRecord> logExporter;

    private readonly ConcurrentDictionary<ActivitySpanId, Activity> activeActivities;
    private readonly ConcurrentQueue<KeyValuePair<ActivitySpanId, Activity>> endedActivities;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartialActivityProcessor"/> class.
    /// </summary>
    /// <param name="logExporter">Log exporter to be used.</param>
    /// <param name="scheduledDelayMilliseconds">Heartbeat value.</param>
    public PartialActivityProcessor(
        BaseExporter<LogRecord> logExporter,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds)
    {
        if (this.logExporter == null)
        {
            throw new ArgumentNullException(nameof(logExporter));
        }

        this.logExporter = logExporter;

        if (this.scheduledDelayMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(this.scheduledDelayMilliseconds),
                "Value must be greater than or equal to 1.");
        }

        this.scheduledDelayMilliseconds = scheduledDelayMilliseconds;

        this.activeActivities = new ConcurrentDictionary<ActivitySpanId, Activity>();
        this.endedActivities = new ConcurrentQueue<KeyValuePair<ActivitySpanId, Activity>>();

        this.shutdownTrigger = new ManualResetEvent(false);

        // Access OpenTelemetry internals as soon as possible to fail fast rather than waiting for the first heartbeat
        AccessOpenTelemetryInternals(
            out writeTraceDataMethod,
            out logRecordConstructor,
            out sdkLimitOptions);

        this.exporterThread = new Thread(this.ExporterProc)
        {
            IsBackground = true, Name = $"OpenTelemetry-{nameof(PartialActivityProcessor)}",
        };
        this.exporterThread.Start();
    }

    /// <summary>
    /// Gets for testing purposes.
    /// </summary>
    public IReadOnlyDictionary<ActivitySpanId, Activity> ActiveActivities => this.activeActivities;

    /// <summary>
    /// Gets for testing purposes.
    /// </summary>
    public IReadOnlyCollection<KeyValuePair<ActivitySpanId, Activity>> EndedActivities =>
        this.endedActivities;

    /// <inheritdoc />
    public override void OnStart(Activity data)
    {
        if (data == null)
        {
            return;
        }

        var logRecord = GetLogRecord(data, this.GetHeartbeatLogRecordAttributes());
        this.logExporter.Export(new Batch<LogRecord>(logRecord));
        this.activeActivities[data.SpanId] = data;
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (data == null)
        {
            return;
        }

        var logRecord = GetLogRecord(data, GetStopLogRecordAttributes());
        this.logExporter.Export(new Batch<LogRecord>(logRecord));
        this.endedActivities.Enqueue(new KeyValuePair<ActivitySpanId, Activity>(data.SpanId, data));
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
                this.exporterThread.Join();
                return this.logExporter.Shutdown();
            case 0:
                return this.logExporter.Shutdown(0);
        }

        var sw = Stopwatch.StartNew();
        this.exporterThread.Join(timeoutMilliseconds);
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

    private static List<KeyValuePair<string, object?>> GetStopLogRecordAttributes() =>
    [
        new("partial.event", "stop"),
    ];

    private void ExporterProc()
    {
        var triggers = new WaitHandle[] { this.shutdownTrigger };

        while (true)
        {
            try
            {
                WaitHandle.WaitAny(triggers, this.scheduledDelayMilliseconds);
                this.Heartbeat();
            }
            catch (ObjectDisposedException)
            {
                // the exporter is somehow disposed before the worker thread could finish its job
                return;
            }
        }
    }

    private void Heartbeat()
    {
        // remove ended activities from active activities
        while (this.endedActivities.TryDequeue(out var activity))
        {
            this.activeActivities.TryRemove(activity.Key, out _);
        }

        foreach (var keyValuePair in this.activeActivities)
        {
            var logRecord =
                GetLogRecord(keyValuePair.Value, this.GetHeartbeatLogRecordAttributes());
            this.logExporter.Export(new Batch<LogRecord>(logRecord));
        }
    }

    private List<KeyValuePair<string, object?>> GetHeartbeatLogRecordAttributes() =>
    [
        new("partial.event", "heartbeat"),
        new("partial.frequency", this.scheduledDelayMilliseconds + "ms")
    ];
}
