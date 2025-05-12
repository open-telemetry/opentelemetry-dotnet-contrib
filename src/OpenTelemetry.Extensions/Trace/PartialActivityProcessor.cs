// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    private readonly ManualResetEvent shutdownTrigger;
    private readonly BaseExporter<LogRecord> logExporter;
    private readonly BaseProcessor<LogRecord> logProcessor;
    private readonly ILogger<PartialActivityProcessor> logger;
    private ILoggerFactory loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartialActivityProcessor"/> class.
    /// </summary>
    /// <param name="logExporter">Log exporter to be used.</param>
    public PartialActivityProcessor(BaseExporter<LogRecord> logExporter)
    {
#if NET
        ArgumentNullException.ThrowIfNull(logExporter);
#else
        if (logExporter == null)
        {
            throw new ArgumentOutOfRangeException(nameof(logExporter));
        }
#endif
        this.logExporter = logExporter;
        this.logProcessor = new SimpleLogRecordExportProcessor(logExporter);

        // Configure OpenTelemetry logging to use the provided logExporter
        this.loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddProcessor(this.logProcessor);
            });
        });
        this.logger = this.loggerFactory.CreateLogger<PartialActivityProcessor>();

        this.shutdownTrigger = new ManualResetEvent(false);
    }

    /// <inheritdoc />
    public override void OnStart(Activity data)
    {
        if (data == null)
        {
            return;
        }

        using (this.logger.BeginScope(GetStartLogRecordAttributes()))
        {
            this.logger.LogInformation(
                ActivitySpec.Json(new ActivitySpec(data, ActivitySpec.Signal.Heartbeat)));
        }
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (data == null)
        {
            return;
        }

        using (this.logger.BeginScope(GetEndLogRecordAttributes()))
        {
            this.logger.LogInformation(
                ActivitySpec.Json(new ActivitySpec(data, ActivitySpec.Signal.Heartbeat)));
        }
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
                return this.logExporter.Shutdown() && this.logProcessor.Shutdown();
            case 0:
                return this.logExporter.Shutdown(0) && this.logProcessor.Shutdown(0);
        }

        var sw = Stopwatch.StartNew();
        var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
        return this.logExporter.Shutdown((int)Math.Max(timeout, 0)) &&
               this.logProcessor.Shutdown((int)Math.Max(timeout, 0));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.shutdownTrigger.Dispose();
        this.logProcessor.Dispose();
        this.loggerFactory.Dispose();
    }

    private static List<KeyValuePair<string, object?>> GetEndLogRecordAttributes() =>
    [
        new("span.state", "ended")
    ];

    private static List<KeyValuePair<string, object?>> GetStartLogRecordAttributes() =>
    [
        new("span.state", "started")
    ];
}
