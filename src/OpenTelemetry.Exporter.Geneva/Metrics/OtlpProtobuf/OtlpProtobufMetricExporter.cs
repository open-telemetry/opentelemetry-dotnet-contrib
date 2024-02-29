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

    private Resource resource;

    internal Resource MetricResource => this.resource ??= this.genevaMetricExporter.ParentProvider.GetResource();

    private GenevaMetricExporter genevaMetricExporter;

    public OtlpProtobufMetricExporter(GenevaMetricExporter genevaMetricExporter)
    {
        // TODO
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Temporary until we add support for user_events.
            throw new NotSupportedException("Unix domain socket should not be used on Windows.");
        }

        this.genevaMetricExporter = genevaMetricExporter;

        this.otlpProtobufSerializer = new OtlpProtobufSerializer();
    }

    public ExportResult Export(in Batch<Metric> batch)
    {
        var result = ExportResult.Success;

        int currentPosition = 0;

        try
        {
            this.otlpProtobufSerializer.SerializeMetrics(this.buffer, ref currentPosition, this.MetricResource, batch);

            // Send request.
            MetricEtwDataTransport.Instance.SendOtlpProtobufEvent(this.buffer, currentPosition);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("metric batch failed", ex);
        }

        return result;
    }

    public void Dispose()
    {
    }
}
