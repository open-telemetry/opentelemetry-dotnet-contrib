// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
#pragma warning disable OTEL1001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using System.Diagnostics;
using System.Globalization;
using OpenTelemetry.Internal;
using Serilog.Core;
using Serilog.Events;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetrySerilogSink : ILogEventSink, IDisposable
{
    private static readonly string[] LogEventLevels = new string[]
    {
        nameof(LogEventLevel.Verbose),
        nameof(LogEventLevel.Debug),
        nameof(LogEventLevel.Information),
        nameof(LogEventLevel.Warning),
        nameof(LogEventLevel.Error),
        nameof(LogEventLevel.Fatal),
    };

    private static readonly LogRecordSeverity[] LogRecordSeverityMapping = new LogRecordSeverity[]
    {
        LogRecordSeverity.Trace, // Verbose
        LogRecordSeverity.Debug,
        LogRecordSeverity.Info,
        LogRecordSeverity.Warn,
        LogRecordSeverity.Error,
        LogRecordSeverity.Fatal,
    };

    private readonly LoggerProvider loggerProvider;
    private readonly bool includeRenderedMessage;
    private readonly Logger logger;
    private readonly bool disposeProvider;

    public OpenTelemetrySerilogSink(
        LoggerProvider loggerProvider,
        OpenTelemetrySerilogSinkOptions? options,
        bool disposeProvider)
    {
        Guard.ThrowIfNull(loggerProvider);

        options ??= new();

        this.loggerProvider = loggerProvider;
        this.disposeProvider = disposeProvider;

        this.logger = loggerProvider!.GetLogger("OpenTelemetry.Appenders.Serilog", $"semver:{typeof(OpenTelemetrySerilogSink).Assembly.GetName().Version}");

        this.includeRenderedMessage = options.IncludeRenderedMessage;
    }

    public void Emit(LogEvent logEvent)
    {
        Guard.ThrowIfNull(logEvent);

        LogRecordData data = new(Activity.Current)
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Body = logEvent.MessageTemplate.Text,
        };

        uint severityNumber = (uint)logEvent.Level;
        if (severityNumber < 6)
        {
            data.SeverityText = LogEventLevels[severityNumber];
            data.Severity = LogRecordSeverityMapping[severityNumber];
        }

        LogRecordAttributeList attributes = default;

        if (this.includeRenderedMessage)
        {
            attributes.Add("serilog.rendered_message", logEvent.RenderMessage(CultureInfo.InvariantCulture));
        }

        var exception = logEvent.Exception;
        if (exception != null)
        {
            attributes.RecordException(exception);
        }

        foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
        {
            // TODO: Serilog supports complex type logging. This is not yet
            // supported in OpenTelemetry.
            if (property.Key == Constants.SourceContextPropertyName
                && property.Value is ScalarValue sourceContextValue)
            {
                attributes.Add("serilog.source_context", sourceContextValue.Value as string);
            }
            else if (property.Value is ScalarValue scalarValue)
            {
                attributes.Add(property.Key, scalarValue.Value);
            }
            else if (property.Value is SequenceValue sequenceValue)
            {
                IReadOnlyList<LogEventPropertyValue> elements = sequenceValue.Elements;
                if (elements.Count > 0)
                {
                    // Note: The goal here is to build a typed array (eg
                    // int[]) if all the element types match otherwise
                    // fallback to object[]

                    Type? elementType = null;
                    Array? values = null;

                    for (int i = 0; i < elements.Count; i++)
                    {
                        if (elements[i] is ScalarValue value)
                        {
                            Type currentElementType = value.Value?.GetType() ?? typeof(object);

                            if (values == null)
                            {
                                elementType = currentElementType;
                                values = Array.CreateInstance(elementType, elements.Count);
                            }
                            else if (!elementType!.IsAssignableFrom(currentElementType))
                            {
                                // Array with mixed types detected
                                object[] newValues = new object[elements.Count];
                                values.CopyTo(newValues, 0);
                                values = newValues;
                                elementType = typeof(object);
                            }

                            values.SetValue(value.Value, i);
                        }
                    }

                    if (values != null)
                    {
                        attributes.Add(property.Key, values);
                    }
                }
            }
        }

        this.logger.EmitLog(in data, in attributes);
    }

    public void Dispose()
    {
        if (this.disposeProvider)
        {
            this.loggerProvider.Dispose();
        }
    }
}
