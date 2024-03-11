// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class OtlpProtobufMetricExporter : IDisposable
{
    private const int BufferSize = 65360; // the maximum ETW payload (inclusive)

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly ProtobufSerializer protobufSerializer;

    public OtlpProtobufMetricExporter()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Temporary until we add support for user_events.
            throw new NotSupportedException("Exporting data in protobuf format is not supported on Linux.");
        }

        this.protobufSerializer = new ProtobufSerializer(MetricEtwDataTransport.Instance);
    }

    public ExportResult Export(in Batch<Metric> batch, Resource resource = null)
    {
        var result = ExportResult.Success;

        int currentPosition = 0;

        try
        {
            this.protobufSerializer.SerializeAndSendMetrics(this.buffer, ref currentPosition, resource, batch);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("Failed to export metrics batch", ex);
            result = ExportResult.Failure;
        }

        return result;
    }

    public void Dispose()
    {
    }
}
