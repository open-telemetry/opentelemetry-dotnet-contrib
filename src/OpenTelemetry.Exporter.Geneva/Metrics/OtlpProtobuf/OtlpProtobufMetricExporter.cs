// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

        this.otlpProtobufSerializer = new OtlpProtobufSerializer(MetricWindowsEventTracingDataTransport.Instance, connectionStringBuilder, prepopulatedMetricDimensions);
    }

    public ExportResult Export(in Batch<Metric> batch)
    {
        try
        {
            return this.otlpProtobufSerializer.SerializeAndSendMetrics(this.buffer, this.getResource(), batch);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("Failed to export metrics batch", ex);
            return ExportResult.Failure;
        }
    }

    public void Dispose()
    {
    }
}
