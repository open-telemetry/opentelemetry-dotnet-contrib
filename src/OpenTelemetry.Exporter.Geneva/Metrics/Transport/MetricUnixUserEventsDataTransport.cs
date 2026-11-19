// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers.Binary;
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
    [ThreadStatic]
    private static byte[]? headerBuffer;

    private readonly PerfTracepoint metricsTracepoint;

    private MetricUnixUserEventsDataTransport()
    {
        this.metricsTracepoint = new PerfTracepoint(MetricsTracepointNameArgs);
        if (this.metricsTracepoint.RegisterResult != 0)
        {
            // ENOENT (2): No such file or directory
            if (this.metricsTracepoint.RegisterResult == 2)
            {
                throw new NotSupportedException(
                    $"Tracepoint registration for 'otlp_metrics' failed with result: '{this.metricsTracepoint.RegisterResult}'. Verify your distribution/kernel supports user_events: https://docs.kernel.org/trace/user_events.html.");
            }

            ExporterEventSource.Log.TransportInformation(
                nameof(MetricUnixUserEventsDataTransport),
                $"Tracepoint registration operation for 'otlp_metrics' returned result '{this.metricsTracepoint.RegisterResult}' which is considered recoverable. Entering running state.");
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
            var bufferRelLoc = 0u | ((uint)size << 16);

            // Fixed-size prefix: u32 protocol; char[8] version; __rel_loc u8[] buffer (u32)
            var header = headerBuffer ??= new byte[sizeof(uint) + 8 + sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, sizeof(uint)), MetricsProtocol);
            MetricsVersionUtf8.Span.CopyTo(header.AsSpan(sizeof(uint), 8));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(sizeof(uint) + 8, sizeof(uint)), bufferRelLoc);

            this.metricsTracepoint.Write(
                new ArraySegment<byte>(header),
                new ArraySegment<byte>(body, 0, size));
        }
    }

    public void Dispose()
    {
        this.metricsTracepoint.Dispose();
    }
}

#endif
