// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER

#nullable enable

using Microsoft.LinuxTracepoints.Provider;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MetricUnixUserEventsDataTransport : IMetricDataTransport
{
    private readonly PerfTracepoint metricsTracepoint;

    private MetricUnixUserEventsDataTransport()
    {
        this.metricsTracepoint = new PerfTracepoint("otlp_metrics u32 protocol;char[8] version;__rel_loc u8[] buffer");
        if (this.metricsTracepoint.RegisterResult != 0)
        {
            ExporterEventSource.Log.TransportError(
                nameof(MetricUnixUserEventsDataTransport),
                $"Tracepoint for 'otlp_metrics' user events could not be registered: '{this.metricsTracepoint.RegisterResult}'");
        }
    }

    public static MetricUnixUserEventsDataTransport Instance { get; } = new();

    public bool IsEnabled() => this.metricsTracepoint.IsEnabled;

    public void Send(MetricEventType eventType, byte[] body, int size)
    {
        throw new NotSupportedException();
    }

    public void SendOtlpProtobufEvent(byte[] body, int size)
    {
        var buffer = new ReadOnlySpan<byte>(body, 0, size);

        var bufferRelLoc = 0u | ((uint)buffer.Length << 16);

        this.metricsTracepoint.Write(
            [0U],
            "v0.19.00"u8,
            [bufferRelLoc],
            buffer);

        Console.WriteLine("Wrote metrics!");
    }

    public void Dispose()
    {
        this.metricsTracepoint.Dispose();
    }
}

#endif
