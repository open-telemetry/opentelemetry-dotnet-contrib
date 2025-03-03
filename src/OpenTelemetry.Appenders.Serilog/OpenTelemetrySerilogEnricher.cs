// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using Serilog.Core;
using Serilog.Events;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetrySerilogEnricher : ILogEventEnricher
{
    private readonly OpenTelemetrySerilogEnricherOptions options;

    public OpenTelemetrySerilogEnricher(OpenTelemetrySerilogEnricherOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Enrich(
        LogEvent logEvent,
        ILogEventPropertyFactory propertyFactory)
    {
        Guard.ThrowIfNull(logEvent);
        Guard.ThrowIfNull(propertyFactory);

        Activity? activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(nameof(Activity.SpanId), activity.SpanId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(nameof(Activity.TraceId), activity.TraceId.ToHexString()));

        if (activity.ParentSpanId != default)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(nameof(Activity.ParentSpanId), activity.ParentSpanId.ToHexString()));
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceFlags", activity.ActivityTraceFlags));

        if (this.options.IncludeTraceState)
        {
            var traceState = activity.TraceStateString;
            if (!string.IsNullOrEmpty(traceState))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceState", traceState));
            }
        }
    }
}
