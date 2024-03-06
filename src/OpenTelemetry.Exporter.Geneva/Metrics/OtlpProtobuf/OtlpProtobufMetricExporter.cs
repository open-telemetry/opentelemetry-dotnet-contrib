// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal class OtlpProtobufMetricExporter : IDisposable
{
    private const int BufferSize = 65360; // the maximum ETW payload (inclusive)

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly OtlpProtobufSerializer otlpProtobufSerializer;

    public OtlpProtobufMetricExporter()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Temporary until we add support for user_events.
            throw new NotSupportedException("Unix domain socket should not be used on Windows.");
        }

        this.otlpProtobufSerializer = new OtlpProtobufSerializer(MetricEtwDataTransport.Instance);
    }

    public ExportResult Export(in Batch<Metric> batch, Resource resource = null)
    {
        var result = ExportResult.Success;

        int currentPosition = 0;

        try
        {
            this.otlpProtobufSerializer.SerializeAndSendMetrics(this.buffer, ref currentPosition, resource, batch);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("Failed to export metrics batch", ex);
        }

        return result;
    }

    public void Dispose()
    {
    }
}
