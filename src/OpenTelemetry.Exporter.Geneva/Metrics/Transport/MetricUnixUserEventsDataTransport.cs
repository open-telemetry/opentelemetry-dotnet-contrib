// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER

#nullable enable

using System.Text;
using Microsoft.LinuxTracepoints.Provider;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MetricUnixUserEventsDataTransport : IMetricDataTransport
{
    public const uint MetricsProtocol = 0U;
    public const string MetricsVersion = "v0.19.00";
    public const string MetricsTracepointName = "otlp_metrics";
    public const string MetricsTracepointNameArgs = $"{MetricsTracepointName} u32 protocol;char[8] version;__rel_loc u8[] buffer";

    private static readonly ReadOnlyMemory<byte> MetricsVersionUtf8 = Encoding.UTF8.GetBytes(MetricsVersion);
    private readonly PerfTracepoint metricsTracepoint;

    private MetricUnixUserEventsDataTransport()
    {
        this.metricsTracepoint = new PerfTracepoint(MetricsTracepointNameArgs);
        if (this.metricsTracepoint.RegisterResult != 0)
        {
            ExporterEventSource.Log.TransportError(
                nameof(MetricUnixUserEventsDataTransport),
                $"Tracepoint for 'otlp_metrics' user events could not be registered: '{this.metricsTracepoint.RegisterResult}'");
        }
    }

    public static MetricUnixUserEventsDataTransport Instance { get; } = new();

    public void Send(MetricEventType eventType, byte[] body, int size)
    {
        throw new NotSupportedException();
    }

    public void SendOtlpProtobufEvent(byte[] body, int size)
    {
        if (this.metricsTracepoint.IsEnabled)
        {
            var buffer = new ReadOnlySpan<byte>(body, 0, size);

            var bufferRelLoc = 0u | ((uint)buffer.Length << 16);

            this.metricsTracepoint.Write(
                [MetricsProtocol],
                MetricsVersionUtf8.Span,
                [bufferRelLoc],
                buffer);
        }
    }

    public void Dispose()
    {
        this.metricsTracepoint.Dispose();
    }
}

#endif
