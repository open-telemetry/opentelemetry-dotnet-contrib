// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using Serilog.Core;
using Serilog.Events;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetrySerilogSink : ILogEventSink, IDisposable
{
    // Mapping of Serilog log levels to Microsoft.Extensions.Logging LogLevel
    private static readonly LogLevel[] LogLevelMapping = new LogLevel[]
    {
        LogLevel.Trace, // Verbose
        LogLevel.Debug,
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical,
    };

    private readonly ILogger logger;
    private readonly bool includeRenderedMessage;
    private readonly bool disposeProvider;
    private readonly ILoggerProvider loggerProvider;

    public OpenTelemetrySerilogSink(
        ILoggerProvider loggerProvider,
        OpenTelemetrySerilogSinkOptions? options,
        bool disposeProvider)
    {
        Guard.ThrowIfNull(loggerProvider);

        options ??= new();

        this.loggerProvider = loggerProvider;
        this.disposeProvider = disposeProvider;

        this.logger = loggerProvider.CreateLogger("OpenTelemetry.Appenders.Serilog");

        this.includeRenderedMessage = options.IncludeRenderedMessage;
    }

    public void Emit(LogEvent logEvent)
    {
        Guard.ThrowIfNull(logEvent);

        uint severityNumber = (uint)logEvent.Level;
        if (severityNumber >= LogLevelMapping.Length)
        {
            // Unknown log level, map to Information as a fallback
            severityNumber = (uint)LogEventLevel.Information;
        }

        LogLevel logLevel = LogLevelMapping[severityNumber];

        // Create the log state as a dictionary of properties
        var logState = new Dictionary<string, object>();

        // Add Serilog properties to the log state
        foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
        {
            // TODO: Serilog supports complex type logging. This is not yet
            // supported in OpenTelemetry.
            if (property.Key == Constants.SourceContextPropertyName
                && property.Value is ScalarValue sourceContextValue)
            {
                logState.Add("serilog.source_context", sourceContextValue.Value?.ToString() ?? string.Empty);
            }
            else if (property.Value is ScalarValue scalarValue)
            {
                logState.Add(property.Key, scalarValue.Value ?? string.Empty);
            }
            else if (property.Value is SequenceValue sequenceValue)
            {
                IReadOnlyList<LogEventPropertyValue> elements = sequenceValue.Elements;
                if (elements.Count > 0)
                {
                    // TODO: Match existing implementation.
                    // Process array values
                    var elementValues = new List<object>(elements.Count);
                    foreach (var element in elements)
                    {
                        if (element is ScalarValue value)
                        {
                            elementValues.Add(value.Value ?? string.Empty);
                        }
                    }

                    logState.Add(property.Key, elementValues.ToArray());
                }
            }
        }

        // Add message template and timestamp
        logState.Add("{OriginalFormat}", logEvent.MessageTemplate.Text);
        logState.Add("Timestamp", logEvent.Timestamp.UtcDateTime);

        // Include rendered message if requested
        if (this.includeRenderedMessage)
        {
            logState.Add("serilog.rendered_message", logEvent.RenderMessage(CultureInfo.InvariantCulture));
        }

        // Get any Active span context for correlation
        var activity = Activity.Current;
        if (activity != null)
        {
            logState.Add("SpanId", activity.SpanId.ToHexString());
            logState.Add("TraceId", activity.TraceId.ToHexString());
            logState.Add("TraceFlags", activity.ActivityTraceFlags.ToString());

            if (activity.ParentSpanId != default)
            {
                logState.Add("ParentSpanId", activity.ParentSpanId.ToHexString());
            }
        }

        // Convert log state to FormattedLogValues for ILogger
        var formattedLogValues = new FormattedLogValues(logEvent.MessageTemplate.Text, logState);

        // Log through ILogger - Use the message template, not the rendered message
        this.logger.Log(
            logLevel,
            0, // Event ID is not used by Serilog, so using 0
            formattedLogValues,
            logEvent.Exception,
            (state, ex) => logEvent.MessageTemplate.Text);
    }

    public void Dispose()
    {
        if (this.disposeProvider)
        {
            (this.loggerProvider as IDisposable)?.Dispose();
        }
    }

    // Helper class to format log values
    private sealed class FormattedLogValues : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly string messageTemplate;
        private readonly IReadOnlyList<KeyValuePair<string, object>> values;

        public FormattedLogValues(string messageTemplate, Dictionary<string, object> values)
        {
            this.messageTemplate = messageTemplate;
            this.values = values.ToList();
        }

        public int Count => this.values.Count;

        public KeyValuePair<string, object> this[int index] => this.values[index];

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => this.values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override string ToString() => this.messageTemplate;
    }
}
