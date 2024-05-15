// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class OtlpProtobufMetricExporter : IDisposable
{
    private readonly byte[] buffer = new byte[GenevaMetricExporter.BufferSize];

    private readonly OtlpProtobufSerializer otlpProtobufSerializer;

    private readonly Func<Resource> getResource;

    public OtlpProtobufMetricExporter(Func<Resource> getResource, ConnectionStringBuilder connectionStringBuilder, IReadOnlyDictionary<string, object> prepopulatedMetricDimensions)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Temporary until we add support for user_events.
            throw new NotSupportedException("Exporting data in protobuf format is not supported on Linux.");
        }

        this.getResource = getResource;

        this.otlpProtobufSerializer = new OtlpProtobufSerializer(MetricEtwDataTransport.Instance, connectionStringBuilder, prepopulatedMetricDimensions);
    }

    public ExportResult Export(in Batch<Metric> batch)
    {
        var result = ExportResult.Success;
        try
        {
            result = this.otlpProtobufSerializer.SerializeAndSendMetrics(this.buffer, this.getResource(), batch);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("Failed to export metrics batch", ex);
            result = ExportResult.Failure;
        }

        if (result == ExportResult.Success)
        {
            ExporterEventSource.Log.ExportCompleted(nameof(OtlpProtobufMetricExporter));
        }

        return result;
    }

    public void Dispose()
    {
    }
}
