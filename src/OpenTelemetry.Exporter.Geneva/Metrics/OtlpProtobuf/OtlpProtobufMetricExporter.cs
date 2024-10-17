// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class OtlpProtobufMetricExporter : IDisposable
{
    private readonly byte[] buffer = new byte[GenevaMetricExporter.BufferSize];

    private readonly OtlpProtobufSerializer otlpProtobufSerializer;

    private readonly Func<Resource> getResource;

    public OtlpProtobufMetricExporter(
        Func<Resource> getResource,
        ConnectionStringBuilder connectionStringBuilder,
        IReadOnlyDictionary<string, object>? prepopulatedMetricDimensions)
    {
        Debug.Assert(getResource != null, "getResource was null");

        this.getResource = getResource!;

#if NET6_0_OR_GREATER
        IMetricDataTransport transport = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? MetricUnixUserEventsDataTransport.Instance
            : MetricWindowsEventTracingDataTransport.Instance;
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new NotSupportedException("Exporting data in protobuf format is not supported on Linux.");
        }

        var transport = MetricWindowsEventTracingDataTransport.Instance;
#endif

        this.otlpProtobufSerializer = new OtlpProtobufSerializer(
            transport,
            connectionStringBuilder,
            prepopulatedMetricDimensions);
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
